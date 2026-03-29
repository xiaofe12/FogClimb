using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Zorro.Core.CLI.ParsableTypes;
using Zorro.Core.Serizalization;
using Zorro.Core.SmallShadows;
using Zorro.UI.Modal;

namespace Zorro.Core.CLI
{
	// Token: 0x020003A9 RID: 937
	public class ConsoleCommands
	{
		// Token: 0x04001694 RID: 5780
		public static List<ConsoleCommand> ConsoleCommandMethods = new List<ConsoleCommand>
		{
			new ConsoleCommand(new Action(AchievementManager.ClearAchievements).Method),
			new ConsoleCommand(new Action<int>(AchievementManager.GiveAscentLevel).Method),
			new ConsoleCommand(new Action<ACHIEVEMENTTYPE>(AchievementManager.Grant).Method),
			new ConsoleCommand(new Action(Ascents.LockAll).Method),
			new ConsoleCommand(new Action(Ascents.UnlockAll).Method),
			new ConsoleCommand(new Action(Ascents.UnlockOne).Method),
			new ConsoleCommand(new Action(Backpack.PrintBackpacks).Method),
			new ConsoleCommand(new Action(Character.Die).Method),
			new ConsoleCommand(new Action(Character.GainFullStamina).Method),
			new ConsoleCommand(new Action(Character.InfiniteStamina).Method),
			new ConsoleCommand(new Action(Character.LockStatuses).Method),
			new ConsoleCommand(new Action(Character.PassOut).Method),
			new ConsoleCommand(new Action(Character.Revive).Method),
			new ConsoleCommand(new Action(Character.TestWarp).Method),
			new ConsoleCommand(new Action(Character.TestWin).Method),
			new ConsoleCommand(new Action(Character.WarpToSpawn).Method),
			new ConsoleCommand(new Action(Character.Zombify).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.AddCold).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.AddCurse).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.AddDrowsy).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.AddHot).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.AddHunger).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.AddInjury).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.AddPoison).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.AddSpores).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.AddTinyHunger).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.ClearAll).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.ClearAllAilments).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.ClearCold).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.ClearCurse).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.ClearDrowsy).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.ClearHot).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.ClearHunger).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.ClearInjury).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.ClearPoison).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.Die).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.GetThorned).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.GetUnThorned).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.Hungry).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.Starve).Method),
			new ConsoleCommand(new Action(CharacterAfflictions.TestExactStatus).Method),
			new ConsoleCommand(new Action(CharacterCustomization.Randomize).Method),
			new ConsoleCommand(new Action<Item>(ItemDatabase.Add).Method),
			new ConsoleCommand(new Action(MapDebugUI.IncrementLevel).Method),
			new ConsoleCommand(new Action(MapDebugUI.ToggleDebugText).Method),
			new ConsoleCommand(new Action<Segment>(MapHandler.JumpToSegment).Method),
			new ConsoleCommand(new Action(PassportManager.TestAllCosmetics).Method),
			new ConsoleCommand(new Action<Player>(Player.PrintInventory).Method),
			new ConsoleCommand(new Action<int>(ApplicationCLI.SetTargetFramerate).Method),
			new ConsoleCommand(new Action(ConsoleSettings.Clear).Method),
			new ConsoleCommand(new Action(ConsoleSettings.ClearMuted).Method),
			new ConsoleCommand(new Action(ConsoleSettings.Pause).Method),
			new ConsoleCommand(new Action<float>(ConsoleSettings.SetDPI).Method),
			new ConsoleCommand(new Action(ConsoleSettings.Unpause).Method),
			new ConsoleCommand(new Func<ScriptPath, Task>(Script.Execute).Method),
			new ConsoleCommand(new Action(IBinarySerializable.EnableLog).Method),
			new ConsoleCommand(new Action(SmallShadowHandler.DebugDisable).Method),
			new ConsoleCommand(new Action(SmallShadowHandler.DebugEnable).Method),
			new ConsoleCommand(new Action(Modal.TestModal).Method)
		};

		// Token: 0x04001695 RID: 5781
		public static Dictionary<Type, CLITypeParser> TypeParsers = new Dictionary<Type, CLITypeParser>
		{
			{
				typeof(ACHIEVEMENTTYPE),
				new AchievementCLIParser()
			},
			{
				typeof(Item),
				new ItemCLIParser()
			},
			{
				typeof(bool),
				new BoolCLIParser()
			},
			{
				typeof(byte),
				new ByteCLIParser()
			},
			{
				typeof(float),
				new FloatCLIParser()
			},
			{
				typeof(int),
				new IntCLIParser()
			},
			{
				typeof(ScriptPath),
				new ScriptPathCLIParser()
			},
			{
				typeof(ushort),
				new UShortCLIParser()
			}
		};
	}
}
