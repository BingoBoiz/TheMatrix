#nullable enable
using Feeder.MCP.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Feeder.MCP.Editor.UI
{
    /// <summary>
    /// Feeder fork only: Matrix look for the AI Connector window — a green USS overlay on top of
    /// MainWindow.uss plus a <see cref="MatrixRainRenderer"/> digital-rain background drawn in an
    /// IMGUIContainer behind the UI Toolkit tree.
    /// </summary>
    public partial class MainWindowEditor
    {
        private static readonly string[] _matrixThemeUssPaths = EditorAssetLoader.GetEditorAssetPaths("Editor/UI/uss/FeederMatrixConnector.uss");

        private const long MatrixRainRepaintIntervalMs = 50;
        private static readonly Color MatrixWindowBackground = new Color(0.004f, 0.016f, 0.006f, 1f);

        private MatrixRainRenderer? _matrixRain;
        private IMGUIContainer? _matrixRainContainer;
        private double _lastMatrixRainStep;

        private void ApplyMatrixTheme(VisualElement root)
        {
            // Separate styleSheets.Add on purpose: base ApplyStyleSheets/LoadAssetAtPath returns
            // only the FIRST path that resolves, so the theme cannot ride along in _windowUssPaths.
            var sheet = EditorAssetLoader.LoadAssetAtPath<StyleSheet>(_matrixThemeUssPaths, Logger);
            if (sheet != null && !root.styleSheets.Contains(sheet))
                root.styleSheets.Add(sheet);

            root.style.backgroundColor = MatrixWindowBackground;

            if (_matrixRain == null)
                _matrixRain = new MatrixRainRenderer();

            if (_matrixRainContainer == null)
            {
                var container = new IMGUIContainer(DrawMatrixRain)
                {
                    name = "matrix-rain-background",
                    pickingMode = PickingMode.Ignore,
                };
                container.style.position = Position.Absolute;
                container.style.top = 0;
                container.style.left = 0;
                container.style.right = 0;
                container.style.bottom = 0;
                container.schedule
                    .Execute(() => container.MarkDirtyRepaint())
                    .Every(MatrixRainRepaintIntervalMs);
                _matrixRainContainer = container;
                _lastMatrixRainStep = EditorApplication.timeSinceStartup;
            }

            // CreateGUI clears the root on every rebuild (e.g. Invalidate), so the background
            // has to be re-inserted behind the freshly cloned tree.
            if (_matrixRainContainer.parent != root)
                root.Insert(0, _matrixRainContainer);
            else
                _matrixRainContainer.SendToBack();
        }

        private void DrawMatrixRain()
        {
            if (_matrixRain == null || _matrixRainContainer == null)
                return;
            if (Event.current.type != EventType.Repaint)
                return;

            var now = EditorApplication.timeSinceStartup;
            var dt = Mathf.Clamp((float)(now - _lastMatrixRainStep), 0f, 0.1f);
            _lastMatrixRainStep = now;

            var rect = _matrixRainContainer.contentRect;
            if (rect.width < 1f || rect.height < 1f)
                return;

            _matrixRain.Step(dt);
            _matrixRain.Draw(rect.width, rect.height);
        }

        private void OnDestroy()
        {
            if (_matrixRain != null)
            {
                _matrixRain.Dispose();
                _matrixRain = null;
            }
        }
    }
}
