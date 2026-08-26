using System.Threading.Tasks;
using Feeder.McpPlugin.Common.Model;

namespace Feeder.McpPlugin.Common.Hub.Server;

public interface IServerPromptHub
{
	Task<ResponseData> NotifyAboutUpdatedPrompts(RequestPromptsUpdated request);
}
