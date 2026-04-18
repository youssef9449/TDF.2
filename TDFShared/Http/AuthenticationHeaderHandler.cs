using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace TDFShared.Http
{
    /// <summary>
    /// Delegating handler that stamps outgoing requests with the bearer token
    /// held by <see cref="IAuthTokenStore"/>. Requests that already carry an
    /// <c>Authorization</c> header are left untouched so callers can still
    /// override the default credential on a per-request basis.
    /// </summary>
    public sealed class AuthenticationHeaderHandler : DelegatingHandler
    {
        private readonly IAuthTokenStore _tokenStore;

        public AuthenticationHeaderHandler(IAuthTokenStore tokenStore)
        {
            _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Headers.Authorization is null)
            {
                var token = _tokenStore.GetToken();
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
