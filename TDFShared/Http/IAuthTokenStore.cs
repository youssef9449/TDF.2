namespace TDFShared.Http
{
    /// <summary>
    /// Abstraction over the single bearer token that the HTTP pipeline attaches
    /// to every outgoing request via <see cref="AuthenticationHeaderHandler"/>.
    /// Implementations must be safe to share across threads.
    /// </summary>
    public interface IAuthTokenStore
    {
        /// <summary>Returns the current bearer token, or null if none is set.</summary>
        string? GetToken();

        /// <summary>Stores a new bearer token.</summary>
        void SetToken(string? token);

        /// <summary>Clears the current bearer token.</summary>
        void Clear();
    }
}
