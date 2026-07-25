#nullable enable

using System;

namespace Feeder.MCP.Editor.MatrixSpace
{
    /// <summary>
    /// Abstraction over one CLI agent driving one pane. Implementations must raise
    /// <see cref="EventReceived"/> on the Unity main thread.
    /// </summary>
    public interface IAgentBackend : IDisposable
    {
        string Name { get; }

        /// <summary>True while a turn's process is alive.</summary>
        bool IsRunning { get; }

        /// <summary>Backend conversation id used to resume across turns (and domain reloads).</summary>
        string? SessionId { get; set; }

        /// <summary>Model id for this pane ("default"/null = CLI default).</summary>
        string? ModelOverride { get; set; }

        /// <summary>Agent mode for this pane: "manual", "plan" or "auto" (null = default).</summary>
        string? ModeOverride { get; set; }

        /// <summary>Reasoning effort for this pane ("default"/null = CLI default).</summary>
        string? EffortOverride { get; set; }

        /// <summary>Pane designation (e.g. "AGENT 01") used for the shared mailbox protocol.</summary>
        string? AgentLabel { get; set; }

        event Action<AgentEvent>? EventReceived;

        /// <summary>Starts a turn. Throws <see cref="InvalidOperationException"/> if one is running.</summary>
        void SendPrompt(string prompt);

        /// <summary>Kills the current turn's process (session stays resumable).</summary>
        void Cancel();
    }
}
