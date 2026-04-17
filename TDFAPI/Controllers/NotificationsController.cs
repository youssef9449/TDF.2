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
                var dtos = notifications.Select(n => new NotificationDto
                {
                    NotificationId = n.NotificationID,
                    UserId = n.ReceiverID,
                    SenderId = n.SenderID,
                    Message = n.Message ?? string.Empty,
                    Timestamp = n.Timestamp,
                    IsSeen = n.IsSeen,
                    Title = "Notification"
                });
                return Ok(ApiResponse<IEnumerable<NotificationDto>>.SuccessResponse(dtos));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unread notifications");
                return StatusCode(500, ApiResponse<IEnumerable<NotificationDto>>.ErrorResponse("Error retrieving unread notifications"));
            }
        }

        [HttpPost("{notificationId}/seen")]
        public async Task<ActionResult<ApiResponse<bool>>> MarkAsSeen(int notificationId)
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

        [HttpDelete("{notificationId}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteNotification(int notificationId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var result = await _notificationService.DeleteNotificationAsync(notificationId, userId);
                return Ok(ApiResponse<bool>.SuccessResponse(result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification");
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("Error deleting notification"));
            }
        }

        [HttpPost("broadcast")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<ActionResult<ApiResponse<bool>>> BroadcastNotification([FromBody] BroadcastNotificationDto notification)
        {
            try
            {
                await _notificationService.SendDepartmentNotificationAsync(notification.Department ?? string.Empty, "Broadcast", notification.Message);
                return Ok(ApiResponse<bool>.SuccessResponse(true));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting notification");
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("Error broadcasting notification"));
            }
        }
    }
}
