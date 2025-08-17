using ClickerGame.GameCore.Application.Services;
using ClickerGame.GameCore.Infrastructure.Data;
using ClickerGame.GameCore.Middleware;
using ClickerGame.GameCore.Hubs;
using ClickerGame.Shared.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add correlation logging
builder.Services.AddCorrelationLogging("GameCore-Service");
builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GameCore Service API",
        Version = "v1",
        Description = "Clicker Game Core Microservice with Real-time Communication"
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
});

// Database
builder.Services.AddDbContext<GameCoreDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    var connectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(connectionString);
});

// JWT Authentication - Enhanced for SignalR
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
            ClockSkew = TimeSpan.FromSeconds(30),
            RequireExpirationTime = true
        };

        // Enhanced JWT events for SignalR
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/gameHub") || path.StartsWithSegments("/hubs/")))
                {
                    context.Token = accessToken;
                    Log.Debug("JWT token extracted from query string for SignalR connection");
                }

                return Task.CompletedTask;
            },

            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var username = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

                logger.LogDebug("JWT token validated for user {UserId} ({Username})", userId, username);
                return Task.CompletedTask;
            },

            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("JWT authentication failed: {Exception}", context.Exception.Message);

                if (context.Request.Path.StartsWithSegments("/gameHub"))
                {
                    context.NoResult();
                }

                return Task.CompletedTask;
            },

            OnChallenge = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

                if (context.Request.Path.StartsWithSegments("/gameHub"))
                {
                    logger.LogWarning("JWT challenge for SignalR connection. Error: {Error}, Description: {Description}",
                        context.Error, context.ErrorDescription);

                    context.HandleResponse();
                    context.Response.StatusCode = 401;
                    return context.Response.WriteAsync("Unauthorized: Invalid or missing JWT token for SignalR connection");
                }

                return Task.CompletedTask;
            }
        };
    });

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequirePlayer", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("role", "player");
    });

    options.AddPolicy("RequireValidPlayerId", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(System.Security.Claims.ClaimTypes.NameIdentifier);
    });
});

// SignalR Configuration - ENHANCED with Transport Configuration (Task 5.1)
builder.Services.AddSignalR(options =>
{
    // Basic hub configuration
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    options.MaximumReceiveMessageSize = 32 * 1024; // 32KB
    options.StreamBufferCapacity = 10;
    options.MaximumParallelInvocationsPerClient = 1;

    // Enhanced connection timeout for poor network conditions
    options.StatefulReconnectBufferSize = 1000;
})
.AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.PayloadSerializerOptions.WriteIndented = false; // Minimize payload size
});

// Application Services
builder.Services.AddScoped<IGameEngineService, GameEngineService>();
builder.Services.AddScoped<IGameNotificationService, GameNotificationService>();
builder.Services.AddScoped<ISignalRConnectionManager, SignalRConnectionManager>();
builder.Services.AddScoped<ISystemEventService, SystemEventService>();
builder.Services.AddScoped<IScoreUpdateThrottleService, ScoreUpdateThrottleService>();
builder.Services.AddScoped<IScoreBroadcastService, ScoreBroadcastService>();
builder.Services.AddScoped<IPresenceService, PresenceService>();
builder.Services.AddScoped<ISignalRMetricsService, SignalRMetricsService>();

builder.Services.AddHostedService<SignalRMetricsBackgroundService>();
builder.Services.AddHostedService<PresenceCleanupBackgroundService>();
builder.Services.AddHostedService<ScheduledEventBackgroundService>();
builder.Services.AddHostedService<PassiveIncomeBackgroundService>();

builder.Services.AddHttpClient();

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<GameCoreDbContext>()
    .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? "redis:6379");

// CORS - Enhanced for SignalR with proper transport support
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSignalR", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                           ?? new[] { "http://localhost:4200", "https://localhost:4200", "http://localhost:3000" };

        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials() // Required for SignalR
              .WithExposedHeaders("X-Correlation-ID")
              .SetPreflightMaxAge(TimeSpan.FromMinutes(5)); // Cache preflight requests
    });
});

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "GameCore Service API V1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowSignalR");

app.UseStaticFiles();

// Add correlation middleware
app.UseMiddleware<CorrelationMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Map SignalR Hub with enhanced transport configuration
app.MapHub<GameHub>("/gameHub", options =>
{
    // Configure transport priorities: WebSockets → Server-Sent Events → Long Polling
    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets |
                        Microsoft.AspNetCore.Http.Connections.HttpTransportType.ServerSentEvents |
                        Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;

    // Additional connection options for browser compatibility
    options.CloseOnAuthenticationExpiration = true;
    options.ApplicationMaxBufferSize = 32 * 1024; // 32KB buffer
    options.TransportMaxBufferSize = 64 * 1024;   // 64KB transport buffer

    // Long polling configuration for fallback support
    options.LongPolling.PollTimeout = TimeSpan.FromSeconds(90);
}).RequireAuthorization("RequireValidPlayerId");

app.MapGet("/js/signalr-transport-config.js", async context =>
{
    context.Response.ContentType = "application/javascript";
    var filePath = Path.Combine(app.Environment.WebRootPath, "js", "signalr-transport-config.js");
    await context.Response.SendFileAsync(filePath);
});

// Auto-migrate database in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<GameCoreDbContext>();
        dbContext.Database.Migrate();
        Log.Information("GameCore Service database migration completed successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "GameCore Service database migration failed");
    }
}

Log.Information("GameCore Service with Enhanced SignalR Transport Configuration starting on port 5002");
app.Run();