using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using TDFMAUI.Config;

namespace TDFMAUI.Services
{
    /// <summary>
    /// Custom HttpClientHandler that bypasses SSL certificate validation in development mode
    /// </summary>
    public class DevelopmentHttpClientHandler : HttpClientHandler
    {
        private readonly ILogger<DevelopmentHttpClientHandler> _logger;
        private readonly bool _isDevelopmentMode;
        private static readonly string[] ProductionEnvironments = { "Production", "Staging", "QA" };

        public DevelopmentHttpClientHandler(ILogger<DevelopmentHttpClientHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _isDevelopmentMode = ApiConfig.DevelopmentMode;

            // Check if we're accidentally in production
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (!_isDevelopmentMode && !ProductionEnvironments.Contains(environment))
            {
                _logger.LogWarning("DevelopmentHttpClientHandler initialized in non-production environment: {Environment}", environment);
            }

            try
            {
                ConfigureHandler();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error configuring DevelopmentHttpClientHandler");
                ConfigureFallbackHandler();
            }
        }

        private void ConfigureHandler()
        {
            // Always enable these settings for better compatibility
            SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;
            CheckCertificateRevocationList = false;
            UseCookies = true;
            AllowAutoRedirect = true;
            MaxAutomaticRedirections = 10;

            _logger.LogInformation("HttpClientHandler configured with SslProtocols: {SslProtocols}", SslProtocols);

            if (_isDevelopmentMode)
            {
                ConfigureDevelopmentMode();
            }
            else
            {
                ConfigureProductionMode();
            }
        }

        private void ConfigureDevelopmentMode()
        {
            _logger.LogWarning("DEVELOPMENT MODE: SSL certificate validation is disabled. DO NOT USE IN PRODUCTION!");

            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
            {
                if (sslPolicyErrors != SslPolicyErrors.None)
                {
                    LogCertificateValidationError(cert, chain, sslPolicyErrors);
                }

                // Double-check we're still in development mode
                if (!_isDevelopmentMode)
                {
                    _logger.LogError("Certificate validation bypass attempted in non-development mode!");
                    return false;
                }

                return true;
            };

            DebugService.LogInfo("DevelopmentHttpClientHandler", "Configured for development with certificate validation disabled");
        }

        private void ConfigureProductionMode()
        {
            _logger.LogInformation("PRODUCTION MODE: Using standard SSL certificate validation");
            
            // Use default certificate validation
            ServerCertificateCustomValidationCallback = null;
            
            DebugService.LogInfo("DevelopmentHttpClientHandler", "Configured for production with standard certificate validation");
        }

        private void LogCertificateValidationError(X509Certificate2? cert, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            _logger.LogWarning("SSL certificate validation error: {SslPolicyErrors} - Bypassing in development mode", sslPolicyErrors);

            if (cert != null)
            {
                _logger.LogDebug("Certificate Subject: {Subject}", cert.Subject);
                _logger.LogDebug("Certificate Issuer: {Issuer}", cert.Issuer);
                _logger.LogDebug("Certificate Valid From: {ValidFrom}, Valid To: {ValidTo}",
                    cert.NotBefore, cert.NotAfter);
                _logger.LogDebug("Certificate Thumbprint: {Thumbprint}", cert.Thumbprint);
            }

            if (chain != null)
            {
                _logger.LogDebug("Certificate chain has {Count} elements", chain.ChainElements.Count);
                foreach (var element in chain.ChainElements)
                {
                    _logger.LogDebug("Chain Element: {Subject} - Status: {Status}", 
                        element.Certificate.Subject, element.ChainElementStatus);
                }
            }
        }

        private void ConfigureFallbackHandler()
        {
            try
            {
                if (_isDevelopmentMode)
                {
                    _logger.LogWarning("Using fallback certificate validation configuration");
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
                    {
                        if (!_isDevelopmentMode)
                        {
                            _logger.LogError("Fallback certificate validation bypass attempted in non-development mode!");
                            return false;
                        }
                        return true;
                    };
                    DebugService.LogWarning("DevelopmentHttpClientHandler", "Fallback certificate validation configured");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallback configuration failed");
                DebugService.LogError("DevelopmentHttpClientHandler", $"Fallback configuration failed: {ex.Message}");
                throw; // Re-throw as this is a critical configuration error
            }
        }
    }
}
