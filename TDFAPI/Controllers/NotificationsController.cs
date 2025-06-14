using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TDFShared.Services;
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
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                {
                    _logger.LogWarning("UnreadNotifications: Missing or invalid userId claim. Claims: {Claims}", string.Join(", ", User.Claims.Select(c => $"{c.Type}:{c.Value}")));
                    return BadRequest(ApiResponse<IEnumerable<NotificationDto>>.ErrorResponse("User ID claim missing or invalid in token."));
                }
                var notifications = await _notificationService.GetUnreadNotificationsAsync(userId);
                var notificationDtos = notifications.Select(MapToNotificationDto);
                return Ok(ApiResponse<IEnumerable<NotificationDto>>.SuccessResponse(notificationDtos));
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

        private NotificationDto MapToNotificationDto(TDFShared.Models.Notification.NotificationEntity entity)
        {
            return new NotificationDto
            {
                NotificationId = entity.NotificationID,
                UserId = entity.ReceiverID,
                Message = entity.Message,
                Timestamp = entity.Timestamp,
                IsSeen = entity.IsSeen
            };
        }
    }
}