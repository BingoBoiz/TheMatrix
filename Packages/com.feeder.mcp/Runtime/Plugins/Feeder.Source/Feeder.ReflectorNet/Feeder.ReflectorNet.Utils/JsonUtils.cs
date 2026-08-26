using System.Text.Json;

namespace Feeder.ReflectorNet.Utils
{
public static class JsonUtils
{
	public static bool TryUnstringifyJson(JsonElement jsonElement, out JsonElement? result)
	{
		if (jsonElement.ValueKind == JsonValueKind.String)
		{
			string json = jsonElement.GetString() ?? string.Empty;
			try
			{
				result = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(json);
				return true;
			}
			catch (JsonException)
			{
			}
		}
		result = null;
		return false;
	}
}
}
