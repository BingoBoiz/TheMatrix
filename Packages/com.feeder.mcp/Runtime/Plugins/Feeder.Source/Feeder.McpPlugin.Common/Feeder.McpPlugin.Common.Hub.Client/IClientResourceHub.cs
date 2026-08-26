using System.Threading.Tasks;
using Feeder.McpPlugin.Common.Model;

namespace Feeder.McpPlugin.Common.Hub.Client
{
public interface IClientResourceHub
{
	Task<ResponseData<ResponseResourceContent[]>> RunResourceContent(RequestResourceContent request);

	Task<ResponseData<ResponseListResource[]>> RunListResources(RequestListResources request);

	Task<ResponseData<ResponseResourceTemplate[]>> RunResourceTemplates(RequestListResourceTemplates request);
}
}
