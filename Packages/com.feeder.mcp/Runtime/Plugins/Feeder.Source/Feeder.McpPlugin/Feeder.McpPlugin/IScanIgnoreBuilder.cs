namespace Feeder.McpPlugin
{
public interface IScanIgnoreBuilder
{
	IScanIgnoreBuilder IgnoreAssemblies(params string[] assemblyNamePrefixes);

	IScanIgnoreBuilder IgnoreNamespaces(params string[] namespacePrefixes);
}
}
