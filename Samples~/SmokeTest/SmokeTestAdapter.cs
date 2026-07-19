using AutoPilot.Core;
using AutoPilot.InputSim;
using UnityEngine;

namespace AutoPilot.Samples.SmokeTest
{
	/// <summary>
	/// 最小の動作確認サンプル。ゲームのクラスを一切参照せず、フレームワークだけで完結する。
	/// Tools > AutoPilot > Enable On Play を有効にしてPlayすると:
	///   - 仮想Gamepadが追加され、左スティックを前方へ倒し続ける(接続中の実プレイヤーがいれば動く)
	///   - Gameビュー左上にデバッグオーバーレイが出る
	///   - 1秒ごとにSouthボタンを短く押す
	/// これで「入力注入が届いているか」を切り分けられる(オーバーレイのstick/buttons表示を見る)。
	///
	/// 使い方: Package Manager からこのサンプルをインポートし、Enable On Play → Play。
	/// 本物のアダプタを書くときは、この構造(Boot + Sensor + Scenario)を土台にする。
	/// </summary>
	public static class SmokeTestBoot
	{
		private static BotRunner _runner;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Bootstrap()
		{
			if (_runner != null || !IsEnabled())
				return;

			Application.runInBackground = true;
			_runner = BotRunner.Create(new VirtualGamepad(), "AutoPilot-SmokeTest");
			_runner.Sensors.Add(new SmokeTestSensor());
			_runner.Director.SetHandler(NormalizedPhase.Playing,
				() => new EnumeratorTask("SmokeDrive", DriveForever));
			_runner.Context.Log.Info("Smoke test started: hold-forward + tap South every second");
		}

		private static System.Collections.IEnumerator DriveForever(BotContext ctx)
		{
			while (true)
			{
				ctx.Activity = "smoke: stick forward + tap South";
				ctx.Controller.SetLeftStick(Vector2.up);
				yield return new TapButton(BotButton.South);
				yield return 1f;
			}
		}

		private static bool IsEnabled()
		{
#if UNITY_EDITOR
			return UnityEditor.EditorPrefs.GetBool(AutoPilotHandshake.EnableOnPlayPrefKey, false);
#else
			foreach (var a in System.Environment.GetCommandLineArgs())
				if (a == "-autopilot") return true;
			return false;
#endif
		}
	}

	/// <summary>常にPlayingフェーズを報告し、ダミーのプレイヤー存在フラグを立てるだけのSensor。</summary>
	public sealed class SmokeTestSensor : ISensor
	{
		public void Tick(Blackboard blackboard)
		{
			blackboard.Set(Keys.Phase, NormalizedPhase.Playing);
			blackboard.Set(Keys.PlayerExists, true);
		}
	}
}
