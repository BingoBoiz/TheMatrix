using System;

namespace Feeder.McpPlugin;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public class AiSkillBodyAttribute : Attribute
{
	public string Body { get; }

	public AiSkillBodyAttribute(string body)
	{
		Body = body ?? string.Empty;
	}
}
