#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;

namespace Feeder.MCP.Editor.MatrixSpace
{
    /// <summary>
    /// Survives domain reloads (not editor restarts): remembers each pane's identity and its
    /// claude session id so the next prompt after a reload still resumes the conversation.
    /// </summary>
    public sealed class MatrixSpaceSessionStore : ScriptableSingleton<MatrixSpaceSessionStore>
    {
        [Serializable]
        public sealed class PaneRecord
        {
            public string PaneId = string.Empty;
            public string DisplayName = string.Empty;
            public string BackendId = AgentBackendCatalog.DefaultId;
            public string? BackendSessionId;
            public string? ModelId;
            public string? PermissionModeId;

            /// <summary>Rendered chat history, snapshotted before reloads so panes rehydrate.</summary>
            public List<TranscriptEntry> Transcript = new();
            public double TotalCostUsd;
            public long TotalInputTokens;
            public long TotalOutputTokens;
            public long TotalCacheReadTokens;
            public long TotalCacheCreationTokens;
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

        /// <summary>Last shown center view (MatrixSpaceView) so a domain reload restores it.</summary>
        public int ActiveViewIndex;

        /// <summary>Running swarm mission (role → pane bindings) so the board survives reloads.</summary>
        public SwarmState Swarm = new();

        public PaneRecord GetOrCreatePane(int index)
        {
            while (Panes.Count <= index)
            {
                Panes.Add(new PaneRecord
                {
                    PaneId = Guid.NewGuid().ToString("N"),
                    DisplayName = $"AGENT {Panes.Count + 1:00}",
                });
            }

            return Panes[index];
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
        }
    }
}
