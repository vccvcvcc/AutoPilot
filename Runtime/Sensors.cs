using System;
using System.Collections.Generic;

namespace AutoPilot.Core
{
	public interface ISensor
	{
		void Tick(Blackboard blackboard);
	}

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

		public void DisposeAll()
		{
			for (int i = 0; i < _sensors.Count; i++)
				(_sensors[i] as IDisposable)?.Dispose();
		}
	}
}
