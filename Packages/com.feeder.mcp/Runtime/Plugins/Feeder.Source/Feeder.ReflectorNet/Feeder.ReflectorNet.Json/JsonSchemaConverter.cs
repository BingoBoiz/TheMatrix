using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Feeder.ReflectorNet.Utils;

namespace Feeder.ReflectorNet.Json
{
public abstract class JsonSchemaConverter<T> : JsonConverter<T>, IJsonSchemaConverter
{
	private static readonly Type[] _emptyTypes = Array.Empty<Type>();

	public static string StaticId => TypeUtils.GetSchemaTypeId<T>();

	public virtual string Id => StaticId;

	public abstract JsonNode GetSchema();

	public abstract JsonNode GetSchemaRef();

	public virtual IEnumerable<Type> GetDefinedTypes()
	{
		return _emptyTypes;
	}
}
}
