using System;

namespace Feeder.McpPlugin;

[AttributeUsage(AttributeTargets.Class)]
public class AiResourceTypeAttribute : Attribute
{
	public string? Path { get; set; }
}
