using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zorro.Core;

namespace FogClimb;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("com.github.PEAKModding.PEAKLib.ModConfig", BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BaseUnityPlugin
{
	private enum ConfigKey
	{
		ModEnabled,
		FogSpeed,
		FogDelay,
		FogUiEnabled,
		FogUiX,
		FogUiY,
		FogUiScale
	}

	public const string PluginGuid = "com.github.Thanks.FogClimb";

	public const string PluginName = "FogClimb";

	public const string PluginVersion = "0.0.2";

	private const float DefaultVanillaFogSpeed = 0.3f;

	private const float DefaultFogSpeed = 2f;

	private const float DefaultFogDelaySeconds = 90f;

	private const float MinFogSpeed = 0.3f;

	private const float MaxFogSpeed = 5f;

	private const float MinFogDelaySeconds = 0f;

	private const float MaxFogDelaySeconds = 150f;

	private const float DefaultFogUiX = 60f;

	private const float DefaultFogUiY = 16f;

	private const float DefaultFogUiScale = 1.2f;

	private const float MinFogUiX = -400f;

	private const float MaxFogUiX = 400f;

	private const float MinFogUiY = -500f;

	private const float MaxFogUiY = 500f;

	private const float MinFogUiScale = 0.5f;

	private const float MaxFogUiScale = 2.5f;

	private const float FogHandlerSearchIntervalSeconds = 1f;

	private const float ColdClearIntervalSeconds = 0.2f;

	private const float FogStateSyncIntervalSeconds = 0.18f;

	private const float RemoteStatusSyncIntervalSeconds = 0.25f;

	private const float BeaconRefreshIntervalSeconds = 1f;

	private const float CompassGrantSyncIntervalSeconds = 0.75f;

	private const float FogColdPerSecond = 0.0105f;

	private const float BeaconBaseHeight = 12f;

	private const float BeaconSegmentHeight = 14f;

	private const int BeaconLayerCount = 7;

	private const float BeaconRingRadius = 4.5f;

	private const float FogUiWidth = 920f;

	private const float FogUiHeight = 30f;

	private const string FogUiTitle = "FogClimb";

	private const int SimplifiedChineseLanguageIndex = 9;

	private static readonly int StatusTypeCount = Enum.GetNames(typeof(CharacterAfflictions.STATUSTYPE)).Length;

	private static readonly string[] ColdStatusArrayFields = new string[5]
	{
		"currentStatuses",
		"currentIncrementalStatuses",
		"currentDecrementalStatuses",
		"lastAddedStatus",
		"lastAddedIncrementalStatus"
	};

	private static bool _isClearingColdStatus;

	private Harmony _harmony;

	private OrbFogHandler _orbFogHandler;

	private FogSphere _fogSphere;

	private bool _fogStateInitialized;

	private bool _initialDelayCompleted;

	private int _trackedFogOriginId = -1;

	private float _fogDelayTimer;

	private float _fogHandlerSearchTimer;

	private float _lastColdClearTime = -ColdClearIntervalSeconds;

	private float _lastFogStateSyncTime = -FogStateSyncIntervalSeconds;

	private float _lastRemoteStatusSyncTime = -RemoteStatusSyncIntervalSeconds;

	private float _lastBeaconRefreshTime = -BeaconRefreshIntervalSeconds;

	private float _lastCompassGrantSyncTime = -CompassGrantSyncIntervalSeconds;

	private bool _lastModEnabledState;

	private bool _lastFogUiEnabledState;

	private float _lastFogUiX;

	private float _lastFogUiY;

	private float _lastFogUiScale;

	private readonly List<GameObject> _campfireBeaconObjects = new List<GameObject>();

	private readonly HashSet<int> _grantedCampfireCompassIds = new HashSet<int>();

	private readonly Dictionary<int, int> _playerCompassGrantCounts = new Dictionary<int, int>();

	private Item _flareBeaconPrefab;

	private Item _compassItem;

	private int _activeBeaconSegment = -1;

	private RectTransform _fogUiRect;

	private TextMeshProUGUI _fogUiText;

	private bool _initialCompassGranted;

	private int _totalCompassGrantCount;

	internal static Plugin Instance { get; private set; }

	internal static ConfigEntry<bool> ModEnabled { get; private set; }

	internal static ConfigEntry<float> FogSpeed { get; private set; }

	internal static ConfigEntry<float> FogDelay { get; private set; }

	internal static ConfigEntry<bool> FogUiEnabled { get; private set; }

	internal static ConfigEntry<float> FogUiX { get; private set; }

	internal static ConfigEntry<float> FogUiY { get; private set; }

	internal static ConfigEntry<float> FogUiScale { get; private set; }

	private void Awake()
	{
		Instance = this;
		bool isChineseLanguage = DetectChineseLanguage();
		InitializeConfig(isChineseLanguage);
		_lastModEnabledState = IsModFeatureEnabled();
		_lastFogUiEnabledState = FogUiEnabled?.Value ?? true;
		_lastFogUiX = FogUiX?.Value ?? DefaultFogUiX;
		_lastFogUiY = FogUiY?.Value ?? DefaultFogUiY;
		_lastFogUiScale = FogUiScale?.Value ?? DefaultFogUiScale;
		_harmony = new Harmony(PluginGuid);
		_harmony.PatchAll(Assembly.GetExecutingAssembly());
		SceneManager.sceneLoaded += OnSceneLoaded;
		Logger.LogInfo($"[{PluginName}] Loaded.");
	}

	private void OnDestroy()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
		_harmony?.UnpatchSelf();
		CleanupFogUi();
		CleanupCampfireBeacon();
		if (Instance == this)
		{
			Instance = null;
		}
	}

	private void InitializeConfig(bool isChineseLanguage)
	{
		string sectionName = GetSectionName(isChineseLanguage);
		ModEnabled = Config.Bind(sectionName, GetKeyName(ConfigKey.ModEnabled, isChineseLanguage), true, GetConfigDescription(ConfigKey.ModEnabled, isChineseLanguage));
		FogSpeed = Config.Bind(sectionName, GetKeyName(ConfigKey.FogSpeed, isChineseLanguage), DefaultFogSpeed, new ConfigDescription(GetLocalizedDescription(ConfigKey.FogSpeed, isChineseLanguage), new AcceptableValueRange<float>(MinFogSpeed, MaxFogSpeed), Array.Empty<object>()));
		FogDelay = Config.Bind(sectionName, GetKeyName(ConfigKey.FogDelay, isChineseLanguage), DefaultFogDelaySeconds, new ConfigDescription(GetLocalizedDescription(ConfigKey.FogDelay, isChineseLanguage), new AcceptableValueRange<float>(MinFogDelaySeconds, MaxFogDelaySeconds), Array.Empty<object>()));
		FogUiEnabled = Config.Bind(sectionName, GetKeyName(ConfigKey.FogUiEnabled, isChineseLanguage), true, GetConfigDescription(ConfigKey.FogUiEnabled, isChineseLanguage));
		FogUiX = Config.Bind(sectionName, GetKeyName(ConfigKey.FogUiX, isChineseLanguage), DefaultFogUiX, new ConfigDescription(GetLocalizedDescription(ConfigKey.FogUiX, isChineseLanguage), new AcceptableValueRange<float>(MinFogUiX, MaxFogUiX), Array.Empty<object>()));
		FogUiY = Config.Bind(sectionName, GetKeyName(ConfigKey.FogUiY, isChineseLanguage), DefaultFogUiY, new ConfigDescription(GetLocalizedDescription(ConfigKey.FogUiY, isChineseLanguage), new AcceptableValueRange<float>(MinFogUiY, MaxFogUiY), Array.Empty<object>()));
		FogUiScale = Config.Bind(sectionName, GetKeyName(ConfigKey.FogUiScale, isChineseLanguage), DefaultFogUiScale, new ConfigDescription(GetLocalizedDescription(ConfigKey.FogUiScale, isChineseLanguage), new AcceptableValueRange<float>(MinFogUiScale, MaxFogUiScale), Array.Empty<object>()));
		MigrateLocalizedConfigEntries();
		ClampConfigValues();
	}

	private ConfigDescription GetConfigDescription(ConfigKey configKey, bool isChineseLanguage)
	{
		return new ConfigDescription(GetLocalizedDescription(configKey, isChineseLanguage), null, Array.Empty<object>());
	}

	private void MigrateLocalizedConfigEntries()
	{
		IDictionary orphanedEntries = GetOrphanedEntries(Config);
		if (orphanedEntries == null || orphanedEntries.Count == 0)
		{
			return;
		}
		bool migratedAnyValue = false;
		migratedAnyValue |= TryMigrateLocalizedConfigValue(ModEnabled, ConfigKey.ModEnabled, orphanedEntries);
		migratedAnyValue |= TryMigrateLocalizedConfigValue(FogSpeed, ConfigKey.FogSpeed, orphanedEntries);
		migratedAnyValue |= TryMigrateLocalizedConfigValue(FogDelay, ConfigKey.FogDelay, orphanedEntries);
		migratedAnyValue |= TryMigrateLocalizedConfigValue(FogUiEnabled, ConfigKey.FogUiEnabled, orphanedEntries);
		migratedAnyValue |= TryMigrateLocalizedConfigValue(FogUiX, ConfigKey.FogUiX, orphanedEntries);
		migratedAnyValue |= TryMigrateLocalizedConfigValue(FogUiY, ConfigKey.FogUiY, orphanedEntries);
		migratedAnyValue |= TryMigrateLocalizedConfigValue(FogUiScale, ConfigKey.FogUiScale, orphanedEntries);
		if (migratedAnyValue)
		{
			Config.Save();
		}
	}

	private static bool TryMigrateLocalizedConfigValue(ConfigEntryBase entry, ConfigKey configKey, IDictionary orphanedEntries)
	{
		if (entry?.Definition == null || orphanedEntries == null)
		{
			return false;
		}
		foreach (ConfigDefinition aliasDefinition in GetAliasDefinitions(configKey))
		{
			if (DefinitionsEqual(aliasDefinition, entry.Definition) || !orphanedEntries.Contains(aliasDefinition))
			{
				continue;
			}
			object orphanedValue = orphanedEntries[aliasDefinition];
			if (orphanedValue != null)
			{
				entry.SetSerializedValue(orphanedValue.ToString());
			}
			orphanedEntries.Remove(aliasDefinition);
			return true;
		}
		return false;
	}

	private static IEnumerable<ConfigDefinition> GetAliasDefinitions(ConfigKey configKey)
	{
		string englishSection = GetSectionName(isChineseLanguage: false);
		string chineseSection = GetSectionName(isChineseLanguage: true);
		string englishKey = GetKeyName(configKey, isChineseLanguage: false);
		string chineseKey = GetKeyName(configKey, isChineseLanguage: true);
		yield return new ConfigDefinition(englishSection, englishKey);
		yield return new ConfigDefinition(englishSection, chineseKey);
		yield return new ConfigDefinition(chineseSection, englishKey);
		yield return new ConfigDefinition(chineseSection, chineseKey);
	}

	private static IDictionary GetOrphanedEntries(ConfigFile configFile)
	{
		return configFile?.GetType().GetProperty("OrphanedEntries", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(configFile) as IDictionary;
	}

	private static bool DefinitionsEqual(ConfigDefinition left, ConfigDefinition right)
	{
		return string.Equals(left?.Section, right?.Section, StringComparison.Ordinal) && string.Equals(left?.Key, right?.Key, StringComparison.Ordinal);
	}

	private static void ClampConfigValues()
	{
		if (FogSpeed != null)
		{
			FogSpeed.Value = Mathf.Clamp(FogSpeed.Value, MinFogSpeed, MaxFogSpeed);
		}
		if (FogDelay != null)
		{
			FogDelay.Value = Mathf.Clamp(FogDelay.Value, MinFogDelaySeconds, MaxFogDelaySeconds);
		}
		if (FogUiX != null)
		{
			FogUiX.Value = Mathf.Clamp(FogUiX.Value, MinFogUiX, MaxFogUiX);
		}
		if (FogUiY != null)
		{
			FogUiY.Value = Mathf.Clamp(FogUiY.Value, MinFogUiY, MaxFogUiY);
		}
		if (FogUiScale != null)
		{
			FogUiScale.Value = Mathf.Clamp(FogUiScale.Value, MinFogUiScale, MaxFogUiScale);
		}
		NormalizeShootZombiesFogUiDefaults();
	}

	private static void NormalizeShootZombiesFogUiDefaults()
	{
		if (FogUiX == null || FogUiY == null || FogUiScale == null)
		{
			return;
		}
		bool matchesLegacyPresetA = Approximately(FogUiX.Value, 70f) && Approximately(FogUiY.Value, 4f) && Approximately(FogUiScale.Value, 1.1f);
		bool matchesLegacyPresetB = Approximately(FogUiX.Value, 10f) && Approximately(FogUiY.Value, 10f) && Approximately(FogUiScale.Value, 1.2f);
		bool matchesLegacyPresetC = Approximately(FogUiX.Value, 4f) && Approximately(FogUiY.Value, 4f) && Approximately(FogUiScale.Value, 1.1f);
		bool matchesLegacyPresetD = Approximately(FogUiX.Value, 60f) && Approximately(FogUiY.Value, -200f) && Approximately(FogUiScale.Value, 1.1f);
		bool matchesPreviousFogClimbDefault = Approximately(FogUiX.Value, 60f) && Approximately(FogUiY.Value, -200f) && Approximately(FogUiScale.Value, 1f);
		if (matchesLegacyPresetA || matchesLegacyPresetB || matchesLegacyPresetC || matchesLegacyPresetD || matchesPreviousFogClimbDefault)
		{
			FogUiX.Value = DefaultFogUiX;
			FogUiY.Value = DefaultFogUiY;
			FogUiScale.Value = DefaultFogUiScale;
		}
	}

	private static bool Approximately(float left, float right)
	{
		return Mathf.Abs(left - right) < 0.001f;
	}

	private void Update()
	{
		bool modEnabled = IsModFeatureEnabled();
		if (modEnabled != _lastModEnabledState)
		{
			_lastModEnabledState = modEnabled;
			if (!modEnabled)
			{
				RestoreVanillaFogSpeed();
				ResetFogRuntimeState();
				CleanupCampfireBeacon();
				SetFogUiVisible(false);
			}
			else
			{
				_fogStateInitialized = false;
				_lastFogStateSyncTime = -FogStateSyncIntervalSeconds;
				_lastRemoteStatusSyncTime = -RemoteStatusSyncIntervalSeconds;
				_lastBeaconRefreshTime = -BeaconRefreshIntervalSeconds;
				_lastCompassGrantSyncTime = -CompassGrantSyncIntervalSeconds;
			}
		}
		TryResolveRuntimeObjects();
		HandleFogUiConfigChanges();
		if (!modEnabled)
		{
			UpdateFogUi();
			return;
		}
		if (_orbFogHandler != null && !_fogStateInitialized)
		{
			InitializeFogRuntimeState(_orbFogHandler);
		}
		if (_orbFogHandler != null && HasFogAuthority())
		{
			UpdateAuthorityFogMode();
			SyncFogStateToGuestsIfNeeded();
			SyncRemoteStatusSuppressionIfNeeded();
			RefreshCampfireBeaconIfNeeded();
			SyncCompassGrantsToPlayersIfNeeded();
		}
		else if (!HasFogAuthority())
		{
			CleanupCampfireBeacon();
		}
		ClearLocalAmbientColdStatus();
		UpdateFogUi();
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		ResetFogRuntimeState();
		_orbFogHandler = null;
		_fogSphere = null;
		_flareBeaconPrefab = null;
		_compassItem = null;
		CleanupFogUi();
		CleanupCampfireBeacon();
	}

	private void TryResolveRuntimeObjects()
	{
		bool needFogHandler = _orbFogHandler == null;
		bool needFogSphere = _fogSphere == null;
		if (!needFogHandler && !needFogSphere)
		{
			return;
		}
		_fogHandlerSearchTimer -= Time.unscaledDeltaTime;
		if (_fogHandlerSearchTimer > 0f)
		{
			return;
		}
		_fogHandlerSearchTimer = FogHandlerSearchIntervalSeconds;
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
	}

	private void InitializeFogRuntimeState(OrbFogHandler fogHandler)
	{
		_fogStateInitialized = true;
		_trackedFogOriginId = fogHandler != null ? fogHandler.currentID : -1;
		_fogDelayTimer = 0f;
		_initialDelayCompleted = ShouldSkipInitialDelay(fogHandler);
	}

	private bool ShouldSkipInitialDelay(OrbFogHandler fogHandler)
	{
		if (fogHandler == null || FogDelay == null || FogDelay.Value <= 0f)
		{
			return true;
		}
		return fogHandler.currentID > 0 || fogHandler.isMoving || fogHandler.currentWaitTime > 1f;
	}

	private void UpdateAuthorityFogMode()
	{
		if (_orbFogHandler == null)
		{
			return;
		}
		ClampConfigValues();
		UpdateTrackedFogOrigin();
		if (_trackedFogOriginId != 0)
		{
			_initialDelayCompleted = true;
		}
		if (!_initialDelayCompleted)
		{
			_fogDelayTimer += Time.unscaledDeltaTime;
			_orbFogHandler.speed = 0f;
			if (_fogDelayTimer >= FogDelay.Value)
			{
				_fogDelayTimer = FogDelay.Value;
				_initialDelayCompleted = true;
				if (!_orbFogHandler.isMoving)
				{
					StartFogMovement();
				}
			}
			return;
		}
		_orbFogHandler.speed = FogSpeed.Value;
		TryGrantInitialCompassIfNeeded();
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
			_initialDelayCompleted = true;
			_fogDelayTimer = FogDelay?.Value ?? 0f;
			return;
		}
		if (returnedToFirstOrigin)
		{
			_fogDelayTimer = 0f;
			_initialDelayCompleted = ShouldSkipInitialDelay(_orbFogHandler);
		}
	}

	private void StartFogMovement()
	{
		if (_orbFogHandler == null)
		{
			return;
		}
		bool shouldGrantInitialCompass = !_initialCompassGranted && _orbFogHandler.currentID == 0;
		PhotonView photonView = _orbFogHandler.GetComponent<PhotonView>();
		if (PhotonNetwork.InRoom && photonView != null)
		{
			photonView.RPC("StartMovingRPC", RpcTarget.All, Array.Empty<object>());
			if (shouldGrantInitialCompass)
			{
				_initialCompassGranted = true;
				GrantCompassToAllPlayers("initial-delay-ended");
			}
			return;
		}
		_orbFogHandler.StartMovingRPC();
		if (shouldGrantInitialCompass)
		{
			_initialCompassGranted = true;
			GrantCompassToAllPlayers("initial-delay-ended");
		}
	}

	private void TryGrantInitialCompassIfNeeded()
	{
		if (_initialCompassGranted || _orbFogHandler == null || _orbFogHandler.currentID != 0)
		{
			return;
		}
		if (!_initialDelayCompleted || !_orbFogHandler.isMoving)
		{
			return;
		}
		_initialCompassGranted = true;
		GrantCompassToAllPlayers("initial-delay-ended");
	}

	private void SyncFogStateToGuestsIfNeeded()
	{
		if (_orbFogHandler == null || !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
		{
			return;
		}
		float now = Time.unscaledTime;
		if (now - _lastFogStateSyncTime < FogStateSyncIntervalSeconds)
		{
			return;
		}
		_lastFogStateSyncTime = now;
		PhotonView photonView = _orbFogHandler.GetComponent<PhotonView>();
		if (photonView == null)
		{
			return;
		}
		try
		{
			photonView.RPC("RPCA_SyncFog", RpcTarget.Others, new object[]
			{
				_orbFogHandler.currentSize,
				_orbFogHandler.isMoving
			});
		}
		catch (Exception ex)
		{
			Logger.LogDebug($"[{PluginName}] Fog sync skipped: {ex.Message}");
		}
	}

	private void SyncRemoteStatusSuppressionIfNeeded()
	{
		if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
		{
			return;
		}
		float now = Time.unscaledTime;
		float elapsed = _lastRemoteStatusSyncTime < 0f ? RemoteStatusSyncIntervalSeconds : now - _lastRemoteStatusSyncTime;
		if (elapsed < RemoteStatusSyncIntervalSeconds)
		{
			return;
		}
		_lastRemoteStatusSyncTime = now;
		foreach (Character character in Character.AllCharacters)
		{
			if (!ShouldSendRemoteStatusSuppression(character))
			{
				continue;
			}
			float[] deltas = BuildRemoteStatusSuppressionPayload(character, elapsed);
			if (deltas == null)
			{
				continue;
			}
			try
			{
				character.photonView.RPC("RPC_ApplyStatusesFromFloatArray", character.photonView.Owner, new object[] { deltas });
			}
			catch (Exception ex)
			{
				Logger.LogDebug($"[{PluginName}] Remote status suppression skipped for {character.characterName}: {ex.Message}");
			}
		}
	}

	private static bool ShouldSendRemoteStatusSuppression(Character character)
	{
		return character != null && character.photonView != null && character.photonView.Owner != null && !character.photonView.IsMine && !character.isBot && !character.isZombie && !character.data.dead;
	}

	private float[] BuildRemoteStatusSuppressionPayload(Character character, float elapsed)
	{
		if (!character.data.isSkeleton)
		{
			float[] payload = new float[StatusTypeCount];
			payload[(int)CharacterAfflictions.STATUSTYPE.Cold] = -1f;
			return payload;
		}
		if (!IsCharacterOutsideCurrentFogSphere(character))
		{
			return null;
		}
		float[] skeletonPayload = new float[StatusTypeCount];
		skeletonPayload[(int)CharacterAfflictions.STATUSTYPE.Injury] = -Mathf.Max(FogColdPerSecond / 8f * Mathf.Max(elapsed, RemoteStatusSyncIntervalSeconds) * 1.25f, 0.001f);
		return skeletonPayload;
	}

	private bool IsCharacterOutsideCurrentFogSphere(Character character)
	{
		return _fogSphere != null && Mathf.Approximately(_fogSphere.ENABLE, 1f) && Vector3.Distance(_fogSphere.fogPoint, character.Center) > _fogSphere.currentSize;
	}

	private void RefreshCampfireBeaconIfNeeded()
	{
		if (!IsModFeatureEnabled() || !HasFogAuthority())
		{
			CleanupCampfireBeacon();
			return;
		}
		float now = Time.unscaledTime;
		if (now - _lastBeaconRefreshTime < BeaconRefreshIntervalSeconds)
		{
			return;
		}
		_lastBeaconRefreshTime = now;
		Campfire targetCampfire = TryGetTargetCampfire();
		int targetSegment = targetCampfire != null ? (int)MapHandler.CurrentSegmentNumber : -1;
		bool needsRebuild = targetCampfire == null || targetSegment != _activeBeaconSegment || _campfireBeaconObjects.Count != GetBeaconSpawnCount() || _campfireBeaconObjects.Any(obj => obj == null);
		if (!needsRebuild)
		{
			return;
		}
		CleanupCampfireBeacon();
		if (targetCampfire == null)
		{
			return;
		}
		TrySpawnCampfireBeacon(targetCampfire, targetSegment);
	}

	private Campfire TryGetTargetCampfire()
	{
		try
		{
			if (Singleton<MapHandler>.Instance == null)
			{
				return null;
			}
			if (Singleton<MapHandler>.Instance.GetCurrentSegment() >= Segment.Peak)
			{
				return null;
			}
			return MapHandler.CurrentCampfire;
		}
		catch
		{
			return null;
		}
	}

	private void TrySpawnCampfireBeacon(Campfire campfire, int segment)
	{
		if (campfire == null)
		{
			return;
		}
		TryResolveFlareBeaconPrefab();
		if (_flareBeaconPrefab == null)
		{
			return;
		}
		Vector3 basePosition = campfire.transform.position + Vector3.up * BeaconBaseHeight;
		Vector3 right = campfire.transform.right;
		Vector3 forward = campfire.transform.forward;
		if (right.sqrMagnitude < 0.01f)
		{
			right = Vector3.right;
		}
		if (forward.sqrMagnitude < 0.01f)
		{
			forward = Vector3.forward;
		}
		right.y = 0f;
		forward.y = 0f;
		right.Normalize();
		forward.Normalize();
		for (int layer = 0; layer < BeaconLayerCount; layer++)
		{
			Vector3 layerOrigin = basePosition + Vector3.up * (BeaconSegmentHeight * layer);
			SpawnBeaconFlareAt(layerOrigin);
			SpawnBeaconFlareAt(layerOrigin + right * BeaconRingRadius);
			SpawnBeaconFlareAt(layerOrigin - right * BeaconRingRadius);
			SpawnBeaconFlareAt(layerOrigin + forward * BeaconRingRadius);
			SpawnBeaconFlareAt(layerOrigin - forward * BeaconRingRadius);
		}
		_activeBeaconSegment = segment;
	}

	private int GetBeaconSpawnCount()
	{
		return BeaconLayerCount * 5;
	}

	private void SpawnBeaconFlareAt(Vector3 position)
	{
		GameObject beacon = SpawnBeaconFlare(position);
		if (beacon == null)
		{
			return;
		}
		ConfigureBeaconFlare(beacon, position);
		_campfireBeaconObjects.Add(beacon);
	}

	private void TryResolveFlareBeaconPrefab()
	{
		if (_flareBeaconPrefab != null)
		{
			return;
		}
		ItemDatabase database = SingletonAsset<ItemDatabase>.Instance;
		if (database == null || database.Objects == null)
		{
			return;
		}
		foreach (Item item in database.Objects)
		{
			if (item != null && item.GetComponent<Flare>() != null)
			{
				_flareBeaconPrefab = item;
				return;
			}
		}
	}

	private GameObject SpawnBeaconFlare(Vector3 position)
	{
		try
		{
			if (PhotonNetwork.InRoom)
			{
				return PhotonNetwork.InstantiateItemRoom(_flareBeaconPrefab.name, position, Quaternion.identity);
			}
			return UnityEngine.Object.Instantiate(_flareBeaconPrefab.gameObject, position, Quaternion.identity);
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"[{PluginName}] Failed to spawn beacon flare: {ex.Message}");
			return null;
		}
	}

	private void ConfigureBeaconFlare(GameObject beacon, Vector3 position)
	{
		if (beacon == null)
		{
			return;
		}
		beacon.transform.position = position;
		beacon.transform.rotation = Quaternion.identity;
		Item item = beacon.GetComponent<Item>();
		if (item != null)
		{
			if (PhotonNetwork.InRoom && beacon.GetComponent<PhotonView>() != null)
			{
				item.SetKinematicNetworked(true, position, Quaternion.identity);
			}
			else if (beacon.TryGetComponent<Rigidbody>(out Rigidbody rigidbody))
			{
				rigidbody.isKinematic = true;
				rigidbody.linearVelocity = Vector3.zero;
				rigidbody.angularVelocity = Vector3.zero;
			}
		}
		Flare flare = beacon.GetComponent<Flare>();
		if (flare == null)
		{
			return;
		}
		if (PhotonNetwork.InRoom && beacon.GetComponent<PhotonView>() != null)
		{
			flare.LightFlare();
			return;
		}
		flare.SetFlareLitRPC();
	}

	private void CleanupCampfireBeacon()
	{
		bool canDestroyNetworkObjects = PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient;
		foreach (GameObject beacon in _campfireBeaconObjects)
		{
			if (beacon == null)
			{
				continue;
			}
			try
			{
				if (canDestroyNetworkObjects && beacon.GetComponent<PhotonView>() != null)
				{
					PhotonNetwork.Destroy(beacon);
				}
				else
				{
					UnityEngine.Object.Destroy(beacon);
				}
			}
			catch
			{
			}
		}
		_campfireBeaconObjects.Clear();
		_activeBeaconSegment = -1;
	}

	private bool TryResolveCompassItem()
	{
		if (_compassItem != null)
		{
			return true;
		}
		ItemDatabase database = SingletonAsset<ItemDatabase>.Instance;
		if (database == null || database.Objects == null)
		{
			return false;
		}
		foreach (Item item in database.Objects)
		{
			if (item == null)
			{
				continue;
			}
			CompassPointer compassPointer = item.GetComponentInChildren<CompassPointer>(true);
			if (compassPointer != null && compassPointer.compassType == CompassPointer.CompassType.Normal)
			{
				_compassItem = item;
				return true;
			}
		}
		return false;
	}

	private IEnumerable<Player> EnumerateTargetPlayers()
	{
		if (PhotonNetwork.InRoom)
		{
			foreach (Player player in PlayerHandler.GetAllPlayers())
			{
				if (player != null)
				{
					yield return player;
				}
			}
			yield break;
		}
		Player[] players = UnityEngine.Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
		foreach (Player player2 in players)
		{
			if (player2 != null)
			{
				yield return player2;
			}
		}
	}

	private void GrantCompassToAllPlayers(string reason)
	{
		if (!IsModFeatureEnabled() || !HasFogAuthority())
		{
			return;
		}
		_totalCompassGrantCount++;
		Logger.LogDebug($"[{PluginName}] Queued compass grant #{_totalCompassGrantCount} ({reason}).");
		SyncCompassGrantsToPlayers(force: true, reason);
	}

	private void SyncCompassGrantsToPlayersIfNeeded()
	{
		if (!IsModFeatureEnabled() || !HasFogAuthority() || _totalCompassGrantCount <= 0)
		{
			return;
		}
		float now = Time.unscaledTime;
		if (now - _lastCompassGrantSyncTime < CompassGrantSyncIntervalSeconds)
		{
			return;
		}
		SyncCompassGrantsToPlayers(force: false, "periodic-sync");
	}

	private void SyncCompassGrantsToPlayers(bool force, string reason)
	{
		if (!IsModFeatureEnabled() || !HasFogAuthority() || _totalCompassGrantCount <= 0)
		{
			return;
		}
		if (!TryResolveCompassItem())
		{
			Logger.LogWarning($"[{PluginName}] Failed to resolve compass item while syncing grants ({reason}).");
			return;
		}
		_lastCompassGrantSyncTime = Time.unscaledTime;
		HashSet<int> activePlayerKeys = new HashSet<int>();
		foreach (Player player in EnumerateTargetPlayers())
		{
			int playerKey = GetPlayerCompassGrantKey(player);
			activePlayerKeys.Add(playerKey);
			int deliveredCount = _playerCompassGrantCounts.TryGetValue(playerKey, out int value) ? value : 0;
			while (deliveredCount < _totalCompassGrantCount)
			{
				if (!GrantCompassToPlayer(player, $"{reason}-{deliveredCount + 1}/{_totalCompassGrantCount}"))
				{
					break;
				}
				deliveredCount++;
			}
			_playerCompassGrantCounts[playerKey] = deliveredCount;
		}
		int[] staleKeys = _playerCompassGrantCounts.Keys.Where(key => !activePlayerKeys.Contains(key)).ToArray();
		foreach (int staleKey in staleKeys)
		{
			_playerCompassGrantCounts.Remove(staleKey);
		}
	}

	private static int GetPlayerCompassGrantKey(Player player)
	{
		if (player == null)
		{
			return 0;
		}
		PhotonView photonView = player.GetComponent<PhotonView>();
		if (photonView?.Owner != null)
		{
			return photonView.Owner.ActorNumber;
		}
		return player.GetInstanceID();
	}

	private bool GrantCompassToPlayer(Player player, string reason)
	{
		if (player == null || _compassItem == null)
		{
			return false;
		}
		if (player.AddItem(_compassItem.itemID, null, out _))
		{
			Logger.LogDebug($"[{PluginName}] Granted compass to {player.name} ({reason}).");
			return true;
		}
		return DropCompassNearPlayer(player, reason);
	}

	private bool DropCompassNearPlayer(Player player, string reason)
	{
		if (player == null || _compassItem == null)
		{
			return false;
		}
		Vector3 spawnPosition = GetCompassDropPosition(player);
		try
		{
			if (PhotonNetwork.InRoom)
			{
				PhotonNetwork.InstantiateItemRoom(_compassItem.name, spawnPosition, Quaternion.identity);
			}
			else
			{
				UnityEngine.Object.Instantiate(_compassItem.gameObject, spawnPosition, Quaternion.identity);
			}
			Logger.LogDebug($"[{PluginName}] Dropped fallback compass near {player.name} ({reason}).");
			return true;
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"[{PluginName}] Failed to drop fallback compass near {player.name} ({reason}): {ex.Message}");
			return false;
		}
	}

	private static Vector3 GetCompassDropPosition(Player player)
	{
		Character character = player.character;
		Vector3 origin = character != null ? character.Center : player.transform.position;
		Vector3 forward = character != null ? Vector3.ProjectOnPlane(character.transform.forward, Vector3.up) : Vector3.forward;
		if (forward.sqrMagnitude < 0.01f)
		{
			forward = Vector3.forward;
		}
		return origin + Vector3.up * 0.5f + forward.normalized * 1.1f;
	}

	private void HandleFogUiConfigChanges()
	{
		bool fogUiEnabled = FogUiEnabled?.Value ?? true;
		float fogUiX = FogUiX?.Value ?? DefaultFogUiX;
		float fogUiY = FogUiY?.Value ?? DefaultFogUiY;
		float fogUiScale = FogUiScale?.Value ?? DefaultFogUiScale;
		if (fogUiEnabled == _lastFogUiEnabledState && Approximately(fogUiX, _lastFogUiX) && Approximately(fogUiY, _lastFogUiY) && Approximately(fogUiScale, _lastFogUiScale))
		{
			return;
		}
		_lastFogUiEnabledState = fogUiEnabled;
		_lastFogUiX = fogUiX;
		_lastFogUiY = fogUiY;
		_lastFogUiScale = fogUiScale;
		CreateFogUi();
	}

	private bool ShouldShowFogUi()
	{
		return IsModFeatureEnabled() && (FogUiEnabled?.Value ?? true) && ResolveHudCanvas() != null;
	}

	private Canvas ResolveHudCanvas()
	{
		GUIManager instance = GUIManager.instance;
		if (instance != null)
		{
			TextMeshProUGUI source = instance.itemPromptMain != null ? instance.itemPromptMain : instance.interactNameText;
			if (source != null)
			{
				Canvas canvas = source.GetComponentInParent<Canvas>();
				if (canvas != null)
				{
					return canvas;
				}
			}
		}
		Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
		Canvas bestCanvas = null;
		int bestSortingOrder = int.MinValue;
		float bestArea = -1f;
		foreach (Canvas canvas in canvases)
		{
			if (canvas == null || !canvas.isRootCanvas || !canvas.isActiveAndEnabled || !canvas.gameObject.activeInHierarchy || canvas.renderMode == RenderMode.WorldSpace)
			{
				continue;
			}
			RectTransform rectTransform = canvas.GetComponent<RectTransform>();
			float area = rectTransform != null ? Mathf.Abs(rectTransform.rect.width * rectTransform.rect.height) : 0f;
			if (bestCanvas == null || canvas.sortingOrder > bestSortingOrder || (canvas.sortingOrder == bestSortingOrder && area > bestArea))
			{
				bestCanvas = canvas;
				bestSortingOrder = canvas.sortingOrder;
				bestArea = area;
			}
		}
		return bestCanvas;
	}

	private TextMeshProUGUI ResolveFogStyleSource()
	{
		GUIManager instance = GUIManager.instance;
		if (instance == null)
		{
			return null;
		}
		if (instance.itemPromptMain != null)
		{
			return instance.itemPromptMain;
		}
		if (instance.interactNameText != null)
		{
			return instance.interactNameText;
		}
		return instance.interactPromptText;
	}

	private void ApplyGameTextStyle(TextMeshProUGUI target, Color color, float sizeMultiplier = 1f)
	{
		if (target == null)
		{
			return;
		}
		TextMeshProUGUI source = ResolveFogStyleSource();
		if (source != null)
		{
			target.font = source.font;
			target.fontSharedMaterial = source.fontSharedMaterial;
			target.fontStyle = source.fontStyle;
			target.characterSpacing = source.characterSpacing;
			target.wordSpacing = source.wordSpacing;
			target.lineSpacing = source.lineSpacing;
		}
		else if (TMP_Settings.defaultFontAsset != null)
		{
			target.font = TMP_Settings.defaultFontAsset;
		}
		target.textWrappingMode = TextWrappingModes.NoWrap;
		target.fontSize = 18f * sizeMultiplier;
		target.color = color;
		target.alignment = TextAlignmentOptions.BottomLeft;
	}

	private void CreateFogUi()
	{
		CleanupFogUi();
		if (!ShouldShowFogUi())
		{
			return;
		}
		try
		{
			Canvas canvas = ResolveHudCanvas();
			if (canvas == null)
			{
				return;
			}
			GameObject container = new GameObject("FogClimbUI");
			container.transform.SetParent(canvas.transform, false);
			_fogUiRect = container.AddComponent<RectTransform>();
			_fogUiRect.anchorMin = Vector2.zero;
			_fogUiRect.anchorMax = Vector2.zero;
			_fogUiRect.pivot = Vector2.zero;
			_fogUiRect.sizeDelta = new Vector2(FogUiWidth, FogUiHeight);
			container.transform.SetAsLastSibling();
			GameObject labelObject = new GameObject("FogClimbUILabel");
			labelObject.transform.SetParent(container.transform, false);
			RectTransform labelRect = labelObject.AddComponent<RectTransform>();
			labelRect.anchorMin = Vector2.zero;
			labelRect.anchorMax = Vector2.zero;
			labelRect.pivot = Vector2.zero;
			labelRect.anchoredPosition = Vector2.zero;
			labelRect.sizeDelta = new Vector2(FogUiWidth, FogUiHeight);
			_fogUiText = labelObject.AddComponent<TextMeshProUGUI>();
			ApplyGameTextStyle(_fogUiText, Color.white);
			_fogUiText.text = FogUiTitle;
			ClampFogUiToCanvas();
			SetFogUiVisible(true);
		}
		catch
		{
			CleanupFogUi();
		}
	}

	private void UpdateFogUi()
	{
		if (FogUiEnabled != null && !FogUiEnabled.Value)
		{
			SetFogUiVisible(false);
			return;
		}
		if (!ShouldShowFogUi())
		{
			SetFogUiVisible(false);
			return;
		}
		if (_fogUiRect == null || _fogUiText == null)
		{
			CreateFogUi();
			if (_fogUiRect == null || _fogUiText == null)
			{
				return;
			}
		}
		SetFogUiVisible(true);
		ClampFogUiToCanvas();
		bool isChinese = DetectChineseLanguage();
		string title = FogUiTitle;
		float speed = _orbFogHandler != null ? _orbFogHandler.speed : (FogSpeed?.Value ?? DefaultFogSpeed);
		string state = GetFogStateLabel(isChinese);
		string countdown = GetFogCountdownLabel(isChinese);
		if (_orbFogHandler == null)
		{
			float configuredDelay = FogDelay?.Value ?? DefaultFogDelaySeconds;
			_fogUiText.text = isChinese
				? $"{title}  |  速度: {speed:F2}  |  延迟: {configuredDelay:F0}s  |  状态: {state}"
				: $"{title}  |  Speed: {speed:F2}  |  Delay: {configuredDelay:F0}s  |  State: {state}";
			return;
		}
		_fogUiText.text = isChinese
			? $"{title}  |  速度: {speed:F2}  |  状态: {state}{countdown}"
			: $"{title}  |  Speed: {speed:F2}  |  State: {state}{countdown}";
	}

	private string GetFogStateLabel(bool isChinese)
	{
		if (!IsModFeatureEnabled())
		{
			return isChinese ? "关闭" : "OFF";
		}
		if (_orbFogHandler == null)
		{
			return isChinese ? "大厅" : "LOBBY";
		}
		if (HasFogAuthority() && !_initialDelayCompleted && _orbFogHandler.currentID == 0)
		{
			return isChinese ? "等待中" : "WAITING";
		}
		if (_orbFogHandler.isMoving)
		{
			return isChinese ? "运行中" : "RUNNING";
		}
		if (_orbFogHandler.hasArrived)
		{
			return isChinese ? "已到达" : "ARRIVED";
		}
		return isChinese ? "待机中" : "IDLE";
	}

	private string GetFogCountdownLabel(bool isChinese)
	{
		if (!HasFogAuthority() || _orbFogHandler == null || _initialDelayCompleted || _orbFogHandler.currentID != 0)
		{
			return string.Empty;
		}
		float remaining = Mathf.Max((FogDelay?.Value ?? 0f) - _fogDelayTimer, 0f);
		return isChinese ? $"  |  倒计时: {remaining:F1}s" : $"  |  Starts In: {remaining:F1}s";
	}

	private void ClampFogUiToCanvas()
	{
		if (_fogUiRect == null)
		{
			return;
		}
		Canvas canvas = _fogUiRect.GetComponentInParent<Canvas>();
		if (canvas == null)
		{
			return;
		}
		Canvas.ForceUpdateCanvases();
		if (_fogUiRect.parent != canvas.transform)
		{
			_fogUiRect.SetParent(canvas.transform, false);
		}
		RectTransform canvasRect = canvas.GetComponent<RectTransform>();
		if (canvasRect == null)
		{
			return;
		}
		ApplyBottomLeftAnchoredRect(_fogUiRect, canvasRect, FogUiWidth, FogUiHeight, FogUiScale?.Value ?? DefaultFogUiScale, FogUiX?.Value ?? DefaultFogUiX, FogUiY?.Value ?? DefaultFogUiY);
	}

	private static void ApplyBottomLeftAnchoredRect(RectTransform target, RectTransform canvasRect, float width, float height, float scale, float x, float y)
	{
		if (target == null || canvasRect == null)
		{
			return;
		}
		target.localScale = Vector3.one * Mathf.Max(scale, MinFogUiScale);
		target.anchorMin = Vector2.zero;
		target.anchorMax = Vector2.zero;
		target.pivot = Vector2.zero;
		target.sizeDelta = new Vector2(width, height);
		float scaledWidth = width * target.localScale.x;
		float scaledHeight = height * target.localScale.y;
		Rect canvasArea = canvasRect.rect;
		float clampedX = Mathf.Clamp(x, 0f, Mathf.Max(0f, canvasArea.width - scaledWidth));
		float clampedY = Mathf.Clamp(y, 0f, Mathf.Max(0f, canvasArea.height - scaledHeight));
		target.anchoredPosition = new Vector2(clampedX, clampedY);
	}

	private void SetFogUiVisible(bool visible)
	{
		if (_fogUiRect != null)
		{
			_fogUiRect.gameObject.SetActive(visible);
		}
	}

	private void CleanupFogUi()
	{
		if (_fogUiRect != null)
		{
			UnityEngine.Object.Destroy(_fogUiRect.gameObject);
		}
		_fogUiRect = null;
		_fogUiText = null;
	}

	private void RestoreVanillaFogSpeed()
	{
		if (_orbFogHandler != null)
		{
			_orbFogHandler.speed = DefaultVanillaFogSpeed;
		}
	}

	private void ResetFogRuntimeState()
	{
		_fogStateInitialized = false;
		_initialDelayCompleted = false;
		_initialCompassGranted = false;
		_totalCompassGrantCount = 0;
		_trackedFogOriginId = -1;
		_fogDelayTimer = 0f;
		_lastFogStateSyncTime = -FogStateSyncIntervalSeconds;
		_lastRemoteStatusSyncTime = -RemoteStatusSyncIntervalSeconds;
		_lastBeaconRefreshTime = -BeaconRefreshIntervalSeconds;
		_lastCompassGrantSyncTime = -CompassGrantSyncIntervalSeconds;
		_fogHandlerSearchTimer = 0f;
		_grantedCampfireCompassIds.Clear();
		_playerCompassGrantCounts.Clear();
	}

	private static bool DetectChineseLanguage()
	{
		if (TryGetConfiguredGameLanguage(out bool isChineseLanguage))
		{
			return isChineseLanguage;
		}
		if (TryGetLocalizedTextLanguageName(out string languageName))
		{
			return IsChineseLanguageName(languageName);
		}
		return false;
	}

	private static bool TryGetConfiguredGameLanguage(out bool isChineseLanguage)
	{
		isChineseLanguage = false;
		try
		{
			if (!PlayerPrefs.HasKey("LanguageSetting"))
			{
				return false;
			}
			int languageValue = PlayerPrefs.GetInt("LanguageSetting", int.MinValue);
			if (languageValue != int.MinValue)
			{
				isChineseLanguage = languageValue == SimplifiedChineseLanguageIndex;
				return true;
			}
			string languageText = PlayerPrefs.GetString("LanguageSetting", string.Empty);
			if (string.IsNullOrWhiteSpace(languageText))
			{
				return false;
			}
			if (int.TryParse(languageText, NumberStyles.Integer, CultureInfo.InvariantCulture, out languageValue))
			{
				isChineseLanguage = languageValue == SimplifiedChineseLanguageIndex;
				return true;
			}
			isChineseLanguage = IsChineseLanguageName(languageText);
			return true;
		}
		catch
		{
			return false;
		}
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
		return languageName.IndexOf("Chinese", StringComparison.OrdinalIgnoreCase) >= 0 || languageName.IndexOf("中文", StringComparison.OrdinalIgnoreCase) >= 0 || languageName.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
	}

	private static string GetSectionName(bool isChineseLanguage)
	{
		return isChineseLanguage ? "毒雾" : "Fog";
	}

	private static string GetKeyName(ConfigKey configKey, bool isChineseLanguage)
	{
		return configKey switch
		{
			ConfigKey.ModEnabled => isChineseLanguage ? "模组开关" : "Enable Mod",
			ConfigKey.FogSpeed => isChineseLanguage ? "毒雾移动速度" : "Fog Speed",
			ConfigKey.FogDelay => isChineseLanguage ? "毒雾延迟时间s" : "Fog Delay (s)",
			ConfigKey.FogUiEnabled => isChineseLanguage ? "UI启用" : "Fog UI",
			ConfigKey.FogUiX => isChineseLanguage ? "UI X位置" : "UI X Position",
			ConfigKey.FogUiY => isChineseLanguage ? "UI Y位置" : "UI Y Position",
			ConfigKey.FogUiScale => isChineseLanguage ? "UI缩放" : "UI Scale",
			_ => string.Empty
		};
	}

	private static string GetLocalizedDescription(ConfigKey configKey, bool isChineseLanguage)
	{
		return configKey switch
		{
			ConfigKey.ModEnabled => isChineseLanguage ? "启用独立毒雾模组。仅主机需要安装；主机会同步毒雾进度、冷伤害压制与篝火光柱指示。" : "Enable the standalone fog mod. Only the host needs it; the host synchronizes fog progress, cold suppression, and the campfire beacon.",
			ConfigKey.FogSpeed => isChineseLanguage ? "毒雾移动速度，范围 0.3~5，默认 2。" : "Fog movement speed. Range 0.3 to 5, default 2.",
			ConfigKey.FogDelay => isChineseLanguage ? "首段毒雾开始移动前的延迟，范围 0~150 秒，默认 90 秒。" : "Delay before the first fog segment starts moving. Range 0 to 150 seconds, default 90 seconds.",
			ConfigKey.FogUiEnabled => isChineseLanguage ? "显示 FogClimb HUD 文本。" : "Show the FogClimb HUD text.",
			ConfigKey.FogUiX => isChineseLanguage ? "毒雾 HUD 的 X 位置，默认 60。" : "Fog HUD X position. Default 60.",
			ConfigKey.FogUiY => isChineseLanguage ? "毒雾 HUD 的 Y 位置，默认 16。" : "Fog HUD Y position. Default 16.",
			ConfigKey.FogUiScale => isChineseLanguage ? "毒雾 HUD 缩放，默认 1.2。" : "Fog HUD scale. Default 1.2.",
			_ => string.Empty
		};
	}

	private static bool HasFogAuthority()
	{
		return !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;
	}

	internal static bool IsModFeatureEnabled()
	{
		return ModEnabled == null || ModEnabled.Value;
	}

	internal static bool ShouldSuppressFogColdDamage()
	{
		return IsModFeatureEnabled();
	}

	internal static bool IsInternalColdClearInProgress()
	{
		return _isClearingColdStatus;
	}

	internal static bool ShouldSuppressAmbientColdDamageFor(CharacterAfflictions afflictions)
	{
		return afflictions != null && ShouldSuppressAmbientColdDamageFor(afflictions.character);
	}

	internal static bool ShouldSuppressAmbientColdDamageFor(Character character)
	{
		return IsModFeatureEnabled() && character != null && !character.isBot && !character.isZombie && character == Character.localCharacter;
	}

	internal static bool ShouldBlockVanillaOrbFogWait(OrbFogHandler fogHandler)
	{
		if (!IsModFeatureEnabled() || Instance == null || fogHandler == null || !HasFogAuthority())
		{
			return false;
		}
		if (!Instance._fogStateInitialized)
		{
			Instance.InitializeFogRuntimeState(fogHandler);
		}
		Instance.UpdateTrackedFogOrigin();
		return fogHandler.currentID == 0 && !Instance._initialDelayCompleted;
	}

	internal static void EnforceAmbientColdSuppression(CharacterAfflictions afflictions)
	{
		if (!ShouldSuppressAmbientColdDamageFor(afflictions))
		{
			return;
		}
		try
		{
			if (!_isClearingColdStatus)
			{
				_isClearingColdStatus = true;
				afflictions.SetStatus(CharacterAfflictions.STATUSTYPE.Cold, 0f, false);
			}
		}
		catch
		{
		}
		finally
		{
			_isClearingColdStatus = false;
		}
		int coldIndex = (int)CharacterAfflictions.STATUSTYPE.Cold;
		foreach (string fieldName in ColdStatusArrayFields)
		{
			ZeroColdStatusArrayField(afflictions, fieldName, coldIndex);
		}
		try
		{
			CharacterData data = afflictions.character?.data;
			FieldInfo fieldInfo = data?.GetType().GetField("sinceAddedCold", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (fieldInfo != null && fieldInfo.FieldType == typeof(float))
			{
				fieldInfo.SetValue(data, 0f);
			}
		}
		catch
		{
		}
	}

	internal static void ClearLocalAmbientColdStatus()
	{
		if (!IsModFeatureEnabled() || Instance == null || Time.unscaledTime - Instance._lastColdClearTime < ColdClearIntervalSeconds)
		{
			return;
		}
		CharacterAfflictions afflictions = Character.localCharacter?.refs?.afflictions;
		if (afflictions == null)
		{
			return;
		}
		Instance._lastColdClearTime = Time.unscaledTime;
		EnforceAmbientColdSuppression(afflictions);
	}

	internal static void ClearLocalFogColdStatus()
	{
		ClearLocalAmbientColdStatus();
	}

	internal static void NotifyCampfireLit(Campfire campfire)
	{
		Instance?.HandleCampfireLit(campfire);
	}

	private void HandleCampfireLit(Campfire campfire)
	{
		if (!IsModFeatureEnabled() || !HasFogAuthority() || campfire == null)
		{
			return;
		}
		int campfireId = campfire.GetInstanceID();
		if (!_grantedCampfireCompassIds.Add(campfireId))
		{
			return;
		}
		GrantCompassToAllPlayers($"campfire-{campfireId}");
	}

	private static void ZeroColdStatusArrayField(CharacterAfflictions afflictions, string fieldName, int index)
	{
		try
		{
			FieldInfo fieldInfo = typeof(CharacterAfflictions).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			float[] statusArray = fieldInfo?.GetValue(afflictions) as float[];
			if (statusArray != null && index >= 0 && index < statusArray.Length)
			{
				statusArray[index] = 0f;
			}
		}
		catch
		{
		}
	}
}
