using System.Collections.Generic;

namespace Feeder.McpPlugin.AgentConfig
{
public sealed class ConfigurationSection
{
	public string Heading { get; }

	public bool ExpandedFirst { get; }

	public IReadOnlyList<ConfigurationItem> Items { get; }

	public ConfigurationSection(string heading, bool expandedFirst, IReadOnlyList<ConfigurationItem> items)
	{
		Heading = heading;
		ExpandedFirst = expandedFirst;
		Items = items;
	}
}
}
