using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.WebSockets;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using TDFAPI.Configuration;
using TDFAPI.Middleware;
using TDFAPI.Repositories;
using TDFAPI.Services;
using TDFAPI.Messaging;
using Microsoft.EntityFrameworkCore;
using System;
using TDFAPI.Data;
using System.Linq;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using MediatR;
using TDFShared.Models.Message;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.AspNetCore.HttpOverrides;
using TDFShared.Constants;
using TDFShared.DependencyInjection;
using TDFShared.Services;

var builder = WebApplication.CreateBuilder(args);

// Log simple startup message
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine();
Console.WriteLine(" --- TDF API Server v1.0 - Initializing... ---");
Console.WriteLine($" --- Environment: {builder.Environment.EnvironmentName} ---");
Console.WriteLine($" --- Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---");
Console.WriteLine();
Console.ResetColor();

// Create a custom logger for startup
var loggerFactory = LoggerFactory.Create(config =>
{
    config.ClearProviders();
    config.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
        options.UseUtcTimestamp = false;
        options.IncludeScopes = false;
        options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
    });
});
var logger = loggerFactory.CreateLogger("TDF");

// Replace all default logging with custom formatter
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
    options.UseUtcTimestamp = false;
    options.IncludeScopes = false;
    options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
});
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Mvc", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Routing", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.StaticFiles", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);

// Set up global unhandled exception handlers
AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
{
    var ex = args.ExceptionObject as Exception;
    logger.LogCritical(
        ex,
        "UNHANDLED EXCEPTION CAUSING APPLICATION CRASH: {Message}. IsTerminating: {IsTerminating}",
        ex?.Message ?? "Unknown error",
        args.IsTerminating
    );

    // Log to file as well since the process may terminate
    try
    {
        var logsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        logger.LogInformation("Creating logs directory at: {LogsPath}", logsPath);

        if (!Directory.Exists(logsPath))
        {
            Directory.CreateDirectory(logsPath);
            logger.LogInformation("Successfully created logs directory at: {LogsPath}", logsPath);
        }
        else
        {
            logger.LogInformation("Logs directory already exists at: {LogsPath}", logsPath);
        }

        // Implement log rotation - keep only last 10 crash logs
        var existingCrashLogs = Directory.GetFiles(logsPath, "crash_*.txt")
            .OrderByDescending(f => f)
            .Skip(9) // Keep 10 most recent files (including the one we're about to create)
            .ToList();

        foreach (var oldLog in existingCrashLogs)
        {
            try { File.Delete(oldLog); } catch { /* Best effort deletion */ }
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var crashLogPath = Path.Combine(logsPath, $"crash_{timestamp}.txt");

        // Keep it simple with just the essential crash details
        var crashDetails =
            $"Application Crash: {DateTime.Now}\n\n" +
            $"Exception: {ex?.GetType().FullName}\n" +
            $"Message: {ex?.Message}\n\n" +
            $"Stack Trace:\n{ex?.StackTrace}\n\n";

        if (ex?.InnerException != null)
        {
            crashDetails +=
                $"Inner Exception: {ex.InnerException.GetType().FullName}\n" +
                $"Inner Message: {ex.InnerException.Message}\n\n";
        }

        // Limit crash log size to 500KB
        if (crashDetails.Length > 500 * 1024)
        {
            crashDetails = crashDetails.Substring(0, 500 * 1024) + "\n...[truncated]";
        }

        File.WriteAllText(crashLogPath, crashDetails);
    }
    catch
    {
        // Can't do much if we can't write to file
    }
};

// Also handle unobserved task exceptions
TaskScheduler.UnobservedTaskException += (sender, args) =>
{
    var exception = args.Exception;
    logger.LogError(
        exception,
        "UNOBSERVED TASK EXCEPTION: {Message}",
        exception.Message
    );

    // Log unobserved task exceptions to file too
    try
    {
        var logsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        Directory.CreateDirectory(logsPath);

        // Implement log rotation - keep only last 10 task exception logs
        var existingTaskLogs = Directory.GetFiles(logsPath, "task_exception_*.txt")
            .OrderByDescending(f => f)
            .Skip(9) // Keep 10 most recent files
            .ToList();

        foreach (var oldLog in existingTaskLogs)
        {
            try { File.Delete(oldLog); } catch { /* Best effort deletion */ }
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var crashLogPath = Path.Combine(logsPath, $"task_exception_{timestamp}.txt");

        var crashDetails =
            $"Unobserved Task Exception: {DateTime.Now}\n\n" +
            $"Exception: {exception.GetType().FullName}\n" +
            $"Message: {exception.Message}\n\n" +
            $"Stack Trace:\n{exception.StackTrace}\n\n";

        if (exception.InnerException != null)
        {
            crashDetails +=
                $"Inner Exception: {exception.InnerException.GetType().FullName}\n" +
                $"Inner Message: {exception.InnerException.Message}\n\n";
        }

        // Limit log size
        if (crashDetails.Length > 500 * 1024)
        {
            crashDetails = crashDetails.Substring(0, 500 * 1024) + "\n...[truncated]";
        }

        File.WriteAllText(crashLogPath, crashDetails);
    }
    catch
    {
        // Can't do much if we can't write to file
    }

    // Mark as observed to prevent application crash
    args.SetObserved();
};

// Ensure logs directory exists
try
{
    var logsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
    logger.LogInformation("Creating logs directory at: {LogsPath}", logsPath);

    if (!Directory.Exists(logsPath))
    {
        Directory.CreateDirectory(logsPath);
        logger.LogInformation("Successfully created logs directory at: {LogsPath}", logsPath);
    }
    else
    {
        logger.LogInformation("Logs directory already exists at: {LogsPath}", logsPath);
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to create logs directory: {ErrorMessage}", ex.Message);

    // Try creating in a different location
    try {
        var altLogsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TDFAPI", "logs");
        logger.LogInformation("Attempting to create logs in alternative location: {LogsPath}", altLogsPath);
        Directory.CreateDirectory(altLogsPath);
        logger.LogInformation("Successfully created alternative logs directory at: {LogsPath}", altLogsPath);
    }
    catch (Exception ex2) {
        logger.LogError(ex2, "Failed to create alternative logs directory: {ErrorMessage}", ex2.Message);
    }
}

// Initialize configuration from INI file
try {
    logger.LogInformation("Initializing configuration from INI file");
    IniConfiguration.Initialize();
    logger.LogInformation("Successfully initialized configuration from INI file");

    // Update INI file with new sections if needed
    IniConfiguration.UpdateConfigFile();
    logger.LogInformation("Successfully updated INI configuration file");
} catch (Exception ex) {
    logger.LogCritical(ex, "Failed to initialize or update INI configuration: {Message}", ex.Message);
    throw; // This is critical, so we should terminate the application
}

// Configure Gzip compression with higher compression level
builder.Services.AddResponseCompression(options =>
{
    options.Providers.Add<GzipCompressionProvider>();
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/json", "application/jwt" });
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.SmallestSize;
});

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Configure JSON serialization options to handle property name collisions
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        // This helps with cyclic references which can occur in Entity Framework relationships
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Register HttpContextAccessor for accessing the current HTTP context
builder.Services.AddHttpContextAccessor();

// Configure security services and options
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    // Limit form size to prevent DoS attacks
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10 MB
    options.ValueLengthLimit = 4 * 1024 * 1024;          // 4 MB
    options.MemoryBufferThreshold = 2 * 1024 * 1024;     // 2 MB
});

// Test database connection before configuring health checks
try {
    logger.LogInformation("Testing database connection");
    using (var connection = new SqlConnection(IniConfiguration.ConnectionString))
    {
        connection.Open();
        logger.LogInformation("Database connection test successful");
    }
} catch (Exception ex) {
    logger.LogWarning(ex, "Database connection test failed: {Message}", ex.Message);
    // Don't throw here, let the application continue and health checks will report the issue
}

// Configure health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())
    .AddSqlServer(
        IniConfiguration.ConnectionString, // Use connection string from INI file
        name: "database",
        tags: new[] { "db", "sql", "sqlserver" });

// Configure rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Add general rate limiter for all endpoints using IniConfiguration
    var globalLimit = IniConfiguration.GetRateLimitSetting("GlobalLimitPerMinute", 100);
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = globalLimit,
                Window = TimeSpan.FromMinutes(1)
            });
    });

    // Add specific limit for auth endpoints
    var authLimit = IniConfiguration.GetRateLimitSetting("AuthLimitPerMinute", 10);
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = authLimit,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Add policy for API endpoints
    var apiLimit = IniConfiguration.GetRateLimitSetting("ApiLimitPerMinute", 60);
    options.AddPolicy("api", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = apiLimit,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Add policy for static resources
    var staticLimit = IniConfiguration.GetRateLimitSetting("StaticLimitPerMinute", 200);
    options.AddPolicy("static", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = staticLimit,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// Configure CORS
if (builder.Environment.IsDevelopment())
{
    // In development, load allowed origins from INI configuration
    var developmentOrigins = IniConfiguration.DevelopmentAllowedOrigins;

    // Default development origins if none configured
    if (developmentOrigins.Count == 0)
    {
        developmentOrigins.AddRange(new[]
        {
            "http://localhost:3000",       // React development server
            "http://localhost:8080",       // Vue development server
            "http://localhost:4200",       // Angular development server
            "https://localhost:5001",      // .NET development server with HTTPS
            "http://localhost:5000",       // .NET development server
            "http://localhost:5173",       // Vite development server
            "http://127.0.0.1:5173",       // Vite development server alt
            "http://localhost:8000"        // Django/Python development server
        });
    }

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("CorsPolicy", policy =>
        {
            policy.WithOrigins(developmentOrigins.ToArray())
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });

    logger.LogWarning("CORS is configured for development environment with origins: {Origins}", string.Join(", ", developmentOrigins));
}
else
{
    // In production, use allowed origins from INI configuration
    var allowedOrigins = IniConfiguration.AllowedOrigins;

    if (allowedOrigins.Count == 0)
    {
        throw new InvalidOperationException("No allowed origins configured for CORS in production environment. Please configure AllowedOrigins in config.ini.");
    }

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("CorsPolicy", policy =>
        {
            policy.WithOrigins(allowedOrigins.ToArray())
                  .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                  .WithHeaders("Authorization", "Content-Type")
                  .AllowCredentials()
                  .SetIsOriginAllowedToAllowWildcardSubdomains(); // Allow wildcards in domain like *.example.com
        });
    });

    logger.LogInformation("CORS configured with the following origins: {Origins}",
        string.Join(", ", allowedOrigins));
}

// Configure WebSockets
builder.Services.AddWebSockets(options =>
{
    // Configure WebSocket options from INI configuration
    options.KeepAliveInterval = TimeSpan.FromMinutes(
        IniConfiguration.GetWebSocketSetting("KeepAliveMinutes", 2.0));
});

// Add WebSocket manager
builder.Services.AddSingleton<WebSocketConnectionManager>();

// Register MessageStore
builder.Services.AddSingleton<MessageStore>();

// Configure authentication
var key = Encoding.ASCII.GetBytes(IniConfiguration.JwtSecretKey);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        // Always validate these important parameters, even in development
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        // Set issuer and audience from configuration
        ValidIssuer = IniConfiguration.JwtIssuer,
        ValidAudience = IniConfiguration.JwtAudience,
        // Add clock skew tolerance to prevent minor timing issues
        ClockSkew = TimeSpan.FromMinutes(1),
        // Require signature validation
        RequireSignedTokens = true,
        // Ensure tokens aren't expired
        RequireExpirationTime = true
    };

    // Configure challenge events
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
            {
                context.Response.Headers.Append("Token-Expired", "true");
                context.Response.Headers.Append("Access-Control-Expose-Headers", "Token-Expired");
                logger.LogInformation("Token expired for request to {Path}", context.Request.Path);
            }
            else
            {
                logger.LogWarning("Authentication failed: {ExceptionMessage}", context.Exception.Message);
            }
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            // Check if the token has been revoked
            var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            if (!string.IsNullOrEmpty(jti))
            {
                var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
                // Note: Ideally, this check should be async, but JwtBearerEvents are sync.
                // A common workaround is to use .Result or .GetAwaiter().GetResult(),
                // but be mindful of potential deadlocks in some contexts.
                // For simplicity here, using .Result. Consider a custom middleware approach for fully async validation if needed.
                if (authService.IsTokenRevokedAsync(jti).Result)
                {
                    logger.LogWarning("Token validation failed: Token has been revoked (JTI: {Jti})", jti);
                    context.Fail("Token has been revoked.");
                    return Task.CompletedTask; // Important to return early
                }
            }
            else
            {
                 logger.LogWarning("Token validation warning: JTI claim missing, cannot check revocation status.");
                 // Decide if missing JTI should fail validation based on security requirements
                 // context.Fail("Invalid token: Missing JTI claim.");
                 // return Task.CompletedTask;
            }

            // Additional validation could be done here
            var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out _))
            {
                context.Fail("Invalid token: Missing or invalid user identifier");
                logger.LogWarning("Token validation failed: Invalid user identifier");
            }
            else
            {
                var username = context.Principal?.Identity?.Name;
                logger.LogInformation("User {Username} successfully authenticated", username);
            }
            return Task.CompletedTask;
        }
    };
});

// Add database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(IniConfiguration.ConnectionString));

// Add memory cache support
builder.Services.AddMemoryCache();

// Register cache service as singleton
builder.Services.AddSingleton<ICacheService, CacheService>();

// Add MessageStore as a singleton - used by NotificationService
builder.Services.AddSingleton<MessageStore>();

// Add WebSocketConnectionManager as a singleton - used by NotificationService
builder.Services.AddSingleton<WebSocketConnectionManager>();

// Add EventMediator as a singleton
builder.Services.AddSingleton<TDFAPI.Messaging.Interfaces.IEventMediator, EventMediator>();

// Register API-specific services
builder.Services.AddApiServices();

// Register AuthService with the shared interface
builder.Services.AddScoped<TDFShared.Services.IAuthService, AuthService>();

// Register repositories with connection retry policy
builder.Services.AddScoped(provider =>
    new SqlConnectionFactory(IniConfiguration.ConnectionString));

// Register UnitOfWork
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Register repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IRequestRepository, RequestRepository>();
builder.Services.AddScoped<IRevokedTokenRepository, RevokedTokenRepository>();

// Register all services as scoped for consistent lifetime management
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<ILookupService, LookupService>();
builder.Services.AddScoped<IRequestService, RequestService>();
builder.Services.AddScoped<IUserPresenceService, UserPresenceService>();
builder.Services.AddScoped<TDFShared.Services.INotificationService, NotificationService>();

// Add background services
builder.Services.AddHostedService<UserInactivityBackgroundService>();

// Add API versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

// Add controllers with documentation
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Instead of using Swagger, we'll create an API Documentation endpoint manually
builder.Services.AddCors(options =>
{
    options.AddPolicy("ApiDocumentation", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add response compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/json", "application/xml", "text/plain" });
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

// Configure server URLs from INI settings
// Replace the hardcoded URLs with INI configuration
var urls = IniConfiguration.GetServerUrls();
if (urls.Count == 0)
{
    urls.Add("http://localhost:5000");
    urls.Add("https://localhost:5001");
}
builder.WebHost.UseUrls(urls.ToArray());

var app = builder.Build();

// Configure the HTTP request pipeline
// Add forwarded headers middleware early in the pipeline to correctly capture client IP
app.UseForwardedHeaders();

// Configure error handling based on environment
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    // Add manual API documentation endpoint
    app.MapGet($"/{ApiRoutes.Docs}", () =>
    {
        var apiDocs = new
        {
            Title = "TDF API Documentation",
            Version = "v1.0",
            Endpoints = new[]
            {
                new { Path = $"/{ApiRoutes.Auth.Login}", Method = "POST", Description = "User authentication" },
                new { Path = $"/{ApiRoutes.Auth.RefreshToken}", Method = "POST", Description = "Refresh JWT token" },
                new { Path = $"/{ApiRoutes.Users.Base}", Method = "GET", Description = "Get all users" },
                new { Path = $"/{ApiRoutes.Messages.Base}", Method = "GET", Description = "Get messages" },
                // Add other endpoints as needed
            },
            Authentication = "JWT Bearer Token"
        };

        return Results.Json(apiDocs);
    });
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Use response compression
app.UseResponseCompression();

// Configure security headers
app.UseSecurityHeaders();

// Configure rate limiting
app.UseRateLimiter();

// Add global exception handling middleware
app.UseMiddleware<GlobalExceptionMiddleware>();

// Add WebSocket support before other middleware
// app.UseWebSocketServer(); // Removed undefined extension method

// Add security monitoring middleware
app.UseSecurityMonitoring();

// Add request logging middleware
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseHttpsRedirection();
app.UseCors("CorsPolicy");
app.UseRouting();

// Enable WebSockets
var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
};
app.UseWebSockets(webSocketOptions);
app.UseWebSocketHandler(); // Our custom WebSocket middleware

app.UseAuthentication();
app.UseAuthorization();

// Configure endpoints
app.MapControllers();

// Add debug endpoints in development mode
if (app.Environment.IsDevelopment())
{
    app.MapGet("/debug/config", (HttpContext context) => {
        // Only allow local requests for security
        if (context.Connection.RemoteIpAddress == null ||
            context.Connection.LocalIpAddress == null ||
            (!context.Connection.RemoteIpAddress.Equals(context.Connection.LocalIpAddress) &&
            !IPAddress.IsLoopback(context.Connection.RemoteIpAddress)))
        {
            return Results.Forbid();
        }

        return Results.Json(new {
            Environment = app.Environment.EnvironmentName,
            ServerUrls = urls.ToList(),
            DatabaseConfigured = !string.IsNullOrEmpty(IniConfiguration.ConnectionString),
            JwtConfigured = !string.IsNullOrEmpty(IniConfiguration.JwtIssuer) &&
                           !string.IsNullOrEmpty(IniConfiguration.JwtAudience),
            WebSocketsEnabled = true,
            RateLimits = new {
                Global = IniConfiguration.GetRateLimitSetting("GlobalLimitPerMinute", 100),
                Auth = IniConfiguration.GetRateLimitSetting("AuthLimitPerMinute", 10),
                Api = IniConfiguration.GetRateLimitSetting("ApiLimitPerMinute", 60),
                Static = IniConfiguration.GetRateLimitSetting("StaticLimitPerMinute", 200)
            },
            // Add system information
            System = new {
                OSVersion = Environment.OSVersion.ToString(),
                ProcessorCount = Environment.ProcessorCount,
                WorkingSet = Environment.WorkingSet / (1024 * 1024) + " MB",
                ApplicationDirectory = AppDomain.CurrentDomain.BaseDirectory,
                RuntimeVersion = Environment.Version.ToString()
            },
            // Add database connection test
            Database = new {
                ConnectionString = IniConfiguration.ConnectionString.Replace(
                    IniConfiguration.ConnectionString.Contains("Password=")
                        ? "Password=" + new string('*', 8)
                        : "",
                    "Password=********"),
                Status = "Testing..."
            }
        });
    });

    // Add a debug endpoint to test database connection
    app.MapGet("/debug/database", async (HttpContext context) => {
        // Only allow local requests for security
        if (context.Connection.RemoteIpAddress == null ||
            context.Connection.LocalIpAddress == null ||
            (!context.Connection.RemoteIpAddress.Equals(context.Connection.LocalIpAddress) &&
            !IPAddress.IsLoopback(context.Connection.RemoteIpAddress)))
        {
            return Results.Forbid();
        }

        try {
            using (var connection = new SqlConnection(IniConfiguration.ConnectionString))
            {
                await connection.OpenAsync();
                var serverVersion = connection.ServerVersion;
                var database = connection.Database;

                return Results.Json(new {
                    Status = "Connected",
                    ServerVersion = serverVersion,
                    Database = database,
                    ConnectionTime = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex) {
            return Results.Json(new {
                Status = "Failed",
                Error = ex.Message,
                InnerError = ex.InnerException?.Message,
                Time = DateTime.UtcNow
            });
        }
    });
}

// WebSocket endpoint with authentication check
app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        await context.Response.WriteAsync("Expected a WebSocket request");
        return;
    }

    // Create WebSocket auth helper
    var wsAuthHelper = new WebSocketAuthenticationHelper(
        logger,
        key,
        IniConfiguration.JwtIssuer,
        IniConfiguration.JwtAudience,
        !app.Environment.IsDevelopment()
    );

    // Get token from header
    var authToken = wsAuthHelper.ExtractTokenFromHeader(context);

    // Validate token
    if (string.IsNullOrEmpty(authToken))
    {
        await wsAuthHelper.WriteErrorResponse(
            context,
            HttpStatusCode.Unauthorized,
            "Authentication token must be provided via Authorization header"
        );
        return;
    }

    try
    {
        // Validate token and get user info
        var validationResult = wsAuthHelper.ValidateToken(authToken);
        bool isValid = validationResult.isValid;
        ClaimsPrincipal? principal = validationResult.principal;
        string errorReason = validationResult.errorReason;

        if (!isValid || principal == null)
        {
            await wsAuthHelper.WriteErrorResponse(context, HttpStatusCode.Unauthorized, errorReason);
            return;
        }

        // Token is valid, get user info
        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = principal.FindFirst(ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
        {
            await wsAuthHelper.WriteErrorResponse(
                context,
                HttpStatusCode.Unauthorized,
                "Invalid user information in token"
            );
            return;
        }

        // Log successful WebSocket connection
        logger.LogInformation("WebSocket connection authenticated for user {Username} (ID: {UserId})",
            username, userId);

        // Accept the WebSocket connection
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

        // Get required services
        var wsManager = app.Services.GetRequiredService<WebSocketConnectionManager>();
        var notificationService = app.Services.GetRequiredService<TDFShared.Services.INotificationService>();

        // Create a connection object
        var connection = new WebSocketConnectionEntity
        {
            ConnectionId = Guid.NewGuid().ToString(),
            UserId = int.Parse(userId),
            Username = username,
            IsConnected = true,
            ConnectedAt = DateTime.UtcNow,
            MachineName = Environment.MachineName
        };

        try
        {
            // Let the notification service handle the WebSocket connection
            await notificationService.HandleUserConnectionAsync(connection, webSocket);
        }
        catch (Exception wsEx)
        {
            logger.LogError(wsEx, "Error in WebSocket connection handling for user {Username}: {Message}",
                username, wsEx.Message);

            if (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.InternalServerError,
                        "Internal server error",
                        CancellationToken.None);
                }
                catch
                {
                    // Ignore errors during close
                }
            }
        }
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        await context.Response.WriteAsync("Error processing WebSocket request");
        logger.LogError(ex, "Error processing WebSocket request: {Message}", ex.Message);
    }
});

// Health checks
// Log configured URLs before starting server
logger.LogInformation("Server listening on: {Urls}", string.Join(", ", urls));

app.MapHealthChecks($"/{ApiRoutes.Health.GetDefault}", new HealthCheckOptions
{
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    },
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        // Enhanced health check response with more details
        var response = new
        {
            Status = report.Status.ToString(),
            Duration = report.TotalDuration,
            Timestamp = DateTime.UtcNow,
            Environment = app.Environment.EnvironmentName,
            Info = report.Entries.Select(e => new
            {
                Key = e.Key,
                Status = e.Value.Status.ToString(),
                Description = e.Value.Description,
                Duration = e.Value.Duration,
                Exception = e.Value.Exception?.Message,
                Data = e.Value.Data
            })
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response,
            new JsonSerializerOptions { WriteIndented = true }));
    }
});

// Log server started successfully message with full banner
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine();
Console.WriteLine();
Console.WriteLine(@" _____ ___  _____    _    ___ ___ ");
Console.WriteLine(@"|_   _|   \|  ___|  /_\  | _ \_ _|");
Console.WriteLine(@"  | | | |) |  _|   / _ \ |  _/| | ");
Console.WriteLine(@"  |_| |___/|_|    /_/ \_\|_| |___|");
Console.WriteLine();
Console.WriteLine();
Console.ForegroundColor = ConsoleColor.White;
Console.WriteLine(@"   API Server v1.0 - Started Successfully   ");
Console.WriteLine();
Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Gray;
Console.WriteLine($"* Environment: {app.Environment.EnvironmentName}");
Console.WriteLine($"* URLs: {string.Join(", ", urls)}");
Console.WriteLine($"* Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine();
Console.WriteLine();
Console.ResetColor();

app.Run();
