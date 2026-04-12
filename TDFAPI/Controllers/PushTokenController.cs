using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TDFAPI.Services;
using TDFShared.DTOs.Users;

namespace TDFAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route(TDFShared.Constants.ApiRoutes.PushToken.Base)]
    public class PushTokenController : BaseApiController
    {
        private readonly IPushTokenService _pushTokenService;
        private readonly ILogger<PushTokenController> _logger;

        public PushTokenController(
            IPushTokenService pushTokenService,
            ILogger<PushTokenController> logger)
        {
            _pushTokenService = pushTokenService;
            _logger = logger;
        }

        /// <summary>
        /// Register a new push notification token for the current user
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> RegisterToken([FromBody] PushTokenRegistrationDto registration)
        {
            try
            {
                var userId = int.Parse(User.FindFirst("sub")?.Value);
                await _pushTokenService.RegisterTokenAsync(userId, registration);
                return OkResponse(true, "Token registered successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering push token");
                return ErrorResponse("Error registering push token", System.Net.HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        /// <summary>
        /// Unregister a push notification token for the current user
        /// </summary>
        [HttpPost("unregister")]
        public async Task<IActionResult> UnregisterToken([FromBody] string token)
        {
            try
            {
                var userId = int.Parse(User.FindFirst("sub")?.Value);
                await _pushTokenService.UnregisterTokenAsync(userId, token);
                return OkResponse(true, "Token unregistered successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unregistering push token");
                return ErrorResponse("Error unregistering push token", System.Net.HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        /// <summary>
        /// Get all active push tokens for the current user
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetUserTokens()
        {
            try
            {
                var userId = int.Parse(User.FindFirst("sub")?.Value);
                var tokens = await _pushTokenService.GetUserTokensAsync(userId);
                return Ok(tokens);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving push tokens");
                return StatusCode(500, "Error retrieving push tokens");
            }
        }
    }
} 