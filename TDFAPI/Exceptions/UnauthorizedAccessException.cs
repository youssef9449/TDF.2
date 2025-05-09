using System;

namespace TDFAPI.Exceptions
{
    /// <summary>
    /// Exception thrown when a user attempts to access a resource they are not authorized for
    /// </summary>
    public class UnauthorizedAccessException : DomainException
    {
        public string UserId { get; }
        public string Resource { get; }

        public UnauthorizedAccessException(string userId, string resource)
            : base($"User '{userId}' is not authorized to access resource '{resource}'.", "unauthorized_access")
        {
            UserId = userId;
            Resource = resource;
        }

        public UnauthorizedAccessException(string userId, string resource, string message)
            : base($"{message} User '{userId}' is not authorized to access resource '{resource}'.", "unauthorized_access")
        {
            UserId = userId;
            Resource = resource;
        }

        public UnauthorizedAccessException(string message)
            : base(message, "unauthorized_access")
        {
            UserId = string.Empty;
            Resource = string.Empty;
        }
    }
} 