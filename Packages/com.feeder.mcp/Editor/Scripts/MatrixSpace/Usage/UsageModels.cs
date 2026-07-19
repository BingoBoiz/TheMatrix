#nullable enable

using System;
using System.Collections.Generic;

namespace Feeder.MCP.Editor.MatrixSpace
{
    public enum UsageAvailability
    {
        Ok = 0,
        AuthRequired = 1,
        NotSupported = 2,
        Error = 3,
    }

    /// <summary>One subscription rate-limit window (e.g. the 5-hour session or the weekly cap).</summary>
    [Serializable]
    public sealed class UsageWindowSnapshot
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        /// <summary>0–100. How much of the window's budget is consumed.</summary>
        public double UtilizationPercent { get; set; }
        public DateTimeOffset? ResetsAt { get; set; }
    }

    /// <summary>Subscription usage state for one backend preset, as last fetched.</summary>
    [Serializable]
    public sealed class ProviderUsageSnapshot
    {
        public string PresetId { get; set; } = string.Empty;
        public UsageAvailability Status { get; set; }
        /// <summary>Plan tier reported by the provider (e.g. "max"), when known.</summary>
        public string? PlanName { get; set; }
        public List<UsageWindowSnapshot> Windows { get; set; } = new();
        public DateTime FetchedAtUtc { get; set; }
        /// <summary>Human-readable hint for AuthRequired / Error states. Never contains secrets.</summary>
        public string? Error { get; set; }
    }
}
