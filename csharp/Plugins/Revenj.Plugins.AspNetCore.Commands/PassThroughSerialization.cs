﻿using Revenj.Serialization;
using System.Runtime.Serialization;

namespace Revenj.Plugins.AspNetCore.Commands
{
	internal class PassThroughSerialization : ISerialization<object>
	{
		public object Serialize<T>(T value) { return value; }
		public T Deserialize<T>(object data, StreamingContext context) { return (T)data; }
	}
}
