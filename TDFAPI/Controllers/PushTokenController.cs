using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TDFAPI.Services;
using TDFShared.DTOs.Users;
using TDFShared.DTOs.Common;
using TDFAPI.Models;
using TDFAPI.Extensions;
using System.Security.Claims;

namespace TDFAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route(TDFShared.Constants.ApiRoutes.PushToken.Base)]
    public class PushTokenController : ControllerBase
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
        public async Task<ActionResult<ApiResponse<bool>>> RegisterToken([FromBody] PushTokenRegistrationDto registration)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                await _pushTokenService.RegisterTokenAsync(userId, registration);
                return Ok(ApiResponse<bool>.SuccessResponse(true, "Push token registered successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering push token");
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("Error registering push token"));
            }
        }

        /// <summary>
        /// Unregister a push notification token for the current user
        /// </summary>
        [HttpPost("unregister")]
        public async Task<ActionResult<ApiResponse<bool>>> UnregisterToken([FromBody] string token)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                await _pushTokenService.UnregisterTokenAsync(userId, token);
                return Ok(ApiResponse<bool>.SuccessResponse(true, "Push token unregistered successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unregistering push token");
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("Error unregistering push token"));
            }
        }

        /// <summary>
        /// Get all active push tokens for the current user
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<IEnumerable<PushTokenDto>>>> GetUserTokens()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var tokens = await _pushTokenService.GetUserTokensAsync(userId);
                var dtos = tokens.Select(t => t.ToDto());
                return Ok(ApiResponse<IEnumerable<PushTokenDto>>.SuccessResponse(dtos));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving push tokens");
                return StatusCode(500, ApiResponse<IEnumerable<PushTokenDto>>.ErrorResponse("Error retrieving push tokens"));
            }
        }
    }
}
