using System;
using System.Collections.Generic;
using System.Reflection;
using Feeder.McpPlugin.Skills;
using Feeder.ReflectorNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Feeder.McpPlugin;

public interface IMcpPluginBuilder
{
	IServiceCollection Services { get; }

	IMcpPluginBuilder WithTool(Type classType, MethodInfo methodInfo);

	IMcpPluginBuilder WithTool(AiToolAttribute attribute, Type classType, MethodInfo methodInfo);

	IMcpPluginBuilder WithTool(string name, string? title, Type classType, MethodInfo methodInfo);

	IMcpPluginBuilder AddTool(string name, IRunTool runner);

	IMcpPluginBuilder WithTools<T>();

	IMcpPluginBuilder WithTools(params Type[] targetTypes);

	IMcpPluginBuilder WithTools(IEnumerable<Type> targetTypes);

	IMcpPluginBuilder WithTools(Type classType);

	IMcpPluginBuilder WithToolsFromAssembly(IEnumerable<Assembly> assemblies);

	IMcpPluginBuilder WithToolsFromAssembly(Assembly? assembly = null);

	IMcpPluginBuilder WithPrompt(string name, Type classType, MethodInfo methodInfo);

	IMcpPluginBuilder AddPrompt(string name, IRunPrompt runner);

	IMcpPluginBuilder WithPrompts<T>();

	IMcpPluginBuilder WithPrompts(params Type[] targetTypes);

	IMcpPluginBuilder WithPrompts(IEnumerable<Type> targetTypes);

	IMcpPluginBuilder WithPrompts(Type classType);

	IMcpPluginBuilder WithPromptsFromAssembly(IEnumerable<Assembly> assemblies);

	IMcpPluginBuilder WithPromptsFromAssembly(Assembly? assembly = null);

	IMcpPluginBuilder WithResource(Type classType, MethodInfo getContentMethod);

	IMcpPluginBuilder AddResource(IRunResource resourceParams);

	IMcpPluginBuilder WithResources<T>();

	IMcpPluginBuilder WithResources(params Type[] targetTypes);

	IMcpPluginBuilder WithResources(IEnumerable<Type> targetTypes);

	IMcpPluginBuilder WithResources(Type classType);

	IMcpPluginBuilder WithResourcesFromAssembly(IEnumerable<Assembly> assemblies);

	IMcpPluginBuilder WithResourcesFromAssembly(Assembly? assembly = null);

	IMcpPluginBuilder WithSkills<T>();

	IMcpPluginBuilder WithSkills(params Type[] targetTypes);

	IMcpPluginBuilder WithSkills(IEnumerable<Type> targetTypes);

	IMcpPluginBuilder WithSkills(Type classType);

	IMcpPluginBuilder WithSkillsFromAssembly(IEnumerable<Assembly> assemblies);

	IMcpPluginBuilder WithSkillsFromAssembly(Assembly? assembly = null);

	IMcpPluginBuilder AddLogging(Action<ILoggingBuilder> loggingBuilder);

	IMcpPluginBuilder SetConfig(ConnectionConfig config);

	IMcpPluginBuilder WithConfig(Action<ConnectionConfig> config);

	IMcpPluginBuilder WithConfigFromArgsOrEnv(string[]? args = null);

	IMcpPluginBuilder WithSkillFileGenerator<T>() where T : class, ISkillFileGenerator;

	IMcpPluginBuilder WithSkillFileGenerator(ISkillFileGenerator instance);

	IMcpPluginBuilder WithDynamicToolFactory();

	IMcpPluginBuilder WithReflectorModulesFromAssembly(IEnumerable<Assembly> assemblies);

	IMcpPlugin Build(Reflector reflector);

	IMcpPluginBuilder IgnoreAssembly(Assembly assembly);

	IMcpPluginBuilder IgnoreAssembly(string assemblyName);

	IMcpPluginBuilder IgnoreAssemblies(IEnumerable<Assembly> assemblies);

	IMcpPluginBuilder IgnoreAssemblies(params string[] assemblyNames);

	IMcpPluginBuilder IgnoreNamespace(string namespaceName);

	IMcpPluginBuilder IgnoreNamespaces(params string[] namespaceNames);

	IMcpPluginBuilder RemoveIgnoredAssembly(Assembly assembly);

	IMcpPluginBuilder RemoveIgnoredAssembly(string assemblyName);

	IMcpPluginBuilder RemoveIgnoredAssemblies(IEnumerable<Assembly> assemblies);

	IMcpPluginBuilder RemoveIgnoredAssemblies(params string[] assemblyNames);

	IMcpPluginBuilder RemoveIgnoredNamespace(string namespaceName);

	IMcpPluginBuilder RemoveIgnoredNamespaces(params string[] namespaceNames);

	int GetIgnoredAssembliesCount();

	int GetIgnoredTypesCount();

	IMcpPluginBuilder ClearIgnoredAssemblies();

	IMcpPluginBuilder ClearIgnoredNamespaces();

	IMcpPluginBuilder ClearAllIgnored();
}
