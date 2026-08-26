using System;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Feeder.ReflectorNet.Utils;

public static class JsonTypeInfoModifiers
{
	public static void ExcludeObsoleteMembers(JsonTypeInfo typeInfo)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		if ((int)typeInfo.Kind != 1)
		{
			return;
		}
		for (int num = typeInfo.Properties.Count - 1; num >= 0; num--)
		{
			if (IsObsolete(typeInfo.Properties[num]))
			{
				typeInfo.Properties.RemoveAt(num);
			}
		}
	}

	private static bool IsObsolete(JsonPropertyInfo propertyInfo)
	{
		ICustomAttributeProvider attributeProvider = propertyInfo.AttributeProvider;
		if (attributeProvider == null)
		{
			return false;
		}
		if (attributeProvider.IsDefined(typeof(ObsoleteAttribute), inherit: true))
		{
			return true;
		}
		if (attributeProvider is PropertyInfo propertyInfo2)
		{
			MethodInfo? getMethod = propertyInfo2.GetMethod;
			if ((object)getMethod == null || !getMethod.IsDefined(typeof(ObsoleteAttribute), inherit: true))
			{
				return propertyInfo2.SetMethod?.IsDefined(typeof(ObsoleteAttribute), inherit: true) ?? false;
			}
			return true;
		}
		return false;
	}
}
