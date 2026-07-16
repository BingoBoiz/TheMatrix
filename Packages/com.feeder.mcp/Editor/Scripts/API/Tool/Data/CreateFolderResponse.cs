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
    public class CreateFolderResponse
    {
        [Description("List of GUIDs of created folders.")]
        public List<string>? CreatedFolderGuids { get; set; }
        [Description("List of errors encountered during folder creation.")]
        public List<string>? Errors { get; set; }
    }
}
