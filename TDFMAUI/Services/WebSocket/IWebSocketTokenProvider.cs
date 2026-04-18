using System.Threading.Tasks;

namespace TDFMAUI.Services.WebSocket
{
    /// <summary>
    /// Resolves a bearer token suitable for opening a WebSocket connection,
    /// transparently refreshing it when necessary.
    /// </summary>
    public interface IWebSocketTokenProvider
    {
        /// <summary>
        /// Returns a valid bearer token, or <c>null</c> if one cannot be obtained.
        /// </summary>
        /// <param name="providedToken">
        /// Optional caller-supplied token. When non-empty it is returned verbatim,
        /// bypassing secure-storage lookup and refresh.
        /// </param>
        Task<string?> GetValidTokenAsync(string? providedToken = null);
    }
}
