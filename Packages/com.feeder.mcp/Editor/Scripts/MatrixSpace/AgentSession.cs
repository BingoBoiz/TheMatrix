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
        private ReloadLease? _reloadLease;
        private bool _disposed;

        /// <summary>Fired on the main thread after any state/transcript change.</summary>
        public event Action<AgentSession>? Changed;

        /// <summary>Fired when a single entry was appended (index into Transcript).</summary>
        public event Action<AgentSession, int>? EntryAdded;

        /// <summary>Fired once per turn when it settles (true = clean, false = error).</summary>
        public event Action<AgentSession, bool>? TurnEnded;

        public AgentSession(string paneId, string displayName, IAgentBackend backend,
            string backendId = AgentBackendCatalog.DefaultId,
            MatrixSpaceSessionStore.PaneRecord? snapshot = null)
        {
            PaneId = paneId;
            BackendId = backendId;
            DisplayName = displayName;
            Backend = backend;
            Backend.AgentLabel = displayName;
            Backend.EventReceived += OnBackendEvent;
            if (snapshot != null)
                MatrixSpaceSessionStore.instance.RestoreSession(snapshot, this);
        }

        public bool CanSend =>
            (State is AgentSessionState.Idle or AgentSessionState.WaitingInput or AgentSessionState.Error) &&
            MatrixSpaceReloadCoordinator.CanStartTurn(out _);

        public void Send(string prompt)
        {
            if (!CanSend)
                return;
            prompt = prompt.Trim();
            if (prompt.Length == 0)
                return;

            try
            {
                _reloadLease = MatrixSpaceReloadCoordinator.Acquire(PaneId);
            }
            catch (Exception ex)
            {
                AddEntry(new TranscriptEntry(TranscriptEntryKind.System, ex.Message));
                return;
            }

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
                ReleaseReloadLease();
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
            if (_disposed)
                return;
            _disposed = true;
            Backend.EventReceived -= OnBackendEvent;
            Backend.Dispose();
            SetState(AgentSessionState.Exited);
            ReleaseReloadLease();
        }

        private void OnBackendEvent(AgentEvent evt)
        {
            switch (evt.Kind)
            {
                case AgentEventKind.SystemInit:
                    SetState(AgentSessionState.Streaming);
                    break;

                case AgentEventKind.AssistantText:
                    AppendStreamingText(TranscriptEntryKind.Assistant, evt.Text ?? string.Empty, evt.IsDelta);
                    SetState(AgentSessionState.Streaming);
                    break;

                case AgentEventKind.AssistantThinking:
                    AppendStreamingText(TranscriptEntryKind.Thinking, evt.Text ?? string.Empty, evt.IsDelta);
                    SetState(AgentSessionState.Streaming);
                    break;

                case AgentEventKind.ToolUse:
                    CompleteLastStreamingEntry();
                    AddEntry(new TranscriptEntry(TranscriptEntryKind.ToolUse,
                        $"> executing: {evt.ToolName ?? "unknown"}", evt.ToolName));
                    break;

                case AgentEventKind.ToolResult:
                    if (evt.IsError && !string.IsNullOrEmpty(evt.Text))
                        AddEntry(new TranscriptEntry(TranscriptEntryKind.ToolResult, $"< glitch: {evt.Text}"));
                    break;

                case AgentEventKind.Result:
                    CompleteLastStreamingEntry();
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
                    if (!Backend.IsRunning)
                        ReleaseReloadLease();
                    break;

                case AgentEventKind.ProcessExited:
                    CompleteLastStreamingEntry();
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
                    ReleaseReloadLease();
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

        private void AppendStreamingText(TranscriptEntryKind kind, string text, bool isDelta)
        {
            if (text.Length == 0)
                return;

            var last = Transcript.Count > 0 ? Transcript[^1] : null;
            if (last is { IsComplete: false } && last.Kind == kind)
            {
                var separator = isDelta || last.Text.Length == 0 ? string.Empty : "\n\n";
                last.Text += separator + text;
                Changed?.Invoke(this);
            }
            else
            {
                // Switching block type (e.g. thinking → answer) settles the previous one.
                CompleteLastStreamingEntry();
                AddEntry(new TranscriptEntry(kind, text, isComplete: false));
            }
        }

        private void CompleteLastStreamingEntry()
        {
            var last = Transcript.Count > 0 ? Transcript[^1] : null;
            if (last is { IsComplete: false } &&
                last.Kind is TranscriptEntryKind.Assistant or TranscriptEntryKind.Thinking)
            {
                last.IsComplete = true;
                last.CompletedAt = DateTime.Now;
                Changed?.Invoke(this);
            }
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

        private void ReleaseReloadLease()
        {
            var lease = _reloadLease;
            _reloadLease = null;
            lease?.Dispose();
        }

        internal void RestoreTotals(decimal costUsd, long inputTokens, long outputTokens,
            long cacheReadTokens, long cacheCreationTokens)
        {
            TotalCostUsd = costUsd;
            TotalInputTokens = inputTokens;
            TotalOutputTokens = outputTokens;
            TotalCacheReadTokens = cacheReadTokens;
            TotalCacheCreationTokens = cacheCreationTokens;
        }

        internal void RestoreState(AgentSessionState state) => State = state;

        internal void AddRestoredEntryOnce(TranscriptEntry entry)
        {
            if (Transcript.Count > 0 && Transcript[^1].Kind == entry.Kind && Transcript[^1].Text == entry.Text)
                return;
            Transcript.Add(entry);
        }
    }
}
