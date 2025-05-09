using System;
using System.Collections.Generic;

namespace TDFShared.Models.Message
{
    /// <summary>
    /// Represents a WebSocket connection from a user
    /// </summary>
    public class WebSocketConnectionEntity
    {
        /// <summary>
        /// Unique identifier for this connection
        /// </summary>
        public string ConnectionId { get; set; } = string.Empty;
        
        /// <summary>
        /// The ID of the user who owns this connection
        /// </summary>
        public int UserId { get; set; }
        
        /// <summary>
        /// The username of the user who owns this connection
        /// </summary>
        public string Username { get; set; } = string.Empty;
        
        /// <summary>
        /// Groups this connection belongs to
        /// </summary>
        public List<string> Groups { get; set; } = new List<string>();
        
        /// <summary>
        /// Whether the connection is currently active
        /// </summary>
        public bool IsConnected { get; set; }
        
        /// <summary>
        /// When the connection was established
        /// </summary>
        public DateTime ConnectedAt { get; set; }
        
        /// <summary>
        /// The machine name or device info of the client
        /// </summary>
        public string? MachineName { get; set; }
        
        /// <summary>
        /// Time of the last activity on this connection
        /// </summary>
        public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    }
} 