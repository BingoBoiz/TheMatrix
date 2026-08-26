using System.Threading.Tasks;
using Feeder.McpPlugin.Common.Model;

namespace Feeder.McpPlugin.Common.Hub.Server
{
public interface IServerToolHub
{
	Task<ResponseData> NotifyAboutUpdatedTools(RequestToolsUpdated request);

	Task<ResponseData> NotifyToolRequestCompleted(RequestToolCompletedData request);
}
}
