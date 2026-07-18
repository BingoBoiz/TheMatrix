#nullable enable

namespace Feeder.MCP.Editor.MatrixSpace
{
    public enum AgentEventKind
    {
        /// <summary>First event of a turn: system/init. Carries the session id.</summary>
        SystemInit,

        /// <summary>A completed assistant text block.</summary>
        AssistantText,

        /// <summary>The assistant invoked a tool.</summary>
        ToolUse,

        /// <summary>A tool returned its result to the assistant.</summary>
        ToolResult,

        /// <summary>Final event of a turn. Carries session id, cost, and error flag.</summary>
        Result,

        /// <summary>A stdout line that was not valid stream-json. Logged, never fatal.</summary>
        RawLine,

        /// <summary>A stderr line from the CLI process.</summary>
        Stderr,

        /// <summary>The CLI process could not be started or crashed.</summary>
        ProcessError,

        /// <summary>The CLI process exited. <see cref="ExitCode"/> is set.</summary>
        ProcessExited,
    }

    /// <summary>
    /// A parsed event coming out of one claude CLI turn (stream-json line or process signal).
    /// </summary>
    public sealed class AgentEvent
    {
        public AgentEventKind Kind;
        public string? Text;
        public string? ToolName;
        public string? SessionId;
        public string? Subtype;
        public bool IsError;
        public decimal CostUsd;
        public int ExitCode;

        /// <summary>
        /// True when <see cref="Text"/> is a raw continuation of the previous assistant text
        /// (append as-is) rather than a standalone block (append with a blank line).
        /// </summary>
        public bool IsDelta;

        public AgentEvent(AgentEventKind kind)
        {
            Kind = kind;
        }

        public override string ToString()
            => $"{Kind}{(ToolName != null ? $" tool={ToolName}" : "")}{(IsError ? " (error)" : "")}: {Text}";
    }
}
