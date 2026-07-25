#nullable enable

using System;
using Feeder.MCP.Editor.MatrixSpace;
using UnityEngine.UIElements;

namespace Feeder.MCP.Editor.UI.MatrixSpace
{
    /// <summary>
    /// Renders one <see cref="TranscriptEntry"/> as a VisualElement and updates the text
    /// label in place while an assistant entry is streaming. Thinking entries render as a
    /// collapsible section: animated "THINKING…" header while streaming, then a
    /// "THOUGHT FOR Xs" header that toggles the reasoning text on click.
    /// </summary>
    public sealed class TranscriptEntryView : VisualElement
    {
        private readonly TranscriptEntry _entry;
        private readonly Label _text;
        private readonly Label? _thinkingHeader;
        private IVisualElementScheduledItem? _headerAnimation;
        private int _dotPhase;
        private bool _collapsed;
        private bool _settled;

        /// <summary>True once the entry is complete and its final visual state is rendered.</summary>
        public bool IsSettled => _settled;

        public TranscriptEntryView(TranscriptEntry entry)
        {
            _entry = entry;
            AddToClassList("transcript-entry");
            AddToClassList(GetKindClass(entry.Kind));

            if (entry.Kind == TranscriptEntryKind.Thinking)
            {
                _thinkingHeader = new Label();
                _thinkingHeader.AddToClassList("transcript-thinking-header");
                _thinkingHeader.RegisterCallback<ClickEvent>(_ => ToggleCollapsed());
                Add(_thinkingHeader);
            }
            else
            {
                var prefix = GetPrefix(entry.Kind);
                if (prefix != null)
                {
                    var prefixLabel = new Label(prefix);
                    prefixLabel.AddToClassList("transcript-prefix");
                    Add(prefixLabel);
                }
            }

            _text = new Label(entry.Text)
            {
                enableRichText = false,
            };
            _text.AddToClassList("transcript-text");
            if (entry.Kind == TranscriptEntryKind.Thinking)
                _text.AddToClassList("transcript-thinking-body");
            _text.selection.isSelectable = true;
            Add(_text);

            if (_thinkingHeader != null)
            {
                if (_entry.IsComplete)
                {
                    Settle();
                }
                else
                {
                    UpdateThinkingHeader();
                    _headerAnimation = schedule.Execute(() =>
                    {
                        _dotPhase = (_dotPhase + 1) % 4;
                        UpdateThinkingHeader();
                    }).Every(400);
                }
            }
            else
            {
                _settled = _entry.IsComplete;
            }
        }

        /// <summary>Re-syncs the label with the (possibly mutated) entry text and state.</summary>
        public void Refresh()
        {
            if (_text.text != _entry.Text)
                _text.text = _entry.Text;

            if (_settled || !_entry.IsComplete)
                return;

            if (_thinkingHeader != null)
                Settle();
            else
                _settled = true;
        }

        /// <summary>Final state of a thinking entry: stop the animation and auto-collapse.</summary>
        private void Settle()
        {
            _settled = true;
            _headerAnimation?.Pause();
            _headerAnimation = null;
            SetCollapsed(true);
        }

        private void ToggleCollapsed()
        {
            // While streaming the section stays open so the reasoning is visible live.
            if (!_entry.IsComplete)
                return;
            SetCollapsed(!_collapsed);
        }

        private void SetCollapsed(bool collapsed)
        {
            _collapsed = collapsed;
            EnableInClassList("collapsed", collapsed);
            UpdateThinkingHeader();
        }

        private void UpdateThinkingHeader()
        {
            if (_thinkingHeader == null)
                return;

            if (!_entry.IsComplete)
            {
                _thinkingHeader.text = "✳ THINKING" + new string('.', _dotPhase);
                return;
            }

            var completedAt = _entry.CompletedAt ?? DateTime.Now;
            var seconds = Math.Max(1, (int)Math.Round((completedAt - _entry.Timestamp).TotalSeconds));
            var arrow = _collapsed ? "▸" : "▾";
            _thinkingHeader.text = $"{arrow} ✳ THOUGHT FOR {seconds}s";
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
            TranscriptEntryKind.Thinking => "transcript-entry-thinking",
            TranscriptEntryKind.ToolUse => "transcript-entry-tool",
            TranscriptEntryKind.ToolResult => "transcript-entry-tool",
            TranscriptEntryKind.System => "transcript-entry-system",
            _ => "transcript-entry-error",
        };
    }
}
