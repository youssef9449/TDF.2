using System.ComponentModel.DataAnnotations;

namespace TDFShared.DTOs.Users
{
    /// <summary>
    /// Request DTO for updating user connection status
    /// </summary>
    public class UpdateConnectionRequest
    {
        /// <summary>
        /// Whether the user is connected/online
        /// </summary>
        [Required]
        public bool IsConnected { get; set; }
    }
}