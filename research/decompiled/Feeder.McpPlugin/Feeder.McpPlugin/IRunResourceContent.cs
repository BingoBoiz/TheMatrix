using System.Collections.Generic;
using System.Threading.Tasks;
using Feeder.McpPlugin.Common.Model;

namespace Feeder.McpPlugin;

public interface IRunResourceContent
{
	Task<ResponseResourceContent[]> Run(params object?[] parameters);

	Task<ResponseResourceContent[]> Run(IDictionary<string, object?>? namedParameters);
}
