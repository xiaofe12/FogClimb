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

// FogClimb subsystem: Fog
public sealed partial class Plugin
{


	private void HandleRoomChangeIfNeeded(bool modEnabled)
	{
		string roomKey = GetCurrentRoomKey();
		if (!string.Equals(_lastObservedRoomKey, roomKey, StringComparison.Ordinal))
		{
			_lastObservedRoomKey = roomKey;
			ResetFogRuntimeState();
			_orbFogHandler = null;
			_fogSphere = null;
			_legacyFog = null;
			_remotePlayerFirstSeenTimes.Clear();
			_remotePlayerCompassBaselineCounts.Clear();
			_hasAdvertisedInstallState = false;
			_lastFogUiRenderedText = string.Empty;
			_cachedStormVisuals = null;
			_lastStormVisualScanTime = -999f;
			ResetWeatherHazardUiState();
			_pendingWeatherStateBroadcastTime = -1f;
			_weatherStateBroadcastRetryCount = 0;
			if (!modEnabled || !PhotonNetwork.InRoom)
			{
				SetFogUiVisible(visible: false);
				SetCampfireLocatorUiVisible(visible: false);
				CleanupCompassLobbyNotice();
			}
		}
	}


	private void EnsureFogCoverageAcrossAllLevels()
	{
		EnsureFogCoverage(_orbFogHandler);
	}


	private void EnsureFogCoverage(OrbFogHandler fogHandler)
	{
		if (!ShouldForceFogCoverageEverywhere() || fogHandler == null)
		{
			return;
		}
		FogSphereOrigin[] origins = ResolveFogOrigins(fogHandler);
		for (int i = 0; i < origins.Length; i++)
		{
			if (!ShouldDeferFogToVanillaForSegment((Segment)i))
			{
				FogSphereOrigin origin = origins[i];
				if (!(origin == null) && origin.disableFog)
				{
					origin.disableFog = false;
				}
			}
		}
		if (ShouldDeferFogToVanillaInCurrentScene())
		{
			return;
		}
		FogSphere sphere = ResolveFogSphere(fogHandler);
		if (!(sphere == null))
		{
			_fogSphere = sphere;
			if (!sphere.gameObject.activeSelf)
			{
				sphere.gameObject.SetActive(value: true);
			}
			if (fogHandler.currentSize > 0f && sphere.currentSize <= 0f)
			{
				sphere.currentSize = fogHandler.currentSize;
			}
		}
	}


	private static FogSphereOrigin[] ResolveFogOrigins(OrbFogHandler fogHandler)
	{
		if (fogHandler == null)
		{
			return Array.Empty<FogSphereOrigin>();
		}
		if (OrbFogHandlerOriginsField?.GetValue(fogHandler) is FogSphereOrigin[] origins && origins.Length != 0)
		{
			return origins;
		}
		FogSphereOrigin[] origins2 = fogHandler.GetComponentsInChildren<FogSphereOrigin>(includeInactive: true);
		if (origins2 != null && origins2.Length != 0)
		{
			try
			{
				OrbFogHandlerOriginsField?.SetValue(fogHandler, origins2);
			}
			catch
			{
			}
			return origins2;
		}
		return Array.Empty<FogSphereOrigin>();
	}


	private static FogSphere ResolveFogSphere(OrbFogHandler fogHandler)
	{
		if (fogHandler == null)
		{
			return null;
		}
		FogSphere sphere = OrbFogHandlerSphereField?.GetValue(fogHandler) as FogSphere;
		if (sphere != null)
		{
			return sphere;
		}
		sphere = fogHandler.GetComponentInChildren<FogSphere>(includeInactive: true);
		if (sphere != null)
		{
			try
			{
				OrbFogHandlerSphereField?.SetValue(fogHandler, sphere);
			}
			catch
			{
			}
		}
		return sphere;
	}


	private void InitializeFogRuntimeState(OrbFogHandler fogHandler)
	{
		_fogStateInitialized = true;
		_trackedFogOriginId = ((fogHandler != null) ? fogHandler.currentID : (-1));
		_initialDelayCompleted = ShouldSkipInitialDelay(fogHandler);
		_fogDelayTimer = ((!_initialDelayCompleted) ? 0f : (FogDelay?.Value ?? 0f));
		_fogHiddenBufferTimer = (_initialDelayCompleted ? 10f : 0f);
		ResetFogArrivalEstimate();
	}


	private static bool IsVanillaProgressStartRelevantCharacter(Character character)
	{
		if (character != null && character.data != null && !character.data.dead && character.IsPlayerControlled && !character.isBot)
		{
			return !character.isZombie;
		}
		return false;
	}


	private bool ShouldSkipInitialDelay(OrbFogHandler fogHandler)
	{
		if (fogHandler == null || !HasAnyConfiguredFogDelay())
		{
			return true;
		}
		if (IsCampfireDelayPendingForOrigin(fogHandler.currentID))
		{
			return false;
		}
		if (fogHandler.currentID <= 0)
		{
			if (!fogHandler.isMoving && !(fogHandler.currentWaitTime > 1f))
			{
				return fogHandler.hasArrived;
			}
			return true;
		}
		if (ShouldHoldFogUntilCampfireActivation(fogHandler))
		{
			return false;
		}
		if (!fogHandler.isMoving && !(fogHandler.currentWaitTime > 1f))
		{
			return fogHandler.hasArrived;
		}
		return true;
	}


	private void UpdateAuthorityFogMode()
	{
		if (_orbFogHandler == null)
		{
			return;
		}
		ClampConfigValues();
		if (ShouldDeferFogToVanillaInCurrentScene())
		{
			ClearSyntheticFogStage();
			_pendingSyntheticFogSegmentId = -1;
			_delayedFogOriginId = -1;
			RestoreVanillaFogSpeed();
			return;
		}
		TryAlignFogOriginToCurrentSegment();
		TryUpdateSyntheticFogStage();
		UpdateTrackedFogOrigin();
		if (ShouldAutoCompleteDelayForCurrentOrigin(_orbFogHandler))
		{
			_initialDelayCompleted = true;
		}
		if (!_initialDelayCompleted)
		{
			_orbFogHandler.speed = 0f;
			if (!ShouldEnforceConfiguredDelay(_orbFogHandler) || TryStartFogFromVanillaProgressTrigger(_orbFogHandler))
			{
				return;
			}
			if (_fogHiddenBufferTimer < 10f)
			{
				_fogHiddenBufferTimer = Mathf.Min(_fogHiddenBufferTimer + Time.unscaledDeltaTime, 10f);
				if (_fogHiddenBufferTimer < 10f)
				{
					return;
				}
			}
			float configuredDelay = FogDelay?.Value ?? 0f;
			if (configuredDelay <= 0f)
			{
				_initialDelayCompleted = true;
				ClearPendingCampfireDelayForOrigin(_orbFogHandler.currentID);
				if (!_orbFogHandler.isMoving)
				{
					StartFogMovement();
				}
				return;
			}
			_fogDelayTimer += Time.unscaledDeltaTime;
			if (_fogDelayTimer >= configuredDelay)
			{
				_fogDelayTimer = configuredDelay;
				_initialDelayCompleted = true;
				ClearPendingCampfireDelayForOrigin(_orbFogHandler.currentID);
				if (!_orbFogHandler.isMoving)
				{
					StartFogMovement();
				}
			}
		}
		else if (_fogPaused)
		{
			ApplyPausedFogState(syncImmediately: false);
		}
		else
		{
			_orbFogHandler.speed = FogSpeed.Value;
			TryAutoStartCustomRunFogIfNeeded();
			TryAutoResumeStalledFogMovement();
			TryGrantInitialCompassIfNeeded();
		}
	}


	private bool TryStartFogFromVanillaProgressTrigger(OrbFogHandler fogHandler)
	{
		if (ShouldPauseVanillaProgressStartForJoiningPlayers())
		{
			return false;
		}
		if (!TryGetVanillaProgressStartProgress(fogHandler, out var passedCount, out var totalCount, out var requiredHeight, out var requiredForward))
		{
			return false;
		}
		if (passedCount < totalCount)
		{
			return false;
		}
		float remainingBuffer = Mathf.Max(10f - _fogHiddenBufferTimer, 0f);
		float configuredDelay = Mathf.Max(FogDelay?.Value ?? 900f, 0f);
		float remainingDelay = Mathf.Max(configuredDelay - _fogDelayTimer, 0f);
		_initialDelayCompleted = true;
		_fogHiddenBufferTimer = 10f;
		_fogDelayTimer = configuredDelay;
		base.Logger.LogInfo(string.Format("[{0}] Starting fog early from vanilla progress trigger at origin {1}. progress={2}/{3}, thresholdY={4:F1}, thresholdZ={5:F1}, remainingBuffer={6:F1}s, remainingDelay={7:F1}s.", "Fog&ColdControl", fogHandler.currentID, passedCount, totalCount, requiredHeight, requiredForward, remainingBuffer, remainingDelay));
		StartFogMovement();
		return true;
	}


	private bool IsCampfireDelayPendingForOrigin(int originId)
	{
		if (originId >= 0)
		{
			return _delayedFogOriginId == originId;
		}
		return false;
	}


	private bool ShouldAutoCompleteDelayForCurrentOrigin(OrbFogHandler fogHandler)
	{
		if (fogHandler == null)
		{
			return true;
		}
		if (fogHandler.currentID == 0)
		{
			return false;
		}
		if (ShouldHoldFogUntilCampfireActivation(fogHandler))
		{
			return false;
		}
		return !IsCampfireDelayPendingForOrigin(fogHandler.currentID);
	}


	private bool ShouldHoldFogUntilCampfireActivation(OrbFogHandler fogHandler)
	{
		if (fogHandler != null && fogHandler.currentID > 0 && !IsCampfireDelayPendingForOrigin(fogHandler.currentID) && !fogHandler.isMoving && !fogHandler.hasArrived)
		{
			return !_initialDelayCompleted;
		}
		return false;
	}


	private bool HasVisibleFogDelayCountdown()
	{
		return _fogHiddenBufferTimer >= 10f;
	}


	private void ClearPendingCampfireDelayForOrigin(int originId)
	{
		if (_delayedFogOriginId == originId)
		{
			_delayedFogOriginId = -1;
		}
	}


	private int GetAvailableFogOriginCount()
	{
		if (!(_orbFogHandler == null))
		{
			return ResolveFogOrigins(_orbFogHandler).Length;
		}
		return 0;
	}


	private bool TryResolveFogPointForCurrentOrigin(out Vector3 fogPoint, out string pointDescription)
	{
		fogPoint = Vector3.zero;
		pointDescription = string.Empty;
		if (_fogSphere != null)
		{
			fogPoint = _fogSphere.fogPoint;
			pointDescription = "fogSphere";
			return true;
		}
		if (_orbFogHandler == null)
		{
			return false;
		}
		FogSphereOrigin[] origins = ResolveFogOrigins(_orbFogHandler);
		if (origins.Length == 0)
		{
			return false;
		}
		int originId = Mathf.Clamp(_orbFogHandler.currentID, 0, origins.Length - 1);
		FogSphereOrigin origin = origins[originId] ?? origins.LastOrDefault((FogSphereOrigin candidate) => candidate != null);
		if (origin == null)
		{
			return false;
		}
		fogPoint = origin.transform.position;
		pointDescription = $"origin-{originId}";
		return true;
	}


	private bool TryGetPreviousRealFogOriginSize(out float previousOriginSize)
	{
		previousOriginSize = 900f;
		FogSphereOrigin[] origins = ResolveFogOrigins(_orbFogHandler);
		if (origins.Length == 0)
		{
			return false;
		}
		int referenceIndex = Mathf.Clamp(origins.Length - 2, 0, origins.Length - 1);
		FogSphereOrigin referenceOrigin = origins[referenceIndex];
		if (referenceOrigin == null || referenceOrigin.size <= 0f)
		{
			return false;
		}
		previousOriginSize = referenceOrigin.size;
		return true;
	}


	private bool TryResolveFogStageCoverageAnchor(Segment fogStageSegment, out Vector3 stageAnchor, out string anchorDescription)
	{
		stageAnchor = Vector3.zero;
		anchorDescription = string.Empty;
		MapHandler mapHandler = Singleton<MapHandler>.Instance;
		if ((int)fogStageSegment >= 5 && mapHandler != null && mapHandler.respawnThePeak != null)
		{
			stageAnchor = mapHandler.respawnThePeak.position;
			anchorDescription = "respawnThePeak";
			return true;
		}
		if (mapHandler != null && mapHandler.segments != null && mapHandler.segments.Length != 0)
		{
			int segmentIndex = Mathf.Clamp((int)fogStageSegment, 0, mapHandler.segments.Length - 1);
			MapHandler.MapSegment mapSegment = mapHandler.segments[segmentIndex];
			if (mapSegment != null)
			{
				if (mapSegment.segmentCampfire != null)
				{
					Campfire campfire = mapSegment.segmentCampfire.GetComponentInChildren<Campfire>(includeInactive: true);
					if (campfire != null)
					{
						stageAnchor = campfire.transform.position;
						anchorDescription = $"{fogStageSegment} campfire";
						return true;
					}
					stageAnchor = mapSegment.segmentCampfire.transform.position;
					anchorDescription = $"{fogStageSegment} segmentCampfire";
					return true;
				}
				if (mapSegment.reconnectSpawnPos != null)
				{
					stageAnchor = mapSegment.reconnectSpawnPos.position;
					anchorDescription = $"{fogStageSegment} reconnectSpawnPos";
					return true;
				}
				if (mapSegment.segmentParent != null)
				{
					stageAnchor = mapSegment.segmentParent.transform.position;
					anchorDescription = $"{fogStageSegment} segmentParent";
					return true;
				}
			}
		}
		if (TryResolveSyntheticTargetAnchor(fogStageSegment, out stageAnchor, out anchorDescription))
		{
			return true;
		}
		Character localCharacter = Character.localCharacter;
		if (localCharacter != null)
		{
			stageAnchor = localCharacter.Center;
			anchorDescription = "localCharacter";
			return true;
		}
		Character fallbackCharacter = Character.AllCharacters.FirstOrDefault((Character character) => character != null && character.data != null && !character.data.dead);
		if (fallbackCharacter != null)
		{
			stageAnchor = fallbackCharacter.Center;
			anchorDescription = "firstAliveCharacter";
			return true;
		}
		return false;
	}


	private float ComputeFogStartSizeForStage(Segment fogStageSegment, Vector3 fogPoint, float baseSize, float minSize, float maxSize, float anchorMargin, float playerMargin)
	{
		float computedSize = Mathf.Max(baseSize, minSize);
		if (TryResolveFogStageCoverageAnchor(fogStageSegment, out var stageAnchor, out var _))
		{
			computedSize = Mathf.Max(computedSize, Vector3.Distance(fogPoint, stageAnchor) + anchorMargin);
		}
		foreach (Character character in Character.AllCharacters)
		{
			if (!(character == null) && !(character.data == null) && !character.data.dead)
			{
				computedSize = Mathf.Max(computedSize, Vector3.Distance(fogPoint, character.Center) + playerMargin);
			}
		}
		return Mathf.Clamp(computedSize, minSize, maxSize);
	}


	private static bool ShouldUseCustomFogPositionForSegment(Segment segment)
	{
		if ((int)segment >= 5)
		{
			return !ShouldDeferFogToVanillaForSegment(segment);
		}
		return false;
	}


	private static bool ShouldDeferFogToVanillaForSegment(Segment segment)
	{
		return (int)segment >= 4;
	}


	private static bool ShouldRemoveFogForSegment(Segment segment)
	{
		return segment == Segment.TheKiln;
	}


	private static bool TryGetCurrentGameplaySegment(out Segment segment)
	{
		segment = Segment.Beach;
		if (!IsGameplayFogScene(SceneManager.GetActiveScene()) || LoadingScreenHandler.loading)
		{
			return false;
		}
		if (Singleton<MapHandler>.Instance == null)
		{
			return false;
		}
		segment = MapHandler.CurrentSegmentNumber;
		return true;
	}


	private static bool TryGetNextBiomeDisplayName(bool isChineseLanguage, out string biomeName)
	{
		biomeName = string.Empty;
		if (NextBiomeUiEnabled?.Value == false || !TryGetCurrentGameplaySegment(out var currentSegment))
		{
			return false;
		}
		if (!NextBiomeResolver.TryGetNextBiome(Singleton<MapHandler>.Instance, currentSegment, out var nextBiome))
		{
			return false;
		}
		biomeName = GetDisplayNextBiomeName(nextBiome, isChineseLanguage);
		return !string.IsNullOrWhiteSpace(biomeName);
	}


	private static string GetLocalizedBiomeName(Biome.BiomeType biomeType, bool isChineseLanguage)
	{
		if (!isChineseLanguage)
		{
			return biomeType switch
			{
				Biome.BiomeType.Shore => "Shore", 
				Biome.BiomeType.Tropics => "Tropics", 
				Biome.BiomeType.Roots => "Roots", 
				Biome.BiomeType.Alpine => "Alpine", 
				Biome.BiomeType.Mesa => "Mesa", 
				_ => biomeType.ToString(), 
			};
		}
		return biomeType switch
		{
			Biome.BiomeType.Shore => "海岸", 
			Biome.BiomeType.Tropics => "热带", 
			Biome.BiomeType.Alpine => "高山", 
			Biome.BiomeType.Volcano => "火山", 
			Biome.BiomeType.Peak => "峰顶", 
			Biome.BiomeType.Mesa => "台地", 
			Biome.BiomeType.Roots => "树根", 
			Biome.BiomeType.Swamp => "雾沼", 
			Biome.BiomeType.Temple => "神庙", 
			Biome.BiomeType.Grasslands => "草原", 
			Biome.BiomeType.Ocean => "海洋", 
			Biome.BiomeType.Skyblock => "空岛", 
			Biome.BiomeType.Hell => "地狱", 
			Biome.BiomeType.Heaven => "天堂", 
			Biome.BiomeType.Mars => "火星", 
			Biome.BiomeType.Wisconsin => "威斯康星", 
			Biome.BiomeType.Void => "虚空", 
			_ => biomeType.ToString(), 
		};
	}


	private static string GetDisplayNextBiomeName(Biome.BiomeType biomeType, bool isChineseLanguage)
	{
		if (!isChineseLanguage)
		{
			return biomeType switch
			{
				Biome.BiomeType.Tropics => "Tropics", 
				Biome.BiomeType.Roots => "Roots", 
				Biome.BiomeType.Alpine => "Alpine", 
				Biome.BiomeType.Mesa => "Mesa", 
				Biome.BiomeType.Volcano => "Volcano", 
				Biome.BiomeType.Swamp => "Swamp", 
				_ => biomeType.ToString(), 
			};
		}
		return biomeType switch
		{
			Biome.BiomeType.Tropics => "雨林", 
			Biome.BiomeType.Roots => "森蕈", 
			Biome.BiomeType.Alpine => "雪山", 
			Biome.BiomeType.Mesa => "方山", 
			Biome.BiomeType.Volcano => "火山", 
			Biome.BiomeType.Swamp => "雾沼", 
			_ => GetLocalizedBiomeName(biomeType, isChineseLanguage),
		};
	}


	private static bool TryGetNextGameplaySegment(Segment currentSegment, out Segment nextSegment)
	{
		switch (currentSegment)
		{
		case Segment.Beach:
			nextSegment = Segment.Tropics;
			return true;
		case Segment.Tropics:
			nextSegment = Segment.Alpine;
			return true;
		default:
			nextSegment = currentSegment;
			return false;
		}
	}


	private static string GetLocalizedSegmentDisplayName(Segment segment, bool isChineseLanguage)
	{
		if (!isChineseLanguage)
		{
			return segment switch
			{
				Segment.Beach => "Shore", 
				Segment.Tropics => "Tropics", 
				Segment.Alpine => "Alpine", 
				Segment.Caldera => "Volcano", 
				Segment.TheKiln => "Kiln", 
				Segment.Peak => "Peak", 
				_ => segment.ToString(), 
			};
		}
		return segment switch
		{
			Segment.Beach => "海岸", 
			Segment.Tropics => "热带", 
			Segment.Alpine => "高山", 
			Segment.Caldera => "火山", 
			Segment.TheKiln => "熔炉", 
			Segment.Peak => "峰顶", 
			_ => segment.ToString(), 
		};
	}


	private static bool IsFogRemovedInCurrentScene()
	{
		if (TryGetCurrentGameplaySegment(out var segment))
		{
			return ShouldRemoveFogForSegment(segment);
		}
		return false;
	}


	private static bool IsFortressSegmentCurrent()
	{
		if (TryResolveActiveSceneLavaRising(out var lavaRising))
		{
			// 3.0.1 起 LavaRising.isGloom 已降为 private；isGloom 等价于 risingFieldType == Gloom，改为直接读公开字段
			return lavaRising.risingFieldType == LavaRising.RisingFieldType.Gloom
				|| lavaRising.risingFieldType == LavaRising.RisingFieldType.VoidGhosts;
		}
		return false;
	}


	private static bool IsVoidGhostsHazardCurrent()
	{
		if (TryResolveActiveSceneLavaRising(out var lavaRising))
		{
			return lavaRising.risingFieldType == LavaRising.RisingFieldType.VoidGhosts;
		}
		return false;
	}


	private static bool ShouldDeferFogToVanillaInCurrentScene()
	{
		if (TryGetCurrentGameplaySegment(out var segment))
		{
			return ShouldDeferFogToVanillaForSegment(segment);
		}
		return false;
	}


	private void ApplyRemovedFogState()
	{
		if (_orbFogHandler == null)
		{
			return;
		}
		_orbFogHandler.speed = 0f;
		_orbFogHandler.isMoving = false;
		_orbFogHandler.currentWaitTime = 0f;
		_orbFogHandler.hasArrived = false;
		_orbFogHandler.currentStartHeight = float.NegativeInfinity;
		_orbFogHandler.currentStartForward = float.NegativeInfinity;
		_orbFogHandler.currentSize = 0f;
		FogSphere sphere = _fogSphere ?? ResolveFogSphere(_orbFogHandler);
		if (!(sphere == null))
		{
			_fogSphere = sphere;
			sphere.currentSize = 0f;
			if (sphere.gameObject.activeSelf)
			{
				sphere.gameObject.SetActive(value: false);
			}
		}
	}


	private void ClearSyntheticFogStage()
	{
		_pendingSyntheticFogSegmentId = -1;
		_activeSyntheticFogSegmentId = -1;
		_syntheticFogPoint = Vector3.zero;
		_syntheticFogStartSize = 0f;
	}


	private static bool TryMapSegmentToFogOriginId(Segment segment, int availableOriginCount, out int originId)
	{
		originId = -1;
		if (availableOriginCount <= 0)
		{
			return false;
		}
		originId = Mathf.Clamp((int)segment, 0, availableOriginCount - 1);
		return true;
	}


	private void TryAlignFogOriginToCurrentSegment()
	{
		if (!(_orbFogHandler == null) && HasFogAuthority() && !_orbFogHandler.isMoving && !IsFogRemovedInCurrentScene() && TryGetTargetFogOriginId(out var expectedOriginId) && _orbFogHandler.currentID != expectedOriginId)
		{
			int previousOriginId = _orbFogHandler.currentID;
			_orbFogHandler.SetFogOrigin(expectedOriginId);
			EnsureFogCoverage(_orbFogHandler);
			SyncFogOriginToGuests();
			base.Logger.LogInfo(string.Format("[{0}] Aligned fog origin from {1} to {2}.", "Fog&ColdControl", previousOriginId, expectedOriginId));
		}
	}


	private void TryUpdateSyntheticFogStage()
	{
		if (_orbFogHandler == null || !HasFogAuthority())
		{
			return;
		}
		MapHandler instance = Singleton<MapHandler>.Instance;
		int availableOriginCount = GetAvailableFogOriginCount();
		if (instance == null || availableOriginCount <= 0)
		{
			return;
		}
		int currentSceneSegmentId = (int)MapHandler.CurrentSegmentNumber;
		if (currentSceneSegmentId < availableOriginCount && _pendingSyntheticFogSegmentId < availableOriginCount)
		{
			if (_activeSyntheticFogSegmentId >= 0)
			{
				ClearSyntheticFogStage();
			}
			return;
		}
		int targetSyntheticSegmentId = -1;
		if (_pendingSyntheticFogSegmentId >= availableOriginCount && currentSceneSegmentId >= _pendingSyntheticFogSegmentId)
		{
			targetSyntheticSegmentId = _pendingSyntheticFogSegmentId;
			_pendingSyntheticFogSegmentId = -1;
		}
		else if (currentSceneSegmentId >= availableOriginCount)
		{
			targetSyntheticSegmentId = currentSceneSegmentId;
		}
		else if (_activeSyntheticFogSegmentId >= availableOriginCount)
		{
			targetSyntheticSegmentId = _activeSyntheticFogSegmentId;
		}
		if (targetSyntheticSegmentId < availableOriginCount)
		{
			return;
		}
		Segment targetSyntheticSegment = (Segment)targetSyntheticSegmentId;
		if (!ShouldUseCustomFogPositionForSegment(targetSyntheticSegment))
		{
			if (_activeSyntheticFogSegmentId >= availableOriginCount)
			{
				ClearSyntheticFogStage();
			}
		}
		else
		{
			if (!TryBuildSyntheticFogStage(targetSyntheticSegment, out var fogPoint, out var fogSize, out var anchorDescription))
			{
				return;
			}
			bool num = _activeSyntheticFogSegmentId != targetSyntheticSegmentId;
			bool allowRuntimeRefresh = num || !_orbFogHandler.isMoving || _orbFogHandler.hasArrived || _syntheticFogStartSize <= 0f;
			bool pointChanged = num || (allowRuntimeRefresh && Vector3.Distance(_syntheticFogPoint, fogPoint) > 0.1f);
			bool sizeChanged = num || (allowRuntimeRefresh && Mathf.Abs(_syntheticFogStartSize - fogSize) > 0.1f);
			_activeSyntheticFogSegmentId = targetSyntheticSegmentId;
			if (pointChanged)
			{
				_syntheticFogPoint = fogPoint;
			}
			if (sizeChanged)
			{
				_syntheticFogStartSize = fogSize;
			}
			bool num2 = pointChanged || sizeChanged;
			ApplySyntheticFogStageToHandler(sizeChanged);
			if (num2)
			{
				base.Logger.LogInfo(string.Format("[{0}] Activated synthetic fog stage {1} using {2}. Point={3}, size={4:F1}, delayCompleted={5}, moving={6}.", "Fog&ColdControl", targetSyntheticSegmentId, anchorDescription, fogPoint, fogSize, _initialDelayCompleted, _orbFogHandler.isMoving));
				if (_initialDelayCompleted && !_orbFogHandler.isMoving)
				{
					StartFogMovement();
				}
			}
		}
	}


	private void ApplySyntheticFogStageToHandler(bool resetCurrentSize)
	{
		if (_orbFogHandler == null)
		{
			return;
		}
		FogSphere sphere = _fogSphere ?? ResolveFogSphere(_orbFogHandler);
		if (sphere != null)
		{
			_fogSphere = sphere;
			if (!sphere.gameObject.activeSelf)
			{
				sphere.gameObject.SetActive(value: true);
			}
			sphere.fogPoint = _syntheticFogPoint;
			if (resetCurrentSize)
			{
				sphere.currentSize = _syntheticFogStartSize;
			}
		}
		_orbFogHandler.currentStartHeight = float.NegativeInfinity;
		_orbFogHandler.currentStartForward = float.NegativeInfinity;
		if (resetCurrentSize)
		{
			_orbFogHandler.currentSize = _syntheticFogStartSize;
			_orbFogHandler.currentWaitTime = 0f;
			_orbFogHandler.hasArrived = false;
		}
	}


	private bool TryResolveSyntheticTargetAnchor(Segment syntheticSegment, out Vector3 targetAnchor, out string targetDescription)
	{
		targetAnchor = Vector3.zero;
		targetDescription = string.Empty;
		MapHandler mapHandler = Singleton<MapHandler>.Instance;
		if (mapHandler == null)
		{
			return false;
		}
		if ((int)syntheticSegment >= 5 && mapHandler.respawnThePeak != null)
		{
			targetAnchor = mapHandler.respawnThePeak.position;
			targetDescription = "respawnThePeak";
			return true;
		}
		if ((int)syntheticSegment >= 4)
		{
			if (mapHandler.segments != null && mapHandler.segments.Length > 4)
			{
				MapHandler.MapSegment kilnSegment = mapHandler.segments[4];
				if (kilnSegment != null && kilnSegment.reconnectSpawnPos != null)
				{
					targetAnchor = kilnSegment.reconnectSpawnPos.position;
					targetDescription = "TheKiln reconnectSpawnPos";
					return true;
				}
				if (kilnSegment != null && kilnSegment.segmentParent != null)
				{
					targetAnchor = kilnSegment.segmentParent.transform.position;
					targetDescription = "TheKiln segmentParent";
					return true;
				}
			}
		}
		return false;
	}


	private bool TryResolveSyntheticFogAnchorFromOrigins(Segment syntheticSegment, out Vector3 fogPoint, out string anchorDescription)
	{
		fogPoint = Vector3.zero;
		anchorDescription = string.Empty;
		FogSphereOrigin[] origins = ResolveFogOrigins(_orbFogHandler);
		FogSphereOrigin lastOrigin = origins.LastOrDefault((FogSphereOrigin origin) => origin != null);
		if (lastOrigin == null)
		{
			return false;
		}
		FogSphereOrigin previousOrigin = origins.Where((FogSphereOrigin origin) => origin != null).Reverse().Skip(1)
			.FirstOrDefault();
		if (previousOrigin == null)
		{
			return false;
		}
		Vector3 pathStep = lastOrigin.transform.position - previousOrigin.transform.position;
		if (pathStep.sqrMagnitude < 1f)
		{
			return false;
		}
		int extraStageIndex = Mathf.Max((int)syntheticSegment - (origins.Length - 1), 1);
		Vector3 direction = pathStep.normalized;
		Vector3 extrapolatedPoint = lastOrigin.transform.position + pathStep * extraStageIndex;
		if (TryResolveSyntheticTargetAnchor(syntheticSegment, out var targetAnchor, out var targetDescription))
		{
			float desiredDistance = Mathf.Max(Vector3.Dot(targetAnchor - lastOrigin.transform.position, direction) + 180f, pathStep.magnitude * (float)extraStageIndex);
			extrapolatedPoint = lastOrigin.transform.position + direction * desiredDistance;
			Vector3 lateralOffset = Vector3.ProjectOnPlane(targetAnchor - extrapolatedPoint, direction);
			extrapolatedPoint += lateralOffset * 0.25f;
			anchorDescription = "origin-extrapolated + " + targetDescription;
		}
		else
		{
			anchorDescription = "origin-extrapolated";
		}
		fogPoint = extrapolatedPoint;
		return true;
	}


	private bool TryResolveSyntheticFogAnchor(Segment syntheticSegment, out Vector3 fogPoint, out string anchorDescription)
	{
		fogPoint = Vector3.zero;
		anchorDescription = string.Empty;
		if (TryResolveSyntheticFogAnchorFromOrigins(syntheticSegment, out fogPoint, out anchorDescription))
		{
			return true;
		}
		if (Singleton<MapHandler>.Instance == null)
		{
			return false;
		}
		if (TryResolveSyntheticTargetAnchor(syntheticSegment, out fogPoint, out anchorDescription))
		{
			return true;
		}
		Character localCharacter = Character.localCharacter;
		if (localCharacter != null)
		{
			fogPoint = localCharacter.Center;
			anchorDescription = "localCharacter";
			return true;
		}
		Character fallbackCharacter = Character.AllCharacters.FirstOrDefault((Character character) => character != null);
		if (fallbackCharacter != null)
		{
			fogPoint = fallbackCharacter.Center;
			anchorDescription = "firstCharacter";
			return true;
		}
		return false;
	}


	private bool TryResolvePeakVerticalFogAnchor(Segment syntheticSegment, out Vector3 fogPoint, out string anchorDescription)
	{
		fogPoint = Vector3.zero;
		anchorDescription = string.Empty;
		if ((int)syntheticSegment < 5)
		{
			return false;
		}
		if (!TryResolvePeakVerticalFogTargetAnchor(syntheticSegment, out var targetAnchor, out var targetDescription))
		{
			return false;
		}
		fogPoint = new Vector3(targetAnchor.x, 1830f, targetAnchor.z);
		anchorDescription = $"{targetDescription} vertical-up stop@{1800f:F0}";
		return true;
	}


	private bool TryResolvePeakVerticalFogTargetAnchor(Segment syntheticSegment, out Vector3 targetAnchor, out string targetDescription)
	{
		targetAnchor = Vector3.zero;
		targetDescription = string.Empty;
		if ((int)syntheticSegment < 5)
		{
			return false;
		}
		if (TryResolveSyntheticTargetAnchor(syntheticSegment, out targetAnchor, out targetDescription))
		{
			return true;
		}
		Character localCharacter = Character.localCharacter;
		if (localCharacter != null)
		{
			targetAnchor = localCharacter.Center;
			targetDescription = "localCharacter";
			return true;
		}
		Character fallbackCharacter = Character.AllCharacters.FirstOrDefault((Character character) => character != null);
		if (fallbackCharacter == null)
		{
			return false;
		}
		targetAnchor = fallbackCharacter.Center;
		targetDescription = "firstCharacter";
		return true;
	}


	private float ComputeSyntheticFogStartSize(Segment syntheticSegment, Vector3 fogPoint)
	{
		float previousOriginSize = 900f;
		TryGetPreviousRealFogOriginSize(out previousOriginSize);
		float baseSize = previousOriginSize * 0.72f;
		return ComputeFogStartSizeForStage(syntheticSegment, fogPoint, baseSize, 650f, 1400f, 130f, 95f);
	}


	private float ComputePeakVerticalFogStartSize(Vector3 fogPoint, Vector3 targetAnchor)
	{
		float previousOriginSize = 900f;
		TryGetPreviousRealFogOriginSize(out previousOriginSize);
		float computedSize = Mathf.Max(previousOriginSize * 0.68f, 650f);
		float lowestRelevantY = Mathf.Min(targetAnchor.y, 1620f);
		foreach (Character character in Character.AllCharacters)
		{
			if (!(character == null) && !(character.data == null) && !character.data.dead)
			{
				lowestRelevantY = Mathf.Min(lowestRelevantY, character.Center.y);
				computedSize = Mathf.Max(computedSize, Vector3.Distance(fogPoint, character.Center) + 70f);
			}
		}
		computedSize = Mathf.Max(computedSize, fogPoint.y - lowestRelevantY + 120f);
		if (TryResolveFogStageCoverageAnchor(Segment.Peak, out var stageAnchor, out var _))
		{
			computedSize = Mathf.Max(computedSize, Vector3.Distance(fogPoint, stageAnchor) + 90f);
		}
		return Mathf.Clamp(computedSize, 650f, 1400f);
	}


	private void TryAutoStartCustomRunFogIfNeeded()
	{
		if (!(_orbFogHandler == null) && HasFogAuthority() && !_fogPaused && RunSettings.IsCustomRun && !ShouldHoldFogUntilCampfireActivation(_orbFogHandler) && !_orbFogHandler.isMoving && !_orbFogHandler.hasArrived && !(_orbFogHandler.currentWaitTime > 0.2f))
		{
			StartFogMovement();
		}
	}


	private void TryAutoResumeStalledFogMovement()
	{
		if (!(_orbFogHandler == null) && HasFogAuthority() && !_fogPaused && _initialDelayCompleted && !ShouldHoldFogUntilCampfireActivation(_orbFogHandler) && _orbFogHandler.currentID > 0 && !_orbFogHandler.isMoving && !_orbFogHandler.hasArrived && TryGetTargetFogOriginId(out var expectedOriginId) && expectedOriginId == _orbFogHandler.currentID)
		{
			float stalledThreshold = GetStalledFogResumeDelaySeconds(_orbFogHandler);
			if (!(_orbFogHandler.currentWaitTime < stalledThreshold))
			{
				base.Logger.LogInfo(string.Format("[{0}] Resuming stalled fog at origin {1} after waiting {2:F1}s.", "Fog&ColdControl", _orbFogHandler.currentID, _orbFogHandler.currentWaitTime));
				StartFogMovement();
			}
		}
	}


	private static float GetStalledFogResumeDelaySeconds(OrbFogHandler fogHandler)
	{
		if (fogHandler == null)
		{
			return 8f;
		}
		return Mathf.Clamp(fogHandler.maxWaitTime * 0.02f, 2f, 8f);
	}


	private void UpdateTrackedFogOrigin()
	{
		if (_orbFogHandler == null || _trackedFogOriginId == _orbFogHandler.currentID)
		{
			return;
		}
		bool returnedToFirstOrigin = _orbFogHandler.currentID == 0 && _trackedFogOriginId > 0;
		_trackedFogOriginId = _orbFogHandler.currentID;
		if (_trackedFogOriginId > 0)
		{
			if (IsCampfireDelayPendingForOrigin(_trackedFogOriginId))
			{
				_fogHiddenBufferTimer = 0f;
				_fogDelayTimer = 0f;
				_initialDelayCompleted = false;
				if (_initialDelayCompleted)
				{
					ClearPendingCampfireDelayForOrigin(_trackedFogOriginId);
					if (!_orbFogHandler.isMoving)
					{
						StartFogMovement();
					}
				}
			}
			else
			{
				bool isOriginActive = (_initialDelayCompleted = _orbFogHandler.isMoving || _orbFogHandler.hasArrived || _orbFogHandler.currentWaitTime > 0.2f);
				_fogHiddenBufferTimer = (isOriginActive ? 10f : 0f);
				_fogDelayTimer = ((!isOriginActive) ? 0f : (FogDelay?.Value ?? 0f));
			}
		}
		else if (returnedToFirstOrigin)
		{
			_fogHiddenBufferTimer = 0f;
			_fogDelayTimer = 0f;
			_initialDelayCompleted = ShouldSkipInitialDelay(_orbFogHandler);
		}
	}


	private void StartFogMovement()
	{
		if (_orbFogHandler == null || _fogPaused || IsFogRemovedInCurrentScene())
		{
			return;
		}
		if (HasFogAuthority() && FogSpeed != null && _orbFogHandler.speed < 0.02f)
		{
			_orbFogHandler.speed = FogSpeed.Value;
		}
		base.Logger.LogInfo(string.Format("[{0}] Starting fog movement. currentOrigin={1}, syntheticStage={2}, pendingOrigin={3}, currentSize={4:F1}.", "Fog&ColdControl", _orbFogHandler.currentID, _activeSyntheticFogSegmentId, _delayedFogOriginId, _orbFogHandler.currentSize));
		ClearPendingCampfireDelayForOrigin(_orbFogHandler.currentID);
		bool shouldGrantInitialCompass = !_initialCompassGranted && _orbFogHandler.currentID == 0;
		if (!TryInvokeFogStartMovement(_orbFogHandler, out var invocationPath))
		{
			base.Logger.LogError(string.Format("[{0}] Failed to start fog movement because no compatible OrbFogHandler start method was found. currentOrigin={1}.", "Fog&ColdControl", _orbFogHandler.currentID));
			return;
		}
		base.Logger.LogInfo("[Fog&ColdControl] Fog movement invoked via " + invocationPath + ".");
		if (PhotonNetwork.InRoom)
		{
			ForceSyncFogStateToGuests();
		}
		InitializeFogArrivalEstimate(_orbFogHandler.currentID, GetObservedFogEtaSize(), Time.unscaledTime);
		if (shouldGrantInitialCompass && !HasRemotePlayersInJoinGracePeriod())
		{
			_initialCompassGranted = true;
			GrantCompassToAllPlayers("initial-delay-ended");
		}
	}


	private bool TryInvokeFogStartMovement(OrbFogHandler fogHandler, out string invocationPath)
	{
		invocationPath = string.Empty;
		if (fogHandler == null)
		{
			return false;
		}
		Type fogHandlerType = fogHandler.GetType();
		MethodInfo startWithoutInfo = fogHandlerType.GetMethod("StartMovingRPC", InstanceBindingFlags, null, Type.EmptyTypes, null);
		if (TryInvokeFogStartMethod(fogHandler, startWithoutInfo, Array.Empty<object>()))
		{
			invocationPath = "StartMovingRPC()";
			return true;
		}
		MethodInfo startWithInfo = fogHandlerType.GetMethod("StartMovingRPC", InstanceBindingFlags, null, new Type[1] { typeof(PhotonMessageInfo) }, null);
		if (TryInvokeFogStartMethod(fogHandler, startWithInfo, new object[1] { default(PhotonMessageInfo) }))
		{
			invocationPath = "StartMovingRPC(PhotonMessageInfo)";
			return true;
		}
		MethodInfo waitToMove = fogHandlerType.GetMethod("WaitToMove", InstanceBindingFlags, null, Type.EmptyTypes, null);
		if (TryInvokeFogStartMethod(fogHandler, waitToMove, Array.Empty<object>()))
		{
			invocationPath = "WaitToMove()";
			return true;
		}
		return false;
	}


	private bool TryInvokeFogStartMethod(OrbFogHandler fogHandler, MethodInfo method, object[] arguments)
	{
		if (fogHandler == null || method == null)
		{
			return false;
		}
		try
		{
			method.Invoke(fogHandler, arguments);
			return true;
		}
		catch (TargetInvocationException ex)
		{
			base.Logger.LogWarning("[Fog&ColdControl] Fog start method " + method.Name + " failed: " + (ex.InnerException?.Message ?? ex.Message));
			return false;
		}
		catch (Exception ex2)
		{
			base.Logger.LogWarning("[Fog&ColdControl] Fog start method " + method.Name + " failed: " + ex2.Message);
			return false;
		}
	}


	private void ApplyPausedFogState(bool syncImmediately)
	{
		if (!(_orbFogHandler == null))
		{
			_orbFogHandler.speed = 0f;
			_orbFogHandler.isMoving = false;
			if (syncImmediately)
			{
				ForceSyncFogStateToGuests();
			}
		}
	}


	private void ForceSyncFogStateToGuests()
	{
		if (!(_orbFogHandler == null) && HasRemotePlayers() && PhotonNetwork.IsMasterClient && !HasRemotePlayersInJoinGracePeriod())
		{
			_lastFogStateSyncTime = -0.18f;
			ResetFogStateSyncSnapshot();
			SyncFogStateToGuestsIfNeeded();
		}
	}


	private void UpdateFogArrivalEstimate()
	{
		if (_orbFogHandler == null || _fogPaused || IsFogRemovedInCurrentScene() || !_orbFogHandler.isMoving || _orbFogHandler.hasArrived)
		{
			ResetFogArrivalEstimate();
			return;
		}
		float handlerSpeed = _orbFogHandler.speed;
		if (handlerSpeed >= 0.02f && Mathf.Abs(handlerSpeed - _fogEtaLastAppliedHandlerSpeed) > 0.005f)
		{
			_fogEtaLastAppliedHandlerSpeed = handlerSpeed;
			_fogEtaHasReliableRate = false;
			_fogEtaEstimatedUnitsPerSecond = 0f;
			_fogEtaLastObservedSize = float.NaN;
			_fogEtaLastObservedTime = -1f;
			_fogEtaLastRateResetTime = Time.unscaledTime;
		}
		float observedSize = GetObservedFogEtaSize();
		float now = Time.unscaledTime;
		int originId = _orbFogHandler.currentID;
		if (_fogEtaTrackedOriginId != originId || float.IsNaN(_fogEtaLastObservedSize) || _fogEtaLastObservedTime < 0f)
		{
			InitializeFogArrivalEstimate(originId, observedSize, now);
			return;
		}
		float elapsed = now - _fogEtaLastObservedTime;
		if (elapsed < 0.1f)
		{
			return;
		}
		float sizeDelta = _fogEtaLastObservedSize - observedSize;
		if (sizeDelta <= -0.05f)
		{
			InitializeFogArrivalEstimate(originId, observedSize, now);
		}
		else if (!(Mathf.Abs(sizeDelta) < 0.05f))
		{
			_fogEtaLastObservedSize = observedSize;
			_fogEtaLastObservedTime = now;
			float measuredRate = sizeDelta / elapsed;
			if (measuredRate < 0.02f)
			{
				UpdateFogArrivalMetricsCache(force: false);
				return;
			}
			_fogEtaEstimatedUnitsPerSecond = (_fogEtaHasReliableRate ? Mathf.Lerp(_fogEtaEstimatedUnitsPerSecond, measuredRate, 0.15f) : measuredRate);
			_fogEtaHasReliableRate = true;
			UpdateFogArrivalMetricsCache(force: false);
		}
	}


	private bool TryGetFogArrivalEtaSeconds(out float etaSeconds)
	{
		etaSeconds = 0f;
		UpdateFogArrivalMetricsCache(force: false);
		if (!_fogDistanceEtaHasSnapshot)
		{
			return false;
		}
		if (_fogDistanceEtaRemainingDistance <= 0.05f)
		{
			etaSeconds = 0f;
			return true;
		}
		if (!_fogDistanceEtaHasEta)
		{
			return false;
		}
		etaSeconds = _fogDistanceEtaSeconds;
		return true;
	}


	private bool TryGetFogArrivalRemainingDistance(out float remainingDistance)
	{
		remainingDistance = 0f;
		UpdateFogArrivalMetricsCache(force: false);
		if (!_fogDistanceEtaHasSnapshot)
		{
			return false;
		}
		remainingDistance = _fogDistanceEtaRemainingDistance;
		return true;
	}


	private bool TryGetDisplayedFogEtaGeometry(out float currentSize, out Vector3 fogPoint, out Vector3 playerPoint)
	{
		currentSize = GetDisplayedFogEtaSize();
		fogPoint = Vector3.zero;
		playerPoint = Vector3.zero;
		if (!TryResolveFogUiSubjectPosition(out playerPoint))
		{
			return false;
		}
		string pointDescription;
		return TryResolveFogPointForCurrentOrigin(out fogPoint, out pointDescription);
	}


	private float GetObservedFogEtaSize()
	{
		if (_orbFogHandler == null)
		{
			return 30f;
		}
		float rawSize = _orbFogHandler.currentSize;
		if (IsReadOnlyFogUiViewer() && TryGetGuestFogSyncOverrideSize(out var overrideSize))
		{
			rawSize = overrideSize;
		}
		return Mathf.Max(rawSize, 30f);
	}


	private float GetDisplayedFogEtaSize()
	{
		float observedSize = GetObservedFogEtaSize();
		if (!_fogEtaHasReliableRate || float.IsNaN(_fogEtaLastObservedSize) || _fogEtaLastObservedTime < 0f)
		{
			return observedSize;
		}
		if (Mathf.Abs(observedSize - _fogEtaLastObservedSize) >= 0.05f)
		{
			return observedSize;
		}
		float extrapolatedSize = _fogEtaLastObservedSize - _fogEtaEstimatedUnitsPerSecond * Mathf.Max(Time.unscaledTime - _fogEtaLastObservedTime, 0f);
		return Mathf.Clamp(Mathf.Min(observedSize, extrapolatedSize), 30f, observedSize);
	}


	private void InitializeFogArrivalEstimate(int originId, float observedSize, float sampleTime)
	{
		_fogEtaTrackedOriginId = originId;
		_fogEtaLastObservedSize = observedSize;
		_fogEtaLastObservedTime = sampleTime;
		_fogEtaEstimatedUnitsPerSecond = 0f;
		_fogEtaHasReliableRate = false;
	}


	private void ResetFogArrivalEstimate()
	{
		_fogEtaTrackedOriginId = -1;
		_fogEtaLastObservedSize = float.NaN;
		_fogEtaLastObservedTime = -1f;
		_fogEtaEstimatedUnitsPerSecond = 0f;
		_fogEtaHasReliableRate = false;
		ResetFogArrivalMetricsCache();
	}


	private void UpdateFogArrivalMetricsCache(bool force)
	{
		if (_orbFogHandler == null || _fogPaused || IsFogRemovedInCurrentScene() || !_orbFogHandler.isMoving || _orbFogHandler.hasArrived)
		{
			ResetFogArrivalMetricsCache();
			return;
		}
		float now = Time.unscaledTime;
		if (!force && _fogDistanceEtaLastRefreshTime >= 0f && now - _fogDistanceEtaLastRefreshTime < 0.8f)
		{
			return;
		}
		if (!TryGetDisplayedFogEtaGeometry(out var currentSize, out var fogPoint, out var playerPoint))
		{
			ResetFogArrivalMetricsCache();
			return;
		}
		float previousRefreshTime = _fogDistanceEtaLastRefreshTime;
		float remainingDistance = Mathf.Max(currentSize - Vector3.Distance(fogPoint, playerPoint), 0f);
		_fogDistanceEtaHasSnapshot = true;
		_fogDistanceEtaRemainingDistance = remainingDistance;
		float etaRate;
		if (remainingDistance <= 0.05f)
		{
			_fogDistanceEtaHasEta = true;
			_fogDistanceEtaSeconds = 0f;
			_fogDistanceEtaLastRefreshTime = now;
		}
		else if (!TryResolveFogEtaRate(out etaRate))
		{
			_fogDistanceEtaHasEta = false;
			_fogDistanceEtaSeconds = 0f;
			_fogDistanceEtaLastRefreshTime = now;
		}
		else
		{
			float rawEtaSeconds = remainingDistance / etaRate;
			_fogDistanceEtaHasEta = true;
			_fogDistanceEtaSeconds = StabilizeFogEtaSeconds(rawEtaSeconds, now, previousRefreshTime);
			_fogDistanceEtaLastRefreshTime = now;
		}
	}


	private float StabilizeFogEtaSeconds(float rawEtaSeconds, float now, float previousRefreshTime)
	{
		rawEtaSeconds = Mathf.Max(rawEtaSeconds, 0f);
		if (_fogEtaLastRateResetTime >= 0f && now - _fogEtaLastRateResetTime < 3f)
		{
			return rawEtaSeconds;
		}
		if (!_fogDistanceEtaHasEta || previousRefreshTime < 0f || _fogDistanceEtaSeconds <= 0f || rawEtaSeconds <= 0.5f)
		{
			return rawEtaSeconds;
		}
		float elapsed = Mathf.Max(now - previousRefreshTime, 0f);
		float expectedCountdown = Mathf.Max(_fogDistanceEtaSeconds - elapsed, 0f);
		if (rawEtaSeconds > expectedCountdown)
		{
			if (rawEtaSeconds - expectedCountdown <= 2f)
			{
				return expectedCountdown;
			}
			float maxIncrease = Mathf.Max(0.5f, elapsed * 1.5f);
			return Mathf.Min(rawEtaSeconds, expectedCountdown + maxIncrease);
		}
		float maxDecrease = elapsed + Mathf.Max(0.5f, elapsed * 3f);
		return Mathf.Max(rawEtaSeconds, expectedCountdown - maxDecrease);
	}


	private bool TryResolveFogEtaRate(out float etaRate)
	{
		etaRate = 0f;
		if (IsReadOnlyFogUiViewer() && _syncedHostFogSpeed >= 0.02f)
		{
			etaRate = _syncedHostFogSpeed;
			return true;
		}
		if (_fogEtaHasReliableRate && _fogEtaEstimatedUnitsPerSecond >= 0.02f)
		{
			etaRate = _fogEtaEstimatedUnitsPerSecond;
		}
		else if (_orbFogHandler != null && _orbFogHandler.speed >= 0.02f)
		{
			etaRate = _orbFogHandler.speed;
		}
		return etaRate >= 0.02f;
	}


	private void ResetFogArrivalMetricsCache()
	{
		_fogDistanceEtaLastRefreshTime = -1f;
		_fogDistanceEtaHasSnapshot = false;
		_fogDistanceEtaHasEta = false;
		_fogDistanceEtaRemainingDistance = 0f;
		_fogDistanceEtaSeconds = 0f;
	}


	private bool TryGetTargetFogOriginId(out int expectedOriginId)
	{
		if (_delayedFogOriginId >= 0)
		{
			expectedOriginId = _delayedFogOriginId;
			return true;
		}
		return TryGetExpectedFogOriginIdFromScene(out expectedOriginId);
	}


	private bool TryGetExpectedFogOriginIdFromScene(out int expectedOriginId)
	{
		expectedOriginId = -1;
		if (!IsGameplayFogScene(SceneManager.GetActiveScene()) || LoadingScreenHandler.loading)
		{
			return false;
		}
		if (Singleton<MapHandler>.Instance == null)
		{
			return false;
		}
		if (ShouldDeferFogToVanillaForSegment(MapHandler.CurrentSegmentNumber))
		{
			return false;
		}
		int availableOriginCount = GetAvailableFogOriginCount();
		return TryMapSegmentToFogOriginId(MapHandler.CurrentSegmentNumber, availableOriginCount, out expectedOriginId);
	}


	private void ResetFogStateSyncSnapshot()
	{
		_hasSyncedFogStateSnapshot = false;
		_lastSyncedFogOriginId = -1;
		_lastSyncedFogSize = float.NaN;
		_lastSyncedFogIsMoving = false;
		_lastSyncedFogHasArrived = false;
	}


	private void CacheFogStateSyncSnapshot()
	{
		if (_orbFogHandler == null)
		{
			ResetFogStateSyncSnapshot();
			return;
		}
		_hasSyncedFogStateSnapshot = true;
		_lastSyncedFogOriginId = _orbFogHandler.currentID;
		_lastSyncedFogSize = _orbFogHandler.currentSize;
		_lastSyncedFogIsMoving = _orbFogHandler.isMoving;
		_lastSyncedFogHasArrived = _orbFogHandler.hasArrived;
	}


	private bool NeedsFogStateInitSync()
	{
		if (_hasSyncedFogStateSnapshot && !(_orbFogHandler == null) && _orbFogHandler.currentID == _lastSyncedFogOriginId)
		{
			return _orbFogHandler.hasArrived != _lastSyncedFogHasArrived;
		}
		return true;
	}


	private bool NeedsFogStateDeltaSync()
	{
		if (_hasSyncedFogStateSnapshot && !(_orbFogHandler == null) && _orbFogHandler.isMoving == _lastSyncedFogIsMoving && !float.IsNaN(_lastSyncedFogSize))
		{
			return Mathf.Abs(_orbFogHandler.currentSize - _lastSyncedFogSize) >= 0.35f;
		}
		return true;
	}


	private float GetFogStateSyncSizeForGuests()
	{
		if (_orbFogHandler == null)
		{
			return 0f;
		}
		if (IsFogRemovedInCurrentScene())
		{
			return 0f;
		}
		if (!TryGetGuestFogSyncOverrideSize(out var overrideSize))
		{
			return _orbFogHandler.currentSize;
		}
		return overrideSize;
	}


	private bool TryGetGuestFogSyncOverrideSize(out float overrideSize)
	{
		overrideSize = 0f;
		if (_orbFogHandler == null)
		{
			return false;
		}
		if (!TryGetActiveFogSyncReference(out var customFogPoint, out var fogSegment))
		{
			return false;
		}
		if (!TryResolveFogOriginPoint(_orbFogHandler.currentID, out var vanillaFogPoint))
		{
			return false;
		}
		if (Vector3.Distance(vanillaFogPoint, customFogPoint) <= 0.05f)
		{
			return false;
		}
		if (!TryResolveFogStageCoverageAnchor(fogSegment, out var syncReferencePoint, out var _))
		{
			return false;
		}
		float hostReferenceDistance = Vector3.Distance(customFogPoint, syncReferencePoint);
		float vanillaReferenceDistance = Vector3.Distance(vanillaFogPoint, syncReferencePoint);
		overrideSize = Mathf.Clamp(vanillaReferenceDistance + (_orbFogHandler.currentSize - hostReferenceDistance), 30f, 6000f);
		return !Approximately(overrideSize, _orbFogHandler.currentSize);
	}


	private bool TryGetActiveFogSyncReference(out Vector3 customFogPoint, out Segment fogSegment)
	{
		customFogPoint = Vector3.zero;
		fogSegment = Segment.Beach;
		if (_orbFogHandler == null)
		{
			return false;
		}
		int availableOriginCount = GetAvailableFogOriginCount();
		if (_activeSyntheticFogSegmentId >= availableOriginCount && _activeSyntheticFogSegmentId >= 0)
		{
			customFogPoint = _syntheticFogPoint;
			fogSegment = (Segment)_activeSyntheticFogSegmentId;
			return customFogPoint.sqrMagnitude > 0.001f;
		}
		return false;
	}


	private bool TryResolveFogOriginPoint(int originId, out Vector3 fogPoint)
	{
		fogPoint = Vector3.zero;
		if (_orbFogHandler == null)
		{
			return false;
		}
		FogSphereOrigin[] origins = ResolveFogOrigins(_orbFogHandler);
		if (origins.Length == 0)
		{
			return false;
		}
		int resolvedOriginId = Mathf.Clamp(originId, 0, origins.Length - 1);
		FogSphereOrigin origin = origins[resolvedOriginId] ?? origins.LastOrDefault((FogSphereOrigin candidate) => candidate != null);
		if (origin == null)
		{
			return false;
		}
		fogPoint = origin.transform.position;
		return true;
	}


	private void SyncFogOriginToGuests()
	{
		if (_orbFogHandler == null || !HasRemotePlayers() || !PhotonNetwork.IsMasterClient)
		{
			ResetFogStateSyncSnapshot();
		}
		else if (ShouldDeferFogToVanillaInCurrentScene())
		{
			ResetFogStateSyncSnapshot();
		}
		else
		{
			if (HasRemotePlayersInJoinGracePeriod())
			{
				return;
			}
			PhotonView photonView = _orbFogHandler.GetComponent<PhotonView>();
			if (!(photonView == null))
			{
				try
				{
					float syncSize = GetFogStateSyncSizeForGuests();
					photonView.RPC("RPC_InitFog", RpcTarget.Others, _orbFogHandler.currentID, syncSize, _orbFogHandler.hasArrived, _orbFogHandler.isMoving);
					_lastFogStateSyncTime = Time.unscaledTime;
					CacheFogStateSyncSnapshot();
				}
				catch (Exception ex)
				{
					base.Logger.LogDebug("[Fog&ColdControl] Fog origin sync skipped: " + ex.Message);
				}
			}
		}
	}


	private void SyncFogStateToGuestsIfNeeded()
	{
		if (_orbFogHandler == null || !HasRemotePlayers() || !PhotonNetwork.IsMasterClient)
		{
			ResetFogStateSyncSnapshot();
		}
		else if (ShouldDeferFogToVanillaInCurrentScene())
		{
			ResetFogStateSyncSnapshot();
		}
		else
		{
			if (HasRemotePlayersInJoinGracePeriod())
			{
				return;
			}
			float now = Time.unscaledTime;
			if (now - _lastFogStateSyncTime < 0.18f)
			{
				return;
			}
			_lastFogStateSyncTime = now;
			PhotonView photonView = _orbFogHandler.GetComponent<PhotonView>();
			if (photonView == null)
			{
				return;
			}
			bool needsInitSync = NeedsFogStateInitSync();
			bool needsDeltaSync = NeedsFogStateDeltaSync();
			if (!needsInitSync && !needsDeltaSync)
			{
				return;
			}
			try
			{
				float syncSize = GetFogStateSyncSizeForGuests();
				if (needsInitSync)
				{
					photonView.RPC("RPC_InitFog", RpcTarget.Others, _orbFogHandler.currentID, syncSize, _orbFogHandler.hasArrived, _orbFogHandler.isMoving);
				}
				else
				{
					photonView.RPC("RPCA_SyncFog", RpcTarget.Others, syncSize, _orbFogHandler.isMoving);
				}
				TryLogHostFogSyncDiagnostics(needsInitSync, needsDeltaSync, syncSize);
				CacheFogStateSyncSnapshot();
			}
			catch (Exception ex)
			{
				base.Logger.LogDebug("[Fog&ColdControl] Fog sync skipped: " + ex.Message);
			}
		}
	}


	private void TryLogHostFogSyncDiagnostics(bool needsInitSync, bool needsDeltaSync, float syncSize)
	{
		if (!EnableDebugDiagnostics || !PhotonNetwork.IsMasterClient)
		{
			return;
		}
		float now = Time.unscaledTime;
		if (now - _lastHostFogSyncDiagLogTime < 3f)
		{
			return;
		}
		_lastHostFogSyncDiagLogTime = now;
		base.Logger.LogInfo(string.Format("[Fog&ColdControl] [DIAG-HOST-SYNC] init={0}, delta={1}, size={2:F1}, moving={3}, arrived={4}, origin={5}, grace={6}, defer={7}", needsInitSync, needsDeltaSync, syncSize, _orbFogHandler.isMoving, _orbFogHandler.hasArrived, _orbFogHandler.currentID, HasRemotePlayersInJoinGracePeriod(), ShouldDeferFogToVanillaInCurrentScene()));
	}


	private static string FormatFogEtaTime(float etaSeconds)
	{
		int num = Mathf.CeilToInt(Mathf.Max(etaSeconds, 0f));
		int minutes = num / 60;
		int seconds = num % 60;
		return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", minutes, seconds);
	}


	private static string FormatFogEtaSeconds(float etaSeconds)
	{
		int num = Mathf.CeilToInt(Mathf.Max(etaSeconds, 0f));
		return num.ToString(CultureInfo.InvariantCulture) + "s";
	}


	private bool IsCharacterInsideAnyFog(Character character)
	{
		float ratePerSecond;
		if (!IsCharacterInsideFogSphere(character))
		{
			return IsCharacterInsideLegacyFog(character, out ratePerSecond);
		}
		return true;
	}


	private bool IsCharacterInsideFogSphere(Character character)
	{
		if (_fogSphere != null && character != null && Mathf.Approximately(_fogSphere.ENABLE, 1f))
		{
			return Vector3.Distance(_fogSphere.fogPoint, character.Center) > _fogSphere.currentSize;
		}
		return false;
	}


	private bool IsCharacterInsideLegacyFog(Character character, out float ratePerSecond)
	{
		ratePerSecond = 0f;
		if (_legacyFog == null || character == null || !_legacyFog.isActiveAndEnabled || !_legacyFog.gameObject.activeInHierarchy)
		{
			return false;
		}
		if (!(character.Center.y < _legacyFog.transform.position.y))
		{
			return false;
		}
		ratePerSecond = Mathf.Max(_legacyFog.amount, 0f);
		return ratePerSecond > 0f;
	}


	private void HandleFogPauseHotkey()
	{
		if (!IsModFeatureEnabled() || !HasFogAuthority() || !IsGameplayFogScene(SceneManager.GetActiveScene()) || LoadingScreenHandler.loading)
		{
			return;
		}
		KeyCode hotkey = GetFogPauseHotkey();
		if (hotkey == KeyCode.None)
		{
			return;
		}
		GUIManager instance = GUIManager.instance;
		if ((instance != null && instance.windowBlockingInput) || !Input.GetKeyDown(hotkey))
		{
			return;
		}
		_fogPaused = !_fogPaused;
		if (_fogPaused)
		{
			ApplyPausedFogState(syncImmediately: true);
			base.Logger.LogInfo(string.Format("[{0}] Fog paused by hotkey {1}.", "Fog&ColdControl", hotkey));
			return;
		}
		base.Logger.LogInfo(string.Format("[{0}] Fog resumed by hotkey {1}.", "Fog&ColdControl", hotkey));
		if (_orbFogHandler != null && _initialDelayCompleted && !ShouldHoldFogUntilCampfireActivation(_orbFogHandler) && !_orbFogHandler.isMoving)
		{
			StartFogMovement();
		}
		ForceSyncFogStateToGuests();
	}


	private static KeyCode GetFogPauseHotkey()
	{
		return FogPauseHotkey?.Value ?? KeyCode.Y;
	}


	private static string GetFogPauseHotkeyLabel()
	{
		KeyCode hotkey = GetFogPauseHotkey();
		string keyText = hotkey.ToString();
		if (string.IsNullOrWhiteSpace(keyText) || hotkey == KeyCode.None)
		{
			return "None";
		}
		return keyText.ToUpperInvariant();
	}


	private static bool IsGameplayFogScene(Scene scene)
	{
		string sceneName = scene.name ?? string.Empty;
		if (sceneName.IndexOf("Island", StringComparison.OrdinalIgnoreCase) >= 0 || sceneName.IndexOf("Level_", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return true;
		}
		return GameHandler.IsOnIsland;
	}


	private bool TryResolveNextCampfireTarget(out Vector3 targetPosition)
	{
		targetPosition = Vector3.zero;
		if (!IsGameplayFogScene(SceneManager.GetActiveScene()) || LoadingScreenHandler.loading)
		{
			return false;
		}
		MapHandler mapHandler = Singleton<MapHandler>.Instance;
		if (mapHandler == null || mapHandler.segments == null || mapHandler.segments.Length == 0)
		{
			return false;
		}
		for (int segmentIndex = Mathf.Clamp((int)MapHandler.CurrentSegmentNumber, 0, mapHandler.segments.Length - 1); segmentIndex < mapHandler.segments.Length; segmentIndex++)
		{
			if (TryResolveSegmentCampfire(mapHandler.segments[segmentIndex], out var campfire) && !campfire.Lit)
			{
				targetPosition = campfire.transform.position;
				return true;
			}
		}
		return false;
	}


	private bool TryGetNextCampfireDistance(out float distance)
	{
		distance = 0f;
		if (!TryResolveNextCampfireTarget(out var targetPosition) || !TryResolveFogUiSubjectPosition(out var subjectPosition))
		{
			return false;
		}
		distance = Vector3.Distance(subjectPosition, targetPosition);
		return true;
	}


	private static bool TryResolveSegmentCampfire(MapHandler.MapSegment mapSegment, out Campfire campfire)
	{
		campfire = null;
		GameObject segmentCampfire = mapSegment?.segmentCampfire;
		if (segmentCampfire == null)
		{
			return false;
		}
		campfire = segmentCampfire.GetComponentInChildren<Campfire>(includeInactive: true);
		return campfire != null;
	}


	private static string GetCompactFogHandlingValue()
	{
		if (!IsFogColdSuppressionEnabled())
		{
			return Colorize("VAN", "#9FFFA8");
		}
		return Colorize("BLK", "#FFA64D");
	}


	private string GetCompactNextBiomeBadge(bool isChineseLanguage)
	{
		if (!TryGetNextBiomeDisplayName(isChineseLanguage, out var biomeName))
		{
			return string.Empty;
		}
		return BuildFogUiBadge("NXT", "#9FD7A6", Colorize(biomeName, "#E0F5E2"));
	}


	private string GetCompactNextCampfireDistanceBadge()
	{
		if (!TryGetNextCampfireDistance(out var distance))
		{
			return string.Empty;
		}
		return BuildFogUiBadge("FIR", "#FFB864", GetNextCampfireDistanceUiEntryText(distance));
	}


	private string GetCompactFogApproachBadge(bool isChineseLanguage)
	{
		if (!TryGetFogArrivalRemainingDistance(out var remainingDistance) || remainingDistance <= 0.05f)
		{
			return string.Empty;
		}
		GetFogApproachColors(remainingDistance, out var labelColor, out var _);
		return BuildFogUiBadge("FOG", labelColor, GetFogApproachUiEntryText(isChineseLanguage, remainingDistance));
	}


	private static string GetCompactFogHandlingBadge()
	{
		bool blocked = IsFogColdSuppressionEnabled();
		string labelColor = blocked ? "#FFA64D" : "#9FFFA8";
		return BuildFogUiBadge("FOG", labelColor, Colorize(blocked ? "OFF" : "ON", labelColor));
	}


	private string GetCompactLobbyFogDelayBadge(bool isChineseLanguage)
	{
		float delay = Mathf.Max(FogDelay?.Value ?? 900f, 0f);
		return BuildFogUiBadge("DLY", "#F2C75C", Colorize(FormatFogDelaySeconds(delay), "#FFE8A3"));
	}


	private string GetGuestFogRuntimeStateLabel(bool isChineseLanguage)
	{
		if (!IsModFeatureEnabled())
		{
			return Colorize(isChineseLanguage ? "关闭" : "OFF", "#FF6B6B");
		}
		if (IsAirportScene(SceneManager.GetActiveScene()))
		{
			return Colorize(isChineseLanguage ? "大厅" : "LOBBY", "#A3D2FF");
		}
		if (IsFogRemovedInCurrentScene())
		{
			return Colorize(isChineseLanguage ? "已禁用" : "DISABLED", "#FF6B6B");
		}
		if (IsGuestUsingVanillaHostFog())
		{
			return string.Empty;
		}
		if (_orbFogHandler == null)
		{
			return Colorize(isChineseLanguage ? "同步中" : "SYNCING", "#A3D2FF");
		}
		if (_orbFogHandler.isMoving)
		{
			return Colorize(isChineseLanguage ? "运行中" : "RUNNING", "#B5FFB8");
		}
		if (_orbFogHandler.hasArrived)
		{
			return Colorize(isChineseLanguage ? "已到达" : "ARRIVED", "#7BD4FF");
		}
		if (!_initialDelayCompleted || ShouldEnforceConfiguredDelay(_orbFogHandler))
		{
			return Colorize(isChineseLanguage ? "等待中" : "WAITING", "#FFE08A");
		}
		if (!_orbFogHandler.isMoving && !_orbFogHandler.hasArrived)
		{
			return Colorize(isChineseLanguage ? "等待中" : "WAITING", "#FFE08A");
		}
		return Colorize(isChineseLanguage ? "待机" : "IDLE", "#C8C8C8");
	}


	private string GetFogTimingLabel(bool isChineseLanguage)
	{
		float configuredDelay = Mathf.Max(FogDelay?.Value ?? 900f, 0f);
		string fogDelayText = (isChineseLanguage ? "毒雾延迟:" : "Fog Delay:");
		if (_orbFogHandler == null)
		{
			return Colorize(fogDelayText, "#F2C75C") + " " + Colorize(FormatFogDelaySeconds(configuredDelay), "#FFE8A3");
		}
		if (IsFogRemovedInCurrentScene() || _fogPaused || _orbFogHandler.isMoving || _orbFogHandler.hasArrived || _initialDelayCompleted)
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
		string fogDelayLabelColor = LerpHexColor(FogUiDelayCountdownStartLabelColor, FogUiDelayCountdownEndLabelColor, danger);
		string fogDelayValueColor = LerpHexColor(FogUiDelayCountdownStartValueColor, FogUiDelayCountdownEndValueColor, danger);
		return Colorize(fogDelayText, fogDelayLabelColor) + " " + Colorize(FormatFogDelaySeconds(delayRemaining), fogDelayValueColor);
	}


	private string GetFogArrivalEtaLabel(bool isChineseLanguage)
	{
		if (_orbFogHandler == null || IsFogRemovedInCurrentScene() || _fogPaused || !_orbFogHandler.isMoving || _orbFogHandler.hasArrived)
		{
			return string.Empty;
		}
		string etaText = (isChineseLanguage ? "到达:" : "ETA:");
		if (!TryGetFogArrivalEtaSeconds(out var etaSeconds))
		{
			string estimatingText = (isChineseLanguage ? "估算中" : "Estimating");
			return Colorize(etaText, "#B7C0CC") + " " + Colorize(estimatingText, "#E2EAF3");
		}
		if (etaSeconds <= 0.05f)
		{
			return string.Empty;
		}
		GetFogEtaColors(etaSeconds, out var labelColor, out var valueColor);
		return Colorize(etaText, labelColor) + " " + Colorize(FormatFogEtaTime(etaSeconds), valueColor);
	}


	private string GetFogDistanceLabel(bool isChineseLanguage)
	{
		if (!TryGetFogArrivalRemainingDistance(out var remainingDistance))
		{
			return string.Empty;
		}
		if (remainingDistance <= 0.05f)
		{
			return string.Empty;
		}
		GetFogDistanceColors(remainingDistance, out var displayLabelColor, out var displayValueColor);
		return Colorize(isChineseLanguage ? "距离:" : "Distance:", displayLabelColor) + " " + Colorize(FormatFogDistanceValue(remainingDistance), displayValueColor);
	}


	private string GetResolvedFogArrivalEtaLabel(bool isChineseLanguage)
	{
		if (_orbFogHandler == null || IsFogRemovedInCurrentScene() || _fogPaused || !_orbFogHandler.isMoving || _orbFogHandler.hasArrived)
		{
			return string.Empty;
		}
		string etaText = (isChineseLanguage ? "到达:" : "ETA:");
		if (!TryGetFogArrivalEtaSeconds(out var etaSeconds))
		{
			string estimatingText = (isChineseLanguage ? "估算中" : "Estimating");
			return Colorize(etaText, "#B7C0CC") + " " + Colorize(estimatingText, "#E2EAF3");
		}
		if (etaSeconds <= 0.05f)
		{
			return string.Empty;
		}
		float displayedEtaSeconds = QuantizeFogEtaSeconds(etaSeconds);
		GetFogEtaColors(displayedEtaSeconds, out var labelColor, out var valueColor);
		return Colorize(etaText, labelColor) + " " + Colorize(FormatFogEtaTime(displayedEtaSeconds), valueColor);
	}


	private string GetResolvedFogDistanceLabel(bool isChineseLanguage)
	{
		if (!TryGetFogArrivalRemainingDistance(out var remainingDistance))
		{
			return string.Empty;
		}
		if (remainingDistance <= 0.05f)
		{
			return string.Empty;
		}
		GetFogDistanceColors(remainingDistance, out var labelColor, out var valueColor);
		return Colorize(isChineseLanguage ? "距离:" : "Distance:", labelColor) + " " + Colorize(FormatFogDistanceValue(remainingDistance), valueColor);
	}


	private static void GetFogEtaColors(float etaSeconds, out string labelColor, out string valueColor)
	{
		float warning = Mathf.Clamp01(1f - etaSeconds / 90f);
		labelColor = LerpHexColor("#79E2D0", "#FFB864", warning);
		valueColor = LerpHexColor("#D9FFF5", "#FFE6BF", warning);
		if (!(etaSeconds > 45f))
		{
			float danger = Mathf.Clamp01(1f - etaSeconds / 45f);
			labelColor = LerpHexColor(labelColor, FogUiCountdownDangerLabelColor, danger);
			valueColor = LerpHexColor(valueColor, FogUiCountdownDangerValueColor, danger);
		}
	}


	private static void GetFogDistanceColors(float remainingDistance, out string labelColor, out string valueColor)
	{
		float warning = Mathf.Clamp01(1f - remainingDistance / 300f);
		labelColor = LerpHexColor("#79E2D0", "#FFB864", warning);
		valueColor = LerpHexColor("#D9FFF5", "#FFE6BF", warning);
		if (!(remainingDistance > 120f))
		{
			float danger = Mathf.Clamp01(1f - remainingDistance / 120f);
			labelColor = LerpHexColor(labelColor, FogUiCountdownDangerLabelColor, danger);
			valueColor = LerpHexColor(valueColor, FogUiCountdownDangerValueColor, danger);
		}
	}


	private void GetFogApproachColors(float remainingDistance, out string labelColor, out string valueColor)
	{
		GetFogDistanceColors(remainingDistance, out labelColor, out valueColor);
		if (!(_orbFogHandler == null) && !IsFogRemovedInCurrentScene() && !_fogPaused && _orbFogHandler.isMoving && !_orbFogHandler.hasArrived && TryGetFogArrivalEtaSeconds(out var etaSeconds) && !(etaSeconds <= 0.05f))
		{
			GetFogEtaColors(QuantizeFogEtaSeconds(etaSeconds), out var etaLabelColor, out var etaValueColor);
			labelColor = PickMoreUrgentFogUiColor(labelColor, etaLabelColor);
			valueColor = PickMoreUrgentFogUiColor(valueColor, etaValueColor);
		}
	}


	private static string FormatFogDistanceValue(float remainingDistance)
	{
		return string.Format("{0}{1}", Mathf.RoundToInt(Mathf.Max(remainingDistance, 0f)), FormatSmallUiUnit("m"));
	}


	private static string FormatFogSpeedValue(float speed)
	{
		return Mathf.Max(speed, 0f).ToString("0.0", CultureInfo.InvariantCulture);
	}


	private static string FormatFogDelaySeconds(float seconds)
	{
		int num = Mathf.CeilToInt(Mathf.Max(seconds, 0f));
		return num.ToString(CultureInfo.InvariantCulture) + FormatSmallUiUnit("s");
	}


	private static float QuantizeFogEtaSeconds(float etaSeconds)
	{
		if (etaSeconds <= 0f)
		{
			return 0f;
		}
		float step = Mathf.Max(0.5f, 0.1f);
		return Mathf.Ceil(etaSeconds / step) * step;
	}


	private bool TryGetGuestEstimatedFogSpeed(out float speed)
	{
		speed = 0f;
		if (!IsReadOnlyFogUiViewer() || _orbFogHandler == null || IsFogRemovedInCurrentScene()
			|| _fogPaused || !_orbFogHandler.isMoving || _orbFogHandler.hasArrived)
		{
			return false;
		}
		// Host metadata is authoritative: apply changes immediately and use the
		// same formatting as the host, without upward rounding or a stable delay.
		if (FogSpeedSamples.IsValidSpeed(_syncedHostFogSpeed))
		{
			speed = _syncedHostFogSpeed;
			return true;
		}
		if (_guestFogSampleHandler != _orbFogHandler || _guestFogSampleHostId != (PhotonNetwork.MasterClient?.ActorNumber ?? -1))
		{
			return false;
		}
		return _guestFogSpeedSamples.TryGetSpeed(_orbFogHandler.currentID, Time.time, out speed);
	}


	private bool ShouldPauseVanillaProgressStartForJoiningPlayers()
	{
		if (!HasFogAuthority() || !HasRemotePlayers())
		{
			return false;
		}
		if (HasRemotePlayersInJoinGracePeriod())
		{
			return true;
		}
		return !HaveAllRoomPlayersSpawnedProgressCharacters();
	}


	private string GetFogRuntimeStateLabel(bool isChineseLanguage)
	{
		if (!IsModFeatureEnabled())
		{
			return Colorize(isChineseLanguage ? "关闭" : "OFF", "#FF6B6B");
		}
		if (IsAirportScene(SceneManager.GetActiveScene()))
		{
			return Colorize(isChineseLanguage ? "大厅" : "LOBBY", "#A3D2FF");
		}
		if (IsFogRemovedInCurrentScene())
		{
			return Colorize(isChineseLanguage ? "已禁用" : "DISABLED", "#FF6B6B");
		}
		if (_fogPaused)
		{
			return Colorize(isChineseLanguage ? "房主已暂停" : "HOST PAUSED", "#FFA64D");
		}
		if (_orbFogHandler == null)
		{
			return Colorize(isChineseLanguage ? "同步中" : "SYNCING", "#A3D2FF");
		}
		if (!_initialDelayCompleted || ShouldEnforceConfiguredDelay(_orbFogHandler))
		{
			return Colorize(isChineseLanguage ? "等待中" : "WAITING", "#FFE08A");
		}
		if (_orbFogHandler.isMoving)
		{
			return Colorize(isChineseLanguage ? "运行中" : "RUNNING", "#B5FFB8");
		}
		if (_orbFogHandler.hasArrived)
		{
			return Colorize(isChineseLanguage ? "已到达" : "ARRIVED", "#7BD4FF");
		}
		return Colorize(isChineseLanguage ? "待机" : "IDLE", "#C8C8C8");
	}


	private bool HasFogCountdownLabel()
	{
		if (HasFogAuthority() && _orbFogHandler != null && ShouldEnforceConfiguredDelay(_orbFogHandler))
		{
			return HasVisibleFogDelayCountdown();
		}
		return false;
	}


	private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
	{
		Vector2 segment = end - start;
		float lengthSquared = segment.sqrMagnitude;
		if (lengthSquared <= 0.0001f)
		{
			return Vector2.Distance(point, start);
		}
		float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
		Vector2 projection = start + segment * t;
		return Vector2.Distance(point, projection);
	}


	private void RestoreVanillaFogSpeed()
	{
		if (_orbFogHandler != null)
		{
			_orbFogHandler.speed = 0.3f;
		}
	}


	private void ResetFogRuntimeState()
	{
		_fogStateInitialized = false;
		_initialDelayCompleted = false;
		_initialCompassGranted = false;
		_totalCompassGrantCount = 0;
		_delayedFogOriginId = -1;
		_trackedFogOriginId = -1;
		ClearSyntheticFogStage();
		_fogDelayTimer = 0f;
		_fogHiddenBufferTimer = 0f;
		_lastFogStateSyncTime = -0.18f;
		_lastRemoteStatusSyncTime = -0.25f;
		_lastCompassGrantSyncTime = -0.75f;
		_lastAdvertisedFogSpeed = -1f;
		_lastAdvertisedFogSpeedOrigin = -1;
		_lastAdvertisedFogSpeedScene = null;
		_lastFogSpeedAdvertiseTime = -999f;
		_syncedHostFogSpeed = -1f;
		_guestAppliedFogSpeed = -1f;
		ResetGuestFogSpeedSamples();
		_fogEtaLastRateResetTime = -999f;
		_fogHandlerSearchTimer = 0f;
		_grantedCampfireCompassIds.Clear();
		_restoredCheckpointCampfireIds.Clear();
		_playerCompassGrantCounts.Clear();
		_remoteFogSuppressionDebt.Clear();
		_remoteFogSporesSuppressionDebt.Clear();
		_remotePlayerCompassBaselineCounts.Clear();
		_pendingCampfireCompassGrantTimes.Clear();
		_fogPaused = false;
		_localFogStatusSourceDepth = 0;
		ResetHiddenNightTestHotkeyState();
		ResetFogArrivalEstimate();
		ResetFogStateSyncSnapshot();
	}


	private static bool HasFogAuthority()
	{
		if (PhotonNetwork.InRoom)
		{
			return PhotonNetwork.IsMasterClient;
		}
		return true;
	}


	private static bool IsGuestUsingVanillaHostFog()
	{
		if (!IsReadOnlyFogUiViewer())
		{
			return false;
		}
		if (!TryReadHostInstallState(out var hostModEnabled))
		{
			return true;
		}
		return !hostModEnabled;
	}


	internal static bool ShouldForceFogCoverageEverywhere()
	{
		if (HasFogAuthority() && IsModFeatureEnabled())
		{
			return !ShouldDeferFogToVanillaInCurrentScene();
		}
		return false;
	}


	internal static void NotifyFogHandlerChanged(OrbFogHandler fogHandler)
	{
		Instance?.EnsureFogCoverage(fogHandler);
	}


	internal static bool ShouldBlockVanillaOrbFogWait(OrbFogHandler fogHandler)
	{
		if (!IsModFeatureEnabled() || Instance == null || fogHandler == null || !HasFogAuthority())
		{
			return false;
		}
		if (ShouldDeferFogToVanillaInCurrentScene())
		{
			return false;
		}
		if (!Instance._fogStateInitialized)
		{
			Instance.InitializeFogRuntimeState(fogHandler);
		}
		Instance.UpdateTrackedFogOrigin();
		if (!IsFogRemovedInCurrentScene() && !Instance.ShouldEnforceConfiguredDelay(fogHandler))
		{
			return Instance.ShouldHoldFogUntilCampfireActivation(fogHandler);
		}
		return true;
	}


	internal static void NotifyCampfireLit(Campfire campfire)
	{
		Instance?.HandleCampfireLit(campfire);
	}


	private void TryRestoreCheckpointCampfireDelay()
	{
		if (!IsModFeatureEnabled() || !HasFogAuthority() || _orbFogHandler == null || Character.localCharacter == null || !IsGameplayFogScene(SceneManager.GetActiveScene()) || LoadingScreenHandler.loading || ShouldHideFogRelatedUiForCurrentSegment())
		{
			return;
		}
		Campfire currentCampfire = null;
		try
		{
			currentCampfire = MapHandler.CurrentCampfire;
		}
		catch
		{
		}
		if (currentCampfire == null || !currentCampfire.Lit)
		{
			return;
		}
		int campfireId = currentCampfire.GetInstanceID();
		if (!_grantedCampfireCompassIds.Contains(campfireId) && !_restoredCheckpointCampfireIds.Contains(campfireId))
		{
			int delayedOriginId;
			if (_orbFogHandler.isMoving || _orbFogHandler.currentWaitTime > 0.2f)
			{
				_restoredCheckpointCampfireIds.Add(campfireId);
			}
			else if (ScheduleFogDelayAfterCampfire(currentCampfire, out delayedOriginId))
			{
				_restoredCheckpointCampfireIds.Add(campfireId);
				base.Logger.LogInfo("[Fog&ColdControl] Restored fog delay from lit checkpoint campfire: " + currentCampfire.name + ".");
			}
		}
	}


	private void HandleCampfireLit(Campfire campfire)
	{
		if (!IsModFeatureEnabled() || !HasFogAuthority() || campfire == null)
		{
			return;
		}
		int campfireId = campfire.GetInstanceID();
		if (_grantedCampfireCompassIds.Add(campfireId))
		{
			int delayedOriginId;
			bool scheduledFogDelay = ScheduleFogDelayAfterCampfire(campfire, out delayedOriginId);
			if (!scheduledFogDelay)
			{
				base.Logger.LogInfo("[Fog&ColdControl] Skipping campfire compass grant for " + campfire.name + " because no fog segment is scheduled after this campfire.");
			}
			else if (scheduledFogDelay && delayedOriginId == 0 && !_initialCompassGranted)
			{
				base.Logger.LogInfo("[Fog&ColdControl] Skipping campfire compass grant for the opening campfire (" + campfire.name + ") because the initial fog-rise grant is still pending.");
			}
			else if (IsCompassFeatureEnabled())
			{
				_pendingCampfireCompassGrantTimes[campfireId] = Time.unscaledTime + 0.9f;
				base.Logger.LogInfo(string.Format("[{0}] Scheduled campfire compass grant for {1} in {2:F1}s.", "Fog&ColdControl", campfire.name, 0.9f));
			}
		}
	}


	private bool ScheduleFogDelayAfterCampfire(Campfire campfire, out int delayedOriginId)
	{
		delayedOriginId = -1;
		if (campfire == null)
		{
			return false;
		}
		if (!TryResolveCampfireFogOriginId(campfire, out delayedOriginId))
		{
			return false;
		}
		_delayedFogOriginId = delayedOriginId;
		_fogHiddenBufferTimer = 0f;
		_fogDelayTimer = 0f;
		_initialDelayCompleted = false;
		if (_orbFogHandler != null)
		{
			if (_orbFogHandler.currentID != delayedOriginId)
			{
				_orbFogHandler.SetFogOrigin(delayedOriginId);
				EnsureFogCoverage(_orbFogHandler);
				SyncFogOriginToGuests();
			}
			_orbFogHandler.isMoving = false;
			_orbFogHandler.currentWaitTime = 0f;
			_orbFogHandler.speed = 0f;
			_orbFogHandler.hasArrived = false;
			if (_orbFogHandler.currentID == delayedOriginId && _initialDelayCompleted)
			{
				StartFogMovement();
			}
		}
		float configuredDelay = FogDelay?.Value ?? 900f;
		base.Logger.LogInfo(string.Format("[{0}] Applied hidden fog buffer {1:F1}s and configured delay {2:F1}s after lighting campfire. Next origin: {3}.", "Fog&ColdControl", 10f, configuredDelay, delayedOriginId));
		return true;
	}


	private bool TryResolveCampfireFogOriginId(Campfire campfire, out int delayedOriginId)
	{
		delayedOriginId = -1;
		if (campfire == null)
		{
			return false;
		}
		int availableOriginCount = GetAvailableFogOriginCount();
		if (availableOriginCount <= 0)
		{
			base.Logger.LogWarning("[Fog&ColdControl] Unable to resolve fog origin after lighting campfire because no fog origins were found.");
			return false;
		}
		Segment advancedSegment = campfire.advanceToSegment;
		if (ShouldRemoveFogForSegment(advancedSegment))
		{
			_pendingSyntheticFogSegmentId = -1;
			base.Logger.LogInfo(string.Format("[{0}] Skipping fog scheduling for removed segment {1} ({2}). Campfire={3}.", "Fog&ColdControl", (int)advancedSegment, advancedSegment, campfire.name));
			return false;
		}
		if ((int)advancedSegment >= availableOriginCount)
		{
			if (!ShouldUseCustomFogPositionForSegment(advancedSegment))
			{
				_pendingSyntheticFogSegmentId = -1;
				base.Logger.LogInfo(string.Format("[{0}] No synthetic fog stage is defined for segment {1} ({2}). Campfire={3}.", "Fog&ColdControl", (int)advancedSegment, advancedSegment, campfire.name));
				return false;
			}
			_pendingSyntheticFogSegmentId = (int)advancedSegment;
			delayedOriginId = availableOriginCount - 1;
			base.Logger.LogInfo(string.Format("[{0}] Armed synthetic fog stage for segment {1} ({2}) because only {3} real fog origins are available. Campfire={4}, fallbackOrigin={5}.", "Fog&ColdControl", (int)advancedSegment, advancedSegment, availableOriginCount, campfire.name, delayedOriginId));
			return delayedOriginId >= 0;
		}
		_pendingSyntheticFogSegmentId = -1;
		int currentOriginId = ((_orbFogHandler != null) ? _orbFogHandler.currentID : (-1));
		TryMapSegmentToFogOriginId(campfire.advanceToSegment, availableOriginCount, out var advanceOriginId);
		delayedOriginId = advanceOriginId;
		int currentSceneSegmentId = -1;
		int sceneOriginId = -1;
		if (Singleton<MapHandler>.Instance != null)
		{
			currentSceneSegmentId = (int)MapHandler.CurrentSegmentNumber;
			TryMapSegmentToFogOriginId(MapHandler.CurrentSegmentNumber, availableOriginCount, out sceneOriginId);
			if (sceneOriginId > delayedOriginId)
			{
				delayedOriginId = sceneOriginId;
			}
		}
		bool sceneAdvancedBeyondCurrentOrigin = currentOriginId >= 0 && currentSceneSegmentId > currentOriginId;
		bool campfireAdvancedBeyondCurrentOrigin = currentOriginId >= 0 && (int)campfire.advanceToSegment > currentOriginId;
		if (currentOriginId >= 0 && delayedOriginId <= currentOriginId && (sceneAdvancedBeyondCurrentOrigin || campfireAdvancedBeyondCurrentOrigin))
		{
			int fallbackOriginId = Mathf.Min(currentOriginId + 1, availableOriginCount - 1);
			if (fallbackOriginId > delayedOriginId)
			{
				delayedOriginId = fallbackOriginId;
			}
		}
		if (currentOriginId >= 0 && delayedOriginId <= currentOriginId && (sceneAdvancedBeyondCurrentOrigin || campfireAdvancedBeyondCurrentOrigin))
		{
			base.Logger.LogWarning(string.Format("[{0}] Fog origin did not advance after lighting campfire. availableOrigins={1}, currentOrigin={2}, sceneSegment={3}, campfireAdvance={4} ({5}), resolvedOrigin={6}, campfireName={7}.", "Fog&ColdControl", availableOriginCount, currentOriginId, currentSceneSegmentId, (int)campfire.advanceToSegment, campfire.advanceToSegment, delayedOriginId, campfire.name));
		}
		else
		{
			base.Logger.LogInfo(string.Format("[{0}] Resolved fog origin after lighting campfire. availableOrigins={1}, currentOrigin={2}, sceneSegment={3}, campfireAdvance={4} ({5}), resolvedOrigin={6}, campfireName={7}.", "Fog&ColdControl", availableOriginCount, currentOriginId, currentSceneSegmentId, (int)campfire.advanceToSegment, campfire.advanceToSegment, delayedOriginId, campfire.name));
		}
		return delayedOriginId >= 0;
	}
}
