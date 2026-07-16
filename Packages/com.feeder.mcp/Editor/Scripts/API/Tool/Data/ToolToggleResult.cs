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
    public class ToolToggleResult
    {
        [Description("Optional operation logs. Only included when 'includeLogs' is true.")]
        public Logs? Logs { get; set; }

        [Description("Result of each tool operation. Key: original input name as provided by the caller (case preserved as-is). Value: true if the enable/disable operation completed successfully, false if the name was unknown, ambiguous, or empty.")]
        public Dictionary<string, bool> Success { get; set; } = new();
    }
}
