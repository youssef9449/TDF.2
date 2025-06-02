using System;
using System.Collections.Generic;
using System.Linq;
using TDFShared.DTOs.Users;

namespace TDFShared.Services
{
    /// <summary>
    /// Centralized service for managing user roles and role-related operations
    /// </summary>
    public class RoleService : IRoleService
    {
        /// <summary>
        /// Assigns roles to a user based on their role flags
        /// </summary>
        public void AssignRoles(UserDto user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            // Clear existing roles
            user.Roles.Clear();

            // Add roles based on role flags
            if (user.IsAdmin ?? false) user.Roles.Add("Admin");
            if (user.IsManager ?? false) user.Roles.Add("Manager");
            if (user.IsHR ?? false) user.Roles.Add("HR");

            // Add default "User" role if no other roles are assigned
            if (!user.Roles.Any())
            {
                user.Roles.Add("User");
            }
        }

        /// <summary>
        /// Gets all roles for a user
        /// </summary>
        public IReadOnlyList<string> GetRoles(UserDto user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            return user.Roles.AsReadOnly();
        }

        /// <summary>
        /// Checks if a user has a specific role
        /// </summary>
        public bool HasRole(UserDto user, string role)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("Role cannot be null or empty", nameof(role));

            return user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if a user has any of the specified roles
        /// </summary>
        public bool HasAnyRole(UserDto user, params string[] roles)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (roles == null || roles.Length == 0) throw new ArgumentException("Roles cannot be null or empty", nameof(roles));

            return roles.Any(role => HasRole(user, role));
        }

        /// <summary>
        /// Checks if a user has all of the specified roles
        /// </summary>
        public bool HasAllRoles(UserDto user, params string[] roles)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (roles == null || roles.Length == 0) throw new ArgumentException("Roles cannot be null or empty", nameof(roles));

            return roles.All(role => HasRole(user, role));
        }

        /// <summary>
        /// Checks if a user is in an administrative role (Admin or HR)
        /// </summary>
        public bool IsAdministrative(UserDto user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            return (user.IsAdmin ?? false) || (user.IsHR ?? false);
        }

        /// <summary>
        /// Checks if a user is in a management role (Admin, HR, or Manager)
        /// </summary>
        public bool IsManagement(UserDto user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            return (user.IsAdmin ?? false) || (user.IsHR ?? false) || (user.IsManager ?? false);
        }
    }
} 