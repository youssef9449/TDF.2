using TDFShared.Enums;
using TDFShared.DTOs.Requests;

namespace TDFShared.Services
{
    public static class RequestStateManager
    {
        public static bool CanApprove(RequestResponseDto request)
        {
            return request != null && request.Status == RequestStatus.Pending;
        }

        public static bool CanReject(RequestResponseDto request)
        {
            return request != null && request.Status == RequestStatus.Pending;
        }

        public static bool CanEdit(RequestResponseDto request, bool isAdmin, bool isOwner)
        {
            return request != null && 
                   (isAdmin || (isOwner && request.Status == RequestStatus.Pending));
        }

        public static bool CanDelete(RequestResponseDto request, bool isAdmin, bool isOwner)
        {
            return request != null && 
                   (isAdmin || (isOwner && request.Status == RequestStatus.Pending));
        }
    }
}
