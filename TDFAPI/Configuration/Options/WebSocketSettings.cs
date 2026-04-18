namespace TDFAPI.Configuration.Options
{
    /// <summary>
    /// Strongly-typed WebSocket configuration bound from the <c>WebSockets</c> section.
    /// Named <c>WebSocketSettings</c> to avoid collision with
    /// <see cref="Microsoft.AspNetCore.Builder.WebSocketOptions"/>.
    /// </summary>
    public class WebSocketSettings
    {
        public const string SectionName = "WebSockets";

        /// <summary>Idle timeout for a WebSocket connection, in minutes.</summary>
        public int TimeoutMinutes { get; set; } = 30;

        /// <summary>Interval between server-initiated keep-alive pings, in minutes.</summary>
        public double KeepAliveMinutes { get; set; } = 2;

        /// <summary>Upper bound on inbound messages per connection per minute.</summary>
        public int MaxMessagesPerMinute { get; set; } = 120;

        /// <summary>Size of the per-connection receive buffer, in bytes.</summary>
        public int ReceiveBufferSize { get; set; } = 65536;
    }
}
