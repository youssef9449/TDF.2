using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging;

namespace TDFAPI.Utilities
{
    /// <summary>
    /// Helper class for WebSocket authentication
    /// </summary>
    public class WebSocketAuthenticationHelper
    {
        private readonly ILogger _logger;
        private readonly byte[] _key;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly bool _validateIssuerAndAudience;

        public WebSocketAuthenticationHelper(
            ILogger logger, 
            byte[] key, 
            string issuer, 
            string audience, 
            bool validateIssuerAndAudience)
        {
            _logger = logger;
            _key = key;
            _issuer = issuer;
            _audience = audience;
            _validateIssuerAndAudience = validateIssuerAndAudience;
        }

        /// <summary>
        /// Validates a JWT token and returns the claims principal
        /// </summary>
        public (bool isValid, ClaimsPrincipal? principal, string errorReason) ValidateToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(_key),
                    ValidateIssuer = _validateIssuerAndAudience,
                    ValidateAudience = _validateIssuerAndAudience,
                    ValidIssuer = _issuer,
                    ValidAudience = _audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                // Validate and get claims principal from token
                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
                
                if (validatedToken is not JwtSecurityToken jwtToken || 
                    !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    return (false, null, "Invalid token format");
                }

                return (true, principal, string.Empty);
            }
            catch (SecurityTokenExpiredException)
            {
                _logger.LogWarning("WebSocket connection attempt with expired token");
                return (false, null, "Expired token");
            }
            catch (SecurityTokenException ex)
            {
                _logger.LogWarning(ex, "WebSocket connection attempt with invalid token: {Message}", ex.Message);
                return (false, null, "Invalid token");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing WebSocket authentication: {Message}", ex.Message);
                return (false, null, "Internal server error");
            }
        }

        /// <summary>
        /// Extracts auth token from the Authorization header
        /// </summary>
        public string ExtractTokenFromHeader(HttpContext context)
        {
            var authToken = string.Empty;
            var authHeader = context.Request.Headers.Authorization.ToString();
            
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                authToken = authHeader.Substring(7);
            }
            
            return authToken;
        }

        /// <summary>
        /// Writes an error response to the HTTP context
        /// </summary>
        public async Task WriteErrorResponse(HttpContext context, HttpStatusCode statusCode, string message)
        {
            context.Response.StatusCode = (int)statusCode;
            await context.Response.WriteAsync(message);
        }
    }
} 