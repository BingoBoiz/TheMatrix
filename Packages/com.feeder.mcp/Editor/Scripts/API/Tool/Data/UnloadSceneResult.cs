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
    public class UnloadSceneResult
    {
        [Description("Name of the unloaded scene.")]
        public string? Name { get; set; }
        [Description("Reference to the unloaded scene asset.")]
        public AssetObjectRef? AssetObjectRef { get; set; } = null!;
    }
}
