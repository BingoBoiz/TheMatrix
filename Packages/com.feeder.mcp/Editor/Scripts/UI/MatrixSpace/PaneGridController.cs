#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Feeder.MCP.Editor.UI.MatrixSpace
{
    /// <summary>
    /// Lays out agent panes in fixed flexbox presets. Panes are created once and re-parented
    /// on preset change; shrinking hides panes (their sessions stay alive) instead of killing them.
    /// </summary>
    public sealed class PaneGridController
    {
        public static readonly (string Label, int PaneCount, int Columns)[] Presets =
        {
            ("1", 1, 1),
            ("2", 2, 2),
            ("4", 4, 2),
            ("6", 6, 3),
            ("8", 8, 4),
        };

        private readonly VisualElement _gridRoot;
        private readonly List<AgentPaneView> _panes = new();
        private readonly Func<int, AgentPaneView> _paneFactory;
        private int _presetIndex;

        public IReadOnlyList<AgentPaneView> Panes => _panes;
        public int VisiblePaneCount { get; private set; }

        /// <summary>The pane currently expanded to fill the grid, or null when the preset layout is shown.</summary>
        public AgentPaneView? ExpandedPane { get; private set; }

        public PaneGridController(VisualElement gridRoot, Func<int, AgentPaneView> paneFactory)
        {
            _gridRoot = gridRoot;
            _paneFactory = paneFactory;
        }

        public void ApplyPreset(int presetIndex)
        {
            presetIndex = Math.Clamp(presetIndex, 0, Presets.Length - 1);
            _presetIndex = presetIndex;
            ExpandedPane = null;
            var (_, paneCount, columns) = Presets[presetIndex];

            while (_panes.Count < paneCount)
                _panes.Add(_paneFactory(_panes.Count));

            VisiblePaneCount = paneCount;

            // Detach everything, rebuild the row containers, re-attach visible panes.
            foreach (var pane in _panes)
                pane.RemoveFromHierarchy();
            _gridRoot.Clear();

            var rows = (paneCount + columns - 1) / columns;
            var paneIndex = 0;
            for (var r = 0; r < rows; r++)
            {
                var row = new VisualElement();
                row.AddToClassList("matrix-pane-row");
                _gridRoot.Add(row);

                for (var c = 0; c < columns && paneIndex < paneCount; c++, paneIndex++)
                    row.Add(_panes[paneIndex]);
            }
        }

        /// <summary>
        /// Expands one pane to fill the whole grid; calling again with the same pane (or null)
        /// restores the current preset layout. Sessions in hidden panes keep streaming.
        /// </summary>
        public void ToggleExpanded(AgentPaneView pane)
        {
            if (ExpandedPane == pane)
            {
                ApplyPreset(_presetIndex);
                return;
            }

            foreach (var other in _panes)
                other.RemoveFromHierarchy();
            _gridRoot.Clear();

            var row = new VisualElement();
            row.AddToClassList("matrix-pane-row");
            _gridRoot.Add(row);
            row.Add(pane);

            ExpandedPane = pane;
        }
    }
}
