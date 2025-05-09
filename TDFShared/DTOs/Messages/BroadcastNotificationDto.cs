using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace TDFShared.DTOs.Messages
{
    public class BroadcastNotificationDto
    {
        [Required]
        [StringLength(1000)]
        public string Message { get; set; } = string.Empty;

        public string? Department { get; set; }
    }
}
