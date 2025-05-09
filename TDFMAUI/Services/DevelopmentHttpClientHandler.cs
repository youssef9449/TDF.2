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

        public DevelopmentHttpClientHandler(ILogger<DevelopmentHttpClientHandler> logger)
        {
            _logger = logger;

            try
            {
                // Always enable these settings for better compatibility
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;
                CheckCertificateRevocationList = false;
                UseCookies = true;
                AllowAutoRedirect = true;
                MaxAutomaticRedirections = 10;

                // Log the handler configuration
                _logger.LogInformation("HttpClientHandler configured with SslProtocols: {SslProtocols}", SslProtocols);

                // Only bypass certificate validation in development mode
                if (ApiConfig.DevelopmentMode)
                {
                    _logger.LogWarning("DEVELOPMENT MODE: SSL certificate validation is disabled. DO NOT USE IN PRODUCTION!");

                    // Disable certificate validation with detailed logging
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
                    {
                        if (sslPolicyErrors != SslPolicyErrors.None)
                        {
                            _logger.LogWarning("SSL certificate validation error: {SslPolicyErrors} - Bypassing in development mode", sslPolicyErrors);

                            // Log certificate details for debugging
                            if (cert != null)
                            {
                                _logger.LogDebug("Certificate Subject: {Subject}", cert.Subject);
                                _logger.LogDebug("Certificate Issuer: {Issuer}", cert.Issuer);
                                _logger.LogDebug("Certificate Valid From: {ValidFrom}, Valid To: {ValidTo}",
                                    cert.NotBefore, cert.NotAfter);
                            }

                            // Log chain details if available
                            if (chain != null)
                            {
                                _logger.LogDebug("Certificate chain has {Count} elements", chain.ChainElements.Count);
                            }
                        }

                        // Always accept in development mode
                        return true;
                    };

                    // Also set this for older Android versions
                    DebugService.LogInfo("DevelopmentHttpClientHandler", "Configured for development with certificate validation disabled");
                }
                else
                {
                    _logger.LogInformation("PRODUCTION MODE: Using standard SSL certificate validation");
                    DebugService.LogInfo("DevelopmentHttpClientHandler", "Configured for production with standard certificate validation");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error configuring DevelopmentHttpClientHandler");
                DebugService.LogError("DevelopmentHttpClientHandler", $"Error during initialization: {ex.Message}");

                // Still try to set the validation callback as a fallback
                try
                {
                    if (ApiConfig.DevelopmentMode)
                    {
                        ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
                        DebugService.LogWarning("DevelopmentHttpClientHandler", "Fallback certificate validation configured");
                    }
                }
                catch (Exception innerEx)
                {
                    DebugService.LogError("DevelopmentHttpClientHandler", $"Fallback configuration failed: {innerEx.Message}");
                }
            }
        }
    }
}
