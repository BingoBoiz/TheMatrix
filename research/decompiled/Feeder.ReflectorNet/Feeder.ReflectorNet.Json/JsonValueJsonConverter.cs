using System.Text.Json;
using System.Text.Json.Nodes;

namespace Feeder.ReflectorNet.Json;

public class JsonValueJsonConverter : JsonNodeJsonConverter<JsonValue>, IJsonSchemaConverter
{
	public static JsonNode Schema => JsonArrayJsonConverter.JsonAnySchema;

	public static JsonNode SchemaRef => new JsonObject { ["$ref"] = "#/$defs/" + JsonSchemaConverter<JsonValue>.StaticId };

	public override JsonNode GetSchema()
	{
		return Schema;
	}

	public override JsonNode GetSchemaRef()
	{
		return SchemaRef;
	}

	protected override JsonValue? CreateJsonNode(JsonElement element)
	{
		return JsonValue.Create(element);
	}
}
