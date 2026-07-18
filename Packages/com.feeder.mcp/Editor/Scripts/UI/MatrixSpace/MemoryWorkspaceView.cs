#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Feeder.MCP.Editor.MatrixSpace;
using Feeder.MCP.Editor.UI.Controls;
using UnityEditor;
using UnityEngine.UIElements;

namespace Feeder.MCP.Editor.UI.MatrixSpace
{
    /// <summary>
    /// MatrixMemory: three-pane knowledge workspace — left list (search, stats, sections),
    /// center editor/graph toggle, right INSPECTOR with outgoing links + backlinks.
    /// </summary>
    public sealed class MemoryWorkspaceView : VisualElement
    {
        private readonly MemoryGraphStore _store = new();

        private readonly TextField _search;
        private readonly Label _stats;
        private readonly VisualElement _listRoot;
        private readonly SegmentedControl _modeToggle;
        private readonly VisualElement _editorPane;
        private readonly TextField _editorField;
        private readonly Label _editorFileLabel;
        private readonly MemoryGraphView _graph;
        private readonly VisualElement _outgoingList;
        private readonly VisualElement _backlinksList;

        private string? _selectedKey;
        private bool _newEditorOpen;

        public MemoryWorkspaceView()
        {
            AddToClassList("memory-workspace");

            // ── Left: list pane ──────────────────────────────────────────
            var left = new VisualElement();
            left.AddToClassList("memory-list-pane");

            var title = new Label("MEMORY // SHARED");
            title.AddToClassList("memory-pane-title");
            title.tooltip = MatrixSpacePaths.MemoryDir;
            left.Add(title);

            _search = new TextField { tooltip = "Search memories" };
            _search.AddToClassList("styled-text-field");
            _search.AddToClassList("memory-search");
            _search.SetValueWithoutNotify(string.Empty);
            _search.RegisterValueChangedCallback(_ => RebuildList());
            left.Add(_search);

            _stats = new Label(string.Empty);
            _stats.AddToClassList("memory-stats");
            left.Add(_stats);

            var listScroll = new ScrollView(ScrollViewMode.Vertical);
            listScroll.AddToClassList("memory-list-scroll");
            _listRoot = new VisualElement();
            listScroll.Add(_listRoot);
            left.Add(listScroll);

            var bottomRow = new VisualElement();
            bottomRow.AddToClassList("memory-list-bottom");
            var newButton = new Button(ToggleNewEditor) { text = "+ MEMORY" };
            newButton.AddToClassList("btn-secondary");
            newButton.AddToClassList("btn-compact");
            bottomRow.Add(newButton);
            var refreshButton = new Button(Load) { text = "REFRESH" };
            refreshButton.AddToClassList("btn-tertiary");
            refreshButton.AddToClassList("btn-compact");
            bottomRow.Add(refreshButton);
            left.Add(bottomRow);

            Add(left);

            // ── Center: editor / graph ───────────────────────────────────
            var centerPane = new VisualElement();
            centerPane.AddToClassList("memory-center-pane");

            var centerHeader = new VisualElement();
            centerHeader.AddToClassList("memory-center-header");
            _modeToggle = new SegmentedControl(new[] { "memory", "graph" });
            _modeToggle.SetValueWithoutNotify(0);
            _modeToggle.RegisterCallback<ChangeEvent<int>>(_ => ApplyMode());
            centerHeader.Add(_modeToggle);
            var headerSpacer = new VisualElement();
            headerSpacer.AddToClassList("memory-center-header-spacer");
            centerHeader.Add(headerSpacer);
            _editorFileLabel = new Label(string.Empty);
            _editorFileLabel.AddToClassList("memory-file-label");
            centerHeader.Add(_editorFileLabel);
            centerPane.Add(centerHeader);

            _editorPane = new VisualElement();
            _editorPane.AddToClassList("memory-editor-pane");
            var editorScroll = new ScrollView(ScrollViewMode.Vertical);
            editorScroll.AddToClassList("memory-editor-scroll");
            _editorField = new TextField { multiline = true };
            _editorField.AddToClassList("styled-text-field");
            _editorField.AddToClassList("memory-editor-field");
            editorScroll.Add(_editorField);
            _editorPane.Add(editorScroll);

            var editorButtons = new VisualElement();
            editorButtons.AddToClassList("memory-editor-buttons");
            var save = new Button(SaveSelected) { text = "SAVE" };
            save.AddToClassList("btn-primary");
            save.AddToClassList("btn-compact");
            editorButtons.Add(save);
            var delete = new Button(DeleteSelected) { text = "DELETE" };
            delete.AddToClassList("btn-tertiary");
            editorButtons.Add(delete);
            _editorPane.Add(editorButtons);
            centerPane.Add(_editorPane);

            _graph = new MemoryGraphView();
            _graph.NodeSelected += key =>
            {
                Select(key, refreshGraphSelection: false);
            };
            centerPane.Add(_graph);

            Add(centerPane);

            // ── Right: inspector ─────────────────────────────────────────
            var right = new VisualElement();
            right.AddToClassList("memory-inspector-pane");

            var inspectorTitle = new Label("INSPECTOR");
            inspectorTitle.AddToClassList("memory-pane-title");
            right.Add(inspectorTitle);

            var connections = new Label("CONNECTIONS");
            connections.AddToClassList("memory-inspector-section");
            right.Add(connections);

            var outgoingHeader = new Label("Outgoing");
            outgoingHeader.AddToClassList("memory-inspector-subsection");
            right.Add(outgoingHeader);
            _outgoingList = new VisualElement();
            right.Add(_outgoingList);

            var backlinksHeader = new Label("Backlinks");
            backlinksHeader.AddToClassList("memory-inspector-subsection");
            right.Add(backlinksHeader);
            _backlinksList = new VisualElement();
            right.Add(_backlinksList);

            Add(right);

            ApplyMode();
        }

        public void Load()
        {
            _store.Scan();
            if (_selectedKey == null || _store.Find(_selectedKey) == null)
                _selectedKey = _store.Nodes.FirstOrDefault(n => n.IsHub)?.Key ?? _store.Nodes.FirstOrDefault()?.Key;

            RebuildList();
            RebuildInspector();
            LoadEditor();
            _graph.SetData(_store, _selectedKey);
        }

        public int NodeCount => _store.Nodes.Count;

        private void ApplyMode()
        {
            var graphMode = _modeToggle.SelectedIndex == 1;
            _editorPane.style.display = graphMode ? DisplayStyle.None : DisplayStyle.Flex;
            _graph.style.display = graphMode ? DisplayStyle.Flex : DisplayStyle.None;
            if (graphMode)
                _graph.SetData(_store, _selectedKey);
        }

        private void Select(string key, bool refreshGraphSelection = true)
        {
            _selectedKey = key;
            RebuildList();
            RebuildInspector();
            LoadEditor();
            if (refreshGraphSelection)
                _graph.SetSelected(key);
        }

        // ── Left list ────────────────────────────────────────────────────

        private void RebuildList()
        {
            _listRoot.Clear();

            var filter = _search.value?.Trim().ToLowerInvariant() ?? string.Empty;
            var nodes = _store.Nodes
                .Where(n => filter.Length == 0 || n.Title.ToLowerInvariant().Contains(filter) || n.Key.Contains(filter))
                .ToList();

            _stats.text = $"{_store.Nodes.Count} memories · {_store.LinkCount} links · {_store.OrphanCount} orphans";

            // Hub pinned first.
            foreach (var hub in nodes.Where(n => n.IsHub))
                _listRoot.Add(BuildListItem(hub));

            foreach (var group in nodes.Where(n => !n.IsHub).GroupBy(n => n.Section).OrderBy(g => g.Key))
            {
                var header = new Label(group.Key);
                header.AddToClassList("memory-section-header");
                _listRoot.Add(header);

                foreach (var node in group.OrderBy(n => n.Title))
                    _listRoot.Add(BuildListItem(node));
            }
        }

        private VisualElement BuildListItem(MemoryGraphStore.MemoryNode node)
        {
            var item = new VisualElement { tooltip = node.FileName };
            item.AddToClassList("memory-list-item");
            item.EnableInClassList("memory-list-item--selected", node.Key == _selectedKey);
            item.EnableInClassList("memory-list-item--orphan", node.IsOrphan);

            var icon = new Label(node.IsHub ? "◉" : "▪");
            icon.AddToClassList("memory-list-item-icon");
            item.Add(icon);

            var label = new Label(node.Title);
            label.AddToClassList("memory-list-item-label");
            item.Add(label);

            var links = node.OutgoingLinks.Count + node.Backlinks.Count;
            if (links > 0)
            {
                var badge = new Label(links.ToString());
                badge.AddToClassList("memory-list-item-links");
                item.Add(badge);
            }

            item.RegisterCallback<ClickEvent>(_ => Select(node.Key));
            return item;
        }

        private void ToggleNewEditor()
        {
            if (_newEditorOpen)
            {
                RebuildList();
                _newEditorOpen = false;
                return;
            }

            _newEditorOpen = true;

            var editor = new VisualElement();
            editor.AddToClassList("memory-new-editor");

            var titleField = new TextField { tooltip = "Memory title" };
            titleField.AddToClassList("styled-text-field");
            titleField.SetValueWithoutNotify("new-memory");
            editor.Add(titleField);

            var sectionField = new TextField { tooltip = "Section (e.g. ARCHITECTURE, DECISIONS, BUGS)" };
            sectionField.AddToClassList("styled-text-field");
            sectionField.SetValueWithoutNotify("NOTES");
            editor.Add(sectionField);

            var add = new Button(() =>
            {
                _newEditorOpen = false;
                var node = _store.Create(titleField.value?.Trim() ?? string.Empty,
                    sectionField.value?.Trim() ?? "NOTES");
                if (node != null)
                    _selectedKey = node.Key;
                Load();
            }) { text = "ADD" };
            add.AddToClassList("btn-primary");
            add.AddToClassList("btn-compact");
            editor.Add(add);

            _listRoot.Insert(0, editor);
            titleField.Focus();
        }

        // ── Center editor ────────────────────────────────────────────────

        private void LoadEditor()
        {
            var node = _selectedKey != null ? _store.Find(_selectedKey) : null;
            _editorFileLabel.text = node?.FileName ?? string.Empty;
            _editorField.SetValueWithoutNotify(node?.Body ?? string.Empty);
            _editorField.SetEnabled(node != null);
        }

        private void SaveSelected()
        {
            var node = _selectedKey != null ? _store.Find(_selectedKey) : null;
            if (node == null)
                return;
            _store.Save(node, _editorField.value);
            Load();
        }

        private void DeleteSelected()
        {
            var node = _selectedKey != null ? _store.Find(_selectedKey) : null;
            if (node == null)
                return;
            if (!EditorUtility.DisplayDialog("Delete memory",
                    $"Delete {node.FileName}? This removes the file from MatrixSpace/memory/.", "Delete", "Cancel"))
                return;
            _store.Delete(node);
            _selectedKey = null;
            Load();
        }

        // ── Right inspector ──────────────────────────────────────────────

        private void RebuildInspector()
        {
            _outgoingList.Clear();
            _backlinksList.Clear();

            var node = _selectedKey != null ? _store.Find(_selectedKey) : null;
            if (node == null)
                return;

            foreach (var key in node.OutgoingLinks)
                _outgoingList.Add(BuildLinkRow(key));
            if (node.OutgoingLinks.Count == 0)
                _outgoingList.Add(BuildEmptyRow());

            foreach (var key in node.Backlinks)
                _backlinksList.Add(BuildLinkRow(key));
            if (node.Backlinks.Count == 0)
                _backlinksList.Add(BuildEmptyRow());
        }

        private VisualElement BuildLinkRow(string key)
        {
            var exists = _store.Find(key) != null;
            var row = new Label($"[[{key}]]");
            row.AddToClassList("memory-link-row");
            row.EnableInClassList("memory-link-row--missing", !exists);
            if (exists)
                row.RegisterCallback<ClickEvent>(_ => Select(key));
            return row;
        }

        private static VisualElement BuildEmptyRow()
        {
            var row = new Label("—");
            row.AddToClassList("memory-link-row-empty");
            return row;
        }
    }
}
