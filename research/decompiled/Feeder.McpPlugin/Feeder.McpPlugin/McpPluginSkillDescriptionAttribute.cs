using System;

namespace Feeder.McpPlugin;

[Obsolete("Use [AiSkillDescription] instead. This alias will be removed in a future major release.")]
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class McpPluginSkillDescriptionAttribute : AiSkillDescriptionAttribute
{
	public McpPluginSkillDescriptionAttribute(string description)
		: base(description)
	{
	}
}
