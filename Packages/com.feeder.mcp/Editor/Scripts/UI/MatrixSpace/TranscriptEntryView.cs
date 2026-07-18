#nullable enable

using Feeder.MCP.Editor.MatrixSpace;
using UnityEngine.UIElements;

namespace Feeder.MCP.Editor.UI.MatrixSpace
{
    /// <summary>
    /// Renders one <see cref="TranscriptEntry"/> as a VisualElement and updates the text
    /// label in place while an assistant entry is streaming.
    /// </summary>
    public sealed class TranscriptEntryView : VisualElement
    {
        private readonly TranscriptEntry _entry;
        private readonly Label _text;

        public TranscriptEntryView(TranscriptEntry entry)
        {
            _entry = entry;
            AddToClassList("transcript-entry");
            AddToClassList(GetKindClass(entry.Kind));

            var prefix = GetPrefix(entry.Kind);
            if (prefix != null)
            {
                var prefixLabel = new Label(prefix);
                prefixLabel.AddToClassList("transcript-prefix");
                Add(prefixLabel);
            }

            _text = new Label(entry.Text)
            {
                enableRichText = false,
            };
            _text.AddToClassList("transcript-text");
            _text.selection.isSelectable = true;
            Add(_text);
        }

        /// <summary>Re-syncs the label with the (possibly mutated) entry text.</summary>
        public void Refresh()
        {
            if (_text.text != _entry.Text)
                _text.text = _entry.Text;
        }

        private static string? GetPrefix(TranscriptEntryKind kind) => kind switch
        {
            TranscriptEntryKind.User => "[ARCHITECT]",
            TranscriptEntryKind.Assistant => "[SYSTEM]",
            _ => null,
        };

        private static string GetKindClass(TranscriptEntryKind kind) => kind switch
        {
            TranscriptEntryKind.User => "transcript-entry-user",
            TranscriptEntryKind.Assistant => "transcript-entry-assistant",
            TranscriptEntryKind.ToolUse => "transcript-entry-tool",
            TranscriptEntryKind.ToolResult => "transcript-entry-tool",
            TranscriptEntryKind.System => "transcript-entry-system",
            _ => "transcript-entry-error",
        };
    }
}
