using System;

namespace TDFShared.Exceptions
{
    public class NetworkUnavailableException : Exception
    {
        public NetworkUnavailableException() : base("Network is unavailable") { }
        public NetworkUnavailableException(string message) : base(message) { }
        public NetworkUnavailableException(string message, Exception innerException) : base(message, innerException) { }
    }
}