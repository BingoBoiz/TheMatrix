using System.Collections.Generic;
using System.Threading.Tasks;
using Feeder.McpPlugin.Common.Model;

namespace Feeder.McpPlugin;

public interface IRunResourceList
{
	Task<ResponseListResource[]> Run(params object?[] parameters);

	Task<ResponseListResource[]> Run(IDictionary<string, object?>? namedParameters);
}
