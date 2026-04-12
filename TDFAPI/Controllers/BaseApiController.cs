using Microsoft.AspNetCore.Mvc;
using System.Net;
using TDFShared.DTOs.Common;

namespace TDFAPI.Controllers
{
    public abstract class BaseApiController : ControllerBase
    {
        protected IActionResult OkResponse<T>(T data, string message = "Success")
        {
            return Ok(ApiResponse<T>.SuccessResponse(data, message));
        }

        protected IActionResult CreatedResponse<T>(T data, string message = "Created")
        {
            return StatusCode((int)HttpStatusCode.Created, ApiResponse<T>.SuccessResponse(data, message, HttpStatusCode.Created));
        }

        protected IActionResult ErrorResponse(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest, string? errorMessage = null, Dictionary<string, List<string>>? errors = null)
        {
            return StatusCode((int)statusCode, ApiResponse<object>.ErrorResponse(message, statusCode, errorMessage, errors));
        }
    }
}
