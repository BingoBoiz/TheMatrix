using System.Threading.Tasks;
using Feeder.McpPlugin.Common.Model;

namespace Feeder.McpPlugin.Common.Hub.Client;

public interface IClientPromptHub
{
	Task<ResponseData<ResponseGetPrompt>> RunGetPrompt(RequestGetPrompt request);

	Task<ResponseData<ResponseListPrompts>> RunListPrompts(RequestListPrompts request);
}
