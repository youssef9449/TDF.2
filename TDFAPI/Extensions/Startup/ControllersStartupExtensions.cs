using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using TDFShared.DTOs.Common;

namespace TDFAPI.Extensions.Startup
{
    /// <summary>
    /// MVC controllers and the shared JSON serialization configuration used
    /// across all endpoints.
    /// </summary>
    public static class ControllersStartupExtensions
    {
        public static IServiceCollection AddTdfControllers(this IServiceCollection services)
        {
            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    var centralizedOptions = TDFShared.Helpers.JsonSerializationHelper.DefaultOptions;

                    // Force property names to match [JsonPropertyName] exactly.
                    options.JsonSerializerOptions.PropertyNamingPolicy = null;
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = centralizedOptions.PropertyNameCaseInsensitive;
                    options.JsonSerializerOptions.DefaultIgnoreCondition = centralizedOptions.DefaultIgnoreCondition;
                    options.JsonSerializerOptions.ReferenceHandler = centralizedOptions.ReferenceHandler;

                    foreach (var converter in centralizedOptions.Converters)
                    {
                        options.JsonSerializerOptions.Converters.Add(converter);
                    }
                });

            // Single source of truth for model-binding validation errors: the
            // framework-level 400 response produced by [ApiController] gets
            // wrapped in the same ApiResponse envelope every controller emits
            // for its own errors. Individual controllers therefore no longer
            // need redundant `if (!ModelState.IsValid)` guards.
            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                    new BadRequestObjectResult(
                        ApiResponse<object>.FromModelState(context.ModelState));
            });

            services.AddEndpointsApiExplorer();

            // "ApiDocumentation" CORS policy is used exclusively by the docs
            // endpoint and is intentionally permissive.
            services.AddCors(options =>
            {
                options.AddPolicy("ApiDocumentation", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            return services;
        }
    }
}
