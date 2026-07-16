#nullable enable
using Feeder.MCP.Editor.Utils;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;

namespace Feeder.MCP.Editor.UI
{
    [Overlay(typeof(SceneView), id: Id, displayName: "Feeder AI",
        defaultDisplay = true,
        defaultDockZone = DockZone.TopToolbar,
        defaultDockPosition = DockPosition.Top,
        defaultDockIndex = 0,
        defaultLayout = Layout.HorizontalToolbar)]
    [Icon(EditorAssetLoader.PackageLogoIconPath)]
    public class SceneViewToolbarOverlay : ToolbarOverlay
    {
        public const string Id = "feeder-mcp-toolbar";

        private SceneViewToolbarOverlay() : base(OpenWindowButton.Id)
        {
            collapsedIcon = EditorAssetLoader.LoadAssetAtPath<Texture2D>(EditorAssetLoader.PackageLogoIcon);
        }
    }
}
