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
    public class MoveAssetsResponse
    {
        [Description("List of destination paths of successfully moved assets.")]
        public List<string>? MovedPaths { get; set; }
        [Description("List of errors encountered during move operations.")]
        public List<string>? Errors { get; set; }
    }
}
