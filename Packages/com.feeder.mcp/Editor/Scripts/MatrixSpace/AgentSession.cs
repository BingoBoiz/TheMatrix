#nullable enable

using System;
using System.Collections.Generic;

namespace Feeder.MCP.Editor.MatrixSpace
{
    /// <summary>
    /// One pane's conversation: state machine + transcript, fed by an <see cref="IAgentBackend"/>.
    /// All mutations happen on the main thread (backend contract). UI subscribes to
    /// <see cref="Changed"/> and re-renders incrementally.
    /// </summary>
    public sealed class AgentSession : IDisposable
    {
        public string PaneId { get; }
        public string BackendId { get; }
        public string DisplayName { get; set; }
        public AgentSessionState State { get; private set; } = AgentSessionState.Idle;
        public List<TranscriptEntry> Transcript { get; } = new();
        public decimal TotalCostUsd { get; private set; }
        public long TotalInputTokens { get; private set; }
        public long TotalOutputTokens { get; private set; }
        public long TotalCacheReadTokens { get; private set; }
        public long TotalCacheCreationTokens { get; private set; }
        public IAgentBackend Backend { get; }

        /// <summary>Fired on the main thread after any state/transcript change.</summary>
        public event Action<AgentSession>? Changed;

        /// <summary>Fired when a single entry was appended (index into Transcript).</summary>
        public event Action<AgentSession, int>? EntryAdded;

        /// <summary>Fired once per turn when it settles (true = clean, false = error).</summary>
        public event Action<AgentSession, bool>? TurnEnded;

        public AgentSession(string paneId, string displayName, IAgentBackend backend, string backendId = AgentBackendCatalog.DefaultId)
        {
            PaneId = paneId;
            BackendId = backendId;
            DisplayName = displayName;
            Backend = backend;
            Backend.AgentLabel = displayName;
            Backend.EventReceived += OnBackendEvent;
        }

        public bool CanSend => State is AgentSessionState.Idle or AgentSessionState.WaitingInput or AgentSessionState.Error;

        /// <summary>Rehydrates transcript + totals from a pane record after a domain reload.</summary>
        public void RestoreFrom(MatrixSpaceSessionStore.PaneRecord record)
        {
            Transcript.Clear();
            Transcript.AddRange(record.Transcript);

            // A reload kills the CLI process, so a mid-stream entry can never finish.
            var last = Transcript.Count > 0 ? Transcript[^1] : null;
            if (last is { IsComplete: false })
                last.IsComplete = true;

            TotalCostUsd = (decimal)record.TotalCostUsd;
            TotalInputTokens = record.TotalInputTokens;
            TotalOutputTokens = record.TotalOutputTokens;
            TotalCacheReadTokens = record.TotalCacheReadTokens;
            TotalCacheCreationTokens = record.TotalCacheCreationTokens;
        }

        /// <summary>Writes transcript + totals into the pane record so they survive a reload.</summary>
        public void SnapshotTo(MatrixSpaceSessionStore.PaneRecord record)
        {
            record.Transcript = new List<TranscriptEntry>(Transcript);
            record.TotalCostUsd = (double)TotalCostUsd;
            record.TotalInputTokens = TotalInputTokens;
            record.TotalOutputTokens = TotalOutputTokens;
            record.TotalCacheReadTokens = TotalCacheReadTokens;
            record.TotalCacheCreationTokens = TotalCacheCreationTokens;
        }

        public void Send(string prompt)
        {
            if (!CanSend)
                return;
            prompt = prompt.Trim();
            if (prompt.Length == 0)
                return;

            AddEntry(new TranscriptEntry(TranscriptEntryKind.User, prompt));
            SetState(AgentSessionState.Starting);

            try
            {
                Backend.SendPrompt(prompt);
            }
            catch (Exception ex)
            {
                AddEntry(new TranscriptEntry(TranscriptEntryKind.Error, ex.Message));
                SetState(AgentSessionState.Error);
            }
        }

        public void Cancel()
        {
            if (State is not (AgentSessionState.Starting or AgentSessionState.Streaming))
                return;

            Backend.Cancel();
            AddEntry(new TranscriptEntry(TranscriptEntryKind.System, "Turn interrupted by The Architect."));
            // ProcessExited will land afterwards; state settles in OnBackendEvent.
            SetState(AgentSessionState.WaitingInput);
        }

        public void Dispose()
        {
            Backend.EventReceived -= OnBackendEvent;
            Backend.Dispose();
            SetState(AgentSessionState.Exited);
        }

        private void OnBackendEvent(AgentEvent evt)
        {
            switch (evt.Kind)
            {
                case AgentEventKind.SystemInit:
                    SetState(AgentSessionState.Streaming);
                    break;

                case AgentEventKind.AssistantText:
                    AppendAssistantText(evt.Text ?? string.Empty, evt.IsDelta);
                    SetState(AgentSessionState.Streaming);
                    break;

                case AgentEventKind.ToolUse:
                    AddEntry(new TranscriptEntry(TranscriptEntryKind.ToolUse,
                        $"> executing: {evt.ToolName ?? "unknown"}", evt.ToolName));
                    break;

                case AgentEventKind.ToolResult:
                    if (evt.IsError && !string.IsNullOrEmpty(evt.Text))
                        AddEntry(new TranscriptEntry(TranscriptEntryKind.ToolResult, $"< glitch: {evt.Text}"));
                    break;

                case AgentEventKind.Result:
                    CompleteLastAssistantEntry();
                    TotalCostUsd += evt.CostUsd;
                    TotalInputTokens += evt.InputTokens;
                    TotalOutputTokens += evt.OutputTokens;
                    TotalCacheReadTokens += evt.CacheReadTokens;
                    TotalCacheCreationTokens += evt.CacheCreationTokens;
                    if (evt.IsError)
                    {
                        var message = string.IsNullOrEmpty(evt.Text)
                            ? $"Turn failed ({evt.Subtype ?? "unknown error"})."
                            : evt.Text!;
                        AddEntry(new TranscriptEntry(TranscriptEntryKind.Error, message));
                        HandlePossibleAuthError(message);
                    }
                    else
                    {
                        AgentStatusService.ClearAuthRequired(BackendId);
                    }
                    SetState(AgentSessionState.WaitingInput);
                    TurnEnded?.Invoke(this, !evt.IsError);
                    break;

                case AgentEventKind.ProcessError:
                    AddEntry(new TranscriptEntry(TranscriptEntryKind.Error, evt.Text ?? "Process error."));
                    SetState(AgentSessionState.Error);
                    TurnEnded?.Invoke(this, false);
                    break;

                case AgentEventKind.ProcessExited:
                    CompleteLastAssistantEntry();
                    if (evt.IsError)
                    {
                        AddEntry(new TranscriptEntry(TranscriptEntryKind.Error, evt.Text ?? $"Exited with code {evt.ExitCode}."));
                        HandlePossibleAuthError(evt.Text);
                        SetState(AgentSessionState.Error);
                        TurnEnded?.Invoke(this, false);
                    }
                    else if (State is AgentSessionState.Starting or AgentSessionState.Streaming)
                    {
                        // Killed or exited without a result event; keep the pane usable.
                        SetState(AgentSessionState.WaitingInput);
                        TurnEnded?.Invoke(this, true);
                    }
                    break;

                case AgentEventKind.RawLine:
                case AgentEventKind.Stderr:
                    // Ignored in the transcript; backends log them if needed.
                    break;
            }
        }

        private void HandlePossibleAuthError(string? message)
        {
            if (!AgentStatusService.LooksLikeAuthError(message))
                return;

            AgentStatusService.MarkAuthRequired(BackendId);
            AddEntry(new TranscriptEntry(TranscriptEntryKind.System,
                $"{Backend.Name} is not signed in. Open CONFIG > AGENTS and use the LOGIN button, " +
                "then send again — the conversation resumes where it left off."));
        }

        private void AppendAssistantText(string text, bool isDelta)
        {
            if (text.Length == 0)
                return;

            var last = Transcript.Count > 0 ? Transcript[^1] : null;
            if (last is { Kind: TranscriptEntryKind.Assistant, IsComplete: false })
            {
                var separator = isDelta || last.Text.Length == 0 ? string.Empty : "\n\n";
                last.Text += separator + text;
                Changed?.Invoke(this);
            }
            else
            {
                AddEntry(new TranscriptEntry(TranscriptEntryKind.Assistant, text, isComplete: false));
            }
        }

        private void CompleteLastAssistantEntry()
        {
            var last = Transcript.Count > 0 ? Transcript[^1] : null;
            if (last is { Kind: TranscriptEntryKind.Assistant, IsComplete: false })
                last.IsComplete = true;
        }

        private void AddEntry(TranscriptEntry entry)
        {
            Transcript.Add(entry);
            EntryAdded?.Invoke(this, Transcript.Count - 1);
            Changed?.Invoke(this);
        }

        private void SetState(AgentSessionState newState)
        {
            if (State == newState)
                return;
            State = newState;
            Changed?.Invoke(this);
        }
    }
}
