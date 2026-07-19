using UnityEngine;

namespace AutoPilot.Core
{
	/// <summary>
	/// 物理ゲームパッド相当の抽象ボタン。
	/// 「Jump = South」のようなセマンティックな対応付けはアダプタ側の定義に置く。
	/// </summary>
	public enum BotButton
	{
		South,
		East,
		West,
		North,
		Start,
		Select,
		LeftShoulder,
		RightShoulder,
		LeftTrigger,
		RightTrigger,
		DpadUp,
		DpadDown,
		DpadLeft,
		DpadRight,
		LeftStickPress,
		RightStickPress,
	}

	/// <summary>
	/// ボットの出力ポート。実装は入力注入レイヤー(仮想デバイス等)が提供する。
	/// 状態は明示的に変更されるまで保持される(スティックを倒したら倒れたまま)。
	/// </summary>
	public interface IVirtualController
	{
		Vector2 LeftStick { get; }
		Vector2 RightStick { get; }

		void SetLeftStick(Vector2 value);
		void SetRightStick(Vector2 value);
		void Press(BotButton button);
		void Release(BotButton button);
		bool IsPressed(BotButton button);

		/// <summary>全ボタン解放・スティック中立。フェーズ遷移やタスク中断時の安全化に使う。</summary>
		void NeutralAll();
	}
}
