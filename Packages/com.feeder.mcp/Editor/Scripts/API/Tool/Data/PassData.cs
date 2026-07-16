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
    public class PassData
    {
        [Description("Index of this pass within the subshader.")]
        public int Index { get; set; }

        [Description("Name of the pass. Null if unnamed.")]
        public string? Name { get; set; }

        [Description("Source code of the pass. Null if unavailable.")]
        public string? SourceCode { get; set; }
    }
}
