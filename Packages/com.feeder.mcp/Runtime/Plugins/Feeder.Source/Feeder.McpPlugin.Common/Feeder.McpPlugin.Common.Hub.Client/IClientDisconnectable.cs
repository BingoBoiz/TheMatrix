using System.Threading.Tasks;

namespace Feeder.McpPlugin.Common.Hub.Client
{
public interface IClientDisconnectable
{
	Task ForceDisconnect(string? reason = null);
}
}
