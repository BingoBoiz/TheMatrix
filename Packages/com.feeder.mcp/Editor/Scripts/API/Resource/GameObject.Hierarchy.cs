#nullable enable
using System;
using System.Linq;
using Feeder.McpPlugin;
using Feeder.McpPlugin.Common;
using Feeder.McpPlugin.Common.Model;
using Feeder.ReflectorNet;
using Feeder.ReflectorNet.Utils;
using Feeder.MCP.Runtime.Utils;
using Feeder.MCP.Utils;
using Microsoft.Extensions.Logging;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Feeder.MCP.Editor.API
{
    using Consts = Feeder.McpPlugin.Common.Consts;

    [AiResourceType]
    public partial class Resource_GameObject
    {
        [AiResource
        (
            Name = "GameObject from Current Scene by Path",
            Route = "gameobject://currentScene/{path}",
            MimeType = Consts.MimeType.TextJson,
            ListResources = nameof(CurrentSceneAll),
            Description = "Get gameObject's components and the values of each explicit property.",
            Enabled = false
        )]
        public ResponseResourceContent[] CurrentScene(string uri, string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new Exception("Path to the GameObject is empty.");

            return MainThread.Instance.Run(() =>
            {
                var go = GameObjectUtils.FindByPath(path)
                    ?? throw new Exception($"GameObject by path '{path}' not found.");

                var reflector = UnityMcpPluginEditor.Instance.Reflector ?? throw new Exception("Reflector is not available.");

                return ResponseResourceContent.CreateText(
                    uri: uri,
                    mimeType: Consts.MimeType.TextJson,
                    text: reflector.Serialize(
                        obj: go,
                        fallbackType: typeof(GameObject),
                        recursive: false,
                        logger: UnityLoggerFactory.LoggerFactory.CreateLogger<Resource_GameObject>()
                    ).ToJson(reflector) ?? string.Empty
                ).MakeArray();
            });
        }

        public ResponseListResource[] CurrentSceneAll() => MainThread.Instance.Run(() =>
        {
            return EditorSceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => GameObjectUtils.GetAllRecursively(root))
                .Select(kvp => new ResponseListResource(
                    uri: $"gameobject://currentScene/{kvp.Key}",
                    name: kvp.Value.name,
                    enabled: true,
                    mimeType: Consts.MimeType.TextJson))
                .ToArray();
        });
    }
}