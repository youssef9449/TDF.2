using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TDFShared.Helpers
{
    /// <summary>
    /// Provides centralized JSON serialization options and helper methods.
    /// </summary>
    public static class JsonSerializationHelper
    {
        /// <summary>
        /// Default JsonSerializerOptions for consistent serialization throughout the application.
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
        /// Serializes an object to a JSON string using default options.
        /// </summary>
        public static string Serialize<T>(T value)
        {
            return JsonSerializer.Serialize(value, DefaultOptions);
        }

        /// <summary>
        /// Deserializes a JSON string to an object of type T using default options.
        /// Returns default(T) if deserialization fails or input is null/empty.
        /// </summary>
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
    }
} 