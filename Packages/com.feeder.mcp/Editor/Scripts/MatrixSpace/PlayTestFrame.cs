#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Feeder.MCP.Editor.UI;
using Feeder.MCP.Editor.Utils;
using Feeder.MCP.Utils;
using Microsoft.Extensions.Logging;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Feeder.MCP.Editor.MatrixSpace
{
    [InitializeOnLoad]
    internal static class PlayTestFrame
    {
        const string FrameName = "matrix-play-test-frame";

        static readonly string[] _uxmlPaths = EditorAssetLoader.GetEditorAssetPaths("Editor/UI/uxml/PlayTestFrame.uxml");
        static readonly string[] _ussPaths = EditorAssetLoader.GetEditorAssetPaths("Editor/UI/uss/PlayTestFrame.uss");
        static readonly Type? _gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
        static readonly ILogger _logger = UnityLoggerFactory.LoggerFactory.CreateLogger(nameof(PlayTestFrame));

        static PlayTestFrame()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            if (AgentActivity.IsAgentPlay)
                EditorApplication.delayCall += ShowIfAgentPlay;
        }

        internal static void Show()
        {
            foreach (var view in GameViews())
            {
                var root = view.rootVisualElement;
                if (root.Q(FrameName) != null)
                    continue;
                var frame = Create();
                if (frame == null)
                    return;
                root.Add(frame);
            }
        }

        internal static void Hide()
        {
            foreach (var view in GameViews())
                view.rootVisualElement.Q(FrameName)?.RemoveFromHierarchy();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += ShowIfAgentPlay;
            else if (state == PlayModeStateChange.ExitingPlayMode)
                Hide();
        }

        static void ShowIfAgentPlay()
        {
            if (AgentActivity.IsAgentPlay)
                Show();
        }

        static IEnumerable<EditorWindow> GameViews()
        {
            if (_gameViewType == null)
            {
                _logger.LogError("{method} UnityEditor.GameView type not found.", nameof(GameViews));
                return Enumerable.Empty<EditorWindow>();
            }
            return Resources.FindObjectsOfTypeAll(_gameViewType).OfType<EditorWindow>();
        }

        static VisualElement? Create()
        {
            var tree = EditorAssetLoader.LoadAssetAtPath<VisualTreeAsset>(_uxmlPaths, _logger);
            var sheet = EditorAssetLoader.LoadAssetAtPath<StyleSheet>(_ussPaths, _logger);
            if (tree == null || sheet == null)
            {
                _logger.LogError("{method} PlayTestFrame.uxml or PlayTestFrame.uss could not be loaded.", nameof(Create));
                return null;
            }

            var frame = tree.Instantiate();
            frame.name = FrameName;
            frame.pickingMode = PickingMode.Ignore;
            frame.styleSheets.Add(sheet);
            frame.AddToClassList("ptf-host");
            BridgeFont.Apply(frame);
            return frame;
        }
    }
}
