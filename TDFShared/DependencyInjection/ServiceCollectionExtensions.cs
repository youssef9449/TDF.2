using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace TDFShared.DependencyInjection
{
    /// <summary>
    /// Extension methods for IServiceCollection to register shared services
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers shared services that can be used by both API and MAUI projects.
        /// Note: Most shared services are implemented as static classes and don't require DI registration.
        /// Use this method to register any future shared services that do require DI.
        /// </summary>
        public static IServiceCollection AddSharedServices(this IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            // Note: Validation services are now registered as dependency injection services.
            // Use IValidationService and IBusinessRulesService for comprehensive validation.


            return services;
        }

        /// <summary>
        /// Registers API-specific services
        /// </summary>
        public static IServiceCollection AddApiServices(this IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            // Register API-specific services here
            // services.TryAddScoped<IApiSpecificService, ApiSpecificService>();
            return services;
        }


        /// <summary>
        /// Registers MAUI-specific services
        /// </summary>
        public static IServiceCollection AddMauiServices(this IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            // Register MAUI-specific services here
            // services.TryAddSingleton<IMauiSpecificService, MauiSpecificService>();
            return services;
        }
    }
}
