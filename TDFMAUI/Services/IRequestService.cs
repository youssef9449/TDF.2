using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Common;

namespace TDFMAUI.Services;

public interface IRequestService
{
    Task<RequestResponseDto> CreateRequestAsync(RequestCreateDto requestDto);
    Task<RequestResponseDto> UpdateRequestAsync(Guid requestId, RequestUpdateDto requestDto);
    Task<PaginatedResult<RequestResponseDto>> GetMyRequestsAsync(RequestPaginationDto pagination);
    Task<PaginatedResult<RequestResponseDto>> GetAllRequestsAsync(RequestPaginationDto pagination);
    Task<PaginatedResult<RequestResponseDto>> GetRequestsByDepartmentAsync(string department, RequestPaginationDto pagination);
    Task<RequestResponseDto> GetRequestByIdAsync(Guid requestId);
    Task<bool> DeleteRequestAsync(Guid requestId);
    Task<bool> ApproveRequestAsync(Guid requestId, RequestApprovalDto approvalDto);
    Task<bool> RejectRequestAsync(Guid requestId, RequestRejectDto rejectDto);
} 