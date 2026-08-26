using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Feeder.ReflectorNet.Json
{
public interface IJsonSchemaConverter
{
	string Id { get; }

	JsonNode GetSchema();

	JsonNode GetSchemaRef();

	IEnumerable<Type> GetDefinedTypes();
}
}
