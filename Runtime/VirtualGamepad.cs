using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using AutoPilot.Core;

namespace AutoPilot.InputSim
{
	/// <summary>
	/// Input Systemに仮想Gamepadデバイスを追加し、状態イベント(QueueStateEvent)で擬似入力を注入する。
	/// 実機パッドと同じ経路(バインディング解決・ActionMapの有効/無効・UI入力モジュール)を通るため、
	/// ゲーム側のコードを変更せずに、プレイヤー操作と等価な入力検証ができる。
	/// </summary>
	public sealed class VirtualGamepad : IVirtualController, IDisposable
	{
		private Gamepad _device;
		private Vector2 _leftStick;
		private Vector2 _rightStick;
		private float _leftTrigger;
		private float _rightTrigger;
		private readonly HashSet<GamepadButton> _pressed = new HashSet<GamepadButton>();

		public Vector2 LeftStick => _leftStick;
		public Vector2 RightStick => _rightStick;

		public VirtualGamepad()
		{
			_device = InputSystem.AddDevice<Gamepad>("AutoPilotGamepad");
			Flush();
		}

		public void SetLeftStick(Vector2 value)
		{
			_leftStick = Vector2.ClampMagnitude(value, 1f);
			Flush();
		}

		public void SetRightStick(Vector2 value)
		{
			_rightStick = Vector2.ClampMagnitude(value, 1f);
			Flush();
		}

		public void Press(BotButton button)
		{
			Apply(button, true);
			Flush();
		}

		public void Release(BotButton button)
		{
			Apply(button, false);
			Flush();
		}

		public bool IsPressed(BotButton button)
		{
			switch (button)
			{
				case BotButton.LeftTrigger: return _leftTrigger > 0.5f;
				case BotButton.RightTrigger: return _rightTrigger > 0.5f;
				default: return _pressed.Contains(Map(button));
			}
		}

		public void NeutralAll()
		{
			_pressed.Clear();
			_leftStick = Vector2.zero;
			_rightStick = Vector2.zero;
			_leftTrigger = 0f;
			_rightTrigger = 0f;
			Flush();
		}

		public void Dispose()
		{
			if (_device != null && _device.added)
				InputSystem.RemoveDevice(_device);
			_device = null;
		}

		private void Apply(BotButton button, bool down)
		{
			switch (button)
			{
				case BotButton.LeftTrigger:
					_leftTrigger = down ? 1f : 0f;
					return;
				case BotButton.RightTrigger:
					_rightTrigger = down ? 1f : 0f;
					return;
			}

			GamepadButton mapped = Map(button);
			if (down)
				_pressed.Add(mapped);
			else
				_pressed.Remove(mapped);
		}

		private void Flush()
		{
			if (_device == null || !_device.added)
				return;

			var state = new GamepadState
			{
				leftStick = _leftStick,
				rightStick = _rightStick,
				leftTrigger = _leftTrigger,
				rightTrigger = _rightTrigger,
			};
			foreach (GamepadButton button in _pressed)
				state = state.WithButton(button);

			InputSystem.QueueStateEvent(_device, state);
		}

		private static GamepadButton Map(BotButton button)
		{
			switch (button)
			{
				case BotButton.South: return GamepadButton.South;
				case BotButton.East: return GamepadButton.East;
				case BotButton.West: return GamepadButton.West;
				case BotButton.North: return GamepadButton.North;
				case BotButton.Start: return GamepadButton.Start;
				case BotButton.Select: return GamepadButton.Select;
				case BotButton.LeftShoulder: return GamepadButton.LeftShoulder;
				case BotButton.RightShoulder: return GamepadButton.RightShoulder;
				case BotButton.DpadUp: return GamepadButton.DpadUp;
				case BotButton.DpadDown: return GamepadButton.DpadDown;
				case BotButton.DpadLeft: return GamepadButton.DpadLeft;
				case BotButton.DpadRight: return GamepadButton.DpadRight;
				case BotButton.LeftStickPress: return GamepadButton.LeftStick;
				case BotButton.RightStickPress: return GamepadButton.RightStick;
				default: throw new ArgumentOutOfRangeException(nameof(button), button, null);
			}
		}
	}
}
