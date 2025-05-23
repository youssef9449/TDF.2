using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using TDFAPI.Configuration;

namespace TDFAPI.Utilities
{
    /// <summary>
    /// Manages JWT secret keys with appropriate fallback strategies.
    /// </summary>
    public static class JwtKeyManager
    {
        private const string DEV_KEY_FILENAME = "dev-jwt-key.txt";

        /// <summary>
        /// Gets the JWT secret key with appropriate fallback strategies.
        /// </summary>
        /// <param name="environment">The hosting environment.</param>
        /// <param name="logger">The logger instance for logging errors and warnings.</param>
        /// <returns>A byte array representing the JWT secret key.</returns>
        public static byte[] GetJwtSecretKey(IWebHostEnvironment environment, ILogger logger)
        {
            string secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? string.Empty;

            // If the environment variable is not set, always fall back to the development key logic
            // for local debugging purposes, regardless of the detected environment.
            if (string.IsNullOrWhiteSpace(secretKey))
            {
                logger.LogWarning("JWT_SECRET_KEY environment variable not found. Falling back to development key loading/generation.");
                secretKey = LoadDevelopmentKey(logger);
            }

            return Encoding.ASCII.GetBytes(secretKey);
        }

        /// <summary>
        /// Loads the development JWT key from a file or generates a new one if it does not exist.
        /// </summary>
        /// <param name="logger">The logger instance for logging errors and warnings.</param>
        /// <returns>The development JWT key as a string.</returns>
        private static string LoadDevelopmentKey(ILogger logger)
        {
            string devKeyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DEV_KEY_FILENAME);

            if (File.Exists(devKeyPath))
            {
                try
                {
                    string key = File.ReadAllText(devKeyPath).Trim();
                    logger.LogInformation("Using persisted development JWT key.");
                    return key;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to read persisted JWT key. Generating a new one.");
                }
            }

            // Generate a cryptographically secure key for development
            string newKey = GenerateSecureDevelopmentKey();
            try
            {
                File.WriteAllText(devKeyPath, newKey);
                logger.LogInformation("Generated and persisted a new secure development JWT key.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to persist the new JWT key for development.");
            }

            return newKey;
        }

        private static string GenerateSecureDevelopmentKey()
        {
            // Generate a cryptographically secure 256-bit key for development
            using var rng = RandomNumberGenerator.Create();
            byte[] keyBytes = new byte[32]; // 256 bits
            rng.GetBytes(keyBytes);
            return Convert.ToBase64String(keyBytes);
        }
    }
}