using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Zorro.ControllerSupport;
using Zorro.Core;

// Token: 0x020000C9 RID: 201
[CreateAssetMenu(fileName = "InputSpriteData", menuName = "Scouts/Input Sprite Data")]
public class InputSpriteData : SingletonAsset<InputSpriteData>
{
	// Token: 0x060007C1 RID: 1985 RVA: 0x0002B174 File Offset: 0x00029374
	public string GetSpriteTag(InputSpriteData.InputAction action, InputScheme scheme)
	{
		if (scheme == InputScheme.KeyboardMouse)
		{
			if (InputSpriteData.ActionToHardcodedSpriteKeyboard.ContainsKey(action))
			{
				return InputSpriteData.ActionToHardcodedSpriteKeyboard[action];
			}
			if (InputSpriteData.ActionToBackendNameKeyboard.ContainsKey(action))
			{
				bool flag;
				string bindingPath = InputSpriteData.GetBindingPath(InputSpriteData.ActionToBackendNameKeyboard[action], scheme, out flag);
				return this.GetSpriteTagFromInputPathKeyboard(bindingPath);
			}
			Debug.Log(string.Format("Failed to find backend name for {0}", action));
		}
		else if (scheme == InputScheme.Gamepad)
		{
			if (action == InputSpriteData.InputAction.Aim)
			{
				return "<sprite=17 tint=1>";
			}
			if (action == InputSpriteData.InputAction.Move)
			{
				return "<sprite=16 tint=1>";
			}
			if (InputSpriteData.ActionToBackendNameGamepad.ContainsKey(action))
			{
				bool flag2;
				string bindingPath2 = InputSpriteData.GetBindingPath(InputSpriteData.ActionToBackendNameGamepad[action], scheme, out flag2);
				return this.GetSpriteTagFromInputPathGamepad(bindingPath2);
			}
			Debug.Log(string.Format("Failed to find backend name for {0}", action));
		}
		return "";
	}

	// Token: 0x060007C2 RID: 1986 RVA: 0x0002B23C File Offset: 0x0002943C
	public static string GetBindingPath(string actionName, InputScheme scheme, out bool hasOverride)
	{
		hasOverride = false;
		UnityEngine.InputSystem.InputAction inputAction = InputSystem.actions.FindAction(actionName, false);
		if (inputAction != null)
		{
			foreach (InputBinding inputBinding in inputAction.bindings)
			{
				if (scheme == InputScheme.KeyboardMouse && (inputBinding.effectivePath.Contains("<Keyboard>") || inputBinding.effectivePath.Contains("<Mouse>")))
				{
					hasOverride = !string.IsNullOrEmpty(inputBinding.overridePath);
					return inputBinding.effectivePath;
				}
				if ((scheme == InputScheme.Gamepad || scheme == InputScheme.Unknown) && inputBinding.effectivePath.Contains("<Gamepad>"))
				{
					hasOverride = !string.IsNullOrEmpty(inputBinding.overridePath);
					return inputBinding.effectivePath;
				}
			}
		}
		return "";
	}

	// Token: 0x060007C3 RID: 1987 RVA: 0x0002B32C File Offset: 0x0002952C
	public static string GetPathEnd(string inputPath)
	{
		if (string.IsNullOrEmpty(inputPath))
		{
			return "";
		}
		string[] array = inputPath.Split("/", StringSplitOptions.None);
		return array[array.Length - 1];
	}

	// Token: 0x060007C4 RID: 1988 RVA: 0x0002B350 File Offset: 0x00029550
	public string GetSpriteTagFromInputPathGamepad(string inputPath)
	{
		if (string.IsNullOrEmpty(inputPath))
		{
			return "";
		}
		string[] array = inputPath.Split("/", StringSplitOptions.None);
		string key = array[array.Length - 1];
		string result;
		if (this.inputPathToSpriteTagGamepad.TryGetValue(key, out result))
		{
			return result;
		}
		return null;
	}

	// Token: 0x060007C5 RID: 1989 RVA: 0x0002B394 File Offset: 0x00029594
	public string GetSpriteTagFromInputPathKeyboard(string inputPath)
	{
		if (string.IsNullOrEmpty(inputPath))
		{
			return "<sprite=124 tint=1>";
		}
		string[] array = inputPath.Split("/", StringSplitOptions.None);
		string key = array[array.Length - 1];
		string result;
		if (array[0] == "<Mouse>" && this.inputPathToSpriteTagMouse.TryGetValue(key, out result))
		{
			return result;
		}
		string result2;
		if (this.inputPathToSpriteTagKeyboard.TryGetValue(key, out result2))
		{
			return result2;
		}
		return "<sprite=124 tint=1>";
	}

	// Token: 0x04000796 RID: 1942
	public TMP_SpriteAsset keyboardSprites;

	// Token: 0x04000797 RID: 1943
	public TMP_SpriteAsset xboxSprites;

	// Token: 0x04000798 RID: 1944
	public TMP_SpriteAsset switchSprites;

	// Token: 0x04000799 RID: 1945
	public TMP_SpriteAsset ps5Sprites;

	// Token: 0x0400079A RID: 1946
	public TMP_SpriteAsset ps4Sprites;

	// Token: 0x0400079B RID: 1947
	public static Dictionary<InputSpriteData.InputAction, string> ActionToHardcodedSpriteKeyboard = new Dictionary<InputSpriteData.InputAction, string>
	{
		{
			InputSpriteData.InputAction.Aim,
			"<sprite=108 tint=1>"
		},
		{
			InputSpriteData.InputAction.Move,
			"<sprite=115 tint=1>"
		},
		{
			InputSpriteData.InputAction.Scroll,
			"<sprite=112 tint=1>"
		}
	};

	// Token: 0x0400079C RID: 1948
	public static Dictionary<InputSpriteData.InputAction, string> ActionToBackendNameKeyboard = new Dictionary<InputSpriteData.InputAction, string>
	{
		{
			InputSpriteData.InputAction.Interact,
			"Interact"
		},
		{
			InputSpriteData.InputAction.HoldInteract,
			"Interact"
		},
		{
			InputSpriteData.InputAction.UsePrimary,
			"UsePrimary"
		},
		{
			InputSpriteData.InputAction.UseSecondary,
			"UseSecondary"
		},
		{
			InputSpriteData.InputAction.Scroll,
			"Scroll"
		},
		{
			InputSpriteData.InputAction.Throw,
			"Drop"
		},
		{
			InputSpriteData.InputAction.Drop,
			"Drop"
		},
		{
			InputSpriteData.InputAction.Slot1,
			"Hotbar1"
		},
		{
			InputSpriteData.InputAction.Slot2,
			"Hotbar2"
		},
		{
			InputSpriteData.InputAction.Slot3,
			"Hotbar3"
		},
		{
			InputSpriteData.InputAction.Slot4,
			"Hotbar4"
		},
		{
			InputSpriteData.InputAction.SpectateLeft,
			"Hotbar1"
		},
		{
			InputSpriteData.InputAction.SpectateRight,
			"Hotbar2"
		},
		{
			InputSpriteData.InputAction.Move,
			"Move"
		},
		{
			InputSpriteData.InputAction.Aim,
			"Aim"
		},
		{
			InputSpriteData.InputAction.Sprint,
			"Sprint"
		},
		{
			InputSpriteData.InputAction.Jump,
			"Jump"
		},
		{
			InputSpriteData.InputAction.Crouch,
			"Crouch"
		},
		{
			InputSpriteData.InputAction.Ping,
			"Ping"
		},
		{
			InputSpriteData.InputAction.SlotLeft,
			"SelectSlotBackward"
		},
		{
			InputSpriteData.InputAction.SlotRight,
			"SelectSlotForward"
		},
		{
			InputSpriteData.InputAction.DeselectSlot,
			"UnselectSlot"
		},
		{
			InputSpriteData.InputAction.Emote,
			"Emote"
		},
		{
			InputSpriteData.InputAction.PushToTalk,
			"PushToTalk"
		},
		{
			InputSpriteData.InputAction.TabLeft,
			"TabLeft"
		},
		{
			InputSpriteData.InputAction.TabRight,
			"TabRight"
		},
		{
			InputSpriteData.InputAction.MoveForward,
			"MoveForward"
		},
		{
			InputSpriteData.InputAction.MoveBackward,
			"MoveBackward"
		},
		{
			InputSpriteData.InputAction.MoveLeft,
			"MoveLeft"
		},
		{
			InputSpriteData.InputAction.MoveRight,
			"MoveRight"
		},
		{
			InputSpriteData.InputAction.ScrollForward,
			"ScrollForward"
		},
		{
			InputSpriteData.InputAction.ScrollBackward,
			"ScrollBackward"
		},
		{
			InputSpriteData.InputAction.Pause,
			"Pause"
		}
	};

	// Token: 0x0400079D RID: 1949
	public static Dictionary<InputSpriteData.InputAction, string> ActionToBackendNameGamepad = new Dictionary<InputSpriteData.InputAction, string>
	{
		{
			InputSpriteData.InputAction.Interact,
			"Interact"
		},
		{
			InputSpriteData.InputAction.HoldInteract,
			"Interact"
		},
		{
			InputSpriteData.InputAction.UsePrimary,
			"UsePrimary"
		},
		{
			InputSpriteData.InputAction.UseSecondary,
			"UseSecondary"
		},
		{
			InputSpriteData.InputAction.Scroll,
			"Scroll"
		},
		{
			InputSpriteData.InputAction.Throw,
			"Drop"
		},
		{
			InputSpriteData.InputAction.Drop,
			"Drop"
		},
		{
			InputSpriteData.InputAction.Slot1,
			"Hotbar1"
		},
		{
			InputSpriteData.InputAction.Slot2,
			"Hotbar2"
		},
		{
			InputSpriteData.InputAction.Slot3,
			"Hotbar3"
		},
		{
			InputSpriteData.InputAction.Slot4,
			"Hotbar4"
		},
		{
			InputSpriteData.InputAction.SpectateLeft,
			"SelectSlotBackward"
		},
		{
			InputSpriteData.InputAction.SpectateRight,
			"SelectSlotForward"
		},
		{
			InputSpriteData.InputAction.Move,
			"Move"
		},
		{
			InputSpriteData.InputAction.Aim,
			"Aim"
		},
		{
			InputSpriteData.InputAction.Sprint,
			"SprintToggle"
		},
		{
			InputSpriteData.InputAction.Jump,
			"Jump"
		},
		{
			InputSpriteData.InputAction.Crouch,
			"CrouchToggle"
		},
		{
			InputSpriteData.InputAction.Ping,
			"Ping"
		},
		{
			InputSpriteData.InputAction.SlotLeft,
			"SelectSlotBackward"
		},
		{
			InputSpriteData.InputAction.SlotRight,
			"SelectSlotForward"
		},
		{
			InputSpriteData.InputAction.DeselectSlot,
			"UnselectSlot"
		},
		{
			InputSpriteData.InputAction.Emote,
			"Emote"
		},
		{
			InputSpriteData.InputAction.PushToTalk,
			"PushToTalk"
		},
		{
			InputSpriteData.InputAction.TabLeft,
			"TabLeft"
		},
		{
			InputSpriteData.InputAction.TabRight,
			"TabRight"
		},
		{
			InputSpriteData.InputAction.MoveForward,
			"MoveForward"
		},
		{
			InputSpriteData.InputAction.MoveBackward,
			"MoveBackward"
		},
		{
			InputSpriteData.InputAction.MoveLeft,
			"MoveLeft"
		},
		{
			InputSpriteData.InputAction.MoveRight,
			"MoveRight"
		},
		{
			InputSpriteData.InputAction.ScrollForward,
			"ScrollForward"
		},
		{
			InputSpriteData.InputAction.ScrollBackward,
			"ScrollBackward"
		},
		{
			InputSpriteData.InputAction.Pause,
			"Pause"
		}
	};

	// Token: 0x0400079E RID: 1950
	private Dictionary<string, string> inputPathToSpriteTagGamepad = new Dictionary<string, string>
	{
		{
			"leftShoulder",
			"<sprite=4 tint=1>"
		},
		{
			"rightShoulder",
			"<sprite=5 tint=1>"
		},
		{
			"leftTrigger",
			"<sprite=6 tint=1>"
		},
		{
			"rightTrigger",
			"<sprite=7 tint=1>"
		},
		{
			"buttonNorth",
			"<sprite=3 tint=1>"
		},
		{
			"buttonSouth",
			"<sprite=0 tint=1>"
		},
		{
			"buttonWest",
			"<sprite=2 tint=1>"
		},
		{
			"buttonEast",
			"<sprite=1 tint=1>"
		},
		{
			"up",
			"<sprite=12 tint=1>"
		},
		{
			"down",
			"<sprite=13 tint=1>"
		},
		{
			"left",
			"<sprite=14 tint=1>"
		},
		{
			"right",
			"<sprite=15 tint=1>"
		},
		{
			"start",
			"<sprite=8 tint=1>"
		},
		{
			"select",
			"<sprite=9 tint=1>"
		},
		{
			"leftStickPress",
			"<sprite=10 tint=1>"
		},
		{
			"rightStickPress",
			"<sprite=11 tint=1>"
		}
	};

	// Token: 0x0400079F RID: 1951
	private Dictionary<string, string> inputPathToSpriteTagKeyboard = new Dictionary<string, string>
	{
		{
			"0",
			"<sprite=0 tint=1>"
		},
		{
			"1",
			"<sprite=1 tint=1>"
		},
		{
			"2",
			"<sprite=2 tint=1>"
		},
		{
			"3",
			"<sprite=3 tint=1>"
		},
		{
			"4",
			"<sprite=4 tint=1>"
		},
		{
			"5",
			"<sprite=5 tint=1>"
		},
		{
			"6",
			"<sprite=6 tint=1>"
		},
		{
			"7",
			"<sprite=7 tint=1>"
		},
		{
			"8",
			"<sprite=8 tint=1>"
		},
		{
			"9",
			"<sprite=9 tint=1>"
		},
		{
			"a",
			"<sprite=10 tint=1>"
		},
		{
			"b",
			"<sprite=11 tint=1>"
		},
		{
			"c",
			"<sprite=12 tint=1>"
		},
		{
			"d",
			"<sprite=13 tint=1>"
		},
		{
			"e",
			"<sprite=14 tint=1>"
		},
		{
			"f",
			"<sprite=15 tint=1>"
		},
		{
			"g",
			"<sprite=16 tint=1>"
		},
		{
			"h",
			"<sprite=17 tint=1>"
		},
		{
			"i",
			"<sprite=18 tint=1>"
		},
		{
			"j",
			"<sprite=19 tint=1>"
		},
		{
			"k",
			"<sprite=20 tint=1>"
		},
		{
			"l",
			"<sprite=21 tint=1>"
		},
		{
			"m",
			"<sprite=22 tint=1>"
		},
		{
			"n",
			"<sprite=23 tint=1>"
		},
		{
			"o",
			"<sprite=24 tint=1>"
		},
		{
			"p",
			"<sprite=25 tint=1>"
		},
		{
			"q",
			"<sprite=26 tint=1>"
		},
		{
			"r",
			"<sprite=27 tint=1>"
		},
		{
			"s",
			"<sprite=28 tint=1>"
		},
		{
			"t",
			"<sprite=29 tint=1>"
		},
		{
			"u",
			"<sprite=30 tint=1>"
		},
		{
			"v",
			"<sprite=31 tint=1>"
		},
		{
			"w",
			"<sprite=32 tint=1>"
		},
		{
			"x",
			"<sprite=33 tint=1>"
		},
		{
			"y",
			"<sprite=34 tint=1>"
		},
		{
			"z",
			"<sprite=35 tint=1>"
		},
		{
			"f1",
			"<sprite=36 tint=1>"
		},
		{
			"f2",
			"<sprite=37 tint=1>"
		},
		{
			"f3",
			"<sprite=38 tint=1>"
		},
		{
			"f4",
			"<sprite=39 tint=1>"
		},
		{
			"f5",
			"<sprite=40 tint=1>"
		},
		{
			"f6",
			"<sprite=41 tint=1>"
		},
		{
			"f7",
			"<sprite=42 tint=1>"
		},
		{
			"f8",
			"<sprite=43 tint=1>"
		},
		{
			"f9",
			"<sprite=44 tint=1>"
		},
		{
			"f10",
			"<sprite=45 tint=1>"
		},
		{
			"f11",
			"<sprite=46 tint=1>"
		},
		{
			"f12",
			"<sprite=47 tint=1>"
		},
		{
			"minus",
			"<sprite=78 tint=1>"
		},
		{
			"equals",
			"<sprite=80 tint=1>"
		},
		{
			"leftBracket",
			"<sprite=82 tint=1>"
		},
		{
			"rightBracket",
			"<sprite=83 tint=1>"
		},
		{
			"backquote",
			"<sprite=81 tint=1>"
		},
		{
			"tab",
			"<sprite=53 tint=1>"
		},
		{
			"leftShift",
			"<sprite=51 tint=1>"
		},
		{
			"rightShift",
			"<sprite=51 tint=1>"
		},
		{
			"shift",
			"<sprite=51 tint=1>"
		},
		{
			"leftCtrl",
			"<sprite=49 tint=1>"
		},
		{
			"rightCtrl",
			"<sprite=49 tint=1>"
		},
		{
			"ctrl",
			"<sprite=49 tint=1>"
		},
		{
			"leftAlt",
			"<sprite=50 tint=1>"
		},
		{
			"rightAlt",
			"<sprite=50 tint=1>"
		},
		{
			"alt",
			"<sprite=50 tint=1>"
		},
		{
			"space",
			"<sprite=69 tint=1>"
		},
		{
			"semicolon",
			"<sprite=85 tint=1>"
		},
		{
			"quote",
			"<sprite=100 tint=1>"
		},
		{
			"comma",
			"<sprite=87 tint=1>"
		},
		{
			"period",
			"<sprite=88 tint=1>"
		},
		{
			"slash",
			"<sprite=76 tint=1>"
		},
		{
			"backslash",
			"<sprite=84 tint=1>"
		},
		{
			"insert",
			"<sprite=70 tint=1>"
		},
		{
			"delete",
			"<sprite=71 tint=1>"
		},
		{
			"home",
			"<sprite=72 tint=1>"
		},
		{
			"end",
			"<sprite=73 tint=1>"
		},
		{
			"pageUp",
			"<sprite=74 tint=1>"
		},
		{
			"pageDown",
			"<sprite=75 tint=1>"
		},
		{
			"upArrow",
			"<sprite=56 tint=1>"
		},
		{
			"downArrow",
			"<sprite=58 tint=1>"
		},
		{
			"leftArrow",
			"<sprite=59 tint=1>"
		},
		{
			"rightArrow",
			"<sprite=57 tint=1>"
		},
		{
			"numpad0",
			"<sprite=127 tint=1>"
		},
		{
			"numpad1",
			"<sprite=128 tint=1>"
		},
		{
			"numpad2",
			"<sprite=129 tint=1>"
		},
		{
			"numpad3",
			"<sprite=130 tint=1>"
		},
		{
			"numpad4",
			"<sprite=131 tint=1>"
		},
		{
			"numpad5",
			"<sprite=132 tint=1>"
		},
		{
			"numpad6",
			"<sprite=133 tint=1>"
		},
		{
			"numpad7",
			"<sprite=134 tint=1>"
		},
		{
			"numpad8",
			"<sprite=135 tint=1>"
		},
		{
			"numpad9",
			"<sprite=136 tint=1>"
		},
		{
			"numpadPlus",
			"<sprite=119 tint=1>"
		},
		{
			"numpadMinus",
			"<sprite=118 tint=1>"
		},
		{
			"numpadDivide",
			"<sprite=120 tint=1>"
		},
		{
			"numpadMultiply",
			"<sprite=121 tint=1>"
		},
		{
			"numpadEnter",
			"<sprite=122 tint=1>"
		},
		{
			"numpadPeriod",
			"<sprite=123 tint=1>"
		},
		{
			"capsLock",
			"<sprite=52 tint=1>"
		},
		{
			"backspace",
			"<sprite=67 tint=1>"
		},
		{
			"enter",
			"<sprite=68 tint=1>"
		},
		{
			"esc",
			"<sprite=54 tint=1>"
		}
	};

	// Token: 0x040007A0 RID: 1952
	private Dictionary<string, string> inputPathToSpriteTagMouse = new Dictionary<string, string>
	{
		{
			"down",
			"<sprite=112 tint=1>"
		},
		{
			"up",
			"<sprite=112 tint=1>"
		},
		{
			"scroll",
			"<sprite=112 tint=1>"
		},
		{
			"leftButton",
			"<sprite=109 tint=1>"
		},
		{
			"rightButton",
			"<sprite=110 tint=1>"
		},
		{
			"middleButton",
			"<sprite=111 tint=1>"
		}
	};

	// Token: 0x02000447 RID: 1095
	public enum InputAction
	{
		// Token: 0x0400184F RID: 6223
		Interact,
		// Token: 0x04001850 RID: 6224
		HoldInteract,
		// Token: 0x04001851 RID: 6225
		UsePrimary,
		// Token: 0x04001852 RID: 6226
		UseSecondary,
		// Token: 0x04001853 RID: 6227
		Scroll,
		// Token: 0x04001854 RID: 6228
		Throw,
		// Token: 0x04001855 RID: 6229
		Drop,
		// Token: 0x04001856 RID: 6230
		Slot1,
		// Token: 0x04001857 RID: 6231
		Slot2,
		// Token: 0x04001858 RID: 6232
		Slot3,
		// Token: 0x04001859 RID: 6233
		Slot4,
		// Token: 0x0400185A RID: 6234
		SpectateLeft,
		// Token: 0x0400185B RID: 6235
		SpectateRight,
		// Token: 0x0400185C RID: 6236
		Move,
		// Token: 0x0400185D RID: 6237
		Aim,
		// Token: 0x0400185E RID: 6238
		Sprint,
		// Token: 0x0400185F RID: 6239
		Jump,
		// Token: 0x04001860 RID: 6240
		Crouch,
		// Token: 0x04001861 RID: 6241
		Ping,
		// Token: 0x04001862 RID: 6242
		SlotLeft,
		// Token: 0x04001863 RID: 6243
		SlotRight,
		// Token: 0x04001864 RID: 6244
		DeselectSlot,
		// Token: 0x04001865 RID: 6245
		Emote,
		// Token: 0x04001866 RID: 6246
		PushToTalk,
		// Token: 0x04001867 RID: 6247
		TabLeft,
		// Token: 0x04001868 RID: 6248
		TabRight,
		// Token: 0x04001869 RID: 6249
		MoveForward,
		// Token: 0x0400186A RID: 6250
		MoveBackward,
		// Token: 0x0400186B RID: 6251
		MoveLeft,
		// Token: 0x0400186C RID: 6252
		MoveRight,
		// Token: 0x0400186D RID: 6253
		ScrollForward,
		// Token: 0x0400186E RID: 6254
		ScrollBackward,
		// Token: 0x0400186F RID: 6255
		Pause
	}
}
