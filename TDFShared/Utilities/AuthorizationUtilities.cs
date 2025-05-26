using System;
using System.Collections.Generic;
using System.Linq;
using TDFShared.Services;
using TDFShared.DTOs.Users;
using TDFShared.DTOs.Requests;
using TDFShared.Enums;
using System.Threading.Tasks;

namespace TDFShared.Utilities
{
    /// <summary>
    /// Utility methods for authorization and access control operations
    /// Provides consistent authorization patterns across the application
    /// </summary>
    public static class AuthorizationUtilities
    {
        #region Role-Based Authorization

        /// <summary>
        /// Checks if a user has any of the specified roles
        /// </summary>
        /// <param name="user">The user to check</param>
        /// <param name="roles">The roles to check for</param>
        /// <returns>True if the user has any of the specified roles</returns>
        public static bool HasAnyRole(UserDto user, params string[] roles)
        {
            if (user?.Roles == null || roles == null || roles.Length == 0)
                return false;

            return roles.Any(role => user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks if a user has all of the specified roles
        /// </summary>
        /// <param name="user">The user to check</param>
        /// <param name="roles">The roles to check for</param>
        /// <returns>True if the user has all of the specified roles</returns>
        public static bool HasAllRoles(UserDto user, params string[] roles)
        {
            if (user?.Roles == null || roles == null || roles.Length == 0)
                return false;

            return roles.All(role => user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks if a user is in an administrative role (Admin or HR)
        /// </summary>
        /// <param name="user">The user to check</param>
        /// <returns>True if the user is an admin or HR</returns>
        public static bool IsAdministrative(UserDto user)
        {
            return user != null && (user.IsAdmin || user.IsHR);
        }

        /// <summary>
        /// Checks if a user is in a management role (Admin, HR, or Manager)
        /// </summary>
        /// <param name="user">The user to check</param>
        /// <returns>True if the user is in a management role</returns>
        public static bool IsManagement(UserDto user)
        {
            return user != null && (user.IsAdmin || user.IsHR || user.IsManager);
        }

        #endregion

        #region Department-Based Authorization

        /// <summary>
        /// Checks if a user can access resources from a specific department
        /// </summary>
        /// <param name="user">The user to check</param>
        /// <param name="targetDepartment">The department to check access for</param>
        /// <returns>True if the user can access the department</returns>
        public static bool CanAccessDepartment(UserDto user, string targetDepartment)
        {
            if (user == null || string.IsNullOrEmpty(targetDepartment))
                return false;

            // Admin and HR can access all departments
            if (user.IsAdmin || user.IsHR)
                return true;

            // Managers can access their own department (including constituent departments for hyphenated departments)
            if (user.IsManager && !string.IsNullOrEmpty(user.Department))
                return RequestStateManager.CanManageDepartment(user, targetDepartment);

            // Regular users can only access their own department for limited operations
            return !string.IsNullOrEmpty(user.Department) &&
                   RequestStateManager.CanManageDepartment(user, targetDepartment);
        }

        /// <summary>
        /// Gets the departments a user can access based on their role
        /// </summary>
        /// <param name="user">The user to check</param>
        /// <param name="allDepartments">List of all available departments</param>
        /// <returns>List of departments the user can access</returns>
        public static IEnumerable<string> GetAccessibleDepartments(UserDto user, IEnumerable<string> allDepartments)
        {
            if (user == null || allDepartments == null)
                return Enumerable.Empty<string>();

            // Admin and HR can access all departments
            if (user.IsAdmin || user.IsHR)
                return allDepartments;

            // Managers and regular users can access departments based on their department (including constituent departments for hyphenated departments)
            if (!string.IsNullOrEmpty(user.Department))
                return allDepartments.Where(dept => RequestStateManager.CanManageDepartment(user, dept));

            return Enumerable.Empty<string>();
        }

        #endregion

        #region Request-Specific Authorization

        /// <summary>
        /// Determines the request access level for a user
        /// </summary>
        /// <param name="user">The user to check</param>
        /// <returns>The access level for requests</returns>
        public static RequestAccessLevel GetRequestAccessLevel(UserDto user)
        {
            if (user == null)
                return RequestAccessLevel.None;

            if (user.IsAdmin || user.IsHR)
                return RequestAccessLevel.All;

            if (user.IsManager)
                return RequestAccessLevel.Department;

            return RequestAccessLevel.Own;
        }

        /// <summary>
        /// Checks if a user can perform a specific action on a request
        /// </summary>
        /// <param name="user">The user attempting the action</param>
        /// <param name="request">The request being acted upon</param>
        /// <param name="action">The action being attempted</param>
        /// <returns>True if the action is allowed</returns>
        public static bool CanPerformRequestAction(UserDto user, RequestResponseDto request, RequestAction action)
        {
            if (user == null || request == null)
                return false;

            return action switch
            {
                RequestAction.View => CanViewRequest(user, request),
                RequestAction.Edit => CanEditRequest(user, request),
                RequestAction.Delete => CanDeleteRequest(user, request),
                RequestAction.Approve => CanApproveRequest(user, request),
                RequestAction.Reject => CanRejectRequest(user, request),
                _ => false
            };
        }

        #endregion

        #region Private Helper Methods

        private static bool CanViewRequest(UserDto user, RequestResponseDto request)
        {
            // Admin and HR can view all requests
            if (user.IsAdmin || user.IsHR) return true;

            // Users can view their own requests
            if (request.RequestUserID == user.UserID) return true;

            // Managers can view requests from their department
            return user.IsManager && CanAccessDepartment(user, request.RequestDepartment);
        }

        private static bool CanEditRequest(UserDto user, RequestResponseDto request)
        {
            // Only pending requests can be edited
            if (request.Status != RequestStatus.Pending) return false;

            // Admin can edit any pending request
            if (user.IsAdmin) return true;

            // Users can edit their own pending requests
            return request.RequestUserID == user.UserID;
        }

        private static bool CanDeleteRequest(UserDto user, RequestResponseDto request)
        {
            // Only pending requests can be deleted
            if (request.Status != RequestStatus.Pending) return false;

            // Admin can delete any pending request
            if (user.IsAdmin) return true;

            // Users can delete their own pending requests
            return request.RequestUserID == user.UserID;
        }

        private static bool CanApproveRequest(UserDto user, RequestResponseDto request)
        {
            // Only pending requests can be approved
            if (request.Status != RequestStatus.Pending) return false;

            // Users cannot approve their own requests
            if (request.RequestUserID == user.UserID) return false;

            // Admin and HR can approve all requests
            if (user.IsAdmin || user.IsHR) return true;

            // Managers can approve requests from their department
            return user.IsManager && CanAccessDepartment(user, request.RequestDepartment);
        }

        private static bool CanRejectRequest(UserDto user, RequestResponseDto request)
        {
            // Same logic as approval
            return CanApproveRequest(user, request);
        }

        #endregion

        #region Business Rule Context Helpers

        /// <summary>
        /// Creates a standard BusinessRuleContext for authorization validation
        /// </summary>
        /// <param name="getRequestAsync">Function to get request by ID</param>
        /// <param name="getUserAsync">Function to get user by ID</param>
        /// <returns>Configured BusinessRuleContext</returns>
        public static Validation.BusinessRuleContext CreateAuthorizationContext(
            Func<int, Task<RequestResponseDto?>> getRequestAsync,
            Func<int, Task<UserDto?>> getUserAsync)
        {
            return new Validation.BusinessRuleContext
            {
                GetRequestAsync = getRequestAsync,
                GetUserAsync = getUserAsync
            };
        }

        #endregion
    }

    #region Enums

    /// <summary>
    /// Defines the level of access a user has to requests
    /// </summary>
    public enum RequestAccessLevel
    {
        None,       // No access
        Own,        // Can only access own requests
        Department, // Can access own + department requests
        All         // Can access all requests
    }

    /// <summary>
    /// Defines the possible actions that can be performed on requests
    /// </summary>
    public enum RequestAction
    {
        View,
        Edit,
        Delete,
        Approve,
        Reject
    }

    #endregion
}
