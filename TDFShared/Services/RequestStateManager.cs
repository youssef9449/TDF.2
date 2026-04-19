using TDFShared.Enums;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Users;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TDFShared.Services
{
    /// <summary>
    /// Centralized request state and authorization management
    /// Provides consistent access control logic across the application
    /// </summary>
    public static class RequestStateManager
    {
        #region Department Utilities

        /// <summary>
        /// Parses a department name and returns all constituent departments.
        /// For hyphenated departments like "department 1 - department 2", returns both "department 1" and "department 2".
        /// For regular departments, returns the department itself.
        /// </summary>
        /// <param name="department">The department name to parse</param>
        /// <returns>List of constituent department names</returns>
        private static List<string> GetConstituentDepartments(string department)
        {
            if (string.IsNullOrEmpty(department))
                return new List<string>();

            // Split by hyphen and trim whitespace
            var departments = department.Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(d => d.Trim())
                                      .Where(d => !string.IsNullOrEmpty(d))
                                      .ToList();

            // If no hyphen found, return the original department
            return departments.Any() ? departments : new List<string> { department };
        }

        /// <summary>
        /// Checks if a manager's department allows access to a target department.
        /// Handles hyphenated departments where "department 1 - department 2" gives access to both "department 1" and "department 2".
        /// </summary>
        /// <param name="managerDepartment">The manager's department</param>
        /// <param name="targetDepartment">The target department to check access for</param>
        /// <returns>True if the manager can access the target department</returns>
        public static bool CanAccessDepartment(string managerDepartment, string targetDepartment)
        {
            if (string.IsNullOrEmpty(managerDepartment) || string.IsNullOrEmpty(targetDepartment))
                return false;

            // Get all departments the manager can access
            var managerDepartments = GetConstituentDepartments(managerDepartment);

            // Get all departments that the target represents
            var targetDepartments = GetConstituentDepartments(targetDepartment);

            // Check if any of the manager's departments match any of the target departments
            return managerDepartments.Any(md =>
                targetDepartments.Any(td =>
                    string.Equals(md, td, StringComparison.OrdinalIgnoreCase)));
        }

        #endregion

        #region Request State Validation

        /// <summary>
        /// Determines if a request can be edited based on user permissions and request status
        /// </summary>
        /// <param name="request">The request to check</param>
        /// <param name="isAdmin">Whether the user is an admin</param>
        /// <param name="isOwner">Whether the user owns the request</param>
        /// <returns>True if the request can be edited</returns>
        public static bool CanEdit(RequestResponseDto request, bool isAdmin, bool isOwner)
        {
            return request != null &&
                   (isAdmin || (isOwner && request.Status == RequestStatus.Pending));
        }

        /// <summary>
        /// Determines if a request can be deleted based on user permissions and request status
        /// </summary>
        /// <param name="request">The request to check</param>
        /// <param name="isAdmin">Whether the user is an admin</param>
        /// <param name="isOwner">Whether the user owns the request</param>
        /// <returns>True if the request can be deleted</returns>
        public static bool CanDelete(RequestResponseDto request, bool isAdmin, bool isOwner)
        {
            return request != null &&
                   (isAdmin || (isOwner && request.Status == RequestStatus.Pending));
        }

        #endregion

        #region Authorization Logic

        /// <summary>
        /// Determines if a user can view a specific request based on their role and department
        /// </summary>
        /// <param name="request">The request to check access for</param>
        /// <param name="currentUser">The current user attempting to access the request</param>
        /// <returns>True if the user can view the request</returns>
        public static bool CanViewRequest(RequestResponseDto request, UserDto currentUser)
        {
            if (request == null || currentUser == null) return false;

            // Admin and HR can view all requests
            if ((currentUser.IsAdmin ?? false) || (currentUser.IsHR ?? false)) return true;

            // Users can view their own requests
            if (request.RequestUserID == currentUser.UserID) return true;

            // Managers can view requests from their department (including constituent departments for hyphenated departments)
            if ((currentUser.IsManager ?? false) &&
                !string.IsNullOrEmpty(currentUser.Department) &&
                CanAccessDepartment(currentUser.Department, request.RequestDepartment))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines if a user can act at the manager stage (approve or reject while
        /// the request is still pending manager review).
        /// </summary>
        public static bool CanManagerAct(RequestResponseDto request, UserDto currentUser)
        {
            if (request == null || currentUser == null) return false;
            if (request.RequestUserID == currentUser.UserID) return false;

            // Manager stage only applies while the request has not left Pending
            if (request.Status != RequestStatus.Pending) return false;

            // Admin/HR can always act at the manager stage
            if ((currentUser.IsAdmin ?? false) || (currentUser.IsHR ?? false)) return true;

            // Managers can act on requests from their department (including
            // constituent departments for hyphenated departments like "dep1-dep2")
            return (currentUser.IsManager ?? false) &&
                   !string.IsNullOrEmpty(currentUser.Department) &&
                   CanAccessDepartment(currentUser.Department, request.RequestDepartment);
        }

        /// <summary>
        /// Determines if a user can act at the HR stage (approve or reject after
        /// the manager has already approved).
        /// </summary>
        public static bool CanHRAct(RequestResponseDto request, UserDto currentUser)
        {
            if (request == null || currentUser == null) return false;
            if (request.RequestUserID == currentUser.UserID) return false;

            // HR stage only applies after manager approval and before a final HR decision
            if (request.Status != RequestStatus.ManagerApproved) return false;
            if (request.HRStatus != RequestStatus.Pending) return false;

            return (currentUser.IsAdmin ?? false) || (currentUser.IsHR ?? false);
        }

        /// <summary>
        /// Determines if a user can approve a specific request at whichever stage it
        /// is currently at (manager or HR).
        /// </summary>
        public static bool CanApproveRequest(RequestResponseDto request, UserDto currentUser)
        {
            return CanManagerAct(request, currentUser) || CanHRAct(request, currentUser);
        }

        /// <summary>
        /// Determines if a user can reject a specific request at whichever stage it
        /// is currently at (manager or HR).
        /// </summary>
        public static bool CanRejectRequest(RequestResponseDto request, UserDto currentUser)
        {
            return CanApproveRequest(request, currentUser);
        }

        /// <summary>
        /// Determines if a user can approve or reject a specific request (Legacy/Helper)
        /// </summary>
        public static bool CanApproveOrRejectRequest(RequestResponseDto request, UserDto currentUser)
        {
            return CanApproveRequest(request, currentUser);
        }

        /// <summary>
        /// Determines if a user can manage requests (view multiple requests, filter, etc.)
        /// </summary>
        /// <param name="user">The user to check management rights for</param>
        /// <returns>True if the user can manage requests</returns>
        public static bool CanManageRequests(UserDto user)
        {
            return user != null && ((user.IsAdmin ?? false) || (user.IsManager ?? false) || (user.IsHR ?? false));
        }

        /// <summary>
        /// Determines if a user can manage a specific department
        /// </summary>
        /// <param name="user">The user to check department management rights for</param>
        /// <param name="department">The department to check</param>
        /// <returns>True if the user can manage the department</returns>
        public static bool CanManageDepartment(UserDto user, string department)
        {
            if (user == null || string.IsNullOrEmpty(department)) return false;

            // Admin and HR can manage all departments
            if ((user.IsAdmin ?? false) || (user.IsHR ?? false)) return true;

            // Managers can manage their own department (including constituent departments for hyphenated departments)
            return (user.IsManager ?? false) &&
                   !string.IsNullOrEmpty(user.Department) &&
                   CanAccessDepartment(user.Department, department);
        }

        #endregion
    }
}
