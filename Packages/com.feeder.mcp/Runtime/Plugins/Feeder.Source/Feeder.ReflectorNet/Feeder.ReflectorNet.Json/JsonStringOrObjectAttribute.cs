using System;
using System.Text.Json.Nodes;

namespace Feeder.ReflectorNet.Json
{
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class JsonStringOrObjectAttribute : Attribute
{
	public static JsonNode Schema => new JsonObject { ["anyOf"] = new JsonArray
	{
		new JsonObject { ["type"] = "string" },
		new JsonObject
		{
			["type"] = "object",
			["additionalProperties"] = true
		}
	} };
}
}
