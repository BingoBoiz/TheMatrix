using System;

namespace Feeder.McpPlugin;

[Obsolete("Use [AiPrompt] instead. This alias will be removed in a future major release.")]
[AttributeUsage(AttributeTargets.Method)]
public sealed class McpPluginPromptAttribute : AiPromptAttribute
{
}
