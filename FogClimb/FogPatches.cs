using System;
using HarmonyLib;

namespace FogColdControl;

[HarmonyPatch(typeof(Fog), "MakePlayerCold")]
internal static class FogMakePlayerColdPatch
{
	private static bool Prefix()
	{
		if (Plugin.ShouldSuppressFogColdDamage())
		{
			return false;
		}
		Plugin.BeginLocalFogStatusSource();
		return true;
	}

	private static Exception Finalizer(Exception __exception)
	{
		Plugin.EndLocalFogStatusSource();
		return __exception;
	}
}

[HarmonyPatch(typeof(FogSphere), "SetSharderVars")]
internal static class FogSphereSetShaderVarsPatch
{
	private static void Prefix()
	{
		Plugin.BeginLocalFogStatusSource();
	}

	private static Exception Finalizer(Exception __exception)
	{
		Plugin.EndLocalFogStatusSource();
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

[HarmonyPatch(typeof(Ascents), "get_isNightCold")]
internal static class AscentsNightColdPatch
{
	private static void Postfix(ref bool __result)
	{
		if (Plugin.ShouldDisableNightColdFeatureLocally())
		{
			__result = false;
		}
	}
}

// 按名称解析唯一的 AddStatus 重载，避免游戏更新增减参数（如 2026-08 新增 ignoreInvincibility）导致补丁目标失效。
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

[HarmonyPatch(typeof(CharacterAfflictions), "RPC_ApplyStatusesFromFloatArray")]
internal static class CharacterAfflictionsRpcApplyStatusesFromFloatArrayPatch
{
	private static bool Prefix(CharacterAfflictions __instance, float[] data, Photon.Pun.PhotonMessageInfo info)
	{
		return !Plugin.TryApplyHostManagedIncomingStatusSuppression(__instance, data, info);
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
