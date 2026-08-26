namespace Feeder.McpPlugin;

public interface IReflectorModule
{
	int Order { get; }

	void Configure(IReflectorModuleContext ctx);
}
