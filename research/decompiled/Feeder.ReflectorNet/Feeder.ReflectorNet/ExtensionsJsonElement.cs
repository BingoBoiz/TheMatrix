using System;
using System.Text.Json;
using Feeder.ReflectorNet.Model;
using Microsoft.Extensions.Logging;

namespace Feeder.ReflectorNet;

public static class ExtensionsJsonElement
{
	public static T? Deserialize<T>(this JsonElement? jsonElement, Reflector reflector)
	{
		return reflector.JsonSerializer.Deserialize<T>(reflector, jsonElement);
	}

	public static object? Deserialize(this JsonElement? jsonElement, Type type, Reflector reflector)
	{
		return reflector.JsonSerializer.Deserialize(reflector, jsonElement, type);
	}

	public static T? DeserializeValueSerializedMember<T>(this JsonElement? jsonElement, Reflector reflector, string? name = null, int depth = 0, Logs? logs = null, ILogger? logger = null)
	{
		return (T)jsonElement.DeserializeValueSerializedMember(reflector, typeof(T), name, depth, logs, logger);
	}

	public static object? DeserializeValueSerializedMember(this JsonElement? jsonElement, Reflector reflector, Type type, string? name = null, int depth = 0, Logs? logs = null, ILogger? logger = null)
	{
		if (!jsonElement.HasValue)
		{
			return null;
		}
		SerializedMember serializedMember = null;
		try
		{
			serializedMember = jsonElement.Deserialize<SerializedMember>(reflector);
		}
		catch
		{
		}
		if (serializedMember == null)
		{
			return reflector.GetDefaultValue(type);
		}
		if (!serializedMember.valueJsonElement.HasValue)
		{
			return reflector.CreateInstance(type);
		}
		return reflector.Deserialize(serializedMember, type, name, depth, logs, logger);
	}
}
