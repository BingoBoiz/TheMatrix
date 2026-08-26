using System;

namespace Feeder.McpPlugin;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class RequestIDAttribute : Attribute
{
}
