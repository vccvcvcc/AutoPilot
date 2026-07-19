using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoPilot.Core
{
	public sealed class BlackboardKey<T>
	{
		private readonly string _name;
		public BlackboardKey(string name) => _name = name;
		public override string ToString() => _name;
	}

	public sealed class Blackboard
	{
		private readonly Dictionary<object, object> _values = new Dictionary<object, object>();

		public void Set<T>(BlackboardKey<T> key, T value) => _values[key] = value;

		public T Get<T>(BlackboardKey<T> key, T fallback = default(T))
		{
			if (_values.TryGetValue(key, out var value))
				return (T)value;
			return fallback;
		}

		public bool Contains<T>(BlackboardKey<T> key) => _values.ContainsKey(key);
		public void Clear() => _values.Clear();
	}

	public static class Keys
	{
		public static readonly BlackboardKey<NormalizedPhase> Phase = new BlackboardKey<NormalizedPhase>("phase");
		public static readonly BlackboardKey<bool> PlayerExists = new BlackboardKey<bool>("playerExists");
		public static readonly BlackboardKey<Vector3> PlayerPosition = new BlackboardKey<Vector3>("playerPosition");
		public static readonly BlackboardKey<Transform> CameraTransform = new BlackboardKey<Transform>("cameraTransform");
		public static readonly BlackboardKey<string> Activity = new BlackboardKey<string>("activity");
	}
}
