#nullable enable

namespace Feeder.MCP.Editor.MatrixSpace
{
    /// <summary>
    /// Fetches subscription-level usage (session / weekly windows) for one backend preset.
    /// Presets without a provider show as "usage data unavailable" in the USAGE view.
    /// </summary>
    public interface IUsageProvider
    {
        /// <summary>Matching <see cref="AgentBackendPreset.Id"/>.</summary>
        string PresetId { get; }

        /// <summary>Blocking fetch; always called off the main thread by <see cref="UsageService"/>.</summary>
        ProviderUsageSnapshot Fetch();
    }
}
