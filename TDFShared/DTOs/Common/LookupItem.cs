using System;
using System.Text.Json.Serialization;

namespace TDFShared.DTOs.Common
{
    /// <summary>
    /// Generic model used for dropdown lists and lookup values
    /// </summary>
    public class LookupItem
    {
        /// <summary>
        /// Unique identifier for the lookup item
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        
        /// <summary>
        /// Display name for the lookup item
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Optional description for the lookup item
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        
        /// <summary>
        /// Optional category or group for the lookup item
        /// </summary>
        [JsonPropertyName("category")]
        public string? Value { get; set; }
        
        /// <summary>
        /// Optional sort order for the lookup item
        /// </summary>
        [JsonPropertyName("sortOrder")]
        public int? SortOrder { get; set; }
        
        /// <summary>
        /// Creates a new lookup item
        /// </summary>
        public LookupItem() { }
        
        /// <summary>
        /// Creates a new lookup item with the specified id and name
        /// </summary>
        public LookupItem(string id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}