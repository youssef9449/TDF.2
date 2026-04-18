using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TDFShared.Constants;
using TDFShared.DTOs.Messages;
using TDFShared.DTOs.Common;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using MediatR;
using TDFAPI.CQRS.Queries;
using TDFAPI.CQRS.Commands;

namespace TDFAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route(ApiRoutes.Messages.Base)]
    [EnableRateLimiting("api")]
    public class MessagesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<MessagesController> _logger;

        public MessagesController(IMediator mediator, ILogger<MessagesController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedResult<MessageDto>>>> GetMessages([FromQuery] MessagePaginationDto pagination)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var messages = await _mediator.Send(new GetMessagesQuery { UserId = userId, Pagination = pagination });
                return Ok(ApiResponse<PaginatedResult<MessageDto>>.SuccessResponse(messages));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving messages");
                return StatusCode(500, ApiResponse<PaginatedResult<MessageDto>>.ErrorResponse("Error retrieving messages"));
            }
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<MessageDto>>> CreateMessage([FromBody] MessageCreateDto messageDto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";

                var message = await _mediator.Send(new CreateMessageCommand
                {
                    MessageDto = messageDto,
                    SenderId = userId,
                    SenderName = userName
                });
                return Ok(ApiResponse<MessageDto>.SuccessResponse(message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating message");
                return StatusCode(500, ApiResponse<MessageDto>.ErrorResponse("Error creating message"));
            }
        }

        [HttpPost("chat")]
        public async Task<ActionResult<ApiResponse<ChatMessageDto>>> CreateChatMessage([FromBody] ChatMessageCreateDto messageDto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";

                if (!string.IsNullOrEmpty(messageDto.IdempotencyKey))
                {
                    var existingMessage = await _mediator.Send(new GetMessageByIdempotencyKeyQuery
                    {
                        IdempotencyKey = messageDto.IdempotencyKey,
                        UserId = userId
                    });
                    if (existingMessage != null) return Ok(ApiResponse<ChatMessageDto>.SuccessResponse(existingMessage));
                }

                var message = await _mediator.Send(new CreateChatMessageCommand
                {
                    MessageDto = messageDto,
                    SenderId = userId,
                    SenderName = userName
                });
                return Ok(ApiResponse<ChatMessageDto>.SuccessResponse(message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating chat message");
                return StatusCode(500, ApiResponse<ChatMessageDto>.ErrorResponse("Error creating chat message"));
            }
        }

        [HttpGet("chat/recent")]
        public async Task<ActionResult<ApiResponse<List<ChatMessageDto>>>> GetRecentChatMessages([FromQuery] int count = 50)
        {
            try
            {
                var messages = await _mediator.Send(new GetRecentChatMessagesQuery { Count = count });
                return Ok(ApiResponse<List<ChatMessageDto>>.SuccessResponse(messages));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent chat messages");
                return StatusCode(500, ApiResponse<List<ChatMessageDto>>.ErrorResponse("Error retrieving recent chat messages"));
            }
        }

        [HttpPost("{messageId}/read")]
        public async Task<ActionResult<ApiResponse<bool>>> MarkMessageAsRead(int messageId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var result = await _mediator.Send(new MarkMessageAsReadCommand { MessageId = messageId, UserId = userId });
                return Ok(ApiResponse<bool>.SuccessResponse(result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message as read");
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("Error marking message as read"));
            }
        }

        [HttpPost("{messageId}/delivered")]
        public async Task<ActionResult<ApiResponse<bool>>> MarkMessageAsDelivered(int messageId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var result = await _mediator.Send(new MarkMessageAsDeliveredCommand { MessageId = messageId, UserId = userId });
                return Ok(ApiResponse<bool>.SuccessResponse(result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message as delivered");
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("Error marking message as delivered"));
            }
        }

        [HttpGet("unread/count/{userId}")]
        public async Task<ActionResult<ApiResponse<int>>> GetUnreadCount(int userId)
        {
            try
            {
                var count = await _mediator.Send(new GetUnreadMessagesCountQuery { UserId = userId });
                return Ok(ApiResponse<int>.SuccessResponse(count));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread count");
                return StatusCode(500, ApiResponse<int>.ErrorResponse("Error getting unread count"));
            }
        }
    }
}
