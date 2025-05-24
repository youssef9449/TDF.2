using System.Collections.Generic;
using TDFShared.DTOs.Users;

namespace TDFShared.Services
{
    /// <summary>
    /// Interface for centralized role management service
    /// </summary>
    public interface IRoleService
    {
        /// <summary>
        /// Assigns roles to a user based on their role flags
        /// </summary>
        void AssignRoles(UserDto user);

        /// <summary>
        /// Gets all roles for a user
        /// </summary>
        IReadOnlyList<string> GetRoles(UserDto user);

        /// <summary>
        /// Checks if a user has a specific role
        /// </summary>
        bool HasRole(UserDto user, string role);

        /// <summary>
        /// Checks if a user has any of the specified roles
        /// </summary>
        bool HasAnyRole(UserDto user, params string[] roles);

        /// <summary>
        /// Checks if a user has all of the specified roles
        /// </summary>
        bool HasAllRoles(UserDto user, params string[] roles);

        /// <summary>
        /// Checks if a user is in an administrative role (Admin or HR)
        /// </summary>
        bool IsAdministrative(UserDto user);

        /// <summary>
        /// Checks if a user is in a management role (Admin, HR, or Manager)
        /// </summary>
        bool IsManagement(UserDto user);
    }
} 