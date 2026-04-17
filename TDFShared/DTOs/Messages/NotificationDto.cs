using System;
using System.Text.Json.Serialization;
using TDFShared.Enums;

namespace TDFShared.DTOs.Messages
{
    /// <summary>
    /// DTO for system notifications
    /// </summary>
    public class NotificationDto : BaseMessageDTO
    {
        public NotificationDto()
        {
            Type = "notification";
        }

        [JsonPropertyName("notificationId")]
        public int NotificationId { get; set; }

        [JsonPropertyName("userId")]
        public int UserId { get; set; }

        [JsonPropertyName("senderId")]
        public int? SenderId { get; set; }

        [JsonPropertyName("senderName")]
        public string? SenderName { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("notificationType")]
        public NotificationType NotificationType { get; set; } = NotificationType.Info;

        [JsonPropertyName("isSeen")]
        public bool IsSeen { get; set; }

        [JsonPropertyName("isBroadcast")]
        public bool IsBroadcast { get; set; }

        [JsonPropertyName("department")]
        public string? Department { get; set; }

        [JsonPropertyName("data")]
        public string? Data { get; set; }
    }
}
