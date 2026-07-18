#nullable enable

using System;

namespace Feeder.MCP.Editor.MatrixSpace
{
    public enum TranscriptEntryKind
    {
        User,
        Assistant,
        ToolUse,
        ToolResult,
        System,
        Error,
    }

    /// <summary>
    /// One rendered row of a pane transcript. <see cref="Text"/> is mutable while an
    /// assistant entry is still streaming; <see cref="IsComplete"/> flips on turn end.
    /// </summary>
    public sealed class TranscriptEntry
    {
        public TranscriptEntryKind Kind;
        public string Text = string.Empty;
        public string? ToolName;
        public DateTime Timestamp = DateTime.Now;
        public bool IsComplete;

        public TranscriptEntry(TranscriptEntryKind kind, string text, string? toolName = null, bool isComplete = true)
        {
            Kind = kind;
            Text = text;
            ToolName = toolName;
            IsComplete = isComplete;
        }
    }
}
