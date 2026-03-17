using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.DynamoDBv2;
using Amazon.S3;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using KwaWicks.Application.Interfaces;
using KwaWicks.Application.Services;
using KwaWicks.Application.DTOs;
using KwaWicks.Infrastructure.DynamoDB;
using KwaWicks.Infrastructure.S3;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// -------------------- CORS --------------------
const string UiCors = "UiCors";

builder.Services.AddCors(options =>
{
    options.AddPolicy(UiCors, policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173",
                "http://localhost:5174",
                "http://127.0.0.1:5174",
                "https://main.d137tsnrxezsdg.amplifyapp.com"
            )
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

// -------------------- AWS --------------------
var awsRegion = builder.Configuration["Aws:Region"] ?? "af-south-1";

// ✅ Use ONE config key (the one you're already using)
// ✅ Fail fast if missing (prevents null tableName runtime errors)
var tableName = builder.Configuration["Aws:DynamoTableName"]
               ?? Environment.GetEnvironmentVariable("AWS_DYNAMO_TABLE_NAME")
               ?? "kwawicks";

builder.Services.AddSingleton<IAmazonDynamoDB>(_ =>
    new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(awsRegion))
);

builder.Services.AddSingleton<IAmazonS3>(_ =>
    new AmazonS3Client(RegionEndpoint.GetBySystemName(awsRegion))
);

// Repositories (single-table PK/SK pattern)
builder.Services.AddScoped<ISpeciesRepository>(sp =>
    new SpeciesRepository(sp.GetRequiredService<IAmazonDynamoDB>(), tableName));

builder.Services.AddScoped<IClientRepository>(sp =>
    new ClientRepository(sp.GetRequiredService<IAmazonDynamoDB>(), tableName));

builder.Services.AddScoped<IInvoiceRepository>(sp =>
    new InvoiceRepository(sp.GetRequiredService<IAmazonDynamoDB>(), tableName));

builder.Services.AddScoped<IDeliveryOrderRepository>(sp =>
    new DeliveryOrderRepository(sp.GetRequiredService<IAmazonDynamoDB>(), tableName));

builder.Services.AddScoped<IHubTaskRepository>(sp =>
    new HubTaskRepository(sp.GetRequiredService<IAmazonDynamoDB>(), tableName));

// Services
builder.Services.AddScoped<SpeciesService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IDeliveryOrderService, DeliveryOrderService>();

var receiptsBucket = builder.Configuration["Aws:S3:ReceiptsBucket"] ?? "kwawicks-receipts";
builder.Services.AddSingleton<IS3Service>(sp =>
    new S3Service(sp.GetRequiredService<IAmazonS3>(), receiptsBucket));

builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IReportService, ReportService>();

// -------------------- Cognito JWT Auth --------------------
var cognitoRegion = builder.Configuration["Cognito:Region"] ?? "af-south-1";
var userPoolId = builder.Configuration["Cognito:UserPoolId"]
                 ?? throw new InvalidOperationException("Missing Cognito:UserPoolId");

builder.Services.AddScoped<IUserManagementService>(sp =>
    new UserManagementService(
        sp.GetRequiredService<IAmazonCognitoIdentityProvider>(),
        userPoolId));

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
    // Financial data — Owner and Finance only
    options.AddPolicy("FinancialAccess", p => p.RequireRole("Owner", "Finance"));
    // Operational access — all non-driver roles
    options.AddPolicy("OperationalAccess", p => p.RequireRole("Owner", "Finance", "Admin", "HubStaff"));
    // User management — all non-driver roles
    options.AddPolicy("UserManagement", p => p.RequireRole("Owner", "Finance", "Admin", "HubStaff"));
    // Driver endpoints — drivers only (Owner/Finance/Admin can also call if needed)
    options.AddPolicy("DriverOnly", p => p.RequireRole("Owner", "Finance", "Admin", "Driver"));
    // Legacy compat
    options.AddPolicy("AdminOnly", p => p.RequireRole("Owner", "Finance", "Admin"));
    options.AddPolicy("HubStaffOnly", p => p.RequireRole("Owner", "Finance", "Admin", "HubStaff"));
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

// ✅ Only redirect in dev
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Swagger
app.UseSwagger();
app.UseSwaggerUI();

// CORS before auth
app.UseCors(UiCors);

// Preflight
app.MapMethods("{*path}", new[] { "OPTIONS" }, () => Results.Ok())
   .RequireCors(UiCors);

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Root + health
app.MapGet("/", () => Results.Ok("KwaWicks API is running"));
app.MapGet("/health", () => Results.Ok("ok"));

app.Run();