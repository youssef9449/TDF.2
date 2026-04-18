using System;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TDFAPI.Configuration.Options;
using TDFShared.Services;

namespace TDFAPI.Extensions.Startup
{
    /// <summary>
    /// JWT bearer authentication configuration. The registered token
    /// validation parameters enforce issuer / audience / lifetime checks in
    /// every environment and consult <see cref="IAuthService"/> to reject
    /// revoked tokens on validation.
    /// </summary>
    public static class JwtAuthenticationExtensions
    {
        public static IServiceCollection AddTdfJwtAuthentication(
            this IServiceCollection services,
            IHostEnvironment env,
            JwtOptions jwtOptions,
            ILogger logger)
        {
            if (string.IsNullOrEmpty(jwtOptions.SecretKey))
            {
                throw new InvalidOperationException("JWT Secret Key not configured (Jwt:SecretKey).");
            }

            var key = Encoding.ASCII.GetBytes(jwtOptions.SecretKey);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = !env.IsDevelopment();
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    RequireSignedTokens = true,
                    RequireExpirationTime = true
                };

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
                        var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                        if (!string.IsNullOrEmpty(jti))
                        {
                            var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
                            // JwtBearerEvents is a synchronous pipeline; blocking on the revocation check
                            // is deliberate here. A fully async check would require a custom middleware.
                            if (authService.IsTokenRevokedAsync(jti).Result)
                            {
                                logger.LogWarning(
                                    "Token validation failed: Token has been revoked (JTI: {Jti})",
                                    jti);
                                context.Fail("Token has been revoked.");
                                return Task.CompletedTask;
                            }
                        }
                        else
                        {
                            logger.LogWarning(
                                "Token validation warning: JTI claim missing, cannot check revocation status.");
                        }

                        var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier);
                        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out _))
                        {
                            context.Fail("Invalid token: Missing or invalid user identifier");
                            logger.LogWarning("Token validation failed: Invalid user identifier");
                        }
                        else
                        {
                            var username = context.Principal?.Identity?.Name;
                            if (!context.HttpContext.Items.ContainsKey("UserAuthenticatedLogged"))
                            {
                                logger.LogInformation("User {Username} successfully authenticated", username);
                                context.HttpContext.Items["UserAuthenticatedLogged"] = true;
                            }
                        }
                        return Task.CompletedTask;
                    }
                };
            });

            return services;
        }
    }
}
