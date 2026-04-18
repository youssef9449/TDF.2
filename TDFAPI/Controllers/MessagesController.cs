using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TDFAPI.CQRS.Commands;
using TDFAPI.CQRS.Queries;
using TDFShared.Constants;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Messages;

namespace TDFAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route(ApiRoutes.Messages.Base)]
    [EnableRateLimiting("api")]
    public class MessagesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MessagesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedResult<MessageDto>>>> GetMessages(
            [FromQuery] MessagePaginationDto pagination)
        {
            var messages = await _mediator.Send(new GetMessagesQuery
            {
                UserId = GetCurrentUserId(),
                Pagination = pagination
            });
            return Ok(ApiResponse<PaginatedResult<MessageDto>>.SuccessResponse(messages));
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<MessageDto>>> CreateMessage([FromBody] MessageCreateDto messageDto)
        {
            var message = await _mediator.Send(new CreateMessageCommand
            {
                MessageDto = messageDto,
                SenderId = GetCurrentUserId(),
                SenderName = GetCurrentUserName()
            });
            return Ok(ApiResponse<MessageDto>.SuccessResponse(message));
        }

        [HttpPost("chat")]
        public async Task<ActionResult<ApiResponse<ChatMessageDto>>> CreateChatMessage(
            [FromBody] ChatMessageCreateDto messageDto)
        {
            var userId = GetCurrentUserId();

            if (!string.IsNullOrEmpty(messageDto.IdempotencyKey))
            {
                var existingMessage = await _mediator.Send(new GetMessageByIdempotencyKeyQuery
                {
                    IdempotencyKey = messageDto.IdempotencyKey,
                    UserId = userId
                });
                if (existingMessage != null)
                {
                    return Ok(ApiResponse<ChatMessageDto>.SuccessResponse(existingMessage));
                }
            }

            var message = await _mediator.Send(new CreateChatMessageCommand
            {
                MessageDto = messageDto,
                SenderId = userId,
                SenderName = GetCurrentUserName()
            });
            return Ok(ApiResponse<ChatMessageDto>.SuccessResponse(message));
        }

        [HttpGet("chat/recent")]
        public async Task<ActionResult<ApiResponse<List<ChatMessageDto>>>> GetRecentChatMessages(
            [FromQuery] int count = 50)
        {
            var messages = await _mediator.Send(new GetRecentChatMessagesQuery { Count = count });
            return Ok(ApiResponse<List<ChatMessageDto>>.SuccessResponse(messages));
        }

        [HttpPost("{messageId}/read")]
        public async Task<ActionResult<ApiResponse<bool>>> MarkMessageAsRead(int messageId)
        {
            var result = await _mediator.Send(new MarkMessageAsReadCommand
            {
                MessageId = messageId,
                UserId = GetCurrentUserId()
            });
            return Ok(ApiResponse<bool>.SuccessResponse(result));
        }

        [HttpPost("{messageId}/delivered")]
        public async Task<ActionResult<ApiResponse<bool>>> MarkMessageAsDelivered(int messageId)
        {
            var result = await _mediator.Send(new MarkMessageAsDeliveredCommand
            {
                MessageId = messageId,
                UserId = GetCurrentUserId()
            });
            return Ok(ApiResponse<bool>.SuccessResponse(result));
        }

        [HttpGet("unread/count/{userId}")]
        public async Task<ActionResult<ApiResponse<int>>> GetUnreadCount(int userId)
        {
            var count = await _mediator.Send(new GetUnreadMessagesCountQuery { UserId = userId });
            return Ok(ApiResponse<int>.SuccessResponse(count));
        }

        private int GetCurrentUserId()
        {
            var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(value, out var id) ? id : 0;
        }

        private string GetCurrentUserName() =>
            User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
    }
}
