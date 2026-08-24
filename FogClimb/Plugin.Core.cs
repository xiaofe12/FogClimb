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

// FogClimb subsystem: Core
public sealed partial class Plugin
{


	internal static Plugin Instance { get; private set; }


	private void Awake()
	{
		Instance = this;
		_lastHadFogAuthority = HasFogAuthority();
		TryCleanupGeneratedBackupFile();
		TryCleanupLegacyPluginFile();
		EnsurePreferredConfigFile();
		_lastDetectedChineseLanguage = DetectChineseLanguage();
		InitializeConfig(_lastDetectedChineseLanguage);
		RegisterConfigChangeHandlers();
		_lastModEnabledState = IsModFeatureEnabled();
		_lastFogUiEnabledState = FogUiEnabled?.Value ?? true;
		_lastCampfireLocatorUiEnabledState = CampfireLocatorUiEnabled?.Value ?? true;
		_lastFogUiX = FogUiX?.Value ?? 60f;
		_lastFogUiY = FogUiY?.Value ?? 0f;
		_lastFogUiScale = FogUiScale?.Value ?? 0.9f;
		_lastFogUiLayout = FogUiLayout?.Value ?? FogUiLayoutKind.Horizontal;
		MarkConfigFileLocalizationDirty(saveConfigFile: true);
		_harmony = new Harmony("com.github.Thanks.FogClimb");
		// 逐个补丁类应用而非 PatchAll：游戏更新改变某个目标签名时仅跳过该补丁并记录错误，不影响其余补丁与初始化流程。
		foreach (Type patchType in AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly()))
		{
			try
			{
				new PatchClassProcessor(_harmony, patchType).Patch();
			}
			catch (Exception ex)
			{
				base.Logger.LogError($"[Fog&ColdControl] Harmony patch failed on '{patchType.Name}' (game update may have changed its target): {ex.Message}");
			}
		}
		SceneManager.sceneLoaded += OnSceneLoaded;
		base.Logger.LogInfo("[Fog&ColdControl] Loaded.");
	}


	private void OnDestroy()
	{
		UnregisterConfigChangeHandlers();
		SceneManager.sceneLoaded -= OnSceneLoaded;
		_harmony?.UnpatchSelf();
		CleanupFogUi();
		CleanupCampfireLocatorUi();
		CleanupCompassLobbyNotice();
		if (Instance == this)
		{
			Instance = null;
		}
	}


	private static bool Approximately(float left, float right)
	{
		return Mathf.Abs(left - right) < 0.001f;
	}


	private void Update()
	{
		bool modEnabled = IsModFeatureEnabled();
		HandleRoomChangeIfNeeded(modEnabled);
		HandleAuthorityChangeIfNeeded(modEnabled);
		if (modEnabled != _lastModEnabledState)
		{
			_lastModEnabledState = modEnabled;
			if (!modEnabled)
			{
				RestoreVanillaFogSpeed();
				ResetFogRuntimeState();
				SetFogUiVisible(visible: false);
			}
			else
			{
				_fogStateInitialized = false;
				_lastFogStateSyncTime = -0.18f;
				_lastRemoteStatusSyncTime = -0.25f;
				_lastCompassGrantSyncTime = -0.75f;
			}
		}
		TryResolveRuntimeObjects();
		if (modEnabled)
		{
			EnsureFogCoverageAcrossAllLevels();
		}
		UpdateLocalInstallStateAdvertisement();
		HandleLanguageChangeIfNeeded();
		HandlePendingConfigFileLocalizationRefresh();
		HandleFogUiConfigChanges();
		HandleManualCompassHotkey();
		HandleFogPauseHotkey();
		HandleHiddenNightTestHotkey();
		RefreshRemotePlayerJoinGraceState();
		HandlePendingWeatherStateBroadcast();
		ProcessPendingCampfireCompassGrants();
		if (!modEnabled)
		{
			UpdateFogUi();
			UpdateCampfireLocatorUi();
			UpdateCompassLobbyNotice();
			return;
		}
		if (_orbFogHandler != null && !_fogStateInitialized)
		{
			InitializeFogRuntimeState(_orbFogHandler);
		}
		if (_orbFogHandler != null && HasFogAuthority())
		{
			TryRestoreCheckpointCampfireDelay();
			UpdateAuthorityFogMode();
			SyncFogStateToGuestsIfNeeded();
			UpdateFogSpeedAdvertisement();
			SyncRemoteStatusSuppressionIfNeeded();
			SyncCompassGrantsToPlayersIfNeeded();
		}
		SuppressHostManagedStatusesIfNeeded();
		UpdateSyncedHostFogSpeed();
		UpdateFogArrivalEstimate();
		UpdateFogUi();
		UpdateCampfireLocatorUi();
		UpdateCompassLobbyNotice();
	}


	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		ResetFogRuntimeState();
		_orbFogHandler = null;
		_fogSphere = null;
		_legacyFog = null;
		_compassItem = null;
		_cachedStormVisuals = null;
		_lastStormVisualScanTime = -999f;
		ResetWeatherHazardUiState();
		_pendingWeatherStateBroadcastTime = -1f;
		_weatherStateBroadcastRetryCount = 0;
		CleanupFogUi();
		CleanupCampfireLocatorUi();
		CleanupCompassLobbyNotice();
	}


	private static string GetCurrentRoomKey()
	{
		if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
		{
			return "offline";
		}
		string roomName = PhotonNetwork.CurrentRoom.Name ?? string.Empty;
		return "room:" + roomName;
	}


	private void TryResolveRuntimeObjects()
	{
		bool needFogHandler = _orbFogHandler == null;
		bool needFogSphere = _fogSphere == null;
		bool needLegacyFog = _legacyFog == null;
		if (!needFogHandler && !needFogSphere && !needLegacyFog)
		{
			return;
		}
		_fogHandlerSearchTimer -= Time.unscaledDeltaTime;
		if (_fogHandlerSearchTimer > 0f)
		{
			return;
		}
		_fogHandlerSearchTimer = 1f;
		if (needFogHandler)
		{
			_orbFogHandler = UnityEngine.Object.FindAnyObjectByType<OrbFogHandler>();
			if (_orbFogHandler != null)
			{
				_fogStateInitialized = false;
			}
		}
		if (needFogSphere)
		{
			_fogSphere = UnityEngine.Object.FindAnyObjectByType<FogSphere>();
		}
		if (needLegacyFog)
		{
			_legacyFog = UnityEngine.Object.FindAnyObjectByType<Fog>();
		}
	}


	private static bool TryResolveActiveSceneLavaRising(out LavaRising lavaRising)
	{
		lavaRising = null;
		if (!TryGetCurrentGameplaySegment(out var currentSegment))
		{
			return false;
		}
		float now = Time.realtimeSinceStartup;
		if (_cachedSceneLavaValid && _cachedSceneLavaRising != null && _cachedSceneLavaSegment == currentSegment && now - _cachedSceneLavaScanTime < 1f)
		{
			lavaRising = _cachedSceneLavaRising;
			return true;
		}
		LavaRising matched = null;
		try
		{
			if (LavaRising.ALL_LAVA != null)
			{
				foreach (LavaRising lava in LavaRising.ALL_LAVA)
				{
					if (lava == null || lava.ended || lava.lava == null || lava.topTransform == null)
					{
						continue;
					}
					if (lava.requiredSegment == currentSegment)
					{
						matched = lava;
						break;
					}
				}
			}
		}
		catch
		{
			matched = null;
		}
		if (matched == null)
		{
			MapHandler mapHandler = Singleton<MapHandler>.Instance;
			if (mapHandler != null && mapHandler.lavaRising != null && !mapHandler.lavaRising.ended && mapHandler.lavaRising.lava != null && mapHandler.lavaRising.topTransform != null)
			{
				matched = mapHandler.lavaRising;
			}
		}
		_cachedSceneLavaRising = matched;
		_cachedSceneLavaSegment = currentSegment;
		_cachedSceneLavaValid = matched != null;
		_cachedSceneLavaScanTime = now;
		lavaRising = matched;
		return matched != null;
	}


	private static Character TryGetObservedCharacter()
	{
		try
		{
			Character observedCharacter = MainCameraMovement.specCharacter;
			if (observedCharacter != null)
			{
				return observedCharacter;
			}
		}
		catch
		{
		}
		try
		{
			return Character.observedCharacter;
		}
		catch
		{
			return null;
		}
	}


	private static bool IsOwnedAfflictionsInstance(CharacterAfflictions afflictions)
	{
		try
		{
			return afflictions?.character != null && afflictions.character.photonView != null && afflictions.character.photonView.IsMine;
		}
		catch
		{
			return false;
		}
	}


	private static bool TryIsNightTime(out float currentNormalizedTime)
	{
		currentNormalizedTime = 0f;
		DayNightManager dayNightManager = DayNightManager.instance ?? UnityEngine.Object.FindAnyObjectByType<DayNightManager>();
		if (dayNightManager == null)
		{
			return false;
		}
		if (TryGetIsDayFactor(dayNightManager, out var isDayFactor))
		{
			currentNormalizedTime = Mathf.Clamp01(isDayFactor);
			return isDayFactor < 0.5f;
		}
		if (!TryGetCurrentDayNightTimeNormalized(dayNightManager, out currentNormalizedTime))
		{
			return false;
		}
		float dayStart = NormalizeDayNightHourValue(dayNightManager.dayStart);
		float dayEnd = NormalizeDayNightHourValue(dayNightManager.dayEnd);
		return !IsTimeWithinWrappedRange(currentNormalizedTime, dayStart, dayEnd);
	}


	private static bool TryGetDayNightDisplay(out bool isDay, out float phaseProgress, out string timeText, out int dayCount)
	{
		isDay = true;
		phaseProgress = 0f;
		timeText = string.Empty;
		dayCount = 0;
		DayNightManager dayNightManager = DayNightManager.instance ?? UnityEngine.Object.FindAnyObjectByType<DayNightManager>();
		if (dayNightManager == null || !TryGetCurrentDayNightTimeNormalized(dayNightManager, out var currentNormalizedTime))
		{
			return false;
		}
		float dayStart = NormalizeDayNightHourValue(dayNightManager.dayStart);
		float dayEnd = NormalizeDayNightHourValue(dayNightManager.dayEnd);
		isDay = IsTimeWithinWrappedRange(currentNormalizedTime, dayStart, dayEnd);
		phaseProgress = (isDay ? GetWrappedRangeProgress(currentNormalizedTime, dayStart, dayEnd) : GetWrappedRangeProgress(currentNormalizedTime, dayEnd, dayStart));
		if (!TryGetDayNightTimeString(dayNightManager, out timeText))
		{
			timeText = FormatDayNightTimeOfDay(dayNightManager.timeOfDay);
		}
		TryGetDayNightDayCount(dayNightManager, out dayCount);
		return true;
	}


	private static bool TryGetDayNightTimeString(DayNightManager dayNightManager, out string timeText)
	{
		timeText = string.Empty;
		if (dayNightManager == null)
		{
			return false;
		}
		try
		{
			if (dayNightManager.GetType().GetMethod("TimeOfDayString", InstanceBindingFlags, null, Type.EmptyTypes, null)?.Invoke(dayNightManager, null) is string rawText && !string.IsNullOrWhiteSpace(rawText))
			{
				timeText = rawText.Trim();
				return true;
			}
		}
		catch
		{
		}
		return false;
	}


	private static string FormatDayNightTimeOfDay(float timeOfDay)
	{
		float num = NormalizeDayNightHourOfDayValue(timeOfDay);
		int hours = Mathf.FloorToInt(num);
		int minutes = Mathf.RoundToInt((num - (float)hours) * 60f);
		if (minutes >= 60)
		{
			hours = (hours + 1) % 24;
			minutes = 0;
		}
		return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", hours, minutes);
	}


	private static bool TryGetRunElapsedSeconds(out float elapsedSeconds)
	{
		elapsedSeconds = 0f;
		if (!IsGameplayFogScene(SceneManager.GetActiveScene()) || LoadingScreenHandler.loading)
		{
			return false;
		}
		RunManager runManager = RunManager.Instance;
		if (runManager == null)
		{
			return false;
		}
		float runElapsedSeconds = Mathf.Max(runManager.TimeSinceRunStarted, 0f);
		bool startedValue;
		bool runStarted = (TryReadBooleanField(RunManagerRunStartedField, runManager, out startedValue) ? startedValue : (runElapsedSeconds > 0.05f));
		bool activeValue;
		bool timerActive = (TryReadBooleanField(RunManagerTimerActiveField, runManager, out activeValue) ? activeValue : runStarted);
		if (!runStarted && !timerActive && runElapsedSeconds <= 0.05f)
		{
			return false;
		}
		elapsedSeconds = runElapsedSeconds;
		return true;
	}


	private static bool TryReadBooleanField(FieldInfo fieldInfo, object target, out bool value)
	{
		value = false;
		if (fieldInfo == null || target == null)
		{
			return false;
		}
		try
		{
			if (fieldInfo.GetValue(target) is bool booleanValue)
			{
				value = booleanValue;
				return true;
			}
		}
		catch
		{
		}
		return false;
	}


	private static string FormatRunElapsedTime(float elapsedSeconds)
	{
		int num = Mathf.FloorToInt(Mathf.Max(elapsedSeconds, 0f));
		int minutes = num / 60;
		int seconds = num % 60;
		return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", minutes, seconds);
	}


	private static bool TryGetIsDayFactor(DayNightManager dayNightManager, out float isDayFactor)
	{
		isDayFactor = 0f;
		if (dayNightManager == null)
		{
			return false;
		}
		Type managerType = dayNightManager.GetType();
		if (TryReadSingleValue(managerType.GetProperty("isDay", InstanceBindingFlags)?.GetValue(dayNightManager), out var propertyValue))
		{
			isDayFactor = Mathf.Clamp01(propertyValue);
			return true;
		}
		if (TryReadSingleValue(managerType.GetField("isDay", InstanceBindingFlags)?.GetValue(dayNightManager), out var fieldValue))
		{
			isDayFactor = Mathf.Clamp01(fieldValue);
			return true;
		}
		return false;
	}


	private static bool IsTimeWithinWrappedRange(float value, float rangeStart, float rangeEnd)
	{
		value = Mathf.Repeat(value, 1f);
		rangeStart = Mathf.Repeat(rangeStart, 1f);
		rangeEnd = Mathf.Repeat(rangeEnd, 1f);
		if (Mathf.Abs(rangeStart - rangeEnd) < 0.0001f)
		{
			return true;
		}
		if (rangeStart < rangeEnd)
		{
			if (value >= rangeStart)
			{
				return value < rangeEnd;
			}
			return false;
		}
		if (!(value >= rangeStart))
		{
			return value < rangeEnd;
		}
		return true;
	}


	private static float GetWrappedRangeProgress(float value, float rangeStart, float rangeEnd)
	{
		value = Mathf.Repeat(value, 1f);
		rangeStart = Mathf.Repeat(rangeStart, 1f);
		rangeEnd = Mathf.Repeat(rangeEnd, 1f);
		float length = Mathf.Repeat(rangeEnd - rangeStart, 1f);
		if (length < 0.0001f)
		{
			return 1f;
		}
		return Mathf.Clamp01(Mathf.Repeat(value - rangeStart, 1f) / length);
	}


	private static bool TryGetCurrentDayNightTimeNormalized(DayNightManager dayNightManager, out float currentNormalizedTime)
	{
		currentNormalizedTime = 0f;
		if (dayNightManager == null)
		{
			return false;
		}
		Type managerType = dayNightManager.GetType();
		for (int i = 0; i < DayNightTimeMemberCandidates.Length; i++)
		{
			string memberName = DayNightTimeMemberCandidates[i];
			if (TryReadSingleValue(managerType.GetProperty(memberName, InstanceBindingFlags)?.GetValue(dayNightManager), out var propertyValue))
			{
				currentNormalizedTime = NormalizeDayNightMemberValue(memberName, propertyValue);
				return true;
			}
			if (TryReadSingleValue(managerType.GetField(memberName, InstanceBindingFlags)?.GetValue(dayNightManager), out var fieldValue))
			{
				currentNormalizedTime = NormalizeDayNightMemberValue(memberName, fieldValue);
				return true;
			}
		}
		return false;
	}


	private static bool TryGetDayNightDayCount(DayNightManager dayNightManager, out int dayCount)
	{
		dayCount = 0;
		if (dayNightManager == null)
		{
			return false;
		}
		Type managerType = dayNightManager.GetType();
		for (int i = 0; i < DayNightDayCountMemberCandidates.Length; i++)
		{
			string memberName = DayNightDayCountMemberCandidates[i];
			object fieldValue = managerType.GetField(memberName, InstanceBindingFlags)?.GetValue(dayNightManager);
			if (TryReadSingleValue(fieldValue, out var numericValue))
			{
				dayCount = Mathf.Max(Mathf.RoundToInt(numericValue), 0);
				return true;
			}
			object propValue = managerType.GetProperty(memberName, InstanceBindingFlags)?.GetValue(dayNightManager);
			if (TryReadSingleValue(propValue, out var propNumericValue))
			{
				dayCount = Mathf.Max(Mathf.RoundToInt(propNumericValue), 0);
				return true;
			}
		}
		return false;
	}


	private static float NormalizeDayNightMemberValue(string memberName, float rawValue)
	{
		if (string.Equals(memberName, "timeOfDayNormalized", StringComparison.Ordinal) || string.Equals(memberName, "timeNormalized", StringComparison.Ordinal))
		{
			return Mathf.Repeat(rawValue, 1f);
		}
		if (string.Equals(memberName, "timeOfDay", StringComparison.Ordinal))
		{
			return Mathf.Repeat(rawValue, 24f) / 24f;
		}
		return NormalizeDayNightHourValue(rawValue);
	}


	private static float NormalizeDayNightHourValue(float rawValue)
	{
		return NormalizeDayNightHourOfDayValue(rawValue) / 24f;
	}


	private static float NormalizeDayNightHourOfDayValue(float rawValue)
	{
		if (rawValue >= 0f && rawValue <= 1f)
		{
			return Mathf.Repeat(rawValue, 1f) * 24f;
		}
		return Mathf.Repeat(rawValue, 24f);
	}


	private static bool TryReadSingleValue(object rawValue, out float value)
	{
		value = 0f;
		if (!(rawValue is bool boolValue))
		{
			if (!(rawValue is float floatValue))
			{
				if (!(rawValue is double doubleValue))
				{
					if (rawValue is int intValue)
					{
						value = intValue;
						return true;
					}
					return false;
				}
				value = (float)doubleValue;
				return true;
			}
			value = floatValue;
			return true;
		}
		value = (boolValue ? 1f : 0f);
		return true;
	}


	private IEnumerable<Player> EnumerateTargetPlayers()
	{
		HashSet<int> yieldedPlayerIds = new HashSet<int>();
		if (PhotonNetwork.InRoom)
		{
			foreach (Player player in PlayerHandler.GetAllPlayers())
			{
				if (player != null && yieldedPlayerIds.Add(player.GetInstanceID()))
				{
					yield return player;
				}
			}
		}
		Player[] players = UnityEngine.Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
		Player[] array = players;
		foreach (Player player2 in array)
		{
			if (player2 != null && yieldedPlayerIds.Add(player2.GetInstanceID()))
			{
				yield return player2;
			}
		}
	}


	private bool IsPlayerPastJoinGrace(Player player)
	{
		if (player == null)
		{
			return false;
		}
		PhotonView photonView = player.GetComponent<PhotonView>();
		if (photonView?.Owner == null || photonView.IsMine)
		{
			return true;
		}
		if (!_remotePlayerFirstSeenTimes.TryGetValue(photonView.Owner.ActorNumber, out var firstSeenTime))
		{
			return true;
		}
		return Time.unscaledTime - firstSeenTime >= 8f;
	}


	private bool IsActorPastJoinGrace(int actorNumber)
	{
		if (actorNumber <= 0)
		{
			return false;
		}
		int localActorNumber = PhotonNetwork.LocalPlayer?.ActorNumber ?? (-1);
		if (actorNumber == localActorNumber)
		{
			return true;
		}
		if (!_remotePlayerFirstSeenTimes.TryGetValue(actorNumber, out var firstSeenTime))
		{
			return true;
		}
		return Time.unscaledTime - firstSeenTime >= 8f;
	}


	private void HandleHiddenNightTestHotkey()
	{
		if (!IsModFeatureEnabled() || !HasFogAuthority())
		{
			ResetHiddenNightTestHotkeyState();
			return;
		}
		if (!IsGameplayFogScene(SceneManager.GetActiveScene()) || LoadingScreenHandler.loading)
		{
			ResetHiddenNightTestHotkeyState();
			return;
		}
		GUIManager instance = GUIManager.instance;
		if (instance != null && instance.windowBlockingInput)
		{
			ResetHiddenNightTestHotkeyState();
		}
		else if (!Input.GetKey(KeyCode.LeftBracket))
		{
			ResetHiddenNightTestHotkeyState();
		}
		else if (!_hiddenNightTestTriggeredThisHold)
		{
			_hiddenNightTestHoldTimer += Time.unscaledDeltaTime;
			if (!(_hiddenNightTestHoldTimer < 5f))
			{
				_hiddenNightTestTriggeredThisHold = true;
				SwitchToNightForTesting();
			}
		}
	}


	private void ResetHiddenNightTestHotkeyState()
	{
		_hiddenNightTestHoldTimer = 0f;
		_hiddenNightTestTriggeredThisHold = false;
	}


	private void SwitchToNightForTesting()
	{
		DayNightManager dayNightManager = UnityEngine.Object.FindAnyObjectByType<DayNightManager>();
		if (dayNightManager == null)
		{
			base.Logger.LogDebug("[Fog&ColdControl] Hidden night-test hotkey skipped because DayNightManager is unavailable.");
			return;
		}
		float nightTime = CalculateNightTestTime(dayNightManager);
		try
		{
			dayNightManager.setTimeOfDay(nightTime);
			dayNightManager.UpdateCycle();
			PhotonView photonView = dayNightManager.GetComponent<PhotonView>();
			if (PhotonNetwork.InRoom && photonView != null)
			{
				photonView.RPC("RPCA_SyncTime", RpcTarget.Others, dayNightManager.dayCount, nightTime);
			}
			base.Logger.LogInfo(string.Format("[{0}] Hidden night-test hotkey forced night at {1:F3}{2}", "Fog&ColdControl", nightTime, PhotonNetwork.InRoom ? " and synced it to guests." : "."));
		}
		catch (Exception ex)
		{
			base.Logger.LogWarning("[Fog&ColdControl] Hidden night-test hotkey failed: " + ex.Message);
		}
	}


	private static float CalculateNightTestTime(DayNightManager dayNightManager)
	{
		if (dayNightManager == null)
		{
			return 20.400002f;
		}
		float num = NormalizeDayNightHourOfDayValue(dayNightManager.dayStart);
		float dayEnd = NormalizeDayNightHourOfDayValue(dayNightManager.dayEnd);
		float nightDuration = Mathf.Repeat(num - dayEnd, 24f);
		if (nightDuration < 0.01f)
		{
			return 20.400002f;
		}
		return Mathf.Repeat(dayEnd + nightDuration * 0.5f, 24f);
	}


	private static bool TryGetLocalPlayablePlayer(out Player localPlayer)
	{
		localPlayer = Player.localPlayer ?? Character.localCharacter?.player;
		Character character = localPlayer?.character ?? Character.localCharacter;
		if (localPlayer != null && character != null && !character.isBot)
		{
			return !character.isZombie;
		}
		return false;
	}


	private void HandleLanguageChangeIfNeeded()
	{
		bool isChineseLanguage = DetectChineseLanguage();
		if (isChineseLanguage != _lastDetectedChineseLanguage)
		{
			ReinitializeLocalizedConfig(isChineseLanguage);
		}
		TryLocalizeVisibleModConfigUi();
	}


	private static void SetPrivateField(object target, string fieldName, object value)
	{
		if (target != null && !string.IsNullOrWhiteSpace(fieldName))
		{
			target.GetType().GetField(fieldName, InstanceBindingFlags)?.SetValue(target, value);
		}
	}


	private static void TrimTrailingBlankLines(List<string> lines)
	{
		if (lines != null)
		{
			while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
			{
				lines.RemoveAt(lines.Count - 1);
			}
		}
	}


	private static bool IsAirportScene(Scene scene)
	{
		return string.Equals(scene.name, "Airport", StringComparison.OrdinalIgnoreCase);
	}


	private static bool ContainsLocalizedCharacters(string value, bool isChineseLanguage)
	{
		if (string.IsNullOrEmpty(value))
		{
			return false;
		}
		foreach (char character in value)
		{
			if (isChineseLanguage)
			{
				if (character >= '㐀' && character <= '鿿')
				{
					return true;
				}
			}
			else if (character <= '\u007f' && char.IsLetter(character))
			{
				return true;
			}
		}
		return false;
	}


	private bool TryGetLavaApproach(out float distance, out float etaSeconds, out bool hasEta)
	{
		distance = 0f;
		etaSeconds = 0f;
		hasEta = false;
		bool segmentOk = ShouldShowLavaApproachUiForCurrentSegment();
		bool loading = LoadingScreenHandler.loading;
		bool subjectOk = TryResolveFogUiSubjectPosition(out var subjectPosition);
		if (!segmentOk || loading || !subjectOk)
		{
			LogLavaDiag($"skip: segmentOk={segmentOk} loading={loading} subjectOk={subjectOk}");
			return false;
		}
		int hazardValue = -1;
		try
		{
			if (!IsFortressSegmentCurrent())
			{
				hazardValue = RunSettings.GetValue(RunSettings.SETTINGTYPE.Hazard_TheLavaRises);
				if (hazardValue == 0)
				{
					LogLavaDiag("skip: Hazard_TheLavaRises=0 (hazard disabled)");
					return false;
				}
			}
		}
		catch
		{
			LogLavaDiag("skip: RunSettings.GetValue threw exception");
			return false;
		}
		LavaRising lavaRising = null;
		try
		{
			if (!TryResolveActiveSceneLavaRising(out lavaRising))
			{
				LogLavaDiag("skip: no active LavaRising for current segment");
				return false;
			}
		}
		catch
		{
			LogLavaDiag("skip: resolve LavaRising threw exception");
			return false;
		}
		if (lavaRising == null || lavaRising.lava == null || lavaRising.topTransform == null || lavaRising.ended)
		{
			LogLavaDiag($"skip: lavaRising null={lavaRising == null} lava null={lavaRising?.lava == null} top null={lavaRising?.topTransform == null} ended={lavaRising?.ended}");
			return false;
		}
		float lavaHeight = lavaRising.lava.transform.position.y;
		float topHeight = lavaRising.topTransform.position.y;
		distance = Mathf.Max(subjectPosition.y - lavaHeight, 0f);
		if (distance <= 0.05f)
		{
			distance = 0f;
			etaSeconds = 0f;
			hasEta = true;
			return true;
		}
		float remainingTravelTime = Mathf.Max(lavaRising.travelTime - lavaRising.timeTraveled, 0f);
		float remainingHeight = Mathf.Max(topHeight - lavaHeight, 0f);
		float verticalRate = ((remainingTravelTime > 0.05f) ? (remainingHeight / remainingTravelTime) : 0f);
		if (verticalRate <= 0.01f && lavaRising.travelTime > 0.05f)
		{
			verticalRate = Mathf.Max((topHeight - lavaHeight) / lavaRising.travelTime, 0f);
		}
		if (verticalRate <= 0.01f)
		{
			return true;
		}
		float waitRemaining = 0f;
		if (!lavaRising.started)
		{
			waitRemaining = ((lavaRising.secondsWaitedToStart > 0f) ? Mathf.Max(lavaRising.initialWaitTime - lavaRising.secondsWaitedToStart, 0f) : Mathf.Max(lavaRising.initialWaitTime, 0f));
		}
		etaSeconds = waitRemaining + distance / verticalRate;
		hasEta = true;
		return true;
	}


	private void LogLavaDiag(string message)
	{
		if (!EnableDebugDiagnostics)
		{
			return;
		}
		float now = Time.realtimeSinceStartup;
		if (now - _lastLavaDiagLogTime < 5f)
		{
			return;
		}
		_lastLavaDiagLogTime = now;
		try
		{
			Logger.LogWarning("[Fog&ColdControl] LavaDiag: " + message);
		}
		catch
		{
		}
	}


	private static Camera ResolveActiveViewCamera()
	{
		Camera viewCamera = Camera.main;
		if (viewCamera != null && viewCamera.isActiveAndEnabled && viewCamera.gameObject.activeInHierarchy)
		{
			return viewCamera;
		}
		return UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault((Camera candidate) => candidate != null && candidate.isActiveAndEnabled && candidate.gameObject.activeInHierarchy);
	}


	private void LogWindChillZoneFields(WindChillZone zone)
	{
		if (_windChillFieldDiagLogged || zone == null)
		{
			return;
		}
		_windChillFieldDiagLogged = true;
		try
		{
			System.Text.StringBuilder builder = new System.Text.StringBuilder();
			System.Reflection.FieldInfo[] fields = typeof(WindChillZone).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
			foreach (System.Reflection.FieldInfo field in fields)
			{
				try
				{
					object value = field.GetValue(zone);
					builder.Append(field.Name).Append('=').Append(value).Append(" | ");
				}
				catch
				{
					builder.Append(field.Name).Append("=<?> | ");
				}
			}
			base.Logger.LogWarning("[Fog&ColdControl] WindChillZone fields: " + builder);
		}
		catch (Exception ex)
		{
			base.Logger.LogWarning("[Fog&ColdControl] WindChillZone field diag failed: " + ex.Message);
		}
	}


	private StormVisual[] ResolveCachedStormVisuals()
	{
		float now = Time.unscaledTime;
		if (_cachedStormVisuals != null && now - _lastStormVisualScanTime < 1f)
		{
			return _cachedStormVisuals;
		}
		_lastStormVisualScanTime = now;
		try
		{
			_cachedStormVisuals = UnityEngine.Object.FindObjectsByType<StormVisual>(FindObjectsSortMode.None);
		}
		catch
		{
			_cachedStormVisuals = Array.Empty<StormVisual>();
		}
		return _cachedStormVisuals;
	}


	private void HandlePendingWeatherStateBroadcast()
	{
		if (_pendingWeatherStateBroadcastTime < 0f || Time.unscaledTime < _pendingWeatherStateBroadcastTime)
		{
			return;
		}
		_pendingWeatherStateBroadcastTime = -1f;
		if (!TryBroadcastWeatherStateToGuests() && _weatherStateBroadcastRetryCount < 5)
		{
			_weatherStateBroadcastRetryCount++;
			_pendingWeatherStateBroadcastTime = Time.unscaledTime + 2f;
		}
		else
		{
			_weatherStateBroadcastRetryCount = 0;
		}
	}


	private static WindChillZone ReadStormVisualZone(StormVisual visual)
	{
		if (_stormVisualZoneField == null)
		{
			_stormVisualZoneField = typeof(StormVisual).GetField("zone", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		}
		if (_stormVisualZoneField == null || visual == null)
		{
			return null;
		}
		object value = _stormVisualZoneField.GetValue(visual);
		return value as WindChillZone;
	}


	private static Vector3 ReadWindChillDirection(WindChillZone zone)
	{
		if (_windChillDirectionField == null)
		{
			_windChillDirectionField = typeof(WindChillZone).GetField("currentWindDirection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		}
		if (_windChillDirectionField == null || zone == null)
		{
			return Vector3.zero;
		}
		object value = _windChillDirectionField.GetValue(zone);
		return (value is Vector3 vector) ? vector : Vector3.zero;
	}


	private static float ReadWindChillUntilSwitch(WindChillZone zone)
	{
		if (_windChillUntilSwitchField == null)
		{
			_windChillUntilSwitchField = typeof(WindChillZone).GetField("untilSwitch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		}
		if (_windChillUntilSwitchField == null || zone == null)
		{
			return 0f;
		}
		object value = _windChillUntilSwitchField.GetValue(zone);
		return (value is float f) ? f : 0f;
	}


	private static Color ParseColorOrDefault(string hexColor, string fallbackHex = "#FFFFFF")
	{
		if (TryParseHtmlColor(hexColor, out var parsedColor))
		{
			return parsedColor;
		}
		if (TryParseHtmlColor(fallbackHex, out var fallbackColor))
		{
			return fallbackColor;
		}
		return Color.white;
	}


	private static string GetCompactNightValue()
	{
		if (!IsNightColdFeatureEnabled())
		{
			return Colorize("OFF", "#FFB3B3");
		}
		return Colorize("ON", "#9FFFA8");
	}


	private string GetCompactRunTimerBadge()
	{
		if (!TryGetRunElapsedSeconds(out var elapsedSeconds))
		{
			return string.Empty;
		}
		return BuildFogUiBadge("TIM", "#B7C0CC", GetRunTimerUiEntryText(elapsedSeconds));
	}


	private string GetCompactDayNightBadge(bool isChineseLanguage)
	{
		if (!TryGetDayNightDisplay(out var isDay, out var phaseProgress, out var _, out var dayCount))
		{
			return string.Empty;
		}
		string labelColor = (isDay ? "#FFE38A" : "#AFC6FF");
		return BuildFogUiBadge(isDay ? "DAY" : "NGT", labelColor, GetDayNightProgressUiEntryText(isDay, phaseProgress, dayCount));
	}


	private string GetCompactLavaApproachBadge(bool isChineseLanguage)
	{
		if (!TryGetLavaApproach(out var distance, out var etaSeconds, out var hasEta))
		{
			return string.Empty;
		}
		if (IsVoidGhostsHazardCurrent())
		{
			return BuildFogUiBadge("PETR", "#A9B7C6", GetPetrifyApproachUiEntryText(distance, etaSeconds, hasEta));
		}
		return BuildFogUiBadge("LAV", "#FF7A3D", GetLavaApproachUiEntryText(distance, etaSeconds, hasEta));
	}


	private string GetCompactTimingBadge()
	{
		float configuredDelay = Mathf.Max(FogDelay?.Value ?? 900f, 0f);
		if (_orbFogHandler == null || IsFogRemovedInCurrentScene() || _fogPaused || _orbFogHandler.isMoving || _orbFogHandler.hasArrived || _initialDelayCompleted)
		{
			return string.Empty;
		}
		if (_fogHiddenBufferTimer < 10f)
		{
			return string.Empty;
		}
		float delayRemaining = Mathf.Max(configuredDelay - _fogDelayTimer, 0f);
		if (delayRemaining <= 0.05f)
		{
			return string.Empty;
		}
		float danger = ((configuredDelay <= 0.01f) ? 1f : Mathf.Clamp01(1f - delayRemaining / configuredDelay));
		string delayLabelColor = LerpHexColor(FogUiDelayCountdownStartLabelColor, FogUiDelayCountdownEndLabelColor, danger);
		string delayValueColor = LerpHexColor(FogUiDelayCountdownStartValueColor, FogUiDelayCountdownEndValueColor, danger);
		return BuildFogUiBadge("DLY", delayLabelColor, Colorize(FormatFogDelaySeconds(delayRemaining), delayValueColor));
	}


	private string GetCompactDistanceBadge()
	{
		if (!TryGetFogArrivalRemainingDistance(out var remainingDistance) || remainingDistance <= 0.05f)
		{
			return string.Empty;
		}
		GetFogDistanceColors(remainingDistance, out var labelColor, out var valueColor);
		return BuildFogUiBadge("DIS", labelColor, Colorize(FormatFogDistanceValue(remainingDistance), valueColor));
	}


	private string GetCompactEtaBadge(bool isChineseLanguage)
	{
		if (_orbFogHandler == null || IsFogRemovedInCurrentScene() || _fogPaused || !_orbFogHandler.isMoving || _orbFogHandler.hasArrived)
		{
			return string.Empty;
		}
		if (!TryGetFogArrivalEtaSeconds(out var etaSeconds))
		{
			return BuildFogUiBadge("ETA", "#B7C0CC", Colorize(isChineseLanguage ? "估算中" : "...", "#E2EAF3"));
		}
		if (etaSeconds <= 0.05f)
		{
			return string.Empty;
		}
		float displayedEtaSeconds = QuantizeFogEtaSeconds(etaSeconds);
		GetFogEtaColors(displayedEtaSeconds, out var labelColor, out var valueColor);
		return BuildFogUiBadge("ETA", labelColor, Colorize(FormatFogEtaTime(displayedEtaSeconds), valueColor));
	}


	private string GetCompactDirectStartBadge(bool isChineseLanguage)
	{
		if (_orbFogHandler == null || IsAirportScene(SceneManager.GetActiveScene()) || IsFogRemovedInCurrentScene() || _fogPaused)
		{
			return string.Empty;
		}
		if (_orbFogHandler.isMoving || _orbFogHandler.hasArrived || _initialDelayCompleted || !ShouldEnforceConfiguredDelay(_orbFogHandler))
		{
			return string.Empty;
		}
		switch (GetVanillaProgressStartUiState())
		{
		case VanillaProgressStartUiState.Unavailable:
			return BuildFogUiBadge("DIR", "#B7C0CC", Colorize("N/A", "#FFB3B3"));
		default:
			return string.Empty;
		case VanillaProgressStartUiState.Tracking:
		{
			if (!TryGetVanillaProgressStartProgress(_orbFogHandler, out var passedCount, out var totalCount, out var _, out var _))
			{
				return BuildFogUiBadge("DIR", "#B7C0CC", Colorize(isChineseLanguage ? "进度" : "PRG", "#E2EAF3"));
			}
			float progress = ((totalCount <= 0) ? 0f : Mathf.Clamp01((float)passedCount / (float)totalCount));
			string valueColor = LerpHexColor("#E2EAF3", "#B5FFB8", progress);
			return BuildFogUiBadge("DIR", "#B7C0CC", Colorize($"{passedCount}/{totalCount}", valueColor));
		}
		}
	}


	private static string GetCompactPauseBadge()
	{
		return BuildFogUiBadge("PAU", "#B7C0CC", Colorize(GetFogPauseHotkeyLabel(), "#E2EAF3"));
	}


	private static string GetCompactNightBadge()
	{
		bool enabled = IsNightColdFeatureEnabled();
		string labelColor = enabled ? "#9FFFA8" : "#FFB3B3";
		return BuildFogUiBadge("NGT", labelColor, Colorize(enabled ? "ON" : "OFF", labelColor));
	}


	private static string FormatDayCountUnit()
	{
		return "day";
	}


	private static bool HaveAllRoomPlayersSpawnedProgressCharacters()
	{
		if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
		{
			return true;
		}
		HashSet<int> roomActors = new HashSet<int>();
		foreach (Photon.Realtime.Player player in PhotonNetwork.CurrentRoom.Players.Values)
		{
			if (player != null && player.ActorNumber > 0)
			{
				roomActors.Add(player.ActorNumber);
			}
		}
		if (roomActors.Count <= 0)
		{
			return true;
		}
		HashSet<int> spawnedActors = new HashSet<int>();
		foreach (Character character in Character.AllCharacters)
		{
			if (IsVanillaProgressStartRelevantCharacter(character))
			{
				int actorNumber = character.photonView?.Owner?.ActorNumber ?? (-1);
				if (actorNumber > 0)
				{
					spawnedActors.Add(actorNumber);
				}
			}
		}
		return roomActors.All((int item) => spawnedActors.Contains(item));
	}


	private static string GetLobbyPauseHintLabel(bool isChineseLanguage)
	{
		string hotkeyLabel = GetFogPauseHotkeyLabel();
		if (!isChineseLanguage)
		{
			return Colorize("Pause:", "#B7C0CC") + " " + Colorize(hotkeyLabel, "#E2EAF3");
		}
		return Colorize("暂停键:", "#B7C0CC") + " " + Colorize(hotkeyLabel, "#E2EAF3");
	}


	private static string Colorize(string text, string hexColor)
	{
		if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(hexColor))
		{
			return text ?? string.Empty;
		}
		return "<color=" + ApplyFogUiThemeToHexColor(hexColor) + ">" + text + "</color>";
	}


	private static float GetRelativeLuminance(Color color)
	{
		return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
	}


	private static string LerpHexColor(string fromHex, string toHex, float t)
	{
		if (!TryParseHtmlColor(fromHex, out var fromColor) || !TryParseHtmlColor(toHex, out var toColor))
		{
			if (!(t >= 0.5f))
			{
				return fromHex;
			}
			return toHex;
		}
		Color mixed = Color.Lerp(fromColor, toColor, Mathf.Clamp01(t));
		return "#" + ColorUtility.ToHtmlStringRGB(mixed);
	}


	private static bool TryParseHtmlColor(string hexColor, out Color color)
	{
		color = Color.white;
		if (string.IsNullOrWhiteSpace(hexColor))
		{
			return false;
		}
		return ColorUtility.TryParseHtmlString(hexColor.StartsWith("#", StringComparison.Ordinal) ? hexColor : ("#" + hexColor), out color);
	}


	private static void ApplyRightMiddleAnchoredRect(RectTransform target, float width, float height, float rightOffset, float downOffset)
	{
		if (!(target == null))
		{
			target.localScale = Vector3.one;
			target.anchorMin = new Vector2(1f, 0.5f);
			target.anchorMax = new Vector2(1f, 0.5f);
			target.pivot = new Vector2(1f, 0.5f);
			target.sizeDelta = new Vector2(width, height);
			target.anchoredPosition = new Vector2(0f - rightOffset, 0f - downOffset);
		}
	}


	private static void DrawCircleOutline(Color32[] pixels, int size, float centerX, float centerY, float radius, float thickness)
	{
		float inner = Mathf.Max(radius - thickness, 0f);
		float outer = radius + thickness;
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float distance = Vector2.Distance(new Vector2((float)x + 0.5f, (float)y + 0.5f), new Vector2(centerX, centerY));
				if (distance >= inner && distance <= outer)
				{
					pixels[y * size + x] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
				}
			}
		}
	}


	private static void FillCircle(Color32[] pixels, int size, float centerX, float centerY, float radius, Color32 color)
	{
		float radiusSquared = radius * radius;
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float num = (float)x + 0.5f - centerX;
				float dy = (float)y + 0.5f - centerY;
				if (num * num + dy * dy <= radiusSquared)
				{
					pixels[y * size + x] = color;
				}
			}
		}
	}


	private static void FillEllipse(Color32[] pixels, int size, float centerX, float centerY, float radiusX, float radiusY, Color32 color)
	{
		float safeRadiusX = Mathf.Max(radiusX, 0.01f);
		float safeRadiusY = Mathf.Max(radiusY, 0.01f);
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float num = ((float)x + 0.5f - centerX) / safeRadiusX;
				float dy = ((float)y + 0.5f - centerY) / safeRadiusY;
				if (num * num + dy * dy <= 1f)
				{
					pixels[y * size + x] = color;
				}
			}
		}
	}


	private static void DrawRectOutline(Color32[] pixels, int size, float x, float y, float width, float height, float thickness)
	{
		DrawLine(pixels, size, x, y, x + width, y, thickness);
		DrawLine(pixels, size, x, y + height, x + width, y + height, thickness);
		DrawLine(pixels, size, x, y, x, y + height, thickness);
		DrawLine(pixels, size, x + width, y, x + width, y + height, thickness);
	}


	private static void FillRect(Color32[] pixels, int size, int startX, int startY, int width, int height, Color32 color)
	{
		for (int y = startY; y < startY + height; y++)
		{
			if (y < 0 || y >= size)
			{
				continue;
			}
			for (int x = startX; x < startX + width; x++)
			{
				if (x >= 0 && x < size)
				{
					pixels[y * size + x] = color;
				}
			}
		}
	}


	private static void DrawLine(Color32[] pixels, int size, float x0, float y0, float x1, float y1, float thickness)
	{
		DrawLine(pixels, size, x0, y0, x1, y1, thickness, new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
	}


	private static void DrawLine(Color32[] pixels, int size, float x0, float y0, float x1, float y1, float thickness, Color32 color)
	{
		Vector2 start = new Vector2(x0, y0);
		Vector2 end = new Vector2(x1, y1);
		float radius = Mathf.Max(thickness * 0.5f, 0.5f);
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				if (DistanceToSegment(new Vector2((float)j + 0.5f, (float)i + 0.5f), start, end) <= radius)
				{
					pixels[i * size + j] = color;
				}
			}
		}
	}


	private static bool DetectChineseLanguage()
	{
		if (TryGetConfiguredGameLanguage(out var isChineseLanguage))
		{
			return isChineseLanguage;
		}
		if (TryGetLocalizedTextLanguageName(out var languageName))
		{
			return IsChineseLanguageName(languageName);
		}
		return false;
	}


	private static bool TryGetLocalizedTextLanguageName(out string languageName)
	{
		languageName = string.Empty;
		try
		{
			languageName = LocalizedText.CURRENT_LANGUAGE.ToString();
			return !string.IsNullOrWhiteSpace(languageName);
		}
		catch
		{
			return false;
		}
	}


	private static bool IsChineseLanguageName(string languageName)
	{
		if (string.IsNullOrWhiteSpace(languageName))
		{
			return false;
		}
		if (languageName.IndexOf("Chinese", StringComparison.OrdinalIgnoreCase) < 0 && languageName.IndexOf("中文", StringComparison.OrdinalIgnoreCase) < 0)
		{
			return languageName.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}


	private static string GetLocalizedModDisplayName(bool isChineseLanguage)
	{
		return "Fog&ColdControl";
	}


	internal static bool IsModFeatureEnabled()
	{
		if (ModEnabled != null)
		{
			return ModEnabled.Value;
		}
		return true;
	}


	private static bool TryReadHostInstallState(out bool isEnabled)
	{
		isEnabled = false;
		if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || PhotonNetwork.CurrentRoom.Players == null)
		{
			return false;
		}
		Photon.Realtime.Player hostPlayer = PhotonNetwork.CurrentRoom.Players.Values.FirstOrDefault((Photon.Realtime.Player player) => player != null && player.IsMasterClient);
		return hostPlayer != null && TryReadInstallState(hostPlayer, out isEnabled);
	}


	private static bool TryReadHostFogSpeed(out float fogSpeed)
	{
		fogSpeed = 0f;
		if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || PhotonNetwork.CurrentRoom.Players == null)
		{
			return false;
		}
		Photon.Realtime.Player hostPlayer = PhotonNetwork.CurrentRoom.Players.Values.FirstOrDefault((Photon.Realtime.Player player) => player != null && player.IsMasterClient);
		if (hostPlayer == null || !TryReadInstallState(hostPlayer, out var hostModEnabled) || !hostModEnabled)
		{
			return false;
		}
		if (hostPlayer.CustomProperties == null || !hostPlayer.CustomProperties.ContainsKey("FogClimb.FogSpeed"))
		{
			return false;
		}
		object rawValue = hostPlayer.CustomProperties["FogClimb.FogSpeed"];
		if (rawValue is float floatValue)
		{
			fogSpeed = Mathf.Max(floatValue, 0f);
			return true;
		}
		if (rawValue is string stringValue && float.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedSpeed))
		{
			fogSpeed = Mathf.Max(parsedSpeed, 0f);
			return true;
		}
		return false;
	}


	private void UpdateFogSpeedAdvertisement()
	{
		if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || !HasFogAuthority() || _orbFogHandler == null || ShouldDeferFogToVanillaInCurrentScene() || IsFogRemovedInCurrentScene())
		{
			_lastAdvertisedFogSpeed = -1f;
			return;
		}
		float effectiveSpeed = Mathf.Max(_orbFogHandler.speed, 0f);
		if (Mathf.Abs(effectiveSpeed - _lastAdvertisedFogSpeed) <= 0.005f)
		{
			return;
		}
		float now = Time.unscaledTime;
		if (now - _lastFogSpeedAdvertiseTime < 1f)
		{
			return;
		}
		try
		{
			PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "FogClimb.FogSpeed", effectiveSpeed } });
			_lastAdvertisedFogSpeed = effectiveSpeed;
			_lastFogSpeedAdvertiseTime = now;
		}
		catch (Exception ex)
		{
			base.Logger.LogDebug("[Fog&ColdControl] Failed to publish fog speed metadata: " + ex.Message);
		}
	}


	private void UpdateSyncedHostFogSpeed()
	{
		bool canUseSyncedFogSpeed = IsReadOnlyFogUiViewer() && !ShouldDeferFogToVanillaInCurrentScene() && !IsFogRemovedInCurrentScene();
		if (!canUseSyncedFogSpeed)
		{
			_syncedHostFogSpeed = -1f;
			if (_guestAppliedFogSpeed >= 0f && _orbFogHandler != null)
			{
				RestoreVanillaFogSpeed();
				_guestAppliedFogSpeed = -1f;
			}
			return;
		}
		if (TryReadHostFogSpeed(out var hostSpeed))
		{
			_syncedHostFogSpeed = hostSpeed;
			if (_orbFogHandler != null && Mathf.Abs(_orbFogHandler.speed - hostSpeed) > 0.005f)
			{
				_orbFogHandler.speed = hostSpeed;
				_guestAppliedFogSpeed = hostSpeed;
			}
			return;
		}
		_syncedHostFogSpeed = -1f;
		if (_guestAppliedFogSpeed >= 0f && _orbFogHandler != null)
		{
			RestoreVanillaFogSpeed();
			_guestAppliedFogSpeed = -1f;
		}
	}


	private void UpdateLocalInstallStateAdvertisement()
	{
		if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
		{
			_hasAdvertisedInstallState = false;
			return;
		}
		bool isEnabled = IsModFeatureEnabled();
		if (_hasAdvertisedInstallState && _lastAdvertisedInstallState == isEnabled && LocalInstallStateMatches(isEnabled))
		{
			return;
		}
		try
		{
			PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "FogClimb.Enabled", isEnabled } });
			_lastAdvertisedInstallState = isEnabled;
			_hasAdvertisedInstallState = true;
			TryLogInstallAdvertiseDiagnostics(isEnabled);
		}
		catch (Exception ex)
		{
			_hasAdvertisedInstallState = false;
			base.Logger.LogDebug("[Fog&ColdControl] Failed to publish install-state metadata: " + ex.Message);
		}
	}


	private void TryLogInstallAdvertiseDiagnostics(bool isEnabled)
	{
		if (!EnableDebugDiagnostics)
		{
			return;
		}
		float now = Time.unscaledTime;
		if (now - _lastInstallAdvertiseDiagLogTime < 5f)
		{
			return;
		}
		_lastInstallAdvertiseDiagLogTime = now;
		base.Logger.LogInfo(string.Format("[Fog&ColdControl] [DIAG-ADV] isMaster={0}, inRoom={1}, enabled={2}, propertySet={3}", PhotonNetwork.IsMasterClient, PhotonNetwork.InRoom, isEnabled, LocalInstallStateMatches(isEnabled)));
	}


	private static bool LocalInstallStateMatches(bool expected)
	{
		Photon.Realtime.Player localPlayer = PhotonNetwork.LocalPlayer;
		if (localPlayer?.CustomProperties == null || !localPlayer.CustomProperties.ContainsKey("FogClimb.Enabled"))
		{
			return false;
		}
		if (TryReadInstallState(localPlayer, out var currentState))
		{
			return currentState == expected;
		}
		return false;
	}


	private static bool TryReadInstallState(Photon.Realtime.Player player, out bool isEnabled)
	{
		isEnabled = false;
		if (player?.CustomProperties == null || !player.CustomProperties.ContainsKey("FogClimb.Enabled"))
		{
			return false;
		}
		object rawValue = player.CustomProperties["FogClimb.Enabled"];
		if (!(rawValue is bool boolValue))
		{
			if (rawValue is string stringValue)
			{
				return bool.TryParse(stringValue, out isEnabled);
			}
			return false;
		}
		isEnabled = boolValue;
		return true;
	}


	private bool IsCharacterPastJoinGrace(Character character)
	{
		if (character?.photonView?.Owner == null || character.photonView.IsMine)
		{
			return true;
		}
		if (!_remotePlayerFirstSeenTimes.TryGetValue(character.photonView.Owner.ActorNumber, out var firstSeenTime))
		{
			return true;
		}
		return Time.unscaledTime - firstSeenTime >= 8f;
	}


	private void TryCleanupGeneratedBackupFile()
	{
		try
		{
			string pluginPath = base.Info?.Location;
			if (string.IsNullOrWhiteSpace(pluginPath))
			{
				return;
			}
			List<string> backupPaths = new List<string> { pluginPath + "-unrpcpatched.old" };
			string pluginDirectory = Path.GetDirectoryName(pluginPath);
			if (!string.IsNullOrWhiteSpace(pluginDirectory))
			{
				string[] legacyPluginFileNames = LegacyPluginFileNames;
				foreach (string legacyPluginFileName in legacyPluginFileNames)
				{
					backupPaths.Add(Path.Combine(pluginDirectory, legacyPluginFileName + "-unrpcpatched.old"));
				}
			}
			foreach (string backupPath in backupPaths.Distinct(StringComparer.OrdinalIgnoreCase))
			{
				if (File.Exists(backupPath))
				{
					File.Delete(backupPath);
					base.Logger.LogInfo("[Fog&ColdControl] Removed generated backup file: " + Path.GetFileName(backupPath));
				}
			}
		}
		catch (Exception ex)
		{
			base.Logger.LogDebug("[Fog&ColdControl] Failed to clean generated backup file: " + ex.Message);
		}
	}


	private void TryCleanupLegacyPluginFile()
	{
		try
		{
			string pluginPath = base.Info?.Location;
			if (string.IsNullOrWhiteSpace(pluginPath) || !string.Equals(Path.GetFileName(pluginPath), "Thanks.Fog&ColdControl.dll", StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			string pluginDirectory = Path.GetDirectoryName(pluginPath);
			if (string.IsNullOrWhiteSpace(pluginDirectory))
			{
				return;
			}
			string[] legacyPluginFileNames = LegacyPluginFileNames;
			foreach (string legacyPluginFileName in legacyPluginFileNames)
			{
				string legacyPluginPath = Path.Combine(pluginDirectory, legacyPluginFileName);
				if (File.Exists(legacyPluginPath))
				{
					File.Delete(legacyPluginPath);
					base.Logger.LogInfo("[Fog&ColdControl] Removed legacy plugin file: " + legacyPluginFileName);
				}
			}
		}
		catch (Exception ex)
		{
			base.Logger.LogDebug("[Fog&ColdControl] Failed to clean legacy plugin file: " + ex.Message);
		}
	}
}
