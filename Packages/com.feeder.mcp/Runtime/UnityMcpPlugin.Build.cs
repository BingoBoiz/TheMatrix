#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Feeder.McpPlugin;
using Feeder.ReflectorNet;
using Feeder.ReflectorNet.Utils;
using Feeder.MCP.Runtime.Utils;
using Feeder.MCP.Utils;
using Microsoft.Extensions.Logging;
using R3;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Feeder.MCP
{
    using McpPluginCommon = global::Feeder.McpPlugin.Common;
    using Consts = global::Feeder.McpPlugin.Common.Consts;
    using MicrosoftLogLevel = Microsoft.Extensions.Logging.LogLevel;

    public partial class UnityMcpPlugin
    {
        static readonly ILogger _logger = UnityLoggerFactory.LoggerFactory.CreateLogger<UnityMcpPlugin>();

        protected sealed class McpPluginSlot : IDisposable
        {
            private readonly object _mutex = new();
            private IMcpPlugin? _instance;

            public IMcpPlugin? Instance
            {
                get { lock (_mutex) { return _instance; } }
            }

            public bool HasInstance
            {
                get { lock (_mutex) { return _instance != null; } }
            }

            // Calls factory inside lock — guarantees build-once under concurrent access.
            // Returns the built instance if it was just created, null if already built.
            public IMcpPlugin? BuildOnce(Func<IMcpPlugin> factory)
            {
                lock (_mutex)
                {
                    if (_instance != null) return null;
                    _instance = factory();
                    return _instance;
                }
            }

            // Disposes old instance (if any), sets new one, returns it.
            public IMcpPlugin Set(IMcpPlugin plugin)
            {
                lock (_mutex)
                {
                    _instance?.Dispose();
                    _instance = plugin;
                    return plugin;
                }
            }

            // Atomically returns and clears the instance without disposing it.
            // Used by callers that need to control when/how disposal happens (e.g. background thread).
            public IMcpPlugin? TakeInstance()
            {
                lock (_mutex)
                {
                    var instance = _instance;
                    _instance = null;
                    return instance;
                }
            }

            public void Dispose()
            {
                lock (_mutex)
                {
                    _instance?.Dispose();
                    _instance = null;
                }
            }
        }

        protected virtual McpPluginCommon.Version BuildVersion()
        {
            return new McpPluginCommon.Version
            {
                Api = Consts.ApiVersion,
                Plugin = UnityMcpPlugin.Version,
                Environment = Application.unityVersion
            };
        }

        protected virtual ILoggerProvider? BuildLoggerProvider()
        {
            return new UnityLoggerProvider();
        }

        protected virtual IMcpPlugin BuildMcpPlugin(
            McpPluginCommon.Version version,
            Reflector reflector,
            ILoggerProvider? loggerProvider = null,
            Action<IMcpPluginBuilder>? configure = null)
        {
            _logger.LogTrace("{method} called.", nameof(BuildMcpPlugin));

            var assemblies = AssemblyUtils.AllAssemblies;
            var mcpPluginBuilder = new McpPluginBuilder(version, loggerProvider)
                .SetConfig(unityConnectionConfig ?? throw new InvalidOperationException("UnityConnectionConfig must be set before building the plugin."))
                .AddLogging(loggingBuilder =>
                {
                    loggingBuilder.ClearProviders(); // 👈 Clears the default providers
                    loggingBuilder.SetMinimumLevel(MicrosoftLogLevel.Trace);

                    if (loggerProvider != null)
                        loggingBuilder.AddProvider(loggerProvider);
                })
                .IgnoreAssemblies(
                    "mscorlib",
                    "Mono.Security",
                    "netstandard",
                    "nunit.framework",
                    "System",
                    "UnityEngine",
                    "UnityEditor",
                    "Unity.",
                    "Microsoft",
                    "R3",
                    "McpPlugin",
                    "ReflectorNet",
                    "Feeder.MCP.TestFiles",
                    "Feeder.MCP.Editor.Tests",
                    "Feeder.MCP.Tests")
                .WithToolsFromAssembly(assemblies)
                .WithPromptsFromAssembly(assemblies)
                .WithResourcesFromAssembly(assemblies)
                .WithSkillsFromAssembly(assemblies)
                // Auto-discover IReflectorModule implementors across all loaded assemblies so any
                // assembly (including extensions added later, unknown ahead of time) can contribute
                // ReflectorNet JSON/reflection converters, serialization-blacklist entries, and
                // scan-ignore rules without a hardcoded extension list. Discovery honors the
                // .IgnoreAssemblies(...) prune above (heavy assemblies are never type-enumerated)
                // and runs strictly before the heavy attribute scan inside Build(). The hardcoded
                // core converters in CreateDefaultReflector() remain the Order=0 baseline; module
                // contributions layer on top.
                .WithReflectorModulesFromAssembly(assemblies);

            configure?.Invoke(mcpPluginBuilder);

            var mcpPlugin = mcpPluginBuilder.Build(reflector);

            _pluginConnectionSubscription?.Dispose();
            _pluginConnectionSubscription = mcpPlugin.ConnectionState
                .Subscribe(state => _connectionState.Value = state);

            _logger.LogTrace("{method} completed.", nameof(BuildMcpPlugin));

            return mcpPlugin;
        }

        protected virtual void ApplyConfigToMcpPlugin(IMcpPlugin mcpPlugin)
        {
            _logger.LogTrace("{method} called.", nameof(ApplyConfigToMcpPlugin));

            // Enable/Disable tools based on config
            var toolManager = mcpPlugin.McpManager.ToolManager;
            if (toolManager != null)
            {
                var enabledToolsOverride = unityConnectionConfig.EnabledToolsOverride;
                if (enabledToolsOverride != null)
                {
                    var allTools = toolManager.GetAllTools().ToList();
                    var enabledSet = new HashSet<string>(enabledToolsOverride, StringComparer.OrdinalIgnoreCase);

                    // Validate requested tool IDs against the registered tool list
                    var allToolNames = new HashSet<string>(
                        allTools.Select(t => t.Name!),
                        StringComparer.OrdinalIgnoreCase);
                    foreach (var requestedId in enabledToolsOverride)
                    {
                        if (!allToolNames.Contains(requestedId))
                            _logger.LogError("[MCP] {Key}: tool '{ToolId}' not found. Check the tool ID.",
                                EnvironmentUtils.EnvTools, requestedId);
                    }

                    // Apply: enable only tools in the override list, disable all others
                    foreach (var tool in allTools)
                    {
                        var isEnabled = enabledSet.Contains(tool.Name!);
                        toolManager.SetToolEnabled(tool.Name!, isEnabled);
                        _logger.LogDebug("{method}: Tool '{tool}' enabled: {isEnabled} (env override)",
                            nameof(ApplyConfigToMcpPlugin), tool.Name, isEnabled);
                    }
                }
                else
                {
                    foreach (var tool in toolManager.GetAllTools())
                    {
                        var toolFeature = unityConnectionConfig.Tools.FirstOrDefault(t => t.Name == tool.Name!);
                        var isEnabled = toolFeature?.Enabled ?? tool.Enabled;
                        toolManager.SetToolEnabled(tool.Name!, isEnabled);
                        _logger.LogDebug("{method}: Tool '{tool}' enabled: {isEnabled}",
                            nameof(ApplyConfigToMcpPlugin), tool.Name, isEnabled);
                    }
                }
            }

            _logger.LogTrace("{method} completed.", nameof(ApplyConfigToMcpPlugin));
        }
    }
}
