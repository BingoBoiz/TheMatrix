namespace Feeder.ReflectorNet
{
public static class ExtensionsString
{
	public static string ValueOrNull(this string? value)
	{
		if (value != null)
		{
			return value;
		}
		return "null";
	}
}
}
