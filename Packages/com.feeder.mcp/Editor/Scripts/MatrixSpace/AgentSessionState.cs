#nullable enable

namespace Feeder.MCP.Editor.MatrixSpace
{
    /// <summary>
    /// Lifecycle of a single agent pane session.
    /// </summary>
    public enum AgentSessionState
    {
        /// <summary>No turn has been run yet.</summary>
        Idle,

        /// <summary>CLI process spawned, waiting for the first stream event.</summary>
        Starting,

        /// <summary>Receiving streamed events for the current turn.</summary>
        Streaming,

        /// <summary>Turn finished, ready for the next prompt (resume).</summary>
        WaitingInput,

        /// <summary>Last turn failed (process error / non-zero exit). Next send retries.</summary>
        Error,

        /// <summary>Session disposed; pane no longer usable.</summary>
        Exited,
    }
}
