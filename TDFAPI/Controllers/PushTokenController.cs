using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TDFAPI.Extensions;
using TDFAPI.Models;
using TDFAPI.Services;
using TDFShared.Constants;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Users;

namespace TDFAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route(ApiRoutes.PushToken.Base)]
    public class PushTokenController : ControllerBase
    {
        private readonly IPushTokenService _pushTokenService;

        public PushTokenController(IPushTokenService pushTokenService)
        {
            _pushTokenService = pushTokenService;
        }

        /// <summary>
        /// Register a new push notification token for the current user.
        /// </summary>
        [HttpPost("register")]
        public async Task<ActionResult<ApiResponse<bool>>> RegisterToken(
            [FromBody] PushTokenRegistrationDto registration)
        {
            await _pushTokenService.RegisterTokenAsync(GetCurrentUserId(), registration);
            return Ok(ApiResponse<bool>.SuccessResponse(true, "Push token registered successfully"));
        }

        /// <summary>
        /// Unregister a push notification token for the current user.
        /// </summary>
        [HttpPost("unregister")]
        public async Task<ActionResult<ApiResponse<bool>>> UnregisterToken([FromBody] string token)
        {
            await _pushTokenService.UnregisterTokenAsync(GetCurrentUserId(), token);
            return Ok(ApiResponse<bool>.SuccessResponse(true, "Push token unregistered successfully"));
        }

        /// <summary>
        /// Get all active push tokens for the current user.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<IEnumerable<PushTokenDto>>>> GetUserTokens()
        {
            var tokens = await _pushTokenService.GetUserTokensAsync(GetCurrentUserId());
            var dtos = tokens.Select(t => t.ToDto());
            return Ok(ApiResponse<IEnumerable<PushTokenDto>>.SuccessResponse(dtos));
        }

        private int GetCurrentUserId()
        {
            var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(value, out var id) ? id : 0;
        }
    }
}
