#nullable enable
using System;
using Feeder.MCP.Editor.Utils;
using UnityEngine.UIElements;

namespace Feeder.MCP.Editor.UI
{
    public class UITemplate<T> where T : VisualElement
    {
        public T Value { get; private set; }

        public UITemplate(string templatePath)
        {
            var paths = EditorAssetLoader.GetEditorAssetPaths(templatePath);
            var template = EditorAssetLoader.LoadAssetAtPath<VisualTreeAsset>(paths) ?? throw new NullReferenceException($"Failed to load UXML template at path: {templatePath}");
            var root = template.CloneTree();
            Value = root.Q<T>() ?? throw new InvalidCastException($"Root element is not of type {typeof(T).Name}");
        }
    }
}