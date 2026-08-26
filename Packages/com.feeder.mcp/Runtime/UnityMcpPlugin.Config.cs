#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Feeder.McpPlugin;
using Feeder.McpPlugin.Common;
using Feeder.McpPlugin.Common.Utils;
using Feeder.MCP.Runtime.Utils;
using static Feeder.McpPlugin.Common.Consts.MCP.Server;

namespace Feeder.MCP
{
    public partial class UnityMcpPlugin
    {
        protected readonly object configMutex = new();

        protected UnityConnectionConfig unityConnectionConfig = null!; // Set by subclass constructors

        public class UnityConnectionConfig : ConnectionConfig
        {
            public static string DefaultHost => $"http://localhost:{GeneratePortFromDirectory()}";

            public static List<McpFeature> DefaultTools => new();
            public static List<McpFeature> DefaultPrompts => new();
            public static List<McpFeature> DefaultResources => new();

            /// <summary>
            /// Backing field for the local server URL. Serialized as "host" in JSON.
            /// Use <see cref="Host"/> for the active connection URL (routes through Cloud mode).
            /// </summary>
            [JsonPropertyName("host")]
            public string LocalHost { get; set; } = DefaultHost;

            /// <summary>
            /// Backing field for the local auth token. Serialized as "token" in JSON.
            /// Use <see cref="Token"/> for the active token (routes through Cloud mode).
            /// </summary>
            [JsonPropertyName("token")]
            public string? LocalToken { get; set; }

            public static string DefaultCloudServerBaseUrl => DefaultHost;

            public static string CloudServerBaseUrl
            {
                get
                {
                    var args = ArgsUtils.ParseCommandLineArguments();
                    var envValue = args.GetValueOrDefault(EnvironmentUtils.EnvCloudUrl)
                        ?? Environment.GetEnvironmentVariable(EnvironmentUtils.EnvCloudUrl);

                    if (string.IsNullOrWhiteSpace(envValue))
                        return DefaultCloudServerBaseUrl;

                    var normalized = envValue.Trim().Trim('"').TrimEnd('/');

                    if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
                        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    {
                        return DefaultCloudServerBaseUrl;
                    }

                    // Strip trailing "/mcp" so CloudServerUrl doesn't produce "/mcp/mcp"
                    if (normalized.EndsWith("/mcp", StringComparison.OrdinalIgnoreCase))
                        normalized = normalized[..^4];

                    return normalized;
                }
            }

            public static string CloudServerUrl => CloudServerBaseUrl + "/mcp";

            /// <summary>
            /// Returns the active connection host based on <see cref="ConnectionMode"/>.
            /// In Cloud mode, returns <see cref="CloudServerUrl"/>.
            /// In Local mode, returns <see cref="LocalHost"/>.
            /// </summary>
            [JsonIgnore]
            public override string Host
            {
                get => ConnectionMode == ConnectionMode.Cloud ? CloudServerUrl : LocalHost;
                set => LocalHost = value;
            }

            /// <summary>
            /// Gets/sets the active auth token based on <see cref="ConnectionMode"/>.
            /// In Cloud mode, routes to <see cref="CloudToken"/>.
            /// In Local mode, routes to <see cref="LocalToken"/>.
            /// Setter mirrors the getter so env-var / CLI overrides (which write via
            /// the generic Token property) land on the right field regardless of mode.
            /// </summary>
            [JsonIgnore]
            public override string? Token
            {
                get => ConnectionMode == ConnectionMode.Cloud ? CloudToken : LocalToken;
                set
                {
                    if (ConnectionMode == ConnectionMode.Cloud)
                        CloudToken = value;
                    else
                        LocalToken = value;
                }
            }

            public LogLevel LogLevel { get; set; } = LogLevel.Warning;
            public bool KeepServerRunning { get; set; } = false;
            public TransportMethod TransportMethod { get; set; } = TransportMethod.streamableHttp;
            public AuthOption AuthOption { get; set; } = AuthOption.none;
            public ConnectionMode ConnectionMode { get; set; } = ConnectionMode.Custom;
            public string? CloudToken { get; set; }
            public List<McpFeature> Tools { get; set; } = new();
            public List<McpFeature> Prompts { get; set; } = new();
            public List<McpFeature> Resources { get; set; } = new();
            public Dictionary<string, bool> SkillAutoGenerate { get; set; } = new() { ["claude-code"] = true, ["deepseek"] = true };

            /// <summary>
            /// Per-agent opt-in for writing the agent's MCP config file automatically on editor
            /// load (e.g. Claude Code's project-root <c>.mcp.json</c>). Keyed by agentId.
            /// Defaults to enabled for Claude Code and DeepSeek so a freshly installed package
            /// works without opening the connector window.
            /// </summary>
            public Dictionary<string, bool> AgentAutoConfigure { get; set; } = new() { ["claude-code"] = true, ["deepseek"] = true };

            /// <summary>
            /// When non-null, only the tools whose names appear in this list are enabled;
            /// all others are disabled. Set by the <c>UNITY_MCP_TOOLS</c> environment variable
            /// (comma-separated tool IDs). Not persisted to disk.
            /// </summary>
            [JsonIgnore]
            public List<string>? EnabledToolsOverride { get; set; }

            public UnityConnectionConfig()
            {
                SetDefault();
            }

            public UnityConnectionConfig SetDefault()
            {
                Host = DefaultHost;
                var isCi = EnvironmentUtils.IsCi();
                KeepConnected = !isCi;
                KeepServerRunning = !isCi;
                GenerateSkillFiles = false;
                SkillsPath = ".claude/skills"; // default skills location for Claude Code
                SkillAutoGenerate = new() { ["claude-code"] = true, ["deepseek"] = true };
                AgentAutoConfigure = new() { ["claude-code"] = true, ["deepseek"] = true };
                TransportMethod = TransportMethod.streamableHttp;
                AuthOption = AuthOption.none;
                ConnectionMode = ConnectionMode.Custom;
                CloudToken = null;
                LogLevel = LogLevel.Warning;
                TimeoutMs = Consts.Hub.DefaultTimeoutMs;
                Tools = DefaultTools;
                Prompts = DefaultPrompts;
                Resources = DefaultResources;
                Token = GenerateToken();
                return this;
            }

            public class McpFeature
            {
                public string Name { get; set; } = string.Empty;
                public bool Enabled { get; set; } = true;

                public McpFeature() { }
                public McpFeature(string name, bool enabled)
                {
                    Name = name;
                    Enabled = enabled;
                }
            }
        }
    }

    public enum ConnectionMode
    {
        Custom,
        Cloud
    }
}
