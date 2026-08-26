using System;

namespace Feeder.McpPlugin
{
[AttributeUsage(AttributeTargets.Class)]
public class AiPromptTypeAttribute : Attribute
{
	public string? Path { get; set; }
}
}
