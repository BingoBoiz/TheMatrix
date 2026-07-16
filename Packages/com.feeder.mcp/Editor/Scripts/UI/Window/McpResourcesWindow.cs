#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Feeder.McpPlugin;
using Feeder.MCP.Editor.Utils;
using Feeder.MCP.Editor.Persistence;
using Microsoft.Extensions.Logging;
using R3;
using UnityEngine.UIElements;

namespace Feeder.MCP.Editor.UI
{
    public class McpResourcesWindow : McpListWindowBase<McpResourcesWindow.ResourceViewModel>
    {
        private static readonly string[] _windowUxmlPaths = EditorAssetLoader.GetEditorAssetPaths("Editor/UI/uxml/McpResourcesWindow.uxml");
        private static readonly string[] _itemUxmlPaths = EditorAssetLoader.GetEditorAssetPaths("Editor/UI/uxml/ResourceItem.uxml");
        private static readonly string[] _windowUssPaths = EditorAssetLoader.GetEditorAssetPaths("Editor/UI/uss/McpResourcesWindow.uss");

        protected override string[] WindowUxmlPaths => _windowUxmlPaths;
        protected override string[] ItemUxmlPaths => _itemUxmlPaths;
        protected override string[] WindowUssPaths => _windowUssPaths;
        protected override string WindowTitle => "MCP Resources";
        protected override string MissingTemplateMessage =>
            "ResourceItem template is missing. Please ensure ResourceItem.uxml exists in the package or the Packages/com.feeder.mcp folder.";

        public static McpResourcesWindow ShowWindow()
        {
            var window = GetWindow<McpResourcesWindow>("MCP Resources");
            window.SetupWindowWithIcon();
            window.Show();
            window.Focus();
            return window;
        }

        protected override Observable<Unit>? GetOnUpdatedObservable(IMcpPlugin plugin)
            => plugin.McpManager.ResourceManager?.OnResourcesUpdated;

        protected override void RefreshItems()
        {
            var resourceManager = UnityMcpPluginEditor.Instance.Resources;
            var refreshed = new List<ResourceViewModel>();

            if (resourceManager != null)
            {
                foreach (var resource in resourceManager.GetAllResources().Where(resource => resource != null))
                {
                    refreshed.Add(new ResourceViewModel(resourceManager, resource));
                }
            }

            allItems = refreshed;
        }

        protected override void OnItemToggleChanged(ResourceViewModel viewModel, bool isEnabled)
        {
            var resourceManager = UnityMcpPluginEditor.Instance.Resources;
            if (resourceManager == null)
            {
                Logger.LogError("{method} ResourceManager is not available.", nameof(OnItemToggleChanged));
                return;
            }

            viewModel.IsEnabled = isEnabled;
            if (!string.IsNullOrWhiteSpace(viewModel.Name))
            {
                Logger.LogTrace("{method} Setting resource '{name}' enabled state to {enabled}.",
                    nameof(OnItemToggleChanged), viewModel.Name, isEnabled);
                resourceManager.SetResourceEnabled(viewModel.Name, isEnabled);
                UnityMcpPluginEditor.Instance.Save();
            }
        }

        protected override void OnFoldoutChanged(ResourceViewModel viewModel, string foldoutName, bool isExpanded)
        {
            if (foldoutName == "description-foldout")
            {
                viewModel.descriptionExpanded.Value = isExpanded;
            }
        }

        protected override void BindItem(VisualElement element, ResourceViewModel viewModel)
        {
            BindCommonItemFields(element, viewModel);
            BindDescriptionFoldout(element, viewModel, viewModel.descriptionExpanded.Value);

            var uriLabel = element.Q<Label>("item-uri");
            if (uriLabel != null)
            {
                uriLabel.text = viewModel.Uri ?? string.Empty;
                uriLabel.style.display = string.IsNullOrEmpty(viewModel.Uri) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            var mimeTypeLabel = element.Q<Label>("item-mimetype");
            if (mimeTypeLabel != null)
            {
                mimeTypeLabel.text = string.IsNullOrEmpty(viewModel.MimeType) ? string.Empty : $"MimeType: {viewModel.MimeType}";
                mimeTypeLabel.style.display = string.IsNullOrEmpty(viewModel.MimeType) ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        protected override IEnumerable<ResourceViewModel> FilterByText(IEnumerable<ResourceViewModel> items, string filterText)
        {
            return items.Where(t =>
                t.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                (t.Title?.Contains(filterText, StringComparison.OrdinalIgnoreCase) == true) ||
                (t.Description?.Contains(filterText, StringComparison.OrdinalIgnoreCase) == true) ||
                (t.Uri?.Contains(filterText, StringComparison.OrdinalIgnoreCase) == true));
        }

        public class ResourceViewModel : IMcpItemViewModel
        {
            public string Name { get; set; }
            public string? Title { get; set; }
            public string? Description { get; set; }
            public string? Uri { get; set; }
            public string? MimeType { get; set; }
            public bool IsEnabled { get; set; }
            public PlayerPrefsBool descriptionExpanded;

            public ResourceViewModel(IResourceManager resourceManager, IRunResource resource)
            {
                Name = resource.Name;
                Description = resource.Description;
                Uri = resource.Route;
                MimeType = resource.MimeType;
                IsEnabled = resourceManager?.IsResourceEnabled(resource.Name) == true;
                descriptionExpanded = new PlayerPrefsBool(GetFoldoutKey(resource.Name, "description-foldout"));
            }

            private string GetFoldoutKey(string resourceName, string foldoutName)
            {
                var sanitizedName = resourceName.Replace(" ", "_").Replace(".", "_");
                return $"Feeder_MCP_ResourcesWindow_{sanitizedName}_{foldoutName}_Expanded";
            }
        }
    }
}
