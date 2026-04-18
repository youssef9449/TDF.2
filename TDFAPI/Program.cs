using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TDFAPI.Configuration;
using TDFAPI.Configuration.Options;
using TDFAPI.Extensions.Startup;

var builder = WebApplication.CreateBuilder(args);

// Early bootstrap: logging + crash handlers + logs directory.
StartupBanner.WriteStarting(builder.Environment);
var logger = builder.ConfigureConsoleLogging();
StartupLoggingExtensions.RegisterGlobalExceptionHandlers(logger);
StartupLoggingExtensions.EnsureLogsDirectory(logger);

// Parse config.ini, fold its values into IConfiguration, and register
// strongly-typed IOptions<T> bindings for every configuration section.
try
{
    logger.LogInformation("Initializing configuration from INI file");
    IniConfiguration.Initialize();
    IniConfiguration.UpdateConfigFile();
    ConfigurationSetup.BridgeIniIntoConfiguration(builder);
    ConfigurationSetup.AddTypedOptions(builder);
    logger.LogInformation("Successfully bound typed configuration options");
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Failed to initialize or update INI configuration: {Message}", ex.Message);
    throw;
}

// Resolve an eager snapshot of the typed options for bootstrap-time code
// that can't go through DI (rate-limiter factories, minimal APIs, etc.).
var startupOptions = StartupOptionsSnapshot.FromConfiguration(builder.Configuration);

// Database + EF Core.
PersistenceExtensions.ProbeDatabaseConnection(startupOptions.ConnectionString, logger);
builder.Services.AddTdfPersistence(startupOptions.ConnectionString);
builder.Services.AddTdfHealthChecks(startupOptions.ConnectionString);

// HTTP pipeline primitives.
builder.Services.AddTdfResponseCompression();
builder.Services.AddTdfControllers();
builder.Services.AddTdfRateLimiting(startupOptions.RateLimit);
builder.Services.AddTdfCors(builder.Environment, startupOptions.Cors, logger);
builder.Services.AddTdfWebSockets(startupOptions.WebSockets);
builder.Services.AddTdfJwtAuthentication(builder.Environment, startupOptions.Jwt, logger);

// Application services, repositories, MediatR, background workers.
builder.Services.AddTdfApplicationServices();

// Server URLs (still driven from config.ini for backward compat).
var urls = IniConfiguration.GetServerUrls();
if (urls.Count == 0)
{
    urls.Add("http://localhost:5000");
    urls.Add("https://localhost:5001");
}
builder.WebHost.UseUrls(urls.ToArray());

var app = builder.Build();

app.UseTdfRequestPipeline();
app.MapTdfDevelopmentEndpoints(urls, startupOptions);
app.MapTdfWebSocketEndpoint(startupOptions.Jwt, logger);
app.MapTdfHealthChecks();

logger.LogInformation("Server listening on: {Urls}", string.Join(", ", urls));
StartupBanner.WriteStarted(app.Environment, urls);

app.Run();
