using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TDFAPI.Services;
using TDFShared.Constants;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Messages;

namespace TDFAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route(ApiRoutes.Notifications.Base)]
    [EnableRateLimiting("api")]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationDispatchService _notificationService;

        public NotificationsController(INotificationDispatchService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpGet("unread")]
        public async Task<ActionResult<ApiResponse<IEnumerable<NotificationDto>>>> GetUnreadNotifications()
        {
            var notifications = await _notificationService.GetUnreadNotificationsAsync(GetCurrentUserId());
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

        [HttpPost("{notificationId}/seen")]
        public async Task<ActionResult<ApiResponse<bool>>> MarkAsSeen(int notificationId)
        {
            var result = await _notificationService.MarkAsSeenAsync(notificationId, GetCurrentUserId());
            return Ok(ApiResponse<bool>.SuccessResponse(result));
        }

        [HttpDelete("{notificationId}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteNotification(int notificationId)
        {
            var result = await _notificationService.DeleteNotificationAsync(notificationId, GetCurrentUserId());
            return Ok(ApiResponse<bool>.SuccessResponse(result));
        }

        [HttpPost("broadcast")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<ActionResult<ApiResponse<bool>>> BroadcastNotification(
            [FromBody] BroadcastNotificationDto notification)
        {
            await _notificationService.SendDepartmentNotificationAsync(
                notification.Department ?? string.Empty,
                "Broadcast",
                notification.Message);
            return Ok(ApiResponse<bool>.SuccessResponse(true));
        }

        private int GetCurrentUserId()
        {
            var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(value, out var id) ? id : 0;
        }
    }
}
