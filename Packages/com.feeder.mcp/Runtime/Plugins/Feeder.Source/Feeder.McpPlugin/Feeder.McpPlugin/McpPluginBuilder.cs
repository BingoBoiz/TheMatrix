using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Feeder.McpPlugin.Common;
using Feeder.McpPlugin.Common.Hub.Client;
using Feeder.McpPlugin.Common.Model;
using Feeder.McpPlugin.Skills;
using Feeder.ReflectorNet;
using Feeder.ReflectorNet.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Feeder.McpPlugin
{
public class McpPluginBuilder : IMcpPluginBuilder
{
	protected sealed class ReflectorModuleContext : IReflectorModuleContext, IScanIgnoreBuilder
	{
		private readonly McpPluginBuilderIgnoreConfig _ignore;

		private readonly IReadOnlyCollection<Assembly> _protectedAssemblies;

		private readonly IReadOnlyCollection<string> _moduleNamespaces;

		private readonly ILogger? _hostLogger;

		public Reflector Reflector { get; }

		public Assembly OwningAssembly { get; }

		public ILogger Logger { get; }

		public IScanIgnoreBuilder Scan => this;

		public ReflectorModuleContext(Reflector reflector, Assembly owningAssembly, ILogger logger, McpPluginBuilderIgnoreConfig ignore, IReadOnlyCollection<Assembly> protectedAssemblies, IReadOnlyCollection<string> moduleNamespaces, ILogger? hostLogger)
		{
			Reflector = reflector ?? throw new ArgumentNullException("reflector");
			OwningAssembly = owningAssembly ?? throw new ArgumentNullException("owningAssembly");
			Logger = logger ?? throw new ArgumentNullException("logger");
			_ignore = ignore ?? throw new ArgumentNullException("ignore");
			_protectedAssemblies = protectedAssemblies ?? throw new ArgumentNullException("protectedAssemblies");
			_moduleNamespaces = moduleNamespaces ?? throw new ArgumentNullException("moduleNamespaces");
			_hostLogger = hostLogger;
		}

		public IScanIgnoreBuilder IgnoreAssemblies(params string[] assemblyNamePrefixes)
		{
			if (assemblyNamePrefixes == null)
			{
				return this;
			}
			foreach (string prefix in assemblyNamePrefixes)
			{
				if (!string.IsNullOrEmpty(prefix))
				{
					Assembly assembly = _protectedAssemblies.FirstOrDefault(delegate(Assembly a)
					{
						string name = a.GetName().Name;
						return !string.IsNullOrEmpty(name) && name.StartsWith(prefix, StringComparison.Ordinal);
					});
					if (assembly != null)
					{
						_hostLogger?.LogWarning("Reflector module from '{Owner}' tried to ignore assembly prefix '{Prefix}', which would prune '{Protected}' that hosts a discovered module or registered tools. Ignoring this entry.", OwningAssembly.GetName().Name, prefix, assembly.GetName().Name);
					}
					else
					{
						_ignore.IgnoredAssemblyNames.Add(prefix);
					}
				}
			}
			_ignore.InvalidateCaches();
			return this;
		}

		public IScanIgnoreBuilder IgnoreNamespaces(params string[] namespacePrefixes)
		{
			if (namespacePrefixes == null)
			{
				return this;
			}
			foreach (string prefix in namespacePrefixes)
			{
				if (!string.IsNullOrEmpty(prefix))
				{
					string text = _moduleNamespaces.FirstOrDefault((string ns) => ns.StartsWith(prefix, StringComparison.Ordinal));
					if (text != null)
					{
						_hostLogger?.LogWarning("Reflector module from '{Owner}' tried to ignore namespace prefix '{Prefix}', which would prune discovered-module namespace '{Protected}'. Ignoring this entry.", OwningAssembly.GetName().Name, prefix, text);
					}
					else
					{
						_ignore.IgnoredNamespaces.Add(prefix);
					}
				}
			}
			_ignore.InvalidateCaches();
			return this;
		}
	}

	private const BindingFlags MethodBindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

	private const BindingFlags FieldBindingFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

	protected readonly ILogger? _logger;

	protected readonly ILoggerProvider? _loggerProvider;

	protected readonly IServiceCollection _services;

	protected readonly List<ToolMethodData> _toolMethods = new List<ToolMethodData>();

	protected readonly Dictionary<string, IRunTool> _toolRunners = new Dictionary<string, IRunTool>();

	protected readonly List<PromptMethodData> _promptMethods = new List<PromptMethodData>();

	protected readonly Dictionary<string, IRunPrompt> _promptRunners = new Dictionary<string, IRunPrompt>();

	protected readonly List<ResourceMethodData> _resourceMethods = new List<ResourceMethodData>();

	protected readonly Dictionary<string, IRunResource> _resourceRunners = new Dictionary<string, IRunResource>();

	protected readonly List<SkillMemberData> _skillFields = new List<SkillMemberData>();

	protected readonly McpPluginBuilderIgnoreConfig _ignoreConfig = new McpPluginBuilderIgnoreConfig();

	protected ConnectionConfig? _externalConfig;

	protected readonly List<Assembly> _toolAssemblies = new List<Assembly>();

	protected readonly List<Assembly> _promptAssemblies = new List<Assembly>();

	protected readonly List<Assembly> _resourceAssemblies = new List<Assembly>();

	protected readonly List<Assembly> _skillAssemblies = new List<Assembly>();

	protected readonly List<Type> _toolTypes = new List<Type>();

	protected readonly List<Type> _promptTypes = new List<Type>();

	protected readonly List<Type> _resourceTypes = new List<Type>();

	protected readonly List<Type> _skillTypes = new List<Type>();

	protected bool isBuilt;

	protected bool _skillFileGeneratorSet;

	protected readonly List<Assembly> _reflectorModuleAssemblies = new List<Assembly>();

	public IServiceCollection Services => _services;

	public ServiceProvider? ServiceProvider { get; private set; }

	private void ProcessAllAssemblies()
	{
		HashSet<Assembly> hashSet = new HashSet<Assembly>();
		foreach (Assembly toolAssembly in _toolAssemblies)
		{
			hashSet.Add(toolAssembly);
		}
		foreach (Assembly promptAssembly in _promptAssemblies)
		{
			hashSet.Add(promptAssembly);
		}
		foreach (Assembly resourceAssembly in _resourceAssemblies)
		{
			hashSet.Add(resourceAssembly);
		}
		foreach (Assembly skillAssembly in _skillAssemblies)
		{
			hashSet.Add(skillAssembly);
		}
		HashSet<Assembly> hashSet2 = new HashSet<Assembly>(_toolAssemblies);
		HashSet<Assembly> hashSet3 = new HashSet<Assembly>(_promptAssemblies);
		HashSet<Assembly> hashSet4 = new HashSet<Assembly>(_resourceAssemblies);
		HashSet<Assembly> hashSet5 = new HashSet<Assembly>(_skillAssemblies);
		ProcessExplicitTypes();
		foreach (Assembly item in hashSet)
		{
			if (_ignoreConfig.IsIgnored(item))
			{
				continue;
			}
			bool flag = hashSet2.Contains(item);
			bool flag2 = hashSet3.Contains(item);
			bool flag3 = hashSet4.Contains(item);
			bool flag4 = hashSet5.Contains(item);
			Type[] assemblyTypes = AssemblyUtils.GetAssemblyTypes(item);
			foreach (Type type in assemblyTypes)
			{
				if (_ignoreConfig.IsIgnored(type))
				{
					continue;
				}
				bool flag5 = flag && Attribute.IsDefined(type, typeof(AiToolTypeAttribute));
				bool flag6 = flag2 && Attribute.IsDefined(type, typeof(AiPromptTypeAttribute));
				bool flag7 = flag3 && Attribute.IsDefined(type, typeof(AiResourceTypeAttribute));
				bool flag8 = flag4 && Attribute.IsDefined(type, typeof(AiSkillTypeAttribute));
				if (flag5 || flag6 || flag7 || flag8)
				{
					ProcessTypeMethods(type, flag5, flag6, flag7);
					if (flag8)
					{
						ProcessTypeMembers(type);
					}
				}
			}
		}
	}

	private void ProcessExplicitTypes()
	{
		HashSet<Type> hashSet = new HashSet<Type>(_promptTypes);
		HashSet<Type> hashSet2 = new HashSet<Type>(_resourceTypes);
		HashSet<Type> hashSet3 = new HashSet<Type>(_skillTypes);
		HashSet<Type> hashSet4 = new HashSet<Type>();
		HashSet<Type> hashSet5 = new HashSet<Type>();
		foreach (Type toolType in _toolTypes)
		{
			if (!_ignoreConfig.IsIgnored(toolType) && hashSet4.Add(toolType))
			{
				bool processPrompt = hashSet.Contains(toolType);
				bool processResource = hashSet2.Contains(toolType);
				ProcessTypeMethods(toolType, processTool: true, processPrompt, processResource);
				if (hashSet3.Contains(toolType) && hashSet5.Add(toolType))
				{
					ProcessTypeMembers(toolType);
				}
			}
		}
		foreach (Type promptType in _promptTypes)
		{
			if (!_ignoreConfig.IsIgnored(promptType) && !hashSet4.Contains(promptType))
			{
				hashSet4.Add(promptType);
				bool processResource2 = hashSet2.Contains(promptType);
				ProcessTypeMethods(promptType, processTool: false, processPrompt: true, processResource2);
				if (hashSet3.Contains(promptType) && hashSet5.Add(promptType))
				{
					ProcessTypeMembers(promptType);
				}
			}
		}
		foreach (Type resourceType in _resourceTypes)
		{
			if (!_ignoreConfig.IsIgnored(resourceType) && !hashSet4.Contains(resourceType))
			{
				hashSet4.Add(resourceType);
				ProcessTypeMethods(resourceType, processTool: false, processPrompt: false, processResource: true);
				if (hashSet3.Contains(resourceType) && hashSet5.Add(resourceType))
				{
					ProcessTypeMembers(resourceType);
				}
			}
		}
		foreach (Type skillType in _skillTypes)
		{
			if (!_ignoreConfig.IsIgnored(skillType) && hashSet5.Add(skillType))
			{
				ProcessTypeMembers(skillType);
			}
		}
	}

	private void ProcessTypeMethods(Type type, bool processTool, bool processPrompt, bool processResource)
	{
		MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (MethodInfo methodInfo in methods)
		{
			if (processTool)
			{
				AiToolAttribute aiToolAttribute = methodInfo.GetCustomAttributes(typeof(AiToolAttribute), inherit: false).Cast<AiToolAttribute>().FirstOrDefault();
				if (aiToolAttribute != null)
				{
					if (string.IsNullOrEmpty(aiToolAttribute.Name))
					{
						throw new ArgumentException("Tool name cannot be null or empty. Type: " + type.Name + ", Method: " + methodInfo.Name);
					}
					WithTool(aiToolAttribute, type, methodInfo);
				}
			}
			if (processPrompt)
			{
				AiPromptAttribute aiPromptAttribute = methodInfo.GetCustomAttributes(typeof(AiPromptAttribute), inherit: false).Cast<AiPromptAttribute>().FirstOrDefault();
				if (aiPromptAttribute != null)
				{
					if (string.IsNullOrEmpty(aiPromptAttribute.Name))
					{
						throw new ArgumentException("Prompt name cannot be null or empty. Type: " + type.Name + ", Method: " + methodInfo.Name);
					}
					WithPrompt(aiPromptAttribute.Name, type, methodInfo);
				}
			}
			if (processResource && methodInfo.GetCustomAttributes(typeof(AiResourceAttribute), inherit: false).Cast<AiResourceAttribute>().FirstOrDefault() != null)
			{
				WithResource(type, methodInfo);
			}
		}
	}

	private void ProcessTypeMembers(Type type)
	{
		FieldInfo[] fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (FieldInfo fieldInfo in fields)
		{
			AiSkillAttribute aiSkillAttribute = fieldInfo.GetCustomAttributes(typeof(AiSkillAttribute), inherit: true).Cast<AiSkillAttribute>().FirstOrDefault();
			if (aiSkillAttribute != null)
			{
				if (!fieldInfo.IsLiteral || fieldInfo.FieldType != typeof(string))
				{
					throw new ArgumentException("Field '" + fieldInfo.Name + "' in type '" + type.Name + "' has [AiSkill] but is not a const string. Only const string fields and static string properties are supported.");
				}
				if (string.IsNullOrEmpty(aiSkillAttribute.Name))
				{
					throw new ArgumentException("Skill name cannot be null or empty. Type: " + type.Name + ", Field: " + fieldInfo.Name);
				}
				object rawConstantValue = fieldInfo.GetRawConstantValue();
				if (rawConstantValue == null)
				{
					throw new ArgumentException("Skill field '" + fieldInfo.Name + "' in type '" + type.Name + "' has a null constant value. Only non-null const string values are supported.");
				}
				string content = (string)rawConstantValue;
				_skillFields.Add(new SkillMemberData(type, fieldInfo, aiSkillAttribute, content));
			}
		}
		PropertyInfo[] properties = type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (PropertyInfo propertyInfo in properties)
		{
			AiSkillAttribute aiSkillAttribute2 = propertyInfo.GetCustomAttributes(typeof(AiSkillAttribute), inherit: true).Cast<AiSkillAttribute>().FirstOrDefault();
			if (aiSkillAttribute2 != null)
			{
				if (propertyInfo.PropertyType != typeof(string))
				{
					throw new ArgumentException("Property '" + propertyInfo.Name + "' in type '" + type.Name + "' has [AiSkill] but is not a string property. Only const string fields and static string properties are supported.");
				}
				MethodInfo getMethod = propertyInfo.GetGetMethod(nonPublic: true);
				if (getMethod == null || !getMethod.IsStatic)
				{
					throw new ArgumentException("Property '" + propertyInfo.Name + "' in type '" + type.Name + "' has [AiSkill] but is not a static property with a getter. Only const string fields and static string properties are supported.");
				}
				if (string.IsNullOrEmpty(aiSkillAttribute2.Name))
				{
					throw new ArgumentException("Skill name cannot be null or empty. Type: " + type.Name + ", Property: " + propertyInfo.Name);
				}
				string text = (string)propertyInfo.GetValue(null);
				if (text == null)
				{
					throw new ArgumentException("Skill property '" + propertyInfo.Name + "' in type '" + type.Name + "' returned null. Only non-null string values are supported.");
				}
				_skillFields.Add(new SkillMemberData(type, propertyInfo, aiSkillAttribute2, text));
			}
		}
	}

	public McpPluginBuilder(Feeder.McpPlugin.Common.Version version, ILoggerProvider? loggerProvider = null, IServiceCollection? services = null)
	{
		_loggerProvider = loggerProvider;
		_logger = loggerProvider?.CreateLogger("McpPluginBuilder");
		_services = services ?? new ServiceCollection();
		if (_loggerProvider != null)
		{
			_services.AddLogging(delegate(ILoggingBuilder builder)
			{
				builder.AddProvider(_loggerProvider);
			});
		}
		else
		{
			_services.AddLogging();
		}
		_services.AddSingleton(version);
		_services.AddSingleton<IConnectionManager, ConnectionManager>();
		_services.AddSingleton<IHubConnectionProvider, HubConnectionProvider>();
		_services.AddSingleton<IToolManager, McpToolManager>();
		_services.AddSingleton<IPromptManager, McpPromptManager>();
		_services.AddSingleton<IResourceManager, McpResourceManager>();
		_services.AddSingleton<McpSystemToolManager>();
		_services.AddSingleton((Func<IServiceProvider, ISystemToolManager>)((IServiceProvider sp) => sp.GetRequiredService<McpSystemToolManager>()));
		_services.AddSingleton<IMcpPlugin, McpPlugin>();
		_services.AddSingleton<IMcpManagerHub, McpManagerClientHub>();
		_services.AddSingleton<McpManager>();
		_services.AddSingleton((Func<IServiceProvider, IMcpManager>)((IServiceProvider sp) => sp.GetRequiredService<McpManager>()));
		_services.AddSingleton((Func<IServiceProvider, IClientMcpManager>)((IServiceProvider sp) => sp.GetRequiredService<McpManager>()));
		_services.AddSingleton<ISkillFileGenerator, SkillFileGenerator>();
	}

	public virtual IMcpPluginBuilder WithTool(Type classType, MethodInfo methodInfo)
	{
		ThrowIfBuilt();
		AiToolAttribute attribute = methodInfo.GetCustomAttributes(typeof(AiToolAttribute), inherit: false).Cast<AiToolAttribute>().FirstOrDefault();
		return WithTool(attribute, classType, methodInfo);
	}

	public virtual IMcpPluginBuilder WithTool(string name, string? title, Type classType, MethodInfo methodInfo)
	{
		ThrowIfBuilt();
		AiToolAttribute attribute = new AiToolAttribute(name, title);
		return WithTool(attribute, classType, methodInfo);
	}

	public virtual IMcpPluginBuilder WithTool(AiToolAttribute attribute, Type classType, MethodInfo methodInfo)
	{
		ThrowIfBuilt();
		if (attribute == null)
		{
			_logger?.LogWarning("Method " + classType.FullName + methodInfo.Name + " does not have a 'AiToolAttribute'.");
			return this;
		}
		if (string.IsNullOrEmpty(attribute.Name))
		{
			throw new ArgumentException("Tool name cannot be null or empty. Type: " + classType.Name + ", Method: " + methodInfo.Name);
		}
		_toolMethods.Add(new ToolMethodData(classType, methodInfo, attribute));
		return this;
	}

	public virtual IMcpPluginBuilder AddTool(string name, IRunTool runner)
	{
		ThrowIfBuilt();
		if (_toolRunners.ContainsKey(name))
		{
			throw new ArgumentException("Tool with name '" + name + "' already exists.");
		}
		_toolRunners.Add(name, runner);
		return this;
	}

	public virtual IMcpPluginBuilder WithPrompt(string name, Type classType, MethodInfo methodInfo)
	{
		ThrowIfBuilt();
		AiPromptAttribute aiPromptAttribute = methodInfo.GetCustomAttributes(typeof(AiPromptAttribute), inherit: false).Cast<AiPromptAttribute>().FirstOrDefault();
		if (aiPromptAttribute == null)
		{
			_logger?.LogWarning("Method " + classType.FullName + methodInfo.Name + " does not have a 'AiPromptAttribute'.");
			return this;
		}
		if (string.IsNullOrEmpty(aiPromptAttribute.Name))
		{
			throw new ArgumentException("Prompt name cannot be null or empty. Type: " + classType.Name + ", Method: " + methodInfo.Name);
		}
		_promptMethods.Add(new PromptMethodData(classType, methodInfo, aiPromptAttribute));
		return this;
	}

	public virtual IMcpPluginBuilder AddPrompt(string name, IRunPrompt runner)
	{
		ThrowIfBuilt();
		if (_promptRunners.ContainsKey(name))
		{
			throw new ArgumentException("Prompt with name '" + name + "' already exists.");
		}
		_promptRunners.Add(name, runner);
		return this;
	}

	public virtual IMcpPluginBuilder WithResource(Type classType, MethodInfo getContentMethod)
	{
		ThrowIfBuilt();
		AiResourceAttribute aiResourceAttribute = getContentMethod.GetCustomAttributes(typeof(AiResourceAttribute), inherit: false).Cast<AiResourceAttribute>().FirstOrDefault();
		if (aiResourceAttribute == null)
		{
			_logger?.LogWarning("Method " + classType.FullName + getContentMethod.Name + " does not have a 'AiResourceAttribute'.");
			return this;
		}
		string text = aiResourceAttribute.ListResources ?? throw new InvalidOperationException("Method " + getContentMethod.Name + " does not have a 'ListResources'.");
		MethodInfo method = classType.GetMethod(text);
		if (method == null)
		{
			throw new InvalidOperationException("Method " + classType.FullName + text + " not found in type " + classType.Name + ".");
		}
		if (!getContentMethod.ReturnType.IsArray || !typeof(ResponseResourceContent).IsAssignableFrom(getContentMethod.ReturnType.GetElementType()))
		{
			throw new InvalidOperationException("Method " + classType.FullName + getContentMethod.Name + " must return ResponseResourceContent array.");
		}
		if (!method.ReturnType.IsArray || !typeof(ResponseListResource).IsAssignableFrom(method.ReturnType.GetElementType()))
		{
			throw new InvalidOperationException("Method " + classType.FullName + method.Name + " must return ResponseListResource array.");
		}
		List<ResourceMethodData> resourceMethods = _resourceMethods;
		AiResourceAttribute attribute = aiResourceAttribute;
		resourceMethods.Add(new ResourceMethodData(classType, getContentMethod, method, attribute));
		return this;
	}

	public virtual IMcpPluginBuilder AddResource(IRunResource resourceParams)
	{
		ThrowIfBuilt();
		if (_resourceRunners == null)
		{
			throw new ArgumentNullException("_resourceRunners");
		}
		if (resourceParams == null)
		{
			throw new ArgumentNullException("resourceParams");
		}
		if (_resourceRunners.ContainsKey(resourceParams.Route))
		{
			throw new ArgumentException("Resource with routing '" + resourceParams.Route + "' already exists.");
		}
		_resourceRunners.Add(resourceParams.Route, resourceParams);
		return this;
	}

	public virtual IMcpPluginBuilder AddLogging(Action<ILoggingBuilder> loggingBuilder)
	{
		ThrowIfBuilt();
		_services.AddLogging(loggingBuilder);
		return this;
	}

	public virtual IMcpPluginBuilder SetConfig(ConnectionConfig config)
	{
		ThrowIfBuilt();
		_externalConfig = config ?? throw new ArgumentNullException("config");
		return this;
	}

	public virtual IMcpPluginBuilder WithConfig(Action<ConnectionConfig> config)
	{
		ThrowIfBuilt();
		if (_externalConfig != null)
		{
			config(_externalConfig);
		}
		else
		{
			_services.Configure(config);
		}
		return this;
	}

	public virtual IMcpPluginBuilder WithSkillFileGenerator<T>() where T : class, ISkillFileGenerator
	{
		ThrowIfBuilt();
		ThrowIfSkillFileGeneratorSet();
		_skillFileGeneratorSet = true;
		_services.AddSingleton<ISkillFileGenerator, T>();
		return this;
	}

	public virtual IMcpPluginBuilder WithSkillFileGenerator(ISkillFileGenerator instance)
	{
		ThrowIfBuilt();
		ThrowIfSkillFileGeneratorSet();
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		_skillFileGeneratorSet = true;
		_services.AddSingleton(instance);
		return this;
	}

	public virtual IMcpPluginBuilder WithConfigFromArgsOrEnv(string[]? args = null)
	{
		return WithConfig(delegate(ConnectionConfig config)
		{
			config.Host = ConnectionConfig.GetEndpointFromArgsOrEnv(args);
			config.Token = ConnectionConfig.GetTokenFromArgsOrEnv(args);
			config.TimeoutMs = ConnectionConfig.GetTimeoutFromArgsOrEnv(args);
		});
	}

	public virtual IMcpPlugin Build(Reflector reflector)
	{
		ThrowIfBuilt();
		if (reflector == null)
		{
			throw new ArgumentNullException("reflector");
		}
		BootstrapReflectorModules(reflector);
		ProcessAllAssemblies();
		_services.AddSingleton(reflector);
		List<ToolMethodData> methods = _toolMethods.Where((ToolMethodData m) => m.Attribute.ToolType == McpToolType.Standard).ToList();
		List<ToolMethodData> methods2 = _toolMethods.Where((ToolMethodData m) => m.Attribute.ToolType == McpToolType.System).ToList();
		Dictionary<string, IRunTool> runners = _toolRunners.Where<KeyValuePair<string, IRunTool>>((KeyValuePair<string, IRunTool> r) => r.Value.ToolType == McpToolType.Standard).ToDictionary((KeyValuePair<string, IRunTool> r) => r.Key, (KeyValuePair<string, IRunTool> r) => r.Value);
		Dictionary<string, IRunTool> runners2 = _toolRunners.Where<KeyValuePair<string, IRunTool>>((KeyValuePair<string, IRunTool> r) => r.Value.ToolType == McpToolType.System).ToDictionary((KeyValuePair<string, IRunTool> r) => r.Key, (KeyValuePair<string, IRunTool> r) => r.Value);
		_services.AddSingleton(new ToolRunnerCollection(reflector, _loggerProvider?.CreateLogger("ToolRunnerCollection")).Add(methods).Add(runners));
		_services.AddSingleton(new SystemToolRunnerCollection(reflector, _loggerProvider?.CreateLogger("SystemToolRunnerCollection")).Add(methods2).Add(runners2));
		_services.AddSingleton(new PromptRunnerCollection(reflector, _loggerProvider?.CreateLogger("PromptRunnerCollection")).Add(_promptMethods).Add(_promptRunners));
		_services.AddSingleton(new ResourceRunnerCollection(reflector, _loggerProvider?.CreateLogger("ResourceRunnerCollection")).Add(_resourceMethods).Add(_resourceRunners));
		_services.AddSingleton(new SkillContentCollection(_loggerProvider?.CreateLogger("SkillContentCollection")).Add(_skillFields));
		if (_externalConfig != null)
		{
			_services.AddSingleton((IOptions<ConnectionConfig>)new OptionsWrapper<ConnectionConfig>(_externalConfig));
		}
		ServiceProvider = _services.BuildServiceProvider();
		isBuilt = true;
		return ServiceProvider.GetRequiredService<IMcpPlugin>();
	}

	protected virtual void ThrowIfBuilt()
	{
		if (isBuilt)
		{
			throw new InvalidOperationException("The builder has already been built.");
		}
	}

	protected virtual void ThrowIfSkillFileGeneratorSet()
	{
		if (_skillFileGeneratorSet)
		{
			throw new InvalidOperationException("ISkillFileGenerator has already been set. Only one ISkillFileGenerator can be registered.");
		}
	}

	public virtual IMcpPluginBuilder WithDynamicToolFactory()
	{
		ThrowIfBuilt();
		_services.AddSingleton<IDynamicToolFactory, ProxyToolFactory>();
		return this;
	}

	public virtual IMcpPluginBuilder IgnoreAssembly(Assembly assembly)
	{
		ThrowIfBuilt();
		_ignoreConfig.IgnoredAssemblies.Add(assembly);
		_ignoreConfig.InvalidateCaches();
		return this;
	}

	public virtual IMcpPluginBuilder IgnoreAssembly(string assemblyName)
	{
		ThrowIfBuilt();
		_ignoreConfig.IgnoredAssemblyNames.Add(assemblyName);
		_ignoreConfig.InvalidateCaches();
		return this;
	}

	public virtual IMcpPluginBuilder IgnoreAssemblies(IEnumerable<Assembly> assemblies)
	{
		ThrowIfBuilt();
		foreach (Assembly assembly in assemblies)
		{
			if (!(assembly == null))
			{
				_ignoreConfig.IgnoredAssemblies.Add(assembly);
			}
		}
		_ignoreConfig.InvalidateCaches();
		return this;
	}

	public virtual IMcpPluginBuilder IgnoreAssemblies(params string[] assemblyNames)
	{
		ThrowIfBuilt();
		foreach (string text in assemblyNames)
		{
			if (string.IsNullOrEmpty(text))
			{
				throw new ArgumentException("Assembly name to ignore cannot be null or empty.");
			}
			_ignoreConfig.IgnoredAssemblyNames.Add(text);
		}
		_ignoreConfig.InvalidateCaches();
		return this;
	}

	public virtual IMcpPluginBuilder IgnoreNamespace(string namespaceName)
	{
		ThrowIfBuilt();
		_ignoreConfig.IgnoredNamespaces.Add(namespaceName);
		_ignoreConfig.InvalidateCaches();
		return this;
	}

	public virtual IMcpPluginBuilder IgnoreNamespaces(params string[] namespaceNames)
	{
		ThrowIfBuilt();
		foreach (string item in namespaceNames)
		{
			_ignoreConfig.IgnoredNamespaces.Add(item);
		}
		_ignoreConfig.InvalidateCaches();
		return this;
	}

	public virtual IMcpPluginBuilder RemoveIgnoredAssembly(Assembly assembly)
	{
		ThrowIfBuilt();
		_ignoreConfig.IgnoredAssemblies.Remove(assembly);
		_ignoreConfig.InvalidateCaches();
		return this;
	}

	public virtual IMcpPluginBuilder RemoveIgnoredAssembly(string assemblyName)
	{
		ThrowIfBuilt();
		_ignoreConfig.IgnoredAssemblyNames.Remove(assemblyName);
		_ignoreConfig.InvalidateCaches();
		return this;
	}

	public virtual IMcpPluginBuilder RemoveIgnoredAssemblies(IEnumerable<Assembly> assemblies)
	{
		ThrowIfBuilt();
		foreach (Assembly assembly in assemblies)
		{
			if (!(assembly == null))
			{
				_ignoreConfig.IgnoredAssemblies.Remove(assembly);
			}
		}
		_ignoreConfig.InvalidateCaches();
		return this;
	}

	public virtual IMcpPluginBuilder RemoveIgnoredAssemblies(params string[] assemblyNames)
	{
		ThrowIfBuilt();
		foreach (string item in assemblyNames)
		{
			_ignoreConfig.IgnoredAssemblyNames.Remove(item);
		}
		_ignoreConfig.InvalidateCaches();
		return this;
	}

	public virtual IMcpPluginBuilder RemoveIgnoredNamespace(string namespaceName)
	{
		ThrowIfBuilt();
		_ignoreConfig.IgnoredNamespaces.Remove(namespaceName);
		_ignoreConfig.InvalidateCaches();
		return this;
	}

	public virtual IMcpPluginBuilder RemoveIgnoredNamespaces(params string[] namespaceNames)
	{
		ThrowIfBuilt();
		foreach (string item in namespaceNames)
		{
			_ignoreConfig.IgnoredNamespaces.Remove(item);
		}
		_ignoreConfig.InvalidateCaches();
		return this;
	}

	public virtual IMcpPluginBuilder ClearIgnoredAssemblies()
	{
		ThrowIfBuilt();
		_ignoreConfig.IgnoredAssemblies.Clear();
		_ignoreConfig.IgnoredAssemblyNames.Clear();
		_ignoreConfig.InvalidateCaches();
		return this;
	}

	public virtual IMcpPluginBuilder ClearIgnoredNamespaces()
	{
		ThrowIfBuilt();
		_ignoreConfig.IgnoredNamespaces.Clear();
		_ignoreConfig.InvalidateCaches();
		return this;
	}

	public virtual IMcpPluginBuilder ClearAllIgnored()
	{
		ThrowIfBuilt();
		_ignoreConfig.IgnoredAssemblies.Clear();
		_ignoreConfig.IgnoredAssemblyNames.Clear();
		_ignoreConfig.IgnoredNamespaces.Clear();
		_ignoreConfig.InvalidateCaches();
		return this;
	}

	public virtual int GetIgnoredAssembliesCount()
	{
		HashSet<Assembly> hashSet = new HashSet<Assembly>(_toolAssemblies);
		hashSet.UnionWith(_promptAssemblies);
		hashSet.UnionWith(_resourceAssemblies);
		int num = 0;
		foreach (Assembly item in hashSet)
		{
			if (_ignoreConfig.IsIgnored(item))
			{
				num++;
			}
		}
		return num;
	}

	public virtual int GetIgnoredTypesCount()
	{
		int num = 0;
		HashSet<Type> hashSet = new HashSet<Type>();
		HashSet<Type> hashSet2 = new HashSet<Type>(_toolTypes);
		hashSet2.UnionWith(_promptTypes);
		hashSet2.UnionWith(_resourceTypes);
		foreach (Type item in hashSet2)
		{
			if (_ignoreConfig.IsIgnored(item) && hashSet.Add(item))
			{
				num++;
			}
		}
		HashSet<Assembly> hashSet3 = new HashSet<Assembly>(_toolAssemblies);
		hashSet3.UnionWith(_promptAssemblies);
		hashSet3.UnionWith(_resourceAssemblies);
		foreach (Assembly item2 in hashSet3)
		{
			if (_ignoreConfig.IsIgnored(item2))
			{
				continue;
			}
			try
			{
				Type[] exportedTypes = item2.GetExportedTypes();
				foreach (Type type in exportedTypes)
				{
					if (_ignoreConfig.IsIgnored(type) && hashSet.Add(type))
					{
						num++;
					}
				}
			}
			catch
			{
			}
		}
		return num;
	}

	public virtual IMcpPluginBuilder WithPrompts(params Type[] targetTypes)
	{
		return WithPrompts(targetTypes.AsEnumerable());
	}

	public virtual IMcpPluginBuilder WithPrompts(IEnumerable<Type> targetTypes)
	{
		if (targetTypes == null)
		{
			throw new ArgumentNullException("targetTypes");
		}
		foreach (Type targetType in targetTypes)
		{
			WithPrompts(targetType);
		}
		return this;
	}

	public virtual IMcpPluginBuilder WithPrompts<T>()
	{
		return WithPrompts(typeof(T));
	}

	public virtual IMcpPluginBuilder WithPrompts(Type classType)
	{
		if (classType == null)
		{
			throw new ArgumentNullException("classType");
		}
		ThrowIfBuilt();
		_promptTypes.Add(classType);
		return this;
	}

	public virtual IMcpPluginBuilder WithPromptsFromAssembly(IEnumerable<Assembly> assemblies)
	{
		if (assemblies == null)
		{
			throw new ArgumentNullException("assemblies");
		}
		foreach (Assembly assembly in assemblies)
		{
			WithPromptsFromAssembly(assembly);
		}
		return this;
	}

	public virtual IMcpPluginBuilder WithPromptsFromAssembly(Assembly? assembly = null)
	{
		ThrowIfBuilt();
		if ((object)assembly == null)
		{
			assembly = Assembly.GetCallingAssembly();
		}
		_promptAssemblies.Add(assembly);
		return this;
	}

	public virtual IMcpPluginBuilder WithReflectorModulesFromAssembly(IEnumerable<Assembly> assemblies)
	{
		ThrowIfBuilt();
		if (assemblies == null)
		{
			throw new ArgumentNullException("assemblies");
		}
		foreach (Assembly assembly in assemblies)
		{
			if (!(assembly == null))
			{
				_reflectorModuleAssemblies.Add(assembly);
			}
		}
		return this;
	}

	protected virtual void BootstrapReflectorModules(Reflector reflector)
	{
		List<IReflectorModule> list = new List<IReflectorModule>();
		HashSet<Assembly> hashSet = new HashSet<Assembly>();
		HashSet<string> hashSet2 = new HashSet<string>(StringComparer.Ordinal);
		foreach (Assembly item in _reflectorModuleAssemblies.Distinct())
		{
			if (_ignoreConfig.IsIgnored(item))
			{
				continue;
			}
			Type[] assemblyTypes = AssemblyUtils.GetAssemblyTypes(item);
			foreach (Type type in assemblyTypes)
			{
				if (_ignoreConfig.IsIgnored(type) || type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition || !typeof(IReflectorModule).IsAssignableFrom(type))
				{
					continue;
				}
				IReflectorModule reflectorModule;
				try
				{
					reflectorModule = (IReflectorModule)Activator.CreateInstance(type);
				}
				catch (Exception exception)
				{
					_logger?.LogError(exception, "Failed to instantiate reflector module '{Module}'. Skipping it. A public parameterless constructor is required.", type.FullName);
					continue;
				}
				if (reflectorModule != null)
				{
					list.Add(reflectorModule);
					hashSet.Add(item);
					if (!string.IsNullOrEmpty(type.Namespace))
					{
						hashSet2.Add(type.Namespace);
					}
				}
			}
		}
		if (list.Count == 0)
		{
			return;
		}
		HashSet<Assembly> hashSet3 = new HashSet<Assembly>(hashSet);
		hashSet3.UnionWith(_toolAssemblies);
		hashSet3.UnionWith(_promptAssemblies);
		hashSet3.UnionWith(_resourceAssemblies);
		hashSet3.UnionWith(_skillAssemblies);
		foreach (var item2 in Enumerable.ThenBy(Enumerable.ThenBy(from m in list
			select new
			{
				Module = m,
				Assembly = m.GetType().Assembly
			} into x
			orderby x.Module.Order
			select x, x => x.Assembly.FullName, StringComparer.Ordinal), x => x.Module.GetType().FullName, StringComparer.Ordinal).ToList())
		{
			Type type2 = item2.Module.GetType();
			ILogger logger = _loggerProvider?.CreateLogger(type2.FullName ?? "IReflectorModule") ?? NullLogger.Instance;
			ReflectorModuleContext ctx = new ReflectorModuleContext(reflector, item2.Assembly, logger, _ignoreConfig, hashSet3, hashSet2, _logger);
			try
			{
				item2.Module.Configure(ctx);
			}
			catch (Exception exception2)
			{
				_logger?.LogError(exception2, "Reflector module '{Module}' threw during Configure and was skipped. Other modules and tools are unaffected.", type2.FullName);
			}
		}
	}

	public virtual IMcpPluginBuilder WithResources(params Type[] targetTypes)
	{
		return WithResources(targetTypes.AsEnumerable());
	}

	public virtual IMcpPluginBuilder WithResources(IEnumerable<Type> targetTypes)
	{
		if (targetTypes == null)
		{
			throw new ArgumentNullException("targetTypes");
		}
		foreach (Type targetType in targetTypes)
		{
			WithResources(targetType);
		}
		return this;
	}

	public virtual IMcpPluginBuilder WithResources<T>()
	{
		return WithResources(typeof(T));
	}

	public virtual IMcpPluginBuilder WithResources(Type classType)
	{
		if (classType == null)
		{
			throw new ArgumentNullException("classType");
		}
		ThrowIfBuilt();
		_resourceTypes.Add(classType);
		return this;
	}

	public virtual IMcpPluginBuilder WithResourcesFromAssembly(IEnumerable<Assembly> assemblies)
	{
		if (assemblies == null)
		{
			throw new ArgumentNullException("assemblies");
		}
		foreach (Assembly assembly in assemblies)
		{
			WithResourcesFromAssembly(assembly);
		}
		return this;
	}

	public virtual IMcpPluginBuilder WithResourcesFromAssembly(Assembly? assembly = null)
	{
		ThrowIfBuilt();
		if ((object)assembly == null)
		{
			assembly = Assembly.GetCallingAssembly();
		}
		_resourceAssemblies.Add(assembly);
		return this;
	}

	public virtual IMcpPluginBuilder WithSkills(params Type[] targetTypes)
	{
		return WithSkills(targetTypes.AsEnumerable());
	}

	public virtual IMcpPluginBuilder WithSkills(IEnumerable<Type> targetTypes)
	{
		if (targetTypes == null)
		{
			throw new ArgumentNullException("targetTypes");
		}
		foreach (Type targetType in targetTypes)
		{
			WithSkills(targetType);
		}
		return this;
	}

	public virtual IMcpPluginBuilder WithSkills<T>()
	{
		return WithSkills(typeof(T));
	}

	public virtual IMcpPluginBuilder WithSkills(Type classType)
	{
		if (classType == null)
		{
			throw new ArgumentNullException("classType");
		}
		ThrowIfBuilt();
		_skillTypes.Add(classType);
		return this;
	}

	public virtual IMcpPluginBuilder WithSkillsFromAssembly(IEnumerable<Assembly> assemblies)
	{
		if (assemblies == null)
		{
			throw new ArgumentNullException("assemblies");
		}
		foreach (Assembly assembly in assemblies)
		{
			WithSkillsFromAssembly(assembly);
		}
		return this;
	}

	public virtual IMcpPluginBuilder WithSkillsFromAssembly(Assembly? assembly = null)
	{
		ThrowIfBuilt();
		if ((object)assembly == null)
		{
			assembly = Assembly.GetCallingAssembly();
		}
		_skillAssemblies.Add(assembly);
		return this;
	}

	public virtual IMcpPluginBuilder WithTools(params Type[] targetTypes)
	{
		return WithTools(targetTypes.AsEnumerable());
	}

	public virtual IMcpPluginBuilder WithTools(IEnumerable<Type> targetTypes)
	{
		if (targetTypes == null)
		{
			throw new ArgumentNullException("targetTypes");
		}
		foreach (Type targetType in targetTypes)
		{
			WithTools(targetType);
		}
		return this;
	}

	public virtual IMcpPluginBuilder WithTools<T>()
	{
		return WithTools(typeof(T));
	}

	public virtual IMcpPluginBuilder WithTools(Type classType)
	{
		if (classType == null)
		{
			throw new ArgumentNullException("classType");
		}
		ThrowIfBuilt();
		_toolTypes.Add(classType);
		return this;
	}

	public virtual IMcpPluginBuilder WithToolsFromAssembly(IEnumerable<Assembly> assemblies)
	{
		if (assemblies == null)
		{
			throw new ArgumentNullException("assemblies");
		}
		foreach (Assembly assembly in assemblies)
		{
			WithToolsFromAssembly(assembly);
		}
		return this;
	}

	public virtual IMcpPluginBuilder WithToolsFromAssembly(Assembly? assembly = null)
	{
		ThrowIfBuilt();
		if ((object)assembly == null)
		{
			assembly = Assembly.GetCallingAssembly();
		}
		_toolAssemblies.Add(assembly);
		return this;
	}
}
}
