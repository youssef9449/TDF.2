using System.Collections.Generic;

namespace TDFShared.DTOs.Auth
{
    /// <summary>
    /// Data transfer object for user details including roles and profile information
    /// </summary>
    public class UserDetailsDto
    {
        /// <summary>
        /// The unique identifier of the user
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// The username used for login
        /// </summary>
        public string? UserName { get; set; }

        /// <summary>
        /// The user's full name
        /// </summary>
        public string? FullName { get; set; }

        /// <summary>
        /// Whether the user has administrator privileges
        /// </summary>
        public bool IsAdmin { get; set; }

        /// <summary>
        /// Whether the user has manager privileges
        /// </summary>
        public bool IsManager { get; set; }

        /// <summary>
        /// Whether the user has HR privileges
        /// </summary>
        public bool IsHR { get; set; }

        /// <summary>
        /// List of role names assigned to the user
        /// </summary>
        public List<string> Roles { get; set; } = new List<string>();

        /// <summary>
        /// The department to which the user belongs
        /// </summary>
        public string? Department { get; set; }
    }
}