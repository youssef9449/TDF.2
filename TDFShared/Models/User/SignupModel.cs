using System;
using System.Text.Json.Serialization;

namespace TDFShared.Models.User
{
    /// <summary>
    /// Model for user signup
    /// </summary>
    public class SignupModel
    {
        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("password")]
        public string Password { get; set; }

        [JsonPropertyName("FullName")]
        public string FullName { get; set; }

        [JsonPropertyName("department")]
        public string Department { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

    }
} 