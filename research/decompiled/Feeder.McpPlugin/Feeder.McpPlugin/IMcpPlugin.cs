using System;
using Feeder.McpPlugin.Common;
using Feeder.McpPlugin.Common.Model;
using Microsoft.Extensions.Logging;

namespace Feeder.McpPlugin;

public interface IMcpPlugin : IConnection, IDisposable
{
	ILogger Logger { get; }

	IMcpManager McpManager { get; }

	IMcpManagerHub? McpManagerHub { get; }

	Feeder.McpPlugin.Common.Version Version { get; }

	VersionHandshakeResponse? VersionHandshakeStatus { get; }

	ulong ToolCallsCount => 0uL;

	bool GenerateSkillFiles(string? path = null);

	bool GenerateSkillFilesIfNeeded(string? path = null);

	bool DeleteSkillFiles(string? path = null);
}
