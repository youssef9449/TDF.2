using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TDFAPI.Services;
using TDFShared.Exceptions;
using TDFShared.DTOs.Messages;
using TDFShared.DTOs.Common;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using TDFAPI.Exceptions;

namespace TDFAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("api")]
    public class MessagesController : ControllerBase
    {
        private readonly MessageService _messageService;
        private readonly ILogger<MessagesController> _logger;
        private readonly IAntiforgery _antiforgery;

        public MessagesController(MessageService messageService, ILogger<MessagesController> logger, IAntiforgery antiforgery)
        {
            _messageService = messageService;
            _logger = logger;
            _antiforgery = antiforgery;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedResult<MessageDto>>>> GetMessages([FromQuery] MessagePaginationDto pagination)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
.SelectMany(v => v.Errors)
.Select(e => e.ErrorMessage)
.ToList();
                    return BadRequest(ApiResponse<PaginatedResult<MessageDto>>.ErrorResponse(
                        "Invalid request parameters: " + string.Join("; ", errors)));
                }

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var messages = await _messageService.GetByUserIdAsync(userId, pagination);
                return Ok(ApiResponse<PaginatedResult<MessageDto>>.SuccessResponse(messages));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument in GetMessages");
                return BadRequest(ApiResponse<PaginatedResult<MessageDto>>.ErrorResponse(ex.Message));
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

                var message = await _messageService.CreateAsync(messageDto, userId, userName);
                return Ok(ApiResponse<MessageDto>.SuccessResponse(message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating message");
                return StatusCode(500, ApiResponse<MessageDto>.ErrorResponse("Error creating message"));
            }
        }

        [HttpPost("chat")]
        [ProducesResponseType(typeof(ApiResponse<ChatMessageDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ChatMessageDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<ChatMessageDto>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<ChatMessageDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ChatMessageDto>>> CreateChatMessage([FromBody] ChatMessageCreateDto messageDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
    .SelectMany(v => v.Errors)
    .Select(e => e.ErrorMessage)
    .ToList();

                    return BadRequest(ApiResponse<PaginatedResult<MessageDto>>.ErrorResponse(
                        "Invalid request parameters: " + string.Join("; ", errors)));
                }

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";

                // Check for existing message with the same idempotency key if provided
                if (!string.IsNullOrEmpty(messageDto.IdempotencyKey))
                {
                    var existingMessage = await _messageService.GetByIdempotencyKeyAsync(messageDto.IdempotencyKey, userId);
                    if (existingMessage != null)
                    {
                        // Return existing message to avoid duplication
                        _logger.LogInformation("Duplicate message detected with key {Key} from user {UserId}",
                            messageDto.IdempotencyKey, userId);

                        return Ok(ApiResponse<ChatMessageDto>.SuccessResponse(existingMessage));
                    }
                }

                var message = await _messageService.CreateChatMessageAsync(messageDto, userId, userName);
                return Ok(ApiResponse<ChatMessageDto>.SuccessResponse(message));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument in CreateChatMessage");
                return BadRequest(ApiResponse<ChatMessageDto>.ErrorResponse(ex.Message));
            }
            catch (ConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency issue when creating chat message");
                return Conflict(ApiResponse<ChatMessageDto>.ErrorResponse(ex.Message));
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
                var messages = await _messageService.GetRecentChatMessagesAsync(count);
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
                var result = await _messageService.MarkAsReadAsync(messageId, userId);
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
                var result = await _messageService.MarkAsDeliveredAsync(messageId, userId);
                return Ok(ApiResponse<bool>.SuccessResponse(result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message as delivered");
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("Error marking message as delivered"));
            }
        }

        [HttpPost("delivered/bulk")]
        public async Task<ActionResult<ApiResponse<bool>>> MarkMessagesAsDeliveredBulk([FromBody] MarkMessagesDeliveredRequest request)
        {
            try
            {
                if (request == null || (request.MessageIds == null || !request.MessageIds.Any()))
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse("No message IDs provided"));
                }

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var results = new List<bool>();

                foreach (var messageId in request.MessageIds)
                {
                    var result = await _messageService.MarkAsDeliveredAsync(messageId, userId);
                    results.Add(result);
                }

                return Ok(ApiResponse<bool>.SuccessResponse(results.All(r => r)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as delivered in bulk");
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("Error marking messages as delivered in bulk"));
            }
        }

        [HttpPost("delivered/from-sender")]
        public async Task<ActionResult<ApiResponse<bool>>> MarkMessagesAsDeliveredFromSender([FromBody] SenderDeliveredRequest request)
        {
            try
            {
                if (request == null || request.SenderId <= 0)
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse("Invalid sender ID"));
                }

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

                // GetUndeliveredMessages and mark them as delivered
                var undeliveredMessages = await _messageService.GetUndeliveredMessagesAsync(request.SenderId, userId);
                var messageIds = undeliveredMessages.Select(m => m.Id).ToList();

                if (!messageIds.Any())
                {
                    return Ok(ApiResponse<bool>.SuccessResponse(true)); // No messages to mark
                }

                foreach (var messageId in messageIds)
                {
                    await _messageService.MarkAsDeliveredAsync(messageId, userId);
                }

                return Ok(ApiResponse<bool>.SuccessResponse(true));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages from sender as delivered");
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("Error marking messages from sender as delivered"));
            }
        }
    }

    public class MarkMessagesDeliveredRequest
    {
        public List<int> MessageIds { get; set; }
    }

    public class SenderDeliveredRequest
    {
        public int SenderId { get; set; }
    }
}