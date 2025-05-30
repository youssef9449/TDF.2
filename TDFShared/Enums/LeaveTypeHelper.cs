using System;
using System.Collections.Generic;

namespace TDFShared.Enums
{
    /// <summary>
    /// Helper methods for LeaveType enum, including parsing and balance key mapping.
    /// Centralizes all alias and key logic for leave types.
    /// </summary>
    public static class LeaveTypeHelper
    {
        // Mapping of aliases to LeaveType
        private static readonly Dictionary<string, LeaveType> _aliasMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "annual", LeaveType.Annual },
            { "annual leave", LeaveType.Annual },
            { "emergency", LeaveType.Emergency },
            { "emergency leave", LeaveType.Emergency },
            // No 'casual' or 'casual leave' supported
            { "unpaid", LeaveType.Unpaid },
            { "unpaid leave", LeaveType.Unpaid },
            { "permission", LeaveType.Permission },
            { "permission leave", LeaveType.Permission },
            { "external assignment", LeaveType.ExternalAssignment },
            { "external assignment leave", LeaveType.ExternalAssignment },
            { "work from home", LeaveType.WorkFromHome },
            { "wfh", LeaveType.WorkFromHome }
        };

        /// <summary>
        /// Parses a string to a LeaveType, supporting aliases.
        /// Throws if the input is not recognized.
        /// </summary>
        public static LeaveType Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Leave type input is required.", nameof(input));

            if (_aliasMap.TryGetValue(input.Trim(), out var leaveType))
                return leaveType;

            // Try enum parse as fallback
            if (Enum.TryParse<LeaveType>(input.Replace(" ", ""), true, out var parsed))
                return parsed;

            throw new ArgumentException($"Unsupported leave type: {input}", nameof(input));
        }

        /// <summary>
        /// Gets the string key used for leave balances for a given LeaveType.
        /// Returns null for types that do not use a balance.
        /// </summary>
        public static string? GetBalanceKey(LeaveType leaveType)
        {
            return leaveType switch
            {
                LeaveType.Annual => "AnnualBalance",
                LeaveType.Emergency => "EmergencyBalance",
                LeaveType.Permission => "PermissionBalance",
                _ => null // Unpaid, ExternalAssignment, WorkFromHome do not use balances
            };
        }
    }
} 