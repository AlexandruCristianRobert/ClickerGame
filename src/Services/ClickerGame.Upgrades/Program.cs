using ClickerGame.Shared.Logging;
using ClickerGame.Upgrades.Application.Services;
using ClickerGame.Upgrades.Configuration;
using ClickerGame.Upgrades.Infrastructure.Data;
using ClickerGame.Upgrades.Infrastructure.Swagger;
using ClickerGame.Upgrades.Middleware;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using StackExchange.Redis;
using System.Reflection;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add correlation logging
builder.Services.AddCorrelationLogging("Upgrades-Service");
builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Upgrades Service API",
        Version = "v1",
        Description = "ClickerGame Upgrades Microservice with Clean Architecture",
        Contact = new OpenApiContact
        {
            Name = "ClickerGame Team",
            Email = "support@clickergame.com"
        }
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Include XML comments if available
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    // Add enum descriptions
    c.SchemaFilter<EnumSchemaFilter>();
});

// Database
builder.Services.AddDbContext<UpgradesDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    var connectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(connectionString);
});

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");
var key = Encoding.ASCII.GetBytes(jwtSecret);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

// MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

// FluentValidation
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

// AutoMapper
builder.Services.AddAutoMapper(Assembly.GetExecutingAssembly());

// Add the validation configuration
builder.Services.Configure<UpgradeValidationSettings>(
    builder.Configuration.GetSection("UpgradeValidation"));

// Application Services (remove duplicates)
builder.Services.AddScoped<IUpgradeCalculationEngine, UpgradeCalculationEngine>();
builder.Services.AddScoped<IUpgradeService, UpgradeService>();
builder.Services.AddScoped<IPlayerContextService, PlayerContextService>();
builder.Services.AddScoped<IGameCoreIntegrationService, GameCoreIntegrationService>();

// Add HttpClient for service-to-service communication
builder.Services.AddHttpClient();


// Add configuration for service URLs
builder.Services.Configure<ServiceUrlsOptions>(
    builder.Configuration.GetSection("Services"));

// Configure JSON options for better API responses
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = true;
});

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<UpgradesDbContext>()
    .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? "redis:6379");

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Upgrades Service API V1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");

// Add correlation middleware
app.UseMiddleware<CorrelationMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Auto-migrate database and seed data in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<UpgradesDbContext>();

        // Apply migrations
        dbContext.Database.Migrate();
        Log.Information("Upgrades Service database migration completed successfully");

        // Seed initial data
        await UpgradeDataSeeder.SeedUpgradesAsync(dbContext);
        Log.Information("Upgrades Service seed data completed successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Upgrades Service database migration or seeding failed");
    }
}

Log.Information("Upgrades Service starting on port 5003");
app.Run();