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
    public class DestroyComponentsResponse
    {
        [Description("List of destroyed components.")]
        public ComponentRefList? DestroyedComponents { get; set; }
    }
}
