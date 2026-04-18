using System.Threading;

namespace TDFShared.Http
{
    /// <summary>
    /// Default <see cref="IAuthTokenStore"/> that holds the bearer token in a
    /// single volatile reference. Registered as a singleton so every
    /// <see cref="AuthenticationHeaderHandler"/> instance observes the same
    /// value regardless of which <see cref="System.Net.Http.HttpClient"/> the
    /// factory hands out.
    /// </summary>
    public sealed class InMemoryAuthTokenStore : IAuthTokenStore
    {
        private string? _token;

        public string? GetToken() => Volatile.Read(ref _token);

        public void SetToken(string? token) => Volatile.Write(ref _token, token);

        public void Clear() => Volatile.Write(ref _token, null);
    }
}
