using System;

namespace Feeder.McpPlugin
{
[AttributeUsage(AttributeTargets.Class)]
public class AiToolTypeAttribute : Attribute
{
	public string? Path { get; set; }
}
}
