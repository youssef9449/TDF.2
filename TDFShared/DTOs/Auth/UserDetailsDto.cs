using System.Collections.Generic;

namespace TDFShared.DTOs.Auth
{
    public class UserDetailsDto
    {
        public int UserId { get; set; }
        public string? UserName { get; set; } // Typically the login name
        public string? FullName { get; set; }

        public List<string> Roles { get; set; } = new List<string>();
        public string? Department { get; set; }
        // Add any other relevant user details needed client-side
    }
} 