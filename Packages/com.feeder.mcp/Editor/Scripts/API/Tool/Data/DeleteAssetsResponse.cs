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
    public class DeleteAssetsResponse
    {
        [Description("List of paths of deleted assets.")]
        public List<string>? DeletedPaths { get; set; }
        [Description("List of errors encountered during delete operations.")]
        public List<string>? Errors { get; set; }
    }
}
