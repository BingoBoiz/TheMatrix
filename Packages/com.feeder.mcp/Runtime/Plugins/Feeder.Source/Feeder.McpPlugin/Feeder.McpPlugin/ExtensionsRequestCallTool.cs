using Feeder.McpPlugin.Common.Model;

namespace Feeder.McpPlugin
{
public static class ExtensionsRequestCallTool
{
	public static RequestCallTool SetName(this RequestCallTool data, string name)
	{
		data.Name = name;
		return data;
	}
}
}
