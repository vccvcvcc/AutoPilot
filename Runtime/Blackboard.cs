using System.Collections.Generic;
using UnityEngine;

namespace AutoPilot.Core
{
	/// <summary>
	/// 型付きBlackboardキー。キーの同一性は参照で判定するため、
	/// 各キーはstatic readonlyで一度だけ定義して共有する。
	/// </summary>
	public sealed class BlackboardKey<T>
	{
		public readonly string Name;

		public BlackboardKey(string name)
		{
			Name = name;
		}

		public override string ToString() => Name;
	}

	/// <summary>
	/// Sensorが書き込み、ボットの頭脳(Director/Task)が読み取る共有状態。
	/// ボット側はゲームのクラスを一切参照せず、ここにある正規化された値だけを見る。
	/// </summary>
	public sealed class Blackboard
	{
		private readonly Dictionary<object, object> _values = new Dictionary<object, object>();

		public void Set<T>(BlackboardKey<T> key, T value)
		{
			_values[key] = value;
		}

		public T Get<T>(BlackboardKey<T> key, T fallback = default)
		{
			return _values.TryGetValue(key, out object value) ? (T)value : fallback;
		}

		public bool TryGet<T>(BlackboardKey<T> key, out T value)
		{
			if (_values.TryGetValue(key, out object stored))
			{
				value = (T)stored;
				return true;
			}
			value = default;
			return false;
		}
	}

	/// <summary>
	/// どのゲームでも共通で使う標準キー。ゲーム固有のキーはアダプタ側で追加定義する。
	/// </summary>
	public static class Keys
	{
		public static readonly BlackboardKey<NormalizedPhase> Phase = new BlackboardKey<NormalizedPhase>("Phase");
		public static readonly BlackboardKey<bool> PlayerExists = new BlackboardKey<bool>("PlayerExists");
		public static readonly BlackboardKey<Vector3> PlayerPosition = new BlackboardKey<Vector3>("PlayerPosition");
		public static readonly BlackboardKey<Transform> CameraTransform = new BlackboardKey<Transform>("CameraTransform");
	}
}
