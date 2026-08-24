using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zorro.Core;


namespace FogColdControl;

// FogClimb subsystem: Status
public sealed partial class Plugin
{


	private bool IsLateGameFogColdSuppressionActive()
	{
		if (!IsGameplayFogScene(SceneManager.GetActiveScene()) || LoadingScreenHandler.loading)
		{
			return _activeSyntheticFogSegmentId >= 3;
		}
		if (Singleton<MapHandler>.Instance != null && (int)MapHandler.CurrentSegmentNumber >= 3)
		{
			return true;
		}
		return _activeSyntheticFogSegmentId >= 3;
	}


	private void SyncRemoteStatusSuppressionIfNeeded()
	{
		if (!ShouldSuppressHostManagedStatusDamage() || !HasRemotePlayers() || !PhotonNetwork.IsMasterClient)
		{
			_remoteFogSuppressionDebt.Clear();
			_remoteFogSporesSuppressionDebt.Clear();
			return;
		}
		float syncInterval = GetRemoteStatusSyncIntervalSeconds();
		float now = Time.unscaledTime;
		float elapsed = ((_lastRemoteStatusSyncTime < 0f) ? syncInterval : (now - _lastRemoteStatusSyncTime));
		if (elapsed < syncInterval)
		{
			return;
		}
		_lastRemoteStatusSyncTime = now;
		HashSet<int> activeKeys = new HashSet<int>();
		foreach (Character character in Character.AllCharacters)
		{
			if (!ShouldSendRemoteStatusSuppression(character))
			{
				ForgetRemoteStatusSuppression(character);
				continue;
			}
			activeKeys.Add(GetRemoteStatusSuppressionKey(character));
			float[] deltas = BuildRemoteStatusSuppressionPayload(character, elapsed);
			if (deltas != null)
			{
				try
				{
					character.photonView.RPC("RPC_ApplyStatusesFromFloatArray", character.photonView.Owner, deltas);
				}
				catch (Exception ex)
				{
					base.Logger.LogDebug("[Fog&ColdControl] Remote status suppression skipped for " + character.characterName + ": " + ex.Message);
				}
			}
		}
		int[] array = _remoteFogSuppressionDebt.Keys.Where((int key) => !activeKeys.Contains(key)).ToArray();
		foreach (int staleKey in array)
		{
			_remoteFogSuppressionDebt.Remove(staleKey);
		}
		array = _remoteFogSporesSuppressionDebt.Keys.Where((int key) => !activeKeys.Contains(key)).ToArray();
		foreach (int staleKey2 in array)
		{
			_remoteFogSporesSuppressionDebt.Remove(staleKey2);
		}
	}


	private bool ShouldDisableNightColdInCurrentStage()
	{
		float currentNormalizedTime;
		if (ShouldDisableNightColdFeatureLocally() && IsNightColdStageActive())
		{
			return TryIsNightTime(out currentNormalizedTime);
		}
		return false;
	}


	private bool IsNightColdStageActive()
	{
		if (!LoadingScreenHandler.loading && IsGameplayFogScene(SceneManager.GetActiveScene()))
		{
			return true;
		}
		if (_activeSyntheticFogSegmentId < 0)
		{
			return GameHandler.IsOnIsland;
		}
		return true;
	}


	private void SuppressHostManagedStatusesIfNeeded()
	{
		if (!ShouldSuppressHostManagedStatusDamage())
		{
			return;
		}
		foreach (Character character in Character.AllCharacters)
		{
			if (!(character?.data == null) && !character.data.dead && !character.isBot && !character.isZombie && character.refs != null && !(character.refs.afflictions == null))
			{
				bool isInsideFog = IsCharacterInsideAnyFog(character);
				bool suppressFogCold = ShouldSuppressFogColdDamage() && isInsideFog;
				bool num = ShouldSuppressConfiguredNightCold(character) && (suppressFogCold || !isInsideFog);
				bool preserveLateGameNoCold = ShouldPreserveVanillaLateGameNoCold() && isInsideFog;
				if (num || preserveLateGameNoCold)
				{
					SetStatusWithoutChunkRemoval(character.refs.afflictions, GetFogSuppressionStatusType(character), 0f);
				}
				if (suppressFogCold)
				{
					SetStatusWithoutChunkRemoval(character.refs.afflictions, CharacterAfflictions.STATUSTYPE.Spores, 0f);
				}
			}
		}
	}


	private static bool SetStatusWithoutChunkRemoval(CharacterAfflictions afflictions, CharacterAfflictions.STATUSTYPE statusType, float value)
	{
		if (afflictions == null)
		{
			return false;
		}
		if (statusType < CharacterAfflictions.STATUSTYPE.Injury || (int)statusType >= StatusTypeCount)
		{
			return false;
		}
		float clampedValue = Mathf.Max(value, 0f);
		if (IsOwnedAfflictionsInstance(afflictions))
		{
			afflictions.SetStatus(statusType, clampedValue, pushStatus: false);
			return true;
		}
		return TrySetStatusArrayValue(CharacterAfflictionsCurrentStatusesField, afflictions, (int)statusType, clampedValue) | TrySetStatusArrayValue(CharacterAfflictionsCurrentIncrementalStatusesField, afflictions, (int)statusType, 0f) | TrySetStatusArrayValue(CharacterAfflictionsCurrentDecrementalStatusesField, afflictions, (int)statusType, 0f);
	}


	private static bool TrySetStatusArrayValue(FieldInfo field, CharacterAfflictions afflictions, int index, float value)
	{
		if (field == null || afflictions == null || !(field.GetValue(afflictions) is float[] values) || index < 0 || index >= values.Length)
		{
			return false;
		}
		if (Mathf.Approximately(values[index], value))
		{
			return false;
		}
		values[index] = value;
		return true;
	}


	private static bool ShouldSendRemoteStatusSuppression(Character character)
	{
		if (ShouldSuppressHostManagedStatusDamage() && HasFogAuthority() && character != null && character.photonView != null && character.photonView.Owner != null && !character.photonView.IsMine && !character.isBot && !character.isZombie && character.data != null && !character.data.dead)
		{
			if (!(Instance == null))
			{
				return Instance.IsCharacterPastJoinGrace(character);
			}
			return true;
		}
		return false;
	}


	private static float GetRemoteStatusSyncIntervalSeconds()
	{
		if (!ShouldPreserveVanillaLateGameNoCold())
		{
			return 0.25f;
		}
		return 0.1f;
	}


	private float[] BuildRemoteStatusSuppressionPayload(Character character, float elapsed)
	{
		if (!ShouldSuppressHostManagedStatusDamage())
		{
			if (character != null)
			{
				int remoteKey = GetRemoteStatusSuppressionKey(character);
				_remoteFogSuppressionDebt.Remove(remoteKey);
				_remoteFogSporesSuppressionDebt.Remove(remoteKey);
			}
			return null;
		}
		if (character?.data == null)
		{
			return null;
		}
		int playerKey = GetRemoteStatusSuppressionKey(character);
		float storedColdDebt;
		float pendingColdDebt = (_remoteFogSuppressionDebt.TryGetValue(playerKey, out storedColdDebt) ? storedColdDebt : 0f);
		float storedSporeDebt;
		float pendingSporeDebt = (_remoteFogSporesSuppressionDebt.TryGetValue(playerKey, out storedSporeDebt) ? storedSporeDebt : 0f);
		if (TryGetCurrentStatusSuppressionRates(character, out var coldRatePerSecond, out var sporeRatePerSecond))
		{
			float scaledElapsed = Mathf.Max(elapsed, 0f);
			pendingColdDebt += Mathf.Max(coldRatePerSecond, 0f) * scaledElapsed;
			pendingSporeDebt += Mathf.Max(sporeRatePerSecond, 0f) * scaledElapsed;
		}
		if (pendingColdDebt <= 0f && pendingSporeDebt <= 0f)
		{
			_remoteFogSuppressionDebt.Remove(playerKey);
			_remoteFogSporesSuppressionDebt.Remove(playerKey);
			return null;
		}
		float[] payload = new float[StatusTypeCount];
		bool hasPayload = false;
		if (pendingColdDebt > 0f)
		{
			CharacterAfflictions.STATUSTYPE statusType = GetFogSuppressionStatusType(character);
			float coldTransferAmount = GetSuppressionTransferAmount(pendingColdDebt);
			_remoteFogSuppressionDebt[playerKey] = Mathf.Max(pendingColdDebt - coldTransferAmount, 0f);
			if (_remoteFogSuppressionDebt[playerKey] <= 0.0001f)
			{
				_remoteFogSuppressionDebt.Remove(playerKey);
			}
			if (coldTransferAmount > 0f)
			{
				payload[(int)statusType] = 0f - coldTransferAmount;
				hasPayload = true;
			}
		}
		else
		{
			_remoteFogSuppressionDebt.Remove(playerKey);
		}
		if (pendingSporeDebt > 0f)
		{
			float sporeTransferAmount = GetSuppressionTransferAmount(pendingSporeDebt);
			_remoteFogSporesSuppressionDebt[playerKey] = Mathf.Max(pendingSporeDebt - sporeTransferAmount, 0f);
			if (_remoteFogSporesSuppressionDebt[playerKey] <= 0.0001f)
			{
				_remoteFogSporesSuppressionDebt.Remove(playerKey);
			}
			if (sporeTransferAmount > 0f && 10 < payload.Length)
			{
				payload[10] -= sporeTransferAmount;
				hasPayload = true;
			}
		}
		else
		{
			_remoteFogSporesSuppressionDebt.Remove(playerKey);
		}
		if (!hasPayload)
		{
			return null;
		}
		return payload;
	}


	private bool TryGetCurrentStatusSuppressionRates(Character character, out float coldRatePerSecond, out float sporeRatePerSecond)
	{
		coldRatePerSecond = 0f;
		sporeRatePerSecond = 0f;
		if (!ShouldSuppressHostManagedStatusDamage() || character?.data == null)
		{
			return false;
		}
		float fogBaseRatePerSecond = 0f;
		float fogSporeRatePerSecond = 0f;
		if (ShouldSuppressFogColdDamage() && IsCharacterInsideFogSphere(character))
		{
			fogBaseRatePerSecond += 0.0105f;
			fogSporeRatePerSecond += 0.005f;
		}
		if (ShouldSuppressFogColdDamage() && IsCharacterInsideLegacyFog(character, out var legacyRate))
		{
			fogBaseRatePerSecond += legacyRate;
			fogSporeRatePerSecond = Mathf.Max(fogSporeRatePerSecond, (legacyRate > 0f) ? legacyRate : 0.005f);
		}
		if (fogBaseRatePerSecond > 0f)
		{
			coldRatePerSecond += ConvertColdBaseRateToSuppressionRate(character, fogBaseRatePerSecond) * 1.12f;
		}
		if (fogSporeRatePerSecond > 0f)
		{
			sporeRatePerSecond += fogSporeRatePerSecond * 1.12f;
		}
		if (ShouldSuppressConfiguredNightCold(character))
		{
			coldRatePerSecond += ConvertColdBaseRateToSuppressionRate(character, GetCurrentNightColdBaseRatePerSecond());
		}
		if (!(coldRatePerSecond > 0f))
		{
			return sporeRatePerSecond > 0f;
		}
		return true;
	}


	private static float GetCurrentNightColdBaseRatePerSecond()
	{
		float nightFactor = 1f;
		if (TryGetIsDayFactor(DayNightManager.instance ?? UnityEngine.Object.FindAnyObjectByType<DayNightManager>(), out var isDayFactor))
		{
			nightFactor = Mathf.Clamp01(1f - isDayFactor);
		}
		return Mathf.Max(Ascents.nightColdRate, 0.008f) * nightFactor;
	}


	private static float ConvertColdBaseRateToSuppressionRate(Character character, float baseRatePerSecond)
	{
		if (character?.data == null || baseRatePerSecond <= 0f)
		{
			return 0f;
		}
		float statusRatePerSecond = Mathf.Max(baseRatePerSecond, 0f);
		if (character.data.isSkeleton)
		{
			statusRatePerSecond /= 8f;
		}
		return statusRatePerSecond * GetCurrentColdDifficultyMultiplier();
	}


	private static float GetCurrentColdDifficultyMultiplier()
	{
		try
		{
			return Mathf.Max(Ascents.etcDamageMultiplier, 0f);
		}
		catch
		{
			return 1f;
		}
	}


	private string GetNightColdUiEntryText(bool isChineseLanguage)
	{
		bool enabled = IsNightColdFeatureEnabled();
		string label = isChineseLanguage ? "夜晚寒冷" : "Night Cold";
		string state = enabled ? "ON" : "OFF";
		string stateColor = enabled ? "#9FFFA8" : "#FFB3B3";
		return Colorize(label, "#E2EAF3") + " " + Colorize(state, stateColor);
	}


	private static string GetNightColdUiLabel(bool isChineseLanguage)
	{
		string label = (isChineseLanguage ? "夜晚寒冷:" : "Night Cold:");
		if (IsNightColdFeatureEnabled())
		{
			if (!isChineseLanguage)
			{
				return Colorize(label, "#B7C0CC") + " " + Colorize("Night Cold On", "#9FFFA8");
			}
			return Colorize(label, "#B7C0CC") + " " + Colorize("夜晚寒冷开启", "#9FFFA8");
		}
		if (!isChineseLanguage)
		{
			return Colorize(label, "#B7C0CC") + " " + Colorize("Night Cold Off", "#FFB3B3");
		}
		return Colorize(label, "#B7C0CC") + " " + Colorize("夜晚寒冷关闭", "#FFB3B3");
	}


	private Sprite ResolveNativeStatusIconSprite(CharacterAfflictions.STATUSTYPE statusType)
	{
		if (statusType == CharacterAfflictions.STATUSTYPE.Poison && _nativePoisonIconSprite != null)
		{
			return _nativePoisonIconSprite;
		}
		if (statusType == CharacterAfflictions.STATUSTYPE.Hot && _nativeHotIconSprite != null)
		{
			return _nativeHotIconSprite;
		}
		if (statusType == CharacterAfflictions.STATUSTYPE.Drowsy && _nativeDrowsyIconSprite != null)
		{
			return _nativeDrowsyIconSprite;
		}
		if (statusType == CharacterAfflictions.STATUSTYPE.Petrify && _nativePetrifyIconSprite != null)
		{
			return _nativePetrifyIconSprite;
		}
		StaminaBar staminaBar = TryAcquireStaminaBar();
		if (staminaBar == null || staminaBar.afflictions == null)
		{
			return null;
		}
		if (statusType == CharacterAfflictions.STATUSTYPE.Petrify && staminaBar.petrifyAffliction != null && staminaBar.petrifyAffliction.icon != null && staminaBar.petrifyAffliction.icon.sprite != null)
		{
			_nativePetrifyIconSprite = staminaBar.petrifyAffliction.icon.sprite;
			return _nativePetrifyIconSprite;
		}
		try
		{
			foreach (BarAffliction affliction in staminaBar.afflictions)
			{
				if (affliction != null && affliction.afflictionType == statusType && affliction.icon != null && affliction.icon.sprite != null)
				{
					if (statusType == CharacterAfflictions.STATUSTYPE.Poison)
					{
						_nativePoisonIconSprite = affliction.icon.sprite;
					}
					else if (statusType == CharacterAfflictions.STATUSTYPE.Hot)
					{
						_nativeHotIconSprite = affliction.icon.sprite;
					}
					else if (statusType == CharacterAfflictions.STATUSTYPE.Drowsy)
					{
						_nativeDrowsyIconSprite = affliction.icon.sprite;
					}
					else if (statusType == CharacterAfflictions.STATUSTYPE.Petrify)
					{
						_nativePetrifyIconSprite = affliction.icon.sprite;
					}
					return affliction.icon.sprite;
				}
			}
		}
		catch
		{
		}
		return null;
	}


	internal static bool IsFogColdSuppressionEnabled()
	{
		if (FogColdSuppression != null)
		{
			return FogColdSuppression.Value;
		}
		return true;
	}


	internal static bool IsNightColdFeatureEnabled()
	{
		if (NightColdEnabled != null)
		{
			return NightColdEnabled.Value;
		}
		return true;
	}


	internal static bool ShouldDisableNightColdFeatureLocally()
	{
		if (HasFogAuthority() && IsModFeatureEnabled())
		{
			return !IsNightColdFeatureEnabled();
		}
		return false;
	}


	internal static bool ShouldSuppressFogColdLocally()
	{
		if (HasFogAuthority() && IsModFeatureEnabled())
		{
			return IsFogColdSuppressionEnabled();
		}
		return false;
	}


	internal static bool ShouldPreserveVanillaLateGameNoCold()
	{
		if (HasFogAuthority() && IsModFeatureEnabled() && Instance != null)
		{
			return Instance.IsLateGameFogColdSuppressionActive();
		}
		return false;
	}


	internal static bool ShouldSuppressFogColdDamage()
	{
		if (HasFogAuthority() && IsModFeatureEnabled())
		{
			if (!IsFogColdSuppressionEnabled())
			{
				return ShouldPreserveVanillaLateGameNoCold();
			}
			return true;
		}
		return false;
	}


	internal static bool ShouldSuppressHostManagedStatusDamage()
	{
		if (HasFogAuthority() && IsModFeatureEnabled())
		{
			if (!ShouldSuppressFogColdDamage())
			{
				if (Instance != null)
				{
					return Instance.ShouldDisableNightColdInCurrentStage();
				}
				return false;
			}
			return true;
		}
		return false;
	}


	internal static void BeginLocalFogStatusSource()
	{
		if (IsModFeatureEnabled())
		{
			_localFogStatusSourceDepth++;
		}
	}


	internal static void EndLocalFogStatusSource()
	{
		if (_localFogStatusSourceDepth > 0)
		{
			_localFogStatusSourceDepth--;
		}
	}


	internal static bool ShouldSuppressLocalFogSourceStatus(CharacterAfflictions afflictions, CharacterAfflictions.STATUSTYPE statusType)
	{
		if (afflictions == null)
		{
			return false;
		}
		Character character = afflictions.character;
		if (character == null || character != Character.localCharacter || character.isBot || character.isZombie || character.data == null)
		{
			return false;
		}
		bool isColdLikeStatus = (character.data.isSkeleton ? (statusType == CharacterAfflictions.STATUSTYPE.Injury) : (statusType == CharacterAfflictions.STATUSTYPE.Cold));
		bool isFogSporesStatus = statusType == CharacterAfflictions.STATUSTYPE.Spores;
		if (!isColdLikeStatus && !isFogSporesStatus)
		{
			return false;
		}
		if (_localFogStatusSourceDepth > 0)
		{
			return ShouldSuppressFogColdDamage();
		}
		if (isColdLikeStatus && Instance != null && Instance.ShouldDisableNightColdInCurrentStage())
		{
			return true;
		}
		return false;
	}


	internal static void FilterIncomingStatusArrayForLocalCharacter(Character character, float[] deltas)
	{
		if (Instance == null || character == null || deltas == null || character != Character.localCharacter || character.data == null || character.data.dead)
		{
			return;
		}
		bool suppressNightCold = Instance.ShouldDisableNightColdInCurrentStage();
		bool suppressFogCold = ShouldSuppressFogColdLocally() && Instance.IsCharacterInsideAnyFog(character);
		bool preserveLateGameNoCold = ShouldPreserveVanillaLateGameNoCold() && Instance.IsCharacterInsideAnyFog(character);
		if (!suppressNightCold && !suppressFogCold && !preserveLateGameNoCold)
		{
			return;
		}
		int coldLikeIndex = ((!character.data.isSkeleton) ? 2 : 0);
		if (coldLikeIndex >= 0 && coldLikeIndex < deltas.Length && deltas[coldLikeIndex] > 0f && (suppressNightCold || suppressFogCold || preserveLateGameNoCold))
		{
			deltas[coldLikeIndex] = 0f;
		}
		if (suppressFogCold)
		{
			int sporesIndex = 10;
			if (sporesIndex >= 0 && sporesIndex < deltas.Length && deltas[sporesIndex] > 0f)
			{
				deltas[sporesIndex] = 0f;
			}
		}
	}


	internal static bool TryApplyHostManagedIncomingStatusSuppression(CharacterAfflictions afflictions, float[] deltas, PhotonMessageInfo info)
	{
		if (Instance == null || afflictions == null || deltas == null || deltas.Length == 0 || !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient || !IsModFeatureEnabled())
		{
			return false;
		}
		if (info.Sender == null || !info.Sender.IsMasterClient || !TryReadInstallState(info.Sender, out var hostModEnabled) || !hostModEnabled)
		{
			return false;
		}
		Character character = afflictions.character;
		if (character == null || character != Character.localCharacter || character.data == null || character.data.dead || character.isBot || character.isZombie)
		{
			return false;
		}
		bool hasManagedReduction = false;
		for (int i = 0; i < deltas.Length; i++)
		{
			float delta = deltas[i];
			if (!(Mathf.Abs(delta) <= 0.0001f))
			{
				if (delta > 0f || !IsHostManagedStatusReductionIndex(character, i))
				{
					return false;
				}
				hasManagedReduction = true;
			}
		}
		if (!hasManagedReduction)
		{
			return false;
		}
		for (int j = 0; j < deltas.Length; j++)
		{
			float delta2 = deltas[j];
			if (!(delta2 >= -0.0001f))
			{
				CharacterAfflictions.STATUSTYPE statusType = (CharacterAfflictions.STATUSTYPE)j;
				float currentStatus = afflictions.GetCurrentStatus(statusType);
				SetStatusWithoutChunkRemoval(afflictions, statusType, Mathf.Max(currentStatus + delta2, 0f));
			}
		}
		return true;
	}


	private static bool IsHostManagedStatusReductionIndex(Character character, int statusIndex)
	{
		switch (statusIndex)
		{
		case 10:
			return true;
		case 2:
			return true;
		default:
			if (character != null && character.data != null && character.data.isSkeleton)
			{
				return statusIndex == 0;
			}
			return false;
		}
	}


	private static int GetRemoteStatusSuppressionKey(Character character)
	{
		return character?.photonView?.Owner?.ActorNumber ?? character?.GetInstanceID() ?? 0;
	}


	private void ForgetRemoteStatusSuppression(Character character)
	{
		int key = GetRemoteStatusSuppressionKey(character);
		if (key != 0)
		{
			_remoteFogSuppressionDebt.Remove(key);
			_remoteFogSporesSuppressionDebt.Remove(key);
		}
	}


	private static CharacterAfflictions.STATUSTYPE GetFogSuppressionStatusType(Character character)
	{
		if (!(character?.data != null) || !character.data.isSkeleton)
		{
			return CharacterAfflictions.STATUSTYPE.Cold;
		}
		return CharacterAfflictions.STATUSTYPE.Injury;
	}


	private static float GetSuppressionTransferAmount(float pendingDebt)
	{
		if (pendingDebt <= 0f)
		{
			return 0f;
		}
		float chunkTransfer = Mathf.Floor(pendingDebt / 0.025f) * 0.025f;
		if (chunkTransfer >= 0.025f)
		{
			return chunkTransfer;
		}
		if (!(pendingDebt < 0.025f))
		{
			return 0f;
		}
		return pendingDebt;
	}
}
