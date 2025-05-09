using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TDFAPI.Middleware
{
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

        public string? ExtractTokenFromHeader(HttpContext context)
        {
            var header = context.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return header.Substring("Bearer ".Length).Trim();
        }

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
                    ClockSkew = TimeSpan.FromMinutes(1) // Match HTTP auth clock skew
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

                if (validatedToken is JwtSecurityToken jwtToken)
                {
                    var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (string.IsNullOrEmpty(userId))
                    {
                        return (false, null, "Token does not contain user ID claim");
                    }

                    return (true, principal, string.Empty);
                }

                return (false, null, "Invalid token format");
            }
            catch (SecurityTokenExpiredException)
            {
                return (false, null, "Token has expired");
            }
            catch (SecurityTokenInvalidSignatureException)
            {
                return (false, null, "Token has invalid signature");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token validation error");
                return (false, null, "Invalid token");
            }
        }

        public async Task WriteErrorResponse(HttpContext context, HttpStatusCode statusCode, string message)
        {
            context.Response.StatusCode = (int)statusCode;
            await context.Response.WriteAsync(message);
        }
    }
}