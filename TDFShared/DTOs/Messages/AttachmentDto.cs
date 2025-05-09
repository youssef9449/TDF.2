using System;
using System.Text.Json.Serialization;

namespace TDFShared.DTOs.Messages
{
    /// <summary>
    /// DTO for file attachments in messages
    /// </summary>
    public class AttachmentDto
    {
        /// <summary>
        /// The unique identifier for the attachment
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
        
        /// <summary>
        /// The name of the file
        /// </summary>
        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;
        
        /// <summary>
        /// The MIME type of the file
        /// </summary>
        [JsonPropertyName("contentType")]
        public string ContentType { get; set; } = string.Empty;
        
        /// <summary>
        /// The size of the file in bytes
        /// </summary>
        [JsonPropertyName("fileSize")]
        public long FileSize { get; set; }
        
        /// <summary>
        /// The URL or path to access the attachment
        /// </summary>
        [JsonPropertyName("fileUrl")]
        public string FileUrl { get; set; } = string.Empty;
        
        /// <summary>
        /// When the attachment was uploaded
        /// </summary>
        [JsonPropertyName("uploadedAt")]
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
} 