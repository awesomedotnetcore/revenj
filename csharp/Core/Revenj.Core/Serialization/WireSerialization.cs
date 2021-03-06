﻿using System;
using System.IO;
using System.Runtime.Serialization;

namespace Revenj.Serialization
{
	internal class WireSerialization : IWireSerialization
	{
		private readonly JsonSerialization Json;
		private readonly XmlSerialization Xml;
		private readonly ProtobufSerialization Protobuf;
		private readonly PassThroughSerialization Pass;

		public WireSerialization(GenericDeserializationBinder binder)
		{
			Json = new JsonSerialization(binder);
#if NETSTANDARD2_0
			Xml = new XmlSerialization(null, null, Json, binder);
#else
			Xml = new XmlSerialization(null, null, binder);
#endif
			Protobuf = new ProtobufSerialization();
			Pass = new PassThroughSerialization();
		}

		public string Serialize(object value, string accept, Stream destination)
		{
			//fast path
			if (accept == "application/json" || accept == null)
			{
				Json.Serialize(value, destination, false);
				return "application/json";
			}
			if (accept == "application/json;minimal")
			{
				Json.Serialize(value, destination, true);
				return "application/json";
			}
			if (accept == "application/x-protobuf")
			{
				Protobuf.Serialize(value, destination);
				return "application/x-protobuf";
			}
			if (accept == "application/xml")
			{
				Xml.Serialize(value, destination);
				return "application/xml";
			}
			if (accept.StartsWith("application/json", StringComparison.Ordinal))
			{
				Json.Serialize(value, destination, false);
				return "application/json";
			}
			//Slow path
			accept = (accept ?? "application/json").ToLowerInvariant();
			if (accept.Contains("application/json"))
			{
				Json.Serialize(value, destination, false);
				return "application/json";
			}
			if (accept.Contains("application/xml"))
			{
				Xml.Serialize(value, destination);
				return "application/xml";
			}
			if (accept.Contains("application/x-protobuf"))
			{
				Protobuf.Serialize(value, destination);
				return "application/x-protobuf";
			}
			Json.Serialize(value, destination, false);
			return "application/json";
		}

		public object Deserialize(Stream source, Type target, string contentType, StreamingContext context)
		{
			if (source == null)
				return null;
			//fast path
			if (contentType == "application/json")
				return Json.Deserialize(source, target, context);
			if (contentType == "application/json;minimal")
				return Json.Deserialize(source, target, context);
			if (contentType == "application/x-protobuf")
				return Protobuf.Deserialize(source, target, context);
			if (contentType == "application/xml")
				return Xml.Deserialize(source, target, context);
			//slow path
			contentType = (contentType ?? "application/json").ToLowerInvariant();
			if (contentType.Contains("application/xml"))
				return Xml.Deserialize(source, target, context);
			if (contentType.Contains("application/x-protobuf"))
				return Protobuf.Deserialize(source, target, context);
			return Json.Deserialize(source, target, context);
		}

		public ISerialization<TFormat> GetSerializer<TFormat>()
		{
			return Json as ISerialization<TFormat>
				?? Xml as ISerialization<TFormat>
				?? Protobuf as ISerialization<TFormat>
				?? Pass as ISerialization<TFormat>;
		}
	}
}
