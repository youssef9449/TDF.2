using System;

namespace TDFAPI.Exceptions
{
    /// <summary>
    /// Exception thrown when a concurrency conflict occurs during database operations
    /// </summary>
    public class ConcurrencyException : Exception
    {
        public ConcurrencyException() : base("A concurrency conflict occurred")
        {
        }

        public ConcurrencyException(string message) : base(message)
        {
        }

        public ConcurrencyException(string message, Exception innerException) : base(message, innerException)
        {
        }
        
        public object? EntityId { get; set; }
        public string? EntityType { get; set; }
    }
} 