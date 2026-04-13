using System;
using HarmonyLib;

namespace FogClimb;

[HarmonyPatch(typeof(Fog), "MakePlayerCold")]
internal static class FogMakePlayerColdPatch
{
	private static bool Prefix()
	{
		return !Plugin.ShouldSuppressFogColdDamage();
	}
}

[HarmonyPatch(typeof(FogSphere), "SetSharderVars")]
internal static class FogSphereSetShaderVarsPatch
{
	private static void Prefix()
	{
		Plugin.BeginLocalFogStatusSuppression();
	}

	private static Exception Finalizer(Exception __exception)
	{
		Plugin.EndLocalFogStatusSuppression();
		return __exception;
	}
}

[HarmonyPatch(typeof(OrbFogHandler), "WaitToMove")]
internal static class OrbFogHandlerWaitToMovePatch
{
	private static bool Prefix(OrbFogHandler __instance)
	{
		return !Plugin.ShouldBlockVanillaOrbFogWait(__instance);
	}
}

[HarmonyPatch(typeof(OrbFogHandler), "Start")]
internal static class OrbFogHandlerStartPatch
{
	private static void Postfix(OrbFogHandler __instance)
	{
		Plugin.NotifyFogHandlerChanged(__instance);
	}
}

[HarmonyPatch(typeof(OrbFogHandler), "SetFogOrigin")]
internal static class OrbFogHandlerSetFogOriginPatch
{
	private static void Postfix(OrbFogHandler __instance)
	{
		Plugin.NotifyFogHandlerChanged(__instance);
	}
}

[HarmonyPatch(typeof(OrbFogHandler), "RPC_InitFog")]
internal static class OrbFogHandlerRpcInitFogPatch
{
	private static void Postfix(OrbFogHandler __instance)
	{
		Plugin.NotifyFogHandlerChanged(__instance);
	}
}

[HarmonyPatch(typeof(Ascents), "get_fogEnabled")]
internal static class AscentsFogEnabledPatch
{
	private static void Postfix(ref bool __result)
	{
		if (Plugin.ShouldForceFogCoverageEverywhere())
		{
			__result = true;
		}
	}
}

[HarmonyPatch(typeof(CharacterAfflictions), "AddStatus")]
internal static class CharacterAfflictionsAddStatusPatch
{
	private static bool Prefix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, ref bool __result)
	{
		if (!Plugin.ShouldSuppressLocalFogSourceStatus(__instance, statusType))
		{
			return true;
		}
		__result = false;
		return false;
	}
}

[HarmonyPatch(typeof(Campfire), "Light_Rpc")]
internal static class CampfireLightRpcPatch
{
	private static void Postfix(Campfire __instance)
	{
		Plugin.NotifyCampfireLit(__instance);
	}
}
