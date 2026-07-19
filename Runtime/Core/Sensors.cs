using System;
using System.Collections.Generic;

namespace AutoPilot.Core
{
	/// <summary>
	/// ゲームの内部状態を観測してBlackboardへ書き込む。
	/// 実装はアダプタ側(ゲーム固有アセンブリ)に置く。
	/// </summary>
	public interface ISensor
	{
		void Tick(Blackboard blackboard);
	}

	/// <summary>
	/// 登録されたSensorを毎フレーム順に評価する。1つのSensorの例外が他を巻き込まないよう隔離する。
	/// </summary>
	public sealed class SensorHub
	{
		private readonly List<ISensor> _sensors = new List<ISensor>();
		private BotLog _log;

		public void SetLog(BotLog log) => _log = log;

		public void Add(ISensor sensor) => _sensors.Add(sensor);

		public void Tick(Blackboard blackboard)
		{
			for (int i = 0; i < _sensors.Count; i++)
			{
				try
				{
					_sensors[i].Tick(blackboard);
				}
				catch (Exception e)
				{
					_log?.Error($"Sensor {_sensors[i].GetType().Name} threw: {e}");
				}
			}
		}

		/// <summary>IDisposableなSensor(BotReporter等)の後始末。BotRunner破棄時に呼ばれる。</summary>
		public void DisposeAll()
		{
			for (int i = 0; i < _sensors.Count; i++)
				(_sensors[i] as IDisposable)?.Dispose();
		}
	}
}
