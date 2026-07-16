#nullable enable

using System;
using Feeder.MCP.Editor.Utils;
using Feeder.MCP.Utils;
using Microsoft.Extensions.Logging;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Feeder.MCP.Editor.UI
{
    /// <summary>
    /// Base class for MCP Editor windows providing common UXML/USS loading functionality.
    /// </summary>
    public abstract class McpWindowBase : EditorWindow
    {
        protected Microsoft.Extensions.Logging.ILogger Logger { get; private set; } = null!;

        protected abstract string[] WindowUxmlPaths { get; }
        protected abstract string[] WindowUssPaths { get; }
        protected abstract string WindowTitle { get; }

        protected virtual void OnEnable()
        {
            Logger = UnityLoggerFactory.LoggerFactory.CreateLogger(GetType().Name);
        }

        protected void SetupWindowWithIcon(string? customTitle = null)
        {
            var icon = EditorAssetLoader.LoadAssetAtPath<Texture>(EditorAssetLoader.PackageLogoIcon);
            if (icon != null)
                titleContent = new GUIContent(customTitle ?? WindowTitle, icon);
        }

        public virtual void CreateGUI()
        {
            rootVisualElement.Clear();

            var visualTree = EditorAssetLoader.LoadAssetAtPath<VisualTreeAsset>(WindowUxmlPaths, Logger);
            if (visualTree == null)
            {
                Logger.LogWarning("{method} UXML template not found.", nameof(CreateGUI));
                return;
            }

            visualTree.CloneTree(rootVisualElement);
            ApplyStyleSheets(rootVisualElement);
            OnGUICreated(rootVisualElement);
        }

        protected virtual void OnGUICreated(VisualElement root)
        {
        }

        protected void ApplyStyleSheets(VisualElement root)
        {
            var sheet = EditorAssetLoader.LoadAssetAtPath<StyleSheet>(WindowUssPaths, Logger);
            if (sheet == null)
            {
                Logger.LogWarning("{method} USS file not found.", nameof(ApplyStyleSheets));
                return;
            }

            try
            {
                root.styleSheets.Add(sheet);
                Logger.LogTrace("{method} Applied USS", nameof(ApplyStyleSheets));
            }
            catch (Exception ex)
            {
                Logger.LogWarning("{method} Failed to add USS: {ex}", nameof(ApplyStyleSheets), ex);
            }
        }

        protected void UpdateItemClasses(VisualElement? itemContainer, bool isEnabled)
        {
            if (itemContainer == null)
                return;

            itemContainer.EnableInClassList("enabled", isEnabled);
            itemContainer.EnableInClassList("disabled", !isEnabled);
        }

        public static void UpdateFoldoutState(Foldout foldout, bool expanded)
        {
            foldout.EnableInClassList("expanded", expanded);
            foldout.EnableInClassList("collapsed", !expanded);
        }

        public static void EnableSmoothFoldoutTransitions(VisualElement root)
        {
            root.Query<Foldout>().ForEach(foldout =>
            {
                foldout.RegisterValueChangedCallback(evt =>
                {
                    UpdateFoldoutState(foldout, evt.newValue);
                });
                UpdateFoldoutState(foldout, foldout.value);
            });
        }
    }
}
