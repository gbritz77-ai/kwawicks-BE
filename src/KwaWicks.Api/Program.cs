using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.DynamoDBv2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using KwaWicks.Application.Interfaces;
using KwaWicks.Application.Services;
using KwaWicks.Infrastructure.DynamoDB;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// -------------------- CORS --------------------
const string UiCors = "UiCors";

builder.Services.AddCors(options =>
{
    options.AddPolicy(UiCors, policy =>
    {
        // Local dev (Vite)
        policy.WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173",
                "http://localhost:5174",
                "http://127.0.0.1:5174",
                "https://main.d137tsnrxezsdg.amplifyapp.com"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();

        // NOTE: Add your Amplify/CloudFront origins here when ready, e.g.
        // .WithOrigins("https://your-ui-domain.com")
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

builder.Services.AddSingleton<IAmazonDynamoDB>(_ =>
    new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(awsRegion))
);

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

builder.Services.AddScoped<SpeciesService>();
builder.Services.AddScoped<IClientService, ClientService>();

// -------------------- Cognito JWT Auth --------------------
var cognitoRegion = builder.Configuration["Cognito:Region"] ?? "af-south-1";
var userPoolId = builder.Configuration["Cognito:UserPoolId"]
                 ?? throw new InvalidOperationException("Missing Cognito:UserPoolId");

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

// -------------------- Cognito Client --------------------
builder.Services.AddSingleton<IAmazonCognitoIdentityProvider>(_ =>
{
    var region = RegionEndpoint.GetBySystemName(cognitoRegion);
    return new AmazonCognitoIdentityProviderClient(region);
});

var app = builder.Build();

// ✅ Behind ALB: trust X-Forwarded-* headers
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// ✅ Do NOT redirect to HTTPS in production behind an HTTP ALB
// If you later add HTTPS listener on the ALB, you can re-enable this safely.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Swagger: keep always on for now (you can restrict later)
app.UseSwagger();
app.UseSwaggerUI();

// CORS before auth
app.UseCors(UiCors);

app.MapMethods("{*path}", new[] { "OPTIONS" }, () => Results.Ok())
   .RequireCors(UiCors);

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ✅ Add root + health endpoints (fixes your ALB 404 + health checks)
app.MapGet("/", () => Results.Ok("KwaWicks API is running"));
app.MapGet("/health", () => Results.Ok("ok"));

app.Run();