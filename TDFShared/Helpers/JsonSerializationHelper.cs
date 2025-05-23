using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TDFShared.Helpers
{
    /// <summary>
    /// Provides centralized JSON serialization options and helper methods for consistent JSON handling across the application.
    /// </summary>
    public static class JsonSerializationHelper
    {
        /// <summary>
        /// Default JsonSerializerOptions for consistent serialization throughout the application.
        /// Includes camelCase naming, enum string conversion, null value handling, and circular reference protection.
        /// </summary>
        public static JsonSerializerOptions DefaultOptions { get; } = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }, // Handle enums as strings
            ReferenceHandler = ReferenceHandler.IgnoreCycles // Handle circular references
        };

        /// <summary>
        /// Compact JsonSerializerOptions for HTTP client communication (no indentation).
        /// Same as DefaultOptions but optimized for network transmission.
        /// </summary>
        public static JsonSerializerOptions CompactOptions { get; } = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            WriteIndented = false // Explicitly set for network efficiency
        };

        /// <summary>
        /// Pretty-printed JsonSerializerOptions for debugging and logging.
        /// Same as DefaultOptions but with indented formatting.
        /// </summary>
        public static JsonSerializerOptions PrettyOptions { get; } = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            WriteIndented = true // For readable output
        };

        /// <summary>
        /// Basic JsonSerializerOptions for simple API response parsing.
        /// Minimal configuration for backward compatibility.
        /// </summary>
        public static JsonSerializerOptions BasicOptions { get; } = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Serializes an object to a JSON string using default options.
        /// </summary>
        /// <typeparam name="T">Type of object to serialize</typeparam>
        /// <param name="value">Object to serialize</param>
        /// <returns>JSON string representation</returns>
        public static string Serialize<T>(T value)
        {
            return JsonSerializer.Serialize(value, DefaultOptions);
        }

        /// <summary>
        /// Serializes an object to a compact JSON string (no indentation).
        /// </summary>
        /// <typeparam name="T">Type of object to serialize</typeparam>
        /// <param name="value">Object to serialize</param>
        /// <returns>Compact JSON string representation</returns>
        public static string SerializeCompact<T>(T value)
        {
            return JsonSerializer.Serialize(value, CompactOptions);
        }

        /// <summary>
        /// Serializes an object to a pretty-printed JSON string.
        /// </summary>
        /// <typeparam name="T">Type of object to serialize</typeparam>
        /// <param name="value">Object to serialize</param>
        /// <returns>Pretty-printed JSON string representation</returns>
        public static string SerializePretty<T>(T value)
        {
            return JsonSerializer.Serialize(value, PrettyOptions);
        }

        /// <summary>
        /// Deserializes a JSON string to an object of type T using default options.
        /// Returns default(T) if deserialization fails or input is null/empty.
        /// </summary>
        /// <typeparam name="T">Type to deserialize to</typeparam>
        /// <param name="json">JSON string to deserialize</param>
        /// <returns>Deserialized object or default(T) if failed</returns>
        public static T? Deserialize<T>(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return default;
            }
            try
            {
                return JsonSerializer.Deserialize<T>(json, DefaultOptions);
            }
            catch (JsonException)
            {
                // Log the error if a logger is available/injected
                return default;
            }
        }

        /// <summary>
        /// Safely deserializes a JSON string with exception handling.
        /// </summary>
        /// <typeparam name="T">Type to deserialize to</typeparam>
        /// <param name="json">JSON string to deserialize</param>
        /// <param name="result">Deserialized object if successful</param>
        /// <returns>True if deserialization succeeded, false otherwise</returns>
        public static bool TryDeserialize<T>(string json, out T? result)
        {
            result = default;
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            try
            {
                result = JsonSerializer.Deserialize<T>(json, DefaultOptions);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}