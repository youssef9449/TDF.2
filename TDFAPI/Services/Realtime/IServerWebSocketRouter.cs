using System.Threading;
using System.Threading.Tasks;
using TDFShared.Models.Message;

namespace TDFAPI.Services.Realtime
{
    /// <summary>
    /// Dispatches a single inbound WebSocket text frame to the appropriate
    /// server-side handler (message creation, status updates, presence, etc.).
    /// </summary>
    public interface IServerWebSocketRouter
    {
        /// <summary>
        /// Parses the supplied JSON payload and invokes the matching handler.
        /// Unknown message types are logged and an <c>error</c> frame is returned
        /// to the originating connection.
        /// </summary>
        /// <param name="connection">Connection metadata for the authenticated caller.</param>
        /// <param name="messageJson">Raw UTF-8 JSON payload received from the client.</param>
        /// <param name="cancellationToken">Token that cancels in-flight handler work.</param>
        Task RouteAsync(
            WebSocketConnectionEntity connection,
            string messageJson,
            CancellationToken cancellationToken);
    }
}
