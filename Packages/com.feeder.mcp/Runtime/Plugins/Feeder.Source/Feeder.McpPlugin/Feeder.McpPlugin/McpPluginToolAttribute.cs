using System;

namespace Feeder.McpPlugin
{
[Obsolete("Use [AiTool] instead. This alias will be removed in a future major release.")]
[AttributeUsage(AttributeTargets.Method)]
public sealed class McpPluginToolAttribute : AiToolAttribute
{
	public McpPluginToolAttribute(string name, string? title = null)
		: base(name, title)
	{
	}
}
}
