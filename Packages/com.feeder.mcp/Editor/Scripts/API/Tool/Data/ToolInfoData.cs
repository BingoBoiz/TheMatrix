#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Feeder.McpPlugin;
using Feeder.ReflectorNet.Model;
using AIGD;

namespace AIGD
{
    [Description("MCP tool information.")]
    public class ToolInfoData
    {
        [JsonInclude, JsonPropertyName("name")]
        [Description("Tool name.")]
        public string Name { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName("enabled")]
        [Description("Whether the tool is enabled. A disabled tool is registered but cannot be called until tool-set-enabled-state turns it on.")]
        public bool Enabled { get; set; }

        [JsonInclude, JsonPropertyName("description")]
        [Description("Tool description.")]
        public string? Description { get; set; }

        [JsonInclude, JsonPropertyName("inputs")]
        [Description("Tool input arguments.")]
        public ToolInputData[]? Inputs { get; set; }
    }
}
