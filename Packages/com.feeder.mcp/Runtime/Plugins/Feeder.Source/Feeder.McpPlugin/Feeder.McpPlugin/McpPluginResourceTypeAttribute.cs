using System;

namespace Feeder.McpPlugin
{
[Obsolete("Use [AiResourceType] instead. This alias will be removed in a future major release.")]
[AttributeUsage(AttributeTargets.Class)]
public sealed class McpPluginResourceTypeAttribute : AiResourceTypeAttribute
{
}
}
