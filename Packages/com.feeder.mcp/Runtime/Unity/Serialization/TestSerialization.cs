#nullable enable
using System;
using Feeder.McpPlugin.Common.Utils;
using Feeder.ReflectorNet;
using Feeder.MCP.Utils;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace Feeder.MCP.Runtime.Utils
{
    public class TestSerialization : MonoBehaviour
    {
        [SerializeField] UnityEngine.Object? target;
        [SerializeField] bool recursive = false;

        [Header("Automatic trigger")]
        [SerializeField] bool onAwake = false;
        [SerializeField] bool onEnable = false;
        [SerializeField] bool onStart = false;

        private void Awake()
        {
            if (onAwake)
            {
                SerializeTarget();
            }
        }
        private void OnEnable()
        {
            if (onEnable)
            {
                SerializeTarget();
            }
        }
        private void Start()
        {
            if (onStart)
            {
                SerializeTarget();
            }
        }

        public void SerializeTarget()
        {
            var logger = UnityLoggerFactory.LoggerFactory.CreateLogger(nameof(TestSerialization));
            if (!UnityMcpPluginRuntime.HasInstance || UnityMcpPluginRuntime.Instance.McpPluginInstance == null)
                throw new InvalidOperationException("No active UnityMcpPluginRuntime instance. Call UnityMcpPluginRuntime.Initialize().Build() first.");
            var reflector = UnityMcpPluginRuntime.Instance.McpPluginInstance.McpManager.Reflector
                ?? throw new InvalidOperationException("Reflector is null");

            logger.LogInformation($"Serializing target '{target?.name}' of type '{target?.GetType().GetTypeId()}' with recursive={recursive}");

            var serialized = reflector.Serialize(
                obj: target,
                fallbackType: null,
                name: target?.name,
                recursive: recursive,
                context: null,
                logger: logger);

            logger.LogInformation(serialized.ToPrettyJson());
        }
    }
}
