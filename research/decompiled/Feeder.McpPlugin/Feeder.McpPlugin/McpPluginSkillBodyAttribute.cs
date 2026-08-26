using System;

namespace Feeder.McpPlugin;

[Obsolete("Use [AiSkillBody] instead. This alias will be removed in a future major release.")]
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class McpPluginSkillBodyAttribute : AiSkillBodyAttribute
{
	public McpPluginSkillBodyAttribute(string body)
		: base(body)
	{
	}
}
