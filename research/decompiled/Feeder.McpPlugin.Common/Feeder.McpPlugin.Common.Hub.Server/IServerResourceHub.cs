using System.Threading.Tasks;
using Feeder.McpPlugin.Common.Model;

namespace Feeder.McpPlugin.Common.Hub.Server;

public interface IServerResourceHub
{
	Task<ResponseData> NotifyAboutUpdatedResources(RequestResourcesUpdated request);
}
