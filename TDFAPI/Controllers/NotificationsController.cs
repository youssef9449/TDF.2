using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TDFAPI.Services;
using TDFShared.DTOs.Messages;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using TDFShared.DTOs.Common;
using TDFShared.Constants;

namespace TDFAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route(ApiRoutes.Notifications.Base)]
    [EnableRateLimiting("api")]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(INotificationService notificationService, ILogger<NotificationsController> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        [HttpGet("unread")]
        public async Task<ActionResult<ApiResponse<IEnumerable<NotificationDto>>>> GetUnreadNotifications()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var notifications = await _notificationService.GetUnreadNotificationsAsync(userId);
                return Ok(ApiResponse<IEnumerable<NotificationDto>>.SuccessResponse(notifications));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unread notifications");
                return StatusCode(500, ApiResponse<IEnumerable<NotificationDto>>.ErrorResponse("Error retrieving unread notifications"));
            }
        }

        [HttpPost("{notificationId}/seen")]
        public async Task<ActionResult<ApiResponse<bool>>> MarkAsRead(int notificationId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var result = await _notificationService.MarkAsSeenAsync(notificationId, userId);
                return Ok(ApiResponse<bool>.SuccessResponse(result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification as seen");
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("Error marking notification as seen"));
            }
        }

        [HttpPost("broadcast")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<ActionResult<ApiResponse<bool>>> BroadcastNotification([FromBody] BroadcastNotificationDto notification)
        {
            try
            {
                var senderId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var result = await _notificationService.BroadcastNotificationAsync(notification.Message, senderId, notification.Department);
                return Ok(ApiResponse<bool>.SuccessResponse(result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting notification");
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("Error broadcasting notification"));
            }
        }
    }
}