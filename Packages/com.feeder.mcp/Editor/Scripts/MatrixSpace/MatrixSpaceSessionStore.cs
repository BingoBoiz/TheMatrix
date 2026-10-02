#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;

namespace Feeder.MCP.Editor.MatrixSpace
{
    /// <summary>
    /// Durable per-project Matrix Space state. The file lives under UserSettings so pane
    /// identity, transcript, usage and drafts survive both assembly reloads and Editor restarts.
    /// </summary>
    [FilePath("UserSettings/MatrixSpaceSessions.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class MatrixSpaceSessionStore : ScriptableSingleton<MatrixSpaceSessionStore>
    {
        private const int CurrentSchemaVersion = 2;
        private const double PersistDebounceSeconds = 0.5;
        private const int MaxPersistedEntries = 200;

        [Serializable]
        public sealed class TranscriptSnapshot
        {
            public int Kind;
            public string Text = string.Empty;
            public string? ToolName;
            public long TimestampTicksUtc;
            public bool IsComplete;
            public long CompletedAtTicksUtc;

            public void Capture(TranscriptEntry entry)
            {
                Kind = (int)entry.Kind;
                Text = entry.Text;
                ToolName = entry.ToolName;
                TimestampTicksUtc = entry.Timestamp.ToUniversalTime().Ticks;
                IsComplete = entry.IsComplete;
                CompletedAtTicksUtc = entry.CompletedAt?.ToUniversalTime().Ticks ?? 0;
            }

            public TranscriptEntry Restore()
            {
                var kind = Enum.IsDefined(typeof(TranscriptEntryKind), Kind)
                    ? (TranscriptEntryKind)Kind
                    : TranscriptEntryKind.System;
                var entry = new TranscriptEntry(kind, Text ?? string.Empty, ToolName, IsComplete)
                {
                    Timestamp = TicksToLocal(TimestampTicksUtc),
                    CompletedAt = CompletedAtTicksUtc > 0 ? TicksToLocal(CompletedAtTicksUtc) : null,
                };
                return entry;
            }

            private static DateTime TicksToLocal(long ticks)
            {
                if (ticks <= 0)
                    return DateTime.Now;
                try
                {
                    return new DateTime(ticks, DateTimeKind.Utc).ToLocalTime();
                }
                catch (ArgumentOutOfRangeException)
                {
                    return DateTime.Now;
                }
            }
        }

        [Serializable]
        public sealed class PaneRecord
        {
            public string PaneId = string.Empty;
            public string DisplayName = string.Empty;
            public string BackendId = AgentBackendCatalog.DefaultId;
            public string? BackendSessionId;
            public string? ModelId;
            public string? ModeId;
            public string? EffortId;
            public string PromptDraft = string.Empty;
            public List<TranscriptSnapshot> Transcript = new();
            public string TotalCostUsd = "0";
            public long TotalInputTokens;
            public long TotalOutputTokens;
            public long TotalCacheReadTokens;
            public long TotalCacheCreationTokens;
            public int LastState;
            public bool RecoveryAttempted;
        }

        [Serializable]
        public sealed class RoleAssignment
        {
            public string RoleId = string.Empty;
            public string PaneId = string.Empty;
        }

        [Serializable]
        public sealed class SwarmState
        {
            public bool Active;
            public string Mission = string.Empty;
            public long StartedTicksUtc;
            public List<RoleAssignment> Assignments = new();
        }

        public List<PaneRecord> Panes = new();
        public int GridPresetIndex;
        public int ActiveViewIndex;
        public SwarmState Swarm = new();
        public int SchemaVersion;

        private static bool _persistScheduled;
        private static double _persistAfter;

        public void MigrateIfNeeded()
        {
            if (SchemaVersion >= CurrentSchemaVersion)
                return;

            foreach (var pane in Panes)
            {
                pane.Transcript ??= new List<TranscriptSnapshot>();
                pane.PromptDraft ??= string.Empty;
                pane.TotalCostUsd = string.IsNullOrEmpty(pane.TotalCostUsd) ? "0" : pane.TotalCostUsd;

                // Schema 1 recovery could mistake Codex runtime envelopes for user messages.
                // They never belong in a Matrix Space transcript.
                pane.Transcript.RemoveAll(snapshot =>
                    snapshot.Kind == (int)TranscriptEntryKind.User &&
                    BackendTranscriptRecovery.IsImportedEnvelope(snapshot.Text));
            }

            SchemaVersion = CurrentSchemaVersion;
            PersistNow();
        }

        public PaneRecord GetOrCreatePane(int index)
        {
            MigrateIfNeeded();
            while (Panes.Count <= index)
            {
                Panes.Add(new PaneRecord
                {
                    PaneId = Guid.NewGuid().ToString("N"),
                    DisplayName = $"AGENT {Panes.Count + 1:00}",
                });
                SchedulePersist();
            }

            var record = Panes[index];
            if (record.Transcript.Count == 0 && !string.IsNullOrEmpty(record.BackendSessionId) && !record.RecoveryAttempted)
            {
                record.RecoveryAttempted = true;
                if (BackendTranscriptRecovery.TryRecover(record.BackendId, record.BackendSessionId!, out var recovered))
                    record.Transcript.AddRange(recovered);
                SchedulePersist();
            }

            return record;
        }

        public void ResetPane(int index)
        {
            if (index < 0 || index >= Panes.Count)
                return;

            Panes[index] = new PaneRecord
            {
                PaneId = Guid.NewGuid().ToString("N"),
                DisplayName = $"AGENT {index + 1:00}",
            };
            PersistNow();
        }

        public void CaptureSession(PaneRecord record, AgentSession session)
        {
            record.BackendSessionId = session.Backend.SessionId;
            record.TotalCostUsd = session.TotalCostUsd.ToString(CultureInfo.InvariantCulture);
            record.TotalInputTokens = session.TotalInputTokens;
            record.TotalOutputTokens = session.TotalOutputTokens;
            record.TotalCacheReadTokens = session.TotalCacheReadTokens;
            record.TotalCacheCreationTokens = session.TotalCacheCreationTokens;
            record.LastState = (int)session.State;

            var first = Math.Max(0, session.Transcript.Count - MaxPersistedEntries);
            var count = session.Transcript.Count - first;

            while (record.Transcript.Count > count)
                record.Transcript.RemoveAt(record.Transcript.Count - 1);

            for (var i = 0; i < count; i++)
            {
                if (i == record.Transcript.Count)
                    record.Transcript.Add(new TranscriptSnapshot());
                record.Transcript[i].Capture(session.Transcript[first + i]);
            }

            SchedulePersist();
        }

        internal void RestoreSession(PaneRecord record, AgentSession session)
        {
            foreach (var snapshot in record.Transcript)
                session.Transcript.Add(snapshot.Restore());

            decimal.TryParse(record.TotalCostUsd, NumberStyles.Number, CultureInfo.InvariantCulture, out var cost);
            session.RestoreTotals(cost, record.TotalInputTokens, record.TotalOutputTokens,
                record.TotalCacheReadTokens, record.TotalCacheCreationTokens);

            var restoredState = Enum.IsDefined(typeof(AgentSessionState), record.LastState)
                ? (AgentSessionState)record.LastState
                : AgentSessionState.Idle;

            var interrupted = restoredState is AgentSessionState.Starting or AgentSessionState.Streaming;
            if (interrupted)
            {
                restoredState = string.IsNullOrEmpty(record.BackendSessionId)
                    ? AgentSessionState.Error
                    : AgentSessionState.WaitingInput;
                session.AddRestoredEntryOnce(new TranscriptEntry(TranscriptEntryKind.System,
                    "The previous turn was interrupted by a forced Unity reload. Conversation context was preserved; send again to resume."));
            }
            else if (restoredState == AgentSessionState.Exited)
            {
                restoredState = string.IsNullOrEmpty(record.BackendSessionId)
                    ? AgentSessionState.Idle
                    : AgentSessionState.WaitingInput;
            }
            else if (restoredState == AgentSessionState.Idle && !string.IsNullOrEmpty(record.BackendSessionId))
            {
                restoredState = AgentSessionState.WaitingInput;
            }

            session.RestoreState(restoredState);

            if (session.Transcript.Count == 0 && !string.IsNullOrEmpty(record.BackendSessionId))
            {
                session.AddRestoredEntryOnce(new TranscriptEntry(TranscriptEntryKind.System,
                    "Matrix reloaded — conversation context is preserved by the backend session."));
            }
        }

        public void SchedulePersist()
        {
            _persistAfter = EditorApplication.timeSinceStartup + PersistDebounceSeconds;
            if (_persistScheduled)
                return;
            _persistScheduled = true;
            EditorApplication.update += PersistWhenDue;
        }

        public void PersistNow()
        {
            if (_persistScheduled)
            {
                EditorApplication.update -= PersistWhenDue;
                _persistScheduled = false;
            }
            Save(true);
        }

        private static void PersistWhenDue()
        {
            if (EditorApplication.timeSinceStartup < _persistAfter)
                return;
            instance.PersistNow();
        }
    }
}
