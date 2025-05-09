using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace TDFAPI.Utilities
{
    public class IniFile
    {
        private readonly string _filePath;
        private readonly Dictionary<string, Dictionary<string, string>> _data;
        private static readonly object _fileLock = new object();

        // Required fields for validation
        private static readonly Dictionary<string, string[]> _requiredSettings = new Dictionary<string, string[]>
        {
            ["Database"] = new[] { "ServerIP", "Database", "ConnectionMethod" },
            ["Jwt"] = new[] { "SecretKey", "Issuer", "Audience" }
        };
        
        public IniFile(string filePath)
        {
            _filePath = filePath;
            _data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            
            if (!File.Exists(filePath))
            {
                // Create the directory if it doesn't exist
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                
                // Create an empty file with essential sections
                lock (_fileLock)
                {
                    using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
                    {
                        writer.WriteLine("[Database]");
                        writer.WriteLine("ServerIP=.");
                        writer.WriteLine("Database=Users");
                        writer.WriteLine("ConnectionMethod=namedpipes");
                        writer.WriteLine("Trusted_Connection=true");
                        writer.WriteLine("TrustServerCertificate=true");
                        writer.WriteLine();
                        
                        writer.WriteLine("[Jwt]");
                        writer.WriteLine("SecretKey=default_dev_key_" + Guid.NewGuid().ToString("N"));
                        writer.WriteLine("Issuer=tdfapi");
                        writer.WriteLine("Audience=tdfapp");
                        writer.WriteLine();
                        
                        writer.WriteLine("[App]");
                        writer.WriteLine("AllowedOrigins=http://localhost:3000,http://localhost:5173");
                        writer.WriteLine();
                        
                        writer.WriteLine("[Security]");
                        writer.WriteLine("MaxFailedLoginAttempts=5");
                        writer.WriteLine();
                    }
                }
                
                Console.WriteLine($"Created new ini file at {filePath} with default values");
            }
            
            Load();
        }
        
        private void Load()
        {
            _data.Clear();
            
            try
            {
                string currentSection = "";
                
                lock (_fileLock)
                {
                    foreach (var line in File.ReadAllLines(_filePath))
                    {
                        var trimmedLine = line.Trim();
                        
                        // Skip comments and empty lines
                        if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
                            continue;
                        
                        // Check for section header
                        if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                        {
                            currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                            if (!_data.ContainsKey(currentSection))
                            {
                                _data[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            }
                            continue;
                        }
                        
                        // Check for key-value pairs
                        var keyValueSeparator = trimmedLine.IndexOf('=');
                        if (keyValueSeparator > 0 && !string.IsNullOrEmpty(currentSection))
                        {
                            var key = trimmedLine.Substring(0, keyValueSeparator).Trim();
                            var value = trimmedLine.Substring(keyValueSeparator + 1).Trim();
                            
                            // Store the key-value pair
                            _data[currentSection][key] = value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading ini file: {ex.Message}");
                throw new InvalidOperationException($"Failed to load configuration file: {ex.Message}", ex);
            }
        }
        
        public string Read(string section, string key, string defaultValue)
        {
            if (_data.TryGetValue(section, out var sectionData) && sectionData.TryGetValue(key, out var value))
            {
                return value;
            }
            
            return defaultValue;
        }
        
        public void Write(string section, string key, string value)
        {
            if (!_data.ContainsKey(section))
            {
                _data[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            
            _data[section][key] = value;
            Save();
        }
        
        private void Save()
        {
            try
            {
                lock (_fileLock)
                {
                    using (StreamWriter writer = new StreamWriter(_filePath, false, Encoding.UTF8))
                    {
                        foreach (var section in _data)
                        {
                            writer.WriteLine($"[{section.Key}]");
                            
                            foreach (var keyValue in section.Value)
                            {
                                writer.WriteLine($"{keyValue.Key}={keyValue.Value}");
                            }
                            
                            writer.WriteLine(); // Empty line after each section
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving ini file: {ex.Message}");
                throw new InvalidOperationException($"Failed to save configuration file: {ex.Message}", ex);
            }
        }
        
        public bool ValidateRequiredSettings(out List<string> missingSettings)
        {
            missingSettings = new List<string>();
            
            foreach (var section in _requiredSettings)
            {
                foreach (var key in section.Value)
                {
                    var value = Read(section.Key, key, "");
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        missingSettings.Add($"{section.Key}.{key}");
                    }
                }
            }
            
            return missingSettings.Count == 0;
        }
        
        public Dictionary<string, Dictionary<string, string>> GetSafeConfiguration()
        {
            // Return a copy of the configuration with sensitive information redacted
            var safeCopy = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var section in _data)
            {
                var sectionCopy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                safeCopy[section.Key] = sectionCopy;
                
                foreach (var keyValue in section.Value)
                {
                    if (IsSensitiveKey(keyValue.Key))
                    {
                        sectionCopy[keyValue.Key] = "******";
                    }
                    else
                    {
                        sectionCopy[keyValue.Key] = keyValue.Value;
                    }
                }
            }
            
            return safeCopy;
        }
        
        private bool IsSensitiveKey(string key)
        {
            string lowercaseKey = key.ToLowerInvariant();
            return lowercaseKey.Contains("password") || 
                   lowercaseKey.Contains("secret") || 
                   lowercaseKey.Contains("key") || 
                   lowercaseKey.Contains("token");
        }
    }
} 