#nullable enable
using Feeder.MCP.Editor.Utils;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace Feeder.MCP.Editor.UI
{
    [EditorToolbarElement(Id, typeof(SceneView))]
    public class OpenWindowButton : EditorToolbarButton
    {
        public const string Id = "feeder-mcp-toolbar/open-window";

        public OpenWindowButton()
        {
            text = "Feeder AI";
            icon = EditorAssetLoader.LoadAssetAtPath<Texture2D>(EditorAssetLoader.PackageLogoIcon);
            tooltip = "Open Feeder Matrix AI Connector window";
            clicked += MainWindowEditor.ShowWindowVoid;
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            clicked -= MainWindowEditor.ShowWindowVoid;
        }
    }
}
