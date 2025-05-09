using System.Net;
using Microsoft.AspNetCore.Http;

namespace TDFAPI.Extensions
{
    public static class HttpContextExtensions
    {
        /// <summary>
        /// Gets the real IP address of the client by checking X-Forwarded-For headers first,
        /// then falling back to Connection.RemoteIpAddress. Ensures private IP addresses
        /// (like ::1 or 127.0.0.1) are properly identified and reported.
        /// </summary>
        /// <param name="context">The HttpContext</param>
        /// <returns>The client's IP address as a string</returns>
        public static string GetRealIpAddress(this HttpContext context)
        {
            // Check for X-Forwarded-For header first (set by proxies/load balancers)
            string forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            
            if (!string.IsNullOrEmpty(forwarded))
            {
                // X-Forwarded-For can contain multiple IPs, the first one is the client's
                var ips = forwarded.Split(',');
                return ips[0].Trim();
            }
            
            // Fall back to RemoteIpAddress from the connection
            IPAddress remoteIp = context.Connection.RemoteIpAddress;
            
            // Handle null case and local loopback addresses
            if (remoteIp == null)
                return "unknown";
                
            // Check if it's a loopback address (::1 IPv6 or 127.0.0.1 IPv4)
            if (IPAddress.IsLoopback(remoteIp))
            {
                // For local dev, we'll return the loopback address but tag it
                return $"{remoteIp} (loopback)";
            }
            
            // Convert IPv6-mapped IPv4 addresses to their IPv4 form for readability
            if (remoteIp.IsIPv4MappedToIPv6)
            {
                remoteIp = remoteIp.MapToIPv4();
            }
            
            return remoteIp.ToString();
        }
    }
} 