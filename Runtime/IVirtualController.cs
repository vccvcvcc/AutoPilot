using UnityEngine;

namespace AutoPilot.Core
{
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
		DpadUp,
		DpadDown,
		DpadLeft,
		DpadRight,
		LeftStickPress,
		RightStickPress,
		LeftTrigger,
		RightTrigger
	}

	public interface IVirtualController
	{
		Vector2 LeftStick { get; }
		Vector2 RightStick { get; }
		void SetLeftStick(Vector2 value);
		void SetRightStick(Vector2 value);
		void Press(BotButton button);
		void Release(BotButton button);
		bool IsPressed(BotButton button);
		void NeutralAll();
	}
}
