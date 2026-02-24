using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.DynamoDBv2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using KwaWicks.Application.Interfaces;
using KwaWicks.Application.Services;
using KwaWicks.Infrastructure.DynamoDB;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// -------------------- CORS (UI) --------------------
const string UiCors = "UiCors";

builder.Services.AddCors(options =>
{
    options.AddPolicy(UiCors, policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrWhiteSpace(origin)) return false;
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;

                var hostOk = uri.Host is "localhost" or "127.0.0.1";
                if (!hostOk) return false;

                var schemeOk = uri.Scheme is "http" or "https";
                if (!schemeOk) return false;

                return uri.Port >= 5173 && uri.Port <= 5180;
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// -------------------- Swagger --------------------
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "KwaWicks API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// -------------------- AWS DynamoDB --------------------
var awsRegion = builder.Configuration["Aws:Region"] ?? "af-south-1";
var tableName = builder.Configuration["Aws:DynamoTableName"] ?? "kwawicks";

// DynamoDB client can be singleton
builder.Services.AddSingleton<IAmazonDynamoDB>(_ =>
    new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(awsRegion))
);

// Repositories: use factory because they require (ddb, tableName)
builder.Services.AddScoped<ISpeciesRepository>(sp =>
{
    var ddb = sp.GetRequiredService<IAmazonDynamoDB>();
    return new SpeciesRepository(ddb, tableName);
});

builder.Services.AddScoped<IClientRepository>(sp =>
{
    var ddb = sp.GetRequiredService<IAmazonDynamoDB>();
    return new ClientRepository(ddb, tableName);
});

// Application services
builder.Services.AddScoped<SpeciesService>();
builder.Services.AddScoped<IClientService, ClientService>();

// -------------------- Cognito JWT Auth --------------------
var cognitoRegion = builder.Configuration["Cognito:Region"] ?? "af-south-1";
var userPoolId = builder.Configuration["Cognito:UserPoolId"] ?? throw new InvalidOperationException("Missing Cognito:UserPoolId");
var authority = $"https://cognito-idp.{cognitoRegion}.amazonaws.com/{userPoolId}";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authority,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RoleClaimType = "cognito:groups",
            NameClaimType = "cognito:username"
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("HubStaffOnly", p => p.RequireRole("HubStaff"));
    options.AddPolicy("DriverOnly", p => p.RequireRole("Driver"));
});

// -------------------- Cognito Client (for login endpoint) --------------------
builder.Services.AddSingleton<IAmazonCognitoIdentityProvider>(_ =>
{
    var region = RegionEndpoint.GetBySystemName(cognitoRegion);
    return new AmazonCognitoIdentityProviderClient(region);
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

// CORS before auth
app.UseCors(UiCors);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();