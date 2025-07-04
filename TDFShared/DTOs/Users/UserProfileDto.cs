using System;
using System.Collections.Generic;
using TDFShared.Enums;

namespace TDFShared.DTOs.Users
{
    /// <summary>
    /// Data transfer object for user profile information
    /// </summary>
    public class UserProfileDto
    {
        /// <summary>
        /// User ID
        /// </summary>
        public int UserID { get; set; }
        
        /// <summary>
        /// Username
        /// </summary>
        public required string Username { get; set; }
        
        /// <summary>
        /// Full name
        /// </summary>
        public required string FullName { get; set; }
        
        /// <summary>
        /// Department
        /// </summary>
        public required string Department { get; set; }
        
        /// <summary>
        /// Title
        /// </summary>
        public required string Title { get; set; }
        
        /// <summary>
        /// Whether the user is active
        /// </summary>
        public bool IsActive { get; set; }
        
        /// <summary>
        /// Whether the user is an admin
        /// </summary>
        public bool IsAdmin { get; set; }
        
        /// <summary>
        /// Whether the user is a manager
        /// </summary>
        public bool IsManager { get; set; }

         /// <summary>
        /// Whether the user is a HR
        /// </summary>
        public bool IsHR { get; set; }
        
        /// <summary>
        /// Profile picture data as a base64 string
        /// </summary>
        public required byte[] ProfilePictureData { get; set; }
        
        /// <summary>
        /// Current presence status
        /// </summary>
        public UserPresenceStatus PresenceStatus { get; set; }
        
        /// <summary>
        /// User status message
        /// </summary>
        public required string StatusMessage { get; set; }
        
        /// <summary>
        /// Current device information
        /// </summary>
        public required string CurrentDevice { get; set; }
        
        /// <summary>
        /// Whether the user is available for chat
        /// </summary>
        public bool IsAvailableForChat { get; set; }
        
        /// <summary>
        /// Last activity timestamp
        /// </summary>
        public DateTime? LastActivityTime { get; set; }
        
        /// <summary>
        /// Skills/competencies
        /// </summary>
        public required List<string> Skills { get; set; }
        
        /// <summary>
        /// When the user was created
        /// </summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// When the user was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }
} 