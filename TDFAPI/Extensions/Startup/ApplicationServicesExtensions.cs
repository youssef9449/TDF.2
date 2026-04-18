using MediatR;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using TDFAPI.CQRS.Behaviors;
using TDFAPI.Messaging;
using TDFAPI.Messaging.Interfaces;
using TDFAPI.Repositories;
using TDFAPI.Services;

namespace TDFAPI.Extensions.Startup
{
    /// <summary>
    /// Application-level service registration: caching, form limits,
    /// repositories, services, MediatR, background workers, API versioning,
    /// and shared library services.
    /// </summary>
    public static class ApplicationServicesExtensions
    {
        public static IServiceCollection AddTdfApplicationServices(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();

            // Limit form size to prevent DoS attacks.
            services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10 MB
                options.ValueLengthLimit = 4 * 1024 * 1024;          // 4 MB
                options.MemoryBufferThreshold = 2 * 1024 * 1024;     // 2 MB
            });

            services.AddMemoryCache();
            services.AddSingleton<ICacheService, CacheService>();

            services.AddSingleton<MessageStore>();
            services.AddSingleton<WebSocketConnectionManager>();
            services.AddSingleton<IEventMediator, EventMediator>();

            // Shared library services used by both API and MAUI apps.
            services.AddScoped<TDFShared.Services.ISecurityService, TDFShared.Services.SecurityService>();

            // Shared HTTP pipeline: auth header -> retry -> telemetry.
            services.AddSingleton<TDFShared.Http.IAuthTokenStore, TDFShared.Http.InMemoryAuthTokenStore>();
            services.AddSingleton<TDFShared.Http.IHttpTelemetry, TDFShared.Http.HttpTelemetry>();
            services.AddTransient<TDFShared.Http.AuthenticationHeaderHandler>();
            services.AddTransient<TDFShared.Http.PollyRetryingHandler>();
            services.AddTransient<TDFShared.Http.HttpTelemetryHandler>();

            services.AddHttpClient<TDFShared.Services.IHttpClientService, TDFShared.Services.HttpClientService>(client =>
            {
                client.Timeout = System.TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("User-Agent", "TDF-API/1.0");
            })
            .AddHttpMessageHandler<TDFShared.Http.AuthenticationHeaderHandler>()
            .AddHttpMessageHandler<TDFShared.Http.PollyRetryingHandler>()
            .AddHttpMessageHandler<TDFShared.Http.HttpTelemetryHandler>();
            services.AddSingleton<TDFShared.Services.IConnectivityService, TDFShared.Services.ConnectivityService>();
            services.AddScoped<TDFShared.Validation.IValidationService, TDFShared.Validation.ValidationService>();
            services.AddScoped<TDFShared.Validation.IBusinessRulesService, TDFShared.Validation.BusinessRulesService>();
            services.AddScoped<TDFShared.Services.IErrorHandlingService, TDFShared.Services.ErrorHandlingService>();
            services.AddScoped<AuthService>();
            services.AddScoped<TDFAPI.Services.IAuthTokenIssuer>(sp => sp.GetRequiredService<AuthService>());

            // Unit of Work and repositories.
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IMessageRepository, MessageRepository>();
            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<IRequestRepository, RequestRepository>();
            services.AddScoped<IRevokedTokenRepository, RevokedTokenRepository>();

            // API-level services.
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<TDFAPI.Services.IMessageService, MessageService>();
            services.AddScoped<ILookupService, LookupService>();
            services.AddScoped<TDFAPI.Services.IUserPresenceService, UserPresenceService>();
            services.AddScoped<TDFAPI.Services.INotificationDispatchService, TDFAPI.Services.NotificationService>();
            services.AddSingleton<TDFAPI.Services.Realtime.IServerWebSocketRouter, TDFAPI.Services.Realtime.ServerWebSocketRouter>();
            services.AddScoped<TDFShared.Services.INotificationService, NotificationServiceAdapter>();
            services.AddScoped<TDFShared.Services.IRoleService, TDFShared.Services.RoleService>();
            services.AddScoped<IPushTokenService, PushTokenService>();
            services.AddScoped<IBackgroundJobService, BackgroundJobService>();

            // Background workers.
            services.AddHostedService<UserInactivityBackgroundService>();
            services.AddHostedService<BackgroundJobService>();

            // MediatR command / query dispatch + pipeline behaviours.
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ExceptionLoggingBehavior<,>));

            // API versioning.
            services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
            });

            return services;
        }
    }
}
