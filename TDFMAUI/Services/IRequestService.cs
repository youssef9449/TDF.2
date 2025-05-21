using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Common;

namespace TDFMAUI.Services;

public interface IRequestService
{
    Task<RequestResponseDto> CreateRequestAsync(RequestCreateDto requestDto);
    Task<RequestResponseDto> UpdateRequestAsync(int requestId, RequestUpdateDto requestDto);
    Task<PaginatedResult<RequestResponseDto>> GetMyRequestsAsync(RequestPaginationDto pagination);
    Task<PaginatedResult<RequestResponseDto>> GetAllRequestsAsync(RequestPaginationDto pagination);
    Task<PaginatedResult<RequestResponseDto>> GetRequestsByDepartmentAsync(string department, RequestPaginationDto pagination);
    Task<RequestResponseDto> GetRequestByIdAsync(int requestId);
    Task<bool> DeleteRequestAsync(int requestId);
    Task<bool> ApproveRequestAsync(int requestId, RequestApprovalDto approvalDto);
    Task<bool> RejectRequestAsync(int requestId, RequestRejectDto rejectDto);
}