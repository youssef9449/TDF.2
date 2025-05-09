using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TDFShared.DTOs.Common
{
    /// <summary>
    /// Response DTO containing lookup data
    /// </summary>
    public class LookupResponseDto
    {
        /// <summary>
        /// List of departments
        /// </summary>
        [JsonPropertyName("departments")]
        public List<LookupItem> Departments { get; set; } = new List<LookupItem>();
        
        /// <summary>
        /// Dictionary mapping departments to available titles
        /// </summary>
        [JsonPropertyName("titlesByDepartment")]
        public Dictionary<string, List<string>> TitlesByDepartment { get; set; } = new Dictionary<string, List<string>>();
    }
} 