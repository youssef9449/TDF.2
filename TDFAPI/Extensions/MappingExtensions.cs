using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Users;
using TDFShared.Models.Request;
using TDFShared.Models.User;
using TDFShared.Models.Message;
using TDFShared.DTOs.Messages;
using System.Linq;

namespace TDFAPI.Extensions
{
    public static class MappingExtensions
    {
        public static RequestResponseDto ToResponseDto(this RequestEntity entity, int? remainingBalance = null)
        {
            if (entity == null) return null!;

            return new RequestResponseDto
            {
                RequestID = entity.RequestID,
                RequestUserID = entity.RequestUserID,
                UserName = entity.RequestUserFullName,
                LeaveType = entity.RequestType,
                RequestReason = entity.RequestReason,
                RequestStartDate = entity.RequestFromDay,
                RequestEndDate = entity.RequestToDay,
                RequestBeginningTime = entity.RequestBeginningTime,
                RequestEndingTime = entity.RequestEndingTime,
                RequestDepartment = entity.RequestDepartment,
                Status = entity.RequestManagerStatus,
                HRStatus = entity.RequestHRStatus,
                CreatedDate = entity.CreatedAt.GetValueOrDefault(DateTime.MinValue),
                LastModifiedDate = entity.UpdatedAt,
                RequestNumberOfDays = entity.RequestNumberOfDays,
                RemainingBalance = remainingBalance,
                RowVersion = entity.RowVersion
            };
        }

        public static UserDto ToDto(this UserEntity entity)
        {
            if (entity == null) return null!;

            var roles = new List<string>();
            if (entity.IsAdmin == true) roles.Add("Admin");
            if (entity.IsManager == true) roles.Add("Manager");
            if (entity.IsHR == true) roles.Add("HR");

            return new UserDto
            {
                UserID = entity.UserID,
                UserName = entity.UserName,
                FullName = entity.FullName ?? string.Empty,
                Title = entity.Title,
                Department = entity.Department,
                IsConnected = entity.IsConnected,
                PresenceStatus = entity.PresenceStatus,
                IsAdmin = entity.IsAdmin,
                IsManager = entity.IsManager,
                IsHR = entity.IsHR,
                Roles = roles,
                LastLoginDate = entity.LastLoginDate,
                LastLoginIp = entity.LastLoginIp
            };
        }

        public static MessageDto ToDto(this MessageEntity entity, UserEntity? sender = null)
        {
            if (entity == null) return null!;

            return new MessageDto
            {
                Id = entity.MessageID,
                SenderId = entity.SenderID,
                ReceiverId = entity.ReceiverID,
                SenderUsername = sender?.UserName ?? string.Empty,
                SenderFullName = sender?.FullName ?? string.Empty,
                Content = entity.MessageText,
                IsRead = entity.IsRead,
                IsDelivered = entity.IsDelivered,
                Timestamp = entity.Timestamp,
                MessageType = entity.MessageType
            };
        }

        public static ChatMessageDto ToChatDto(this MessageEntity entity, string? senderName = null)
        {
            if (entity == null) return null!;

            return new ChatMessageDto
            {
                MessageId = entity.MessageID,
                SenderId = entity.SenderID,
                SenderName = senderName ?? string.Empty,
                ReceiverId = entity.ReceiverID,
                Content = entity.MessageText,
                MessageType = entity.MessageType,
                IsGlobal = entity.IsGlobal,
                IsRead = entity.IsRead,
                IsDelivered = entity.IsDelivered,
                Department = entity.Department,
                IdempotencyKey = entity.IdempotencyKey,
                Timestamp = entity.Timestamp
            };
        }
    }
}
