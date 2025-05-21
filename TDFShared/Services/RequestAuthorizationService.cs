using System;
using System.Collections.Generic;
using System.Linq;
using TDFShared.DTOs.Requests;
using TDFShared.Enums;

namespace TDFShared.Services
{
    /// <summary>
    /// Provides authorization checks for request-related operations based on user roles
    /// </summary>
    public static class RequestAuthorizationService
    {
        /// <summary>
        /// Determines if a user can manage requests based on their role flags
        /// </summary>
        /// <param name="isAdmin">The admin flag from the database</param>
        /// <param name="isManager">The manager flag from the database</param>
        /// <param name="isHR">The HR flag from the database</param>
        /// <returns>True if the user can manage requests</returns>
        public static bool CanManageRequests(bool? isAdmin, bool? isManager, bool? isHR)
        {
            return isAdmin == true || isManager == true || isHR == true;
        }

        /// <summary>
        /// Determines if a user can edit or delete any request
        /// </summary>
        /// <param name="isAdmin">The admin flag from the database</param>
        /// <returns>True if the user can edit or delete any request</returns>
        public static bool CanEditDeleteAny(bool? isAdmin)
        {
            return isAdmin == true;
        }

        /// <summary>
        /// Determines if a user can filter by department
        /// </summary>
        /// <param name="isManager">The manager flag from the database</param>
        /// <returns>True if the user can filter by department</returns>
        public static bool CanFilterByDepartment(bool? isManager)
        {
            return isManager == true;
        }

        /// <summary>
        /// Determines if a user can manage a specific department's requests
        /// </summary>
        /// <param name="isAdmin">Whether the user is an admin</param>
        /// <param name="isManager">Whether the user is a manager</param>
        /// <param name="isHR">Whether the user is in HR</param>
        /// <param name="userDepartment">The user's department</param>
        /// <param name="requestDepartment">The department the request belongs to</param>
        /// <returns>True if the user can manage requests for the specified department</returns>
        public static bool CanManageDepartment(bool? isAdmin, bool? isManager, bool? isHR, string? userDepartment, string? requestDepartment)
        {
            if (isAdmin == true || isHR == true) return true; // Admin and HR can manage any department
            if (isManager != true || string.IsNullOrEmpty(userDepartment) || string.IsNullOrEmpty(requestDepartment)) return false;

            return userDepartment.Equals(requestDepartment, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines if a user can access reports
        /// </summary>
        /// <param name="isAdmin">The admin flag from the database</param>
        /// <param name="isManager">The manager flag from the database</param>
        /// <param name="isHR">The HR flag from the database</param>
        /// <returns>True if the user can access reports</returns>
        public static bool CanAccessReports(bool? isAdmin, bool? isManager, bool? isHR)
        {
            return isAdmin == true || isManager == true || isHR == true;
        }

        /// <summary>
        /// Determines if a user can edit a specific request
        /// </summary>
        /// <param name="request">The request to check</param>
        /// <param name="currentUserId">The ID of the current user</param>
        /// <param name="isAdmin">The admin flag from the database</param>
        /// <returns>True if the user can edit the request</returns>
        public static bool CanEditRequest(RequestResponseDto request, int currentUserId, bool? isAdmin)
        {
            if (request == null) return false;
            return (request.RequestUserID == currentUserId && request.Status == RequestStatus.Pending) || CanEditDeleteAny(isAdmin);
        }

        /// <summary>
        /// Determines if a user can delete a specific request
        /// </summary>
        /// <param name="request">The request to check</param>
        /// <param name="currentUserId">The ID of the current user</param>
        /// <param name="isAdmin">The admin flag from the database</param>
        /// <returns>True if the user can delete the request</returns>
        public static bool CanDeleteRequest(RequestResponseDto request, int currentUserId, bool? isAdmin)
        {
            if (request == null) return false;
            return (request.RequestUserID == currentUserId && request.Status == RequestStatus.Pending) || CanEditDeleteAny(isAdmin);
        }

        /// <summary>
        /// Determines if a user can approve/reject a specific request
        /// </summary>
        /// <param name="request">The request to check</param>
        /// <param name="currentUserId">The ID of the current user</param>
        /// <param name="isAdmin">The admin flag from the database</param>
        /// <param name="isManager">The manager flag from the database</param>
        /// <param name="isHR">The HR flag from the database</param>
        /// <param name="userDepartment">The user's department</param>
        /// <returns>True if the user can approve/reject the request</returns>
        public static bool CanApproveRejectRequest(RequestResponseDto request, int currentUserId, bool? isAdmin, bool? isManager, bool? isHR, string? userDepartment)
        {
            if (request == null || request.Status != RequestStatus.Pending || request.RequestUserID == currentUserId)
                return false;

            var canManage = CanManageRequests(isAdmin, isManager, isHR);
            if (!canManage) return false;

            if (isManager == true && isAdmin != true && isHR != true)
            {
                return request.RequestDepartment?.Equals(userDepartment, StringComparison.OrdinalIgnoreCase) == true;
            }

            return true;
        }
    }
}
