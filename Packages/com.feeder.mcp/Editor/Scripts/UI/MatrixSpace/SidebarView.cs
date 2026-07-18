#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Feeder.MCP.Editor.UI.MatrixSpace
{
    /// <summary>
    /// BridgeSpace-style left navigation: WORKSPACES header, workspace entries (currently a
    /// single "construct" grid), then boards / memory, with swarm / config pinned at the bottom.
    /// Selected item gets a left accent bar; badges show live counts.
    /// </summary>
    public sealed class SidebarView : VisualElement
    {
        public event Action<MatrixSpaceView>? ItemClicked;

        private readonly Dictionary<MatrixSpaceView, VisualElement> _items = new();
        private readonly Dictionary<MatrixSpaceView, Label> _badges = new();

        public SidebarView()
        {
            name = "matrix-sidebar";
            AddToClassList("matrix-sidebar");

            var header = new VisualElement();
            header.AddToClassList("sidebar-header");
            var headerLabel = new Label("WORKSPACES");
            headerLabel.AddToClassList("sidebar-header-label");
            header.Add(headerLabel);
            var headerSpacer = new VisualElement();
            headerSpacer.AddToClassList("sidebar-header-spacer");
            header.Add(headerSpacer);
            var addButton = new Button { text = "+", tooltip = "Multiple workspaces: coming soon." };
            addButton.AddToClassList("btn-tertiary");
            addButton.AddToClassList("sidebar-add-button");
            addButton.SetEnabled(false);
            header.Add(addButton);
            Add(header);

            AddItem(MatrixSpaceView.Construct, "▦", "construct", "The agent pane grid.");

            var separator = new VisualElement();
            separator.AddToClassList("sidebar-separator");
            Add(separator);

            AddItem(MatrixSpaceView.Boards, "☰", "boards", "Kanban task board (MatrixBoard).");
            AddItem(MatrixSpaceView.Memory, "◈", "memory", "Shared knowledge graph (MatrixMemory).");

            var spacer = new VisualElement();
            spacer.AddToClassList("sidebar-spacer");
            Add(spacer);

            AddItem(MatrixSpaceView.Swarm, "⌬", "swarm", "One mission, a team of role agents (MatrixSwarm).");
            AddItem(MatrixSpaceView.Config, "⚙", "config", "Agents, models, logins, system parameters.");
        }

        private void AddItem(MatrixSpaceView view, string glyph, string label, string tooltip)
        {
            var item = new VisualElement { tooltip = tooltip };
            item.AddToClassList("sidebar-item");

            var accent = new VisualElement();
            accent.AddToClassList("sidebar-item-accent");
            item.Add(accent);

            var icon = new Label(glyph);
            icon.AddToClassList("sidebar-item-icon");
            item.Add(icon);

            var text = new Label(label);
            text.AddToClassList("sidebar-item-label");
            item.Add(text);

            var badge = new Label(string.Empty);
            badge.AddToClassList("sidebar-badge");
            badge.style.display = DisplayStyle.None;
            item.Add(badge);

            item.RegisterCallback<ClickEvent>(_ => ItemClicked?.Invoke(view));

            _items[view] = item;
            _badges[view] = badge;
            Add(item);
        }

        public void SetSelected(MatrixSpaceView view)
        {
            foreach (var (key, item) in _items)
                item.EnableInClassList("sidebar-item--selected", key == view);
        }

        public void SetBadge(MatrixSpaceView view, int count)
        {
            if (!_badges.TryGetValue(view, out var badge))
                return;
            badge.text = count.ToString();
            badge.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
