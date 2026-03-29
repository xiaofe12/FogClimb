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
	private static void Postfix()
	{
		Plugin.ClearLocalFogColdStatus();
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

[HarmonyPatch(typeof(CharacterAfflictions), "AddStatus")]
internal static class CharacterAfflictionsAddStatusPatch
{
	private static bool Prefix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, ref bool __result)
	{
		if (Plugin.IsInternalColdClearInProgress() || statusType != CharacterAfflictions.STATUSTYPE.Cold || !Plugin.ShouldSuppressAmbientColdDamageFor(__instance))
		{
			return true;
		}
		Plugin.EnforceAmbientColdSuppression(__instance);
		__result = false;
		return false;
	}
}

[HarmonyPatch(typeof(CharacterAfflictions), "SetStatus")]
internal static class CharacterAfflictionsSetStatusPatch
{
	private static void Prefix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, ref float amount)
	{
		if (!Plugin.IsInternalColdClearInProgress() && statusType == CharacterAfflictions.STATUSTYPE.Cold && Plugin.ShouldSuppressAmbientColdDamageFor(__instance))
		{
			amount = 0f;
		}
	}

	private static void Postfix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType)
	{
		if (!Plugin.IsInternalColdClearInProgress() && statusType == CharacterAfflictions.STATUSTYPE.Cold && Plugin.ShouldSuppressAmbientColdDamageFor(__instance))
		{
			Plugin.EnforceAmbientColdSuppression(__instance);
		}
	}
}

[HarmonyPatch(typeof(CharacterAfflictions), "ApplyStatusesFromFloatArray")]
internal static class CharacterAfflictionsApplyStatusesFromFloatArrayPatch
{
	private static void Postfix(CharacterAfflictions __instance)
	{
		if (Plugin.ShouldSuppressAmbientColdDamageFor(__instance))
		{
			Plugin.EnforceAmbientColdSuppression(__instance);
		}
	}
}

[HarmonyPatch(typeof(CharacterAfflictions), "RPC_ApplyStatusesFromFloatArray")]
internal static class CharacterAfflictionsRpcApplyStatusesFromFloatArrayPatch
{
	private static void Postfix(CharacterAfflictions __instance)
	{
		if (Plugin.ShouldSuppressAmbientColdDamageFor(__instance))
		{
			Plugin.EnforceAmbientColdSuppression(__instance);
		}
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
