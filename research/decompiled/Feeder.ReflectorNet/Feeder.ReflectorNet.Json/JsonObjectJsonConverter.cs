using System.Text.Json;
using System.Text.Json.Nodes;

namespace Feeder.ReflectorNet.Json;

public class JsonObjectJsonConverter : JsonNodeJsonConverter<JsonObject>, IJsonSchemaConverter
{
	public static JsonNode Schema => new JsonObject
	{
		["type"] = "object",
		["additionalProperties"] = true
	};

	public static JsonNode SchemaRef => new JsonObject { ["$ref"] = "#/$defs/" + JsonSchemaConverter<JsonObject>.StaticId };

	public override JsonNode GetSchema()
	{
		return Schema;
	}

	public override JsonNode GetSchemaRef()
	{
		return SchemaRef;
	}

	protected override JsonObject? CreateJsonNode(JsonElement element)
	{
		return JsonObject.Create(element);
	}
}
