using System.Text.Json;
using System.Text.Json.Serialization;
using Feeder.ReflectorNet;
using Microsoft.AspNetCore.SignalR;

namespace Feeder.McpPlugin.Common;

public static class SignalR_JsonConfiguration
{
	public static void ConfigureJsonSerializer(Reflector reflector, JsonHubProtocolOptions options)
	{
		JsonSerializerOptions jsonSerializerOptions = reflector.JsonSerializerOptions;
		options.PayloadSerializerOptions.DefaultIgnoreCondition = jsonSerializerOptions.DefaultIgnoreCondition;
		options.PayloadSerializerOptions.PropertyNamingPolicy = jsonSerializerOptions.PropertyNamingPolicy;
		options.PayloadSerializerOptions.WriteIndented = jsonSerializerOptions.WriteIndented;
		foreach (JsonConverter converter in jsonSerializerOptions.Converters)
		{
			options.PayloadSerializerOptions.Converters.Add(converter);
		}
	}
}
