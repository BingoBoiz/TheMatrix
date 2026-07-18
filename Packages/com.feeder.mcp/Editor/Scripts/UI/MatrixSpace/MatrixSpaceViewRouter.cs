#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Feeder.MCP.Editor.UI.MatrixSpace
{
    public enum MatrixSpaceView
    {
        Construct = 0,
        Boards = 1,
        Memory = 2,
        Swarm = 3,
        Config = 4,
    }

    /// <summary>
    /// Swaps the center content between the registered views. View roots are added to the
    /// host once and toggled via display so hidden views keep their state (agent panes keep
    /// streaming while another view is shown).
    /// </summary>
    public sealed class MatrixSpaceViewRouter
    {
        private readonly VisualElement _host;
        private readonly Dictionary<MatrixSpaceView, VisualElement> _views = new();
        private readonly Dictionary<MatrixSpaceView, Action> _onShown = new();

        public MatrixSpaceView Current { get; private set; } = MatrixSpaceView.Construct;

        public event Action<MatrixSpaceView>? ViewChanged;

        public MatrixSpaceViewRouter(VisualElement host)
        {
            _host = host;
        }

        public void Register(MatrixSpaceView view, VisualElement root, Action? onShown = null)
        {
            _views[view] = root;
            if (onShown != null)
                _onShown[view] = onShown;
            if (root.parent != _host)
                _host.Add(root);
            root.style.display = DisplayStyle.None;
        }

        public void Show(MatrixSpaceView view)
        {
            if (!_views.ContainsKey(view))
                return;

            Current = view;
            foreach (var (key, root) in _views)
                root.style.display = key == view ? DisplayStyle.Flex : DisplayStyle.None;

            if (_onShown.TryGetValue(view, out var onShown))
                onShown();
            ViewChanged?.Invoke(view);
        }
    }
}
