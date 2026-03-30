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
		CompassHotkey,
		FogUiEnabled,
		FogUiX,
		FogUiY,
		FogUiScale
	}

	public const string PluginGuid = "com.github.Thanks.FogClimb";

	public const string PluginName = "FogClimb";

	public const string PluginVersion = "0.0.6";

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

	private const float CompassGrantSyncIntervalSeconds = 0.75f;

	private const float FogColdPerSecond = 0.0105f;

	private const float FogUiWidth = 920f;

	private const float FogUiHeight = 30f;

	private const float CompassLobbyNoticeRightOffset = 28f;

	private const float CompassLobbyNoticeDownOffset = 96f;

	private const float CompassLobbyNoticeWidth = 980f;

	private const float CompassLobbyNoticeHeight = 108f;

	private const float CompassLobbyNoticeFontSizeMultiplier = 1.6f;

	private const float CompassLobbyNoticeLineSpacing = 1.5f;

	private const string CompassLobbyNoticeKeyColor = "#FF3B30";

	// Manual entry point for adjusting the granted compass item when auto-detection is wrong.
	private static readonly ushort CompassItemIdOverride = 0;

	private const string CompassNameKeyword = "Compass";

	private const string ModConfigPluginGuid = "com.github.PEAKModding.PEAKLib.ModConfig";

	private const int SimplifiedChineseLanguageIndex = 9;

	private static readonly BindingFlags InstanceBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

	private static readonly BindingFlags StaticBindingFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

	private static readonly FieldInfo ConfigEntryDescriptionField = typeof(ConfigEntryBase).GetField("<Description>k__BackingField", InstanceBindingFlags);

	private static readonly PropertyInfo ConfigFileEntriesProperty = typeof(ConfigFile).GetProperty("Entries", InstanceBindingFlags);

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

	private float _lastCompassGrantSyncTime = -CompassGrantSyncIntervalSeconds;

	private bool _lastModEnabledState;

	private bool _lastFogUiEnabledState;

	private bool _lastDetectedChineseLanguage;

	private bool _isRefreshingLanguage;

	private float _lastFogUiX;

	private float _lastFogUiY;

	private float _lastFogUiScale;

	private readonly HashSet<int> _grantedCampfireCompassIds = new HashSet<int>();

	private readonly Dictionary<int, int> _playerCompassGrantCounts = new Dictionary<int, int>();

	private Item _compassItem;

	private RectTransform _fogUiRect;

	private TextMeshProUGUI _fogUiText;

	private RectTransform _compassLobbyNoticeRect;

	private TextMeshProUGUI _compassLobbyNoticeText;

	private string _lastCompassLobbyNoticeText = string.Empty;

	private bool _initialCompassGranted;

	private int _totalCompassGrantCount;

	internal static Plugin Instance { get; private set; }

	internal static ConfigEntry<bool> ModEnabled { get; private set; }

	internal static ConfigEntry<float> FogSpeed { get; private set; }

	internal static ConfigEntry<float> FogDelay { get; private set; }

	internal static ConfigEntry<KeyCode> CompassHotkey { get; private set; }

	internal static ConfigEntry<bool> FogUiEnabled { get; private set; }

	internal static ConfigEntry<float> FogUiX { get; private set; }

	internal static ConfigEntry<float> FogUiY { get; private set; }

	internal static ConfigEntry<float> FogUiScale { get; private set; }

	private void Awake()
	{
		Instance = this;
		TryCleanupGeneratedBackupFile();
		_lastDetectedChineseLanguage = DetectChineseLanguage();
		InitializeConfig(_lastDetectedChineseLanguage);
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
		CleanupCompassLobbyNotice();
		if (Instance == this)
		{
			Instance = null;
		}
	}

	private void InitializeConfig(bool isChineseLanguage)
	{
		string sectionName = GetSectionName(isChineseLanguage);
		ModEnabled = Config.Bind(sectionName, GetKeyName(ConfigKey.ModEnabled, isChineseLanguage), true, CreateConfigDescription(ConfigKey.ModEnabled, isChineseLanguage));
		FogSpeed = Config.Bind(sectionName, GetKeyName(ConfigKey.FogSpeed, isChineseLanguage), DefaultFogSpeed, CreateConfigDescription(ConfigKey.FogSpeed, isChineseLanguage));
		FogDelay = Config.Bind(sectionName, GetKeyName(ConfigKey.FogDelay, isChineseLanguage), DefaultFogDelaySeconds, CreateConfigDescription(ConfigKey.FogDelay, isChineseLanguage));
		CompassHotkey = Config.Bind(sectionName, GetKeyName(ConfigKey.CompassHotkey, isChineseLanguage), KeyCode.G, CreateConfigDescription(ConfigKey.CompassHotkey, isChineseLanguage));
		FogUiEnabled = Config.Bind(sectionName, GetKeyName(ConfigKey.FogUiEnabled, isChineseLanguage), true, CreateConfigDescription(ConfigKey.FogUiEnabled, isChineseLanguage));
		FogUiX = Config.Bind(sectionName, GetKeyName(ConfigKey.FogUiX, isChineseLanguage), DefaultFogUiX, CreateConfigDescription(ConfigKey.FogUiX, isChineseLanguage));
		FogUiY = Config.Bind(sectionName, GetKeyName(ConfigKey.FogUiY, isChineseLanguage), DefaultFogUiY, CreateConfigDescription(ConfigKey.FogUiY, isChineseLanguage));
		FogUiScale = Config.Bind(sectionName, GetKeyName(ConfigKey.FogUiScale, isChineseLanguage), DefaultFogUiScale, CreateConfigDescription(ConfigKey.FogUiScale, isChineseLanguage));
		MigrateLocalizedConfigEntries();
		ClampConfigValues();
	}

	private ConfigDescription CreateConfigDescription(ConfigKey configKey, bool isChineseLanguage)
	{
		return configKey switch
		{
			ConfigKey.FogSpeed => new ConfigDescription(GetLocalizedDescription(configKey, isChineseLanguage), new AcceptableValueRange<float>(MinFogSpeed, MaxFogSpeed), Array.Empty<object>()),
			ConfigKey.FogDelay => new ConfigDescription(GetLocalizedDescription(configKey, isChineseLanguage), new AcceptableValueRange<float>(MinFogDelaySeconds, MaxFogDelaySeconds), Array.Empty<object>()),
			ConfigKey.FogUiX => new ConfigDescription(GetLocalizedDescription(configKey, isChineseLanguage), new AcceptableValueRange<float>(MinFogUiX, MaxFogUiX), Array.Empty<object>()),
			ConfigKey.FogUiY => new ConfigDescription(GetLocalizedDescription(configKey, isChineseLanguage), new AcceptableValueRange<float>(MinFogUiY, MaxFogUiY), Array.Empty<object>()),
			ConfigKey.FogUiScale => new ConfigDescription(GetLocalizedDescription(configKey, isChineseLanguage), new AcceptableValueRange<float>(MinFogUiScale, MaxFogUiScale), Array.Empty<object>()),
			_ => new ConfigDescription(GetLocalizedDescription(configKey, isChineseLanguage), null, Array.Empty<object>())
		};
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
		migratedAnyValue |= TryMigrateLocalizedConfigValue(CompassHotkey, ConfigKey.CompassHotkey, orphanedEntries);
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
				SetFogUiVisible(false);
			}
			else
			{
				_fogStateInitialized = false;
				_lastFogStateSyncTime = -FogStateSyncIntervalSeconds;
				_lastRemoteStatusSyncTime = -RemoteStatusSyncIntervalSeconds;
				_lastCompassGrantSyncTime = -CompassGrantSyncIntervalSeconds;
			}
		}
		TryResolveRuntimeObjects();
		HandleLanguageChangeIfNeeded();
		HandleFogUiConfigChanges();
		HandleManualCompassHotkey();
		if (!modEnabled)
		{
			UpdateFogUi();
			UpdateCompassLobbyNotice();
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
			SyncCompassGrantsToPlayersIfNeeded();
		}
		ClearLocalAmbientColdStatus();
		UpdateFogUi();
		UpdateCompassLobbyNotice();
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		ResetFogRuntimeState();
		_orbFogHandler = null;
		_fogSphere = null;
		_compassItem = null;
		CleanupFogUi();
		CleanupCompassLobbyNotice();
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

	private bool TryResolveCompassItem()
	{
		if (_compassItem != null)
		{
			return true;
		}
		if (TryResolveCompassItemOverride(out Item overriddenCompass))
		{
			_compassItem = overriddenCompass;
			return true;
		}
		ItemDatabase database = SingletonAsset<ItemDatabase>.Instance;
		if (database == null || database.Objects == null)
		{
			return false;
		}
		List<string> normalCompassCandidates = new List<string>();
		foreach (Item item in database.Objects)
		{
			if (item == null)
			{
				continue;
			}
			CompassPointer compassPointer = item.GetComponentInChildren<CompassPointer>(true);
			if (compassPointer == null)
			{
				continue;
			}
			if (compassPointer.compassType != CompassPointer.CompassType.Normal)
			{
				continue;
			}
			normalCompassCandidates.Add(DescribeCompassItem(item));
			if (!LooksLikeStandardCompassItem(item))
			{
				continue;
			}
			_compassItem = item;
			Logger.LogInfo($"[{PluginName}] Using normal compass item: {DescribeCompassItem(item)}");
			return true;
		}
		if (normalCompassCandidates.Count > 0)
		{
			Logger.LogWarning($"[{PluginName}] Failed to identify the standard compass automatically. Set CompassItemIdOverride manually. Candidates: {string.Join(", ", normalCompassCandidates)}");
		}
		return false;
	}

	private bool TryResolveCompassItemOverride(out Item item)
	{
		item = null;
		if (CompassItemIdOverride <= 0)
		{
			return false;
		}
		if (!ItemDatabase.TryGetItem(CompassItemIdOverride, out Item resolvedItem) || resolvedItem == null)
		{
			Logger.LogWarning($"[{PluginName}] CompassItemIdOverride={CompassItemIdOverride} is invalid.");
			return false;
		}
		item = resolvedItem;
		Logger.LogInfo($"[{PluginName}] Using compass item override: {DescribeCompassItem(item)}");
		return true;
	}

	private static bool LooksLikeStandardCompassItem(Item item)
	{
		if (item == null)
		{
			return false;
		}
		string prefabName = item.name ?? string.Empty;
		string uiName = item.UIData?.itemName ?? string.Empty;
		return prefabName.IndexOf(CompassNameKeyword, StringComparison.OrdinalIgnoreCase) >= 0 || uiName.IndexOf(CompassNameKeyword, StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static string DescribeCompassItem(Item item)
	{
		if (item == null)
		{
			return "<null>";
		}
		string uiName = item.UIData?.itemName ?? string.Empty;
		return $"{item.name} (itemID={item.itemID}, uiName={uiName})";
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
		if (ShouldSpawnCompassAboveHead(player))
		{
			return DropCompassAbovePlayerHead(player, reason);
		}
		if (player.AddItem(_compassItem.itemID, null, out _))
		{
			Logger.LogDebug($"[{PluginName}] Granted compass to {player.name} ({reason}).");
			return true;
		}
		return DropCompassAbovePlayerHead(player, reason) || DropCompassNearPlayer(player, reason);
	}

	private static bool ShouldSpawnCompassAboveHead(Player player)
	{
		if (player == null)
		{
			return false;
		}
		Character character = player.character;
		if (character?.data != null && (character.data.currentItem != null || character.data.isClimbingAnything))
		{
			return true;
		}
		for (int i = 0; i < player.itemSlots.Length; i++)
		{
			ItemSlot itemSlot = player.itemSlots[i];
			if (itemSlot != null && itemSlot.IsEmpty())
			{
				return false;
			}
		}
		return true;
	}

	private bool DropCompassAbovePlayerHead(Player player, string reason)
	{
		if (player == null || _compassItem == null)
		{
			return false;
		}
		Vector3 spawnPosition = GetCompassAboveHeadPosition(player);
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
			Logger.LogDebug($"[{PluginName}] Spawned compass above {player.name} ({reason}) to avoid occupying the held-item slot while climbing.");
			return true;
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"[{PluginName}] Failed to spawn compass above {player.name} ({reason}): {ex.Message}");
			return false;
		}
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

	private static Vector3 GetCompassAboveHeadPosition(Player player)
	{
		Character character = player.character;
		Vector3 headPosition = character != null ? character.Head : player.transform.position + Vector3.up * 1.6f;
		Vector3 awayFromWall = character != null ? -character.transform.forward : Vector3.forward;
		if (awayFromWall.sqrMagnitude < 0.01f)
		{
			awayFromWall = Vector3.forward;
		}
		return headPosition + Vector3.up * 0.45f + awayFromWall.normalized * 0.35f;
	}

	private void HandleManualCompassHotkey()
	{
		if (!IsModFeatureEnabled() || !HasFogAuthority())
		{
			return;
		}
		KeyCode hotkey = GetCompassHotkey();
		if (hotkey == KeyCode.None)
		{
			return;
		}
		GUIManager instance = GUIManager.instance;
		if (instance != null && instance.windowBlockingInput)
		{
			return;
		}
		if (!Input.GetKeyDown(hotkey))
		{
			return;
		}
		TrySpawnManualCompassForLocalPlayer();
	}

	private void TrySpawnManualCompassForLocalPlayer()
	{
		if (!TryResolveCompassItem())
		{
			Logger.LogWarning($"[{PluginName}] Failed to resolve compass item for manual hotkey spawn.");
			return;
		}
		if (!TryGetLocalPlayablePlayer(out Player localPlayer))
		{
			return;
		}
		if (!DropCompassAbovePlayerHead(localPlayer, "manual-hotkey"))
		{
			Logger.LogWarning($"[{PluginName}] Manual compass hotkey spawn failed for {localPlayer.name}.");
		}
	}

	private static bool TryGetLocalPlayablePlayer(out Player localPlayer)
	{
		localPlayer = Player.localPlayer ?? Character.localCharacter?.player;
		Character character = localPlayer?.character ?? Character.localCharacter;
		return localPlayer != null && character != null && !character.isBot && !character.isZombie;
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

	private void HandleLanguageChangeIfNeeded()
	{
		bool isChineseLanguage = DetectChineseLanguage();
		if (isChineseLanguage != _lastDetectedChineseLanguage)
		{
			ReinitializeLocalizedConfig(isChineseLanguage);
		}
		TryLocalizeVisibleModConfigUi();
	}

	private void ReinitializeLocalizedConfig(bool isChineseLanguage)
	{
		if (_isRefreshingLanguage)
		{
			return;
		}
		_isRefreshingLanguage = true;
		try
		{
			_lastDetectedChineseLanguage = isChineseLanguage;
			ApplyLocalizedConfigMetadata(isChineseLanguage);
			if (_fogUiText != null)
			{
				ApplyGameTextStyle(_fogUiText, Color.white);
			}
			if (_compassLobbyNoticeText != null)
			{
				ApplyCompassLobbyNoticeStyle(_compassLobbyNoticeText);
			}
			TryLocalizeVisibleModConfigUi();
			UpdateFogUi();
			UpdateCompassLobbyNotice();
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"[{PluginName}] Failed to reinitialize localized config: {ex.Message}");
		}
		finally
		{
			_isRefreshingLanguage = false;
		}
	}

	private void ApplyLocalizedConfigMetadata(bool isChineseLanguage)
	{
		try
		{
			foreach (ConfigEntryBase entry in GetConfigEntriesSnapshot(Config))
			{
				if (entry?.Definition == null || entry.Description == null || !TryGetConfigKey(entry.Definition.Key, out ConfigKey configKey))
				{
					continue;
				}
				string localizedDescription = GetLocalizedDescription(configKey, isChineseLanguage);
				if (!string.IsNullOrWhiteSpace(localizedDescription))
				{
					SetPrivateField(entry.Description, "<Description>k__BackingField", localizedDescription);
				}
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"[{PluginName}] Failed to apply localized config metadata: {ex.Message}");
		}
	}

	private static ConfigEntryBase[] GetConfigEntriesSnapshot(ConfigFile configFile)
	{
		if (configFile == null)
		{
			return Array.Empty<ConfigEntryBase>();
		}
		if (!(ConfigFileEntriesProperty?.GetValue(configFile) is IDictionary dictionary) || dictionary.Count == 0)
		{
			return Array.Empty<ConfigEntryBase>();
		}
		return dictionary.Values.Cast<object>().OfType<ConfigEntryBase>().Where(entry => entry != null).ToArray();
	}

	private static void SetPrivateField(object target, string fieldName, object value)
	{
		if (target == null || string.IsNullOrWhiteSpace(fieldName))
		{
			return;
		}
		target.GetType().GetField(fieldName, InstanceBindingFlags)?.SetValue(target, value);
	}

	private static bool TryGetConfigKey(string keyName, out ConfigKey configKey)
	{
		foreach (ConfigKey candidate in Enum.GetValues(typeof(ConfigKey)))
		{
			if (string.Equals(keyName, GetKeyName(candidate, isChineseLanguage: false), StringComparison.OrdinalIgnoreCase) || string.Equals(keyName, GetKeyName(candidate, isChineseLanguage: true), StringComparison.OrdinalIgnoreCase))
			{
				configKey = candidate;
				return true;
			}
		}
		configKey = default;
		return false;
	}

	private void TryLocalizeVisibleModConfigUi()
	{
		if (!TryGetModConfigMenuInstance(out Type menuType, out object menuInstance))
		{
			return;
		}
		if (!(menuInstance is Behaviour behaviour) || behaviour == null)
		{
			return;
		}
		try
		{
			if (!behaviour.isActiveAndEnabled || !behaviour.gameObject.activeInHierarchy)
			{
				return;
			}
		}
		catch
		{
			return;
		}
		Dictionary<string, string> map = BuildModConfigUiLocalizationMap(DetectChineseLanguage());
		foreach (Transform root in EnumerateModConfigUiRoots(menuInstance, menuType))
		{
			ApplyTextLocalizationToRoot(root, map);
		}
	}

	private static bool TryGetModConfigMenuInstance(out Type menuType, out object menuInstance)
	{
		menuType = null;
		menuInstance = null;
		if (!Chainloader.PluginInfos.TryGetValue(ModConfigPluginGuid, out PluginInfo pluginInfo) || pluginInfo?.Instance == null)
		{
			return false;
		}
		Assembly assembly = pluginInfo.Instance.GetType().Assembly;
		menuType = assembly.GetType("PEAKLib.ModConfig.Components.ModdedSettingsMenu");
		menuInstance = menuType?.GetProperty("Instance", StaticBindingFlags)?.GetValue(null);
		return menuType != null && menuInstance != null;
	}

	private IEnumerable<Transform> EnumerateModConfigUiRoots(object menuInstance, Type menuType)
	{
		HashSet<int> visited = new HashSet<int>();
		foreach (Transform root in EnumerateCandidateTransforms(menuInstance, menuType))
		{
			if (root != null && visited.Add(root.GetInstanceID()))
			{
				yield return root;
			}
		}
	}

	private static IEnumerable<Transform> EnumerateCandidateTransforms(object menuInstance, Type menuType)
	{
		if (menuInstance is Component component)
		{
			yield return component.transform;
		}
		object contentObject = menuType?.GetProperty("Content", InstanceBindingFlags)?.GetValue(menuInstance);
		if (contentObject is Transform content)
		{
			yield return content;
		}
	}

	private void ApplyTextLocalizationToRoot(Transform root, Dictionary<string, string> map)
	{
		if (root == null || map == null || map.Count == 0)
		{
			return;
		}
		foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
		{
			if (text == null)
			{
				continue;
			}
			string trimmed = text.text?.Trim();
			if (string.IsNullOrWhiteSpace(trimmed))
			{
				continue;
			}
			if (map.TryGetValue(trimmed, out string localized) && !string.Equals(text.text, localized, StringComparison.Ordinal))
			{
				text.text = localized;
			}
		}
	}

	private Dictionary<string, string> BuildModConfigUiLocalizationMap(bool isChineseLanguage)
	{
		Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
		AddUiLocalizationPair(map, "Fog Climb", GetLocalizedModDisplayName(isChineseLanguage));
		AddUiLocalizationPair(map, "FogClimb", GetLocalizedModDisplayName(isChineseLanguage));
		AddUiLocalizationPair(map, "毒雾攀登", GetLocalizedModDisplayName(isChineseLanguage));
		AddUiLocalizationPair(map, GetSectionName(isChineseLanguage: false), GetSectionName(isChineseLanguage));
		AddUiLocalizationPair(map, GetSectionName(isChineseLanguage: true), GetSectionName(isChineseLanguage));
		foreach (ConfigKey configKey in Enum.GetValues(typeof(ConfigKey)))
		{
			AddUiLocalizationPair(map, GetKeyName(configKey, isChineseLanguage: false), GetKeyName(configKey, isChineseLanguage));
			AddUiLocalizationPair(map, GetKeyName(configKey, isChineseLanguage: true), GetKeyName(configKey, isChineseLanguage));
			AddUiLocalizationPair(map, GetLocalizedDescription(configKey, isChineseLanguage: false), GetLocalizedDescription(configKey, isChineseLanguage));
			AddUiLocalizationPair(map, GetLocalizedDescription(configKey, isChineseLanguage: true), GetLocalizedDescription(configKey, isChineseLanguage));
		}
		return map;
	}

	private static void AddUiLocalizationPair(Dictionary<string, string> map, string source, string localized)
	{
		if (map == null || string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(localized))
		{
			return;
		}
		string sourceTrimmed = source.Trim();
		string localizedTrimmed = localized.Trim();
		map[sourceTrimmed] = localizedTrimmed;
		map[localizedTrimmed] = localizedTrimmed;
		string sourceCompact = sourceTrimmed.Replace(" ", string.Empty);
		string localizedCompact = localizedTrimmed.Replace(" ", string.Empty);
		if (!map.ContainsKey(sourceCompact))
		{
			map[sourceCompact] = localizedTrimmed;
		}
		if (!map.ContainsKey(localizedCompact))
		{
			map[localizedCompact] = localizedTrimmed;
		}
		map[sourceTrimmed.ToUpperInvariant()] = localizedTrimmed;
		map[localizedTrimmed.ToUpperInvariant()] = localizedTrimmed;
	}

	private static KeyCode GetCompassHotkey()
	{
		return CompassHotkey?.Value ?? KeyCode.G;
	}

	private static string GetCompassHotkeyLabel()
	{
		KeyCode hotkey = GetCompassHotkey();
		string keyText = hotkey.ToString();
		if (string.IsNullOrWhiteSpace(keyText) || hotkey == KeyCode.None)
		{
			return "G";
		}
		return keyText.ToUpperInvariant();
	}

	private string GetCompassLobbyNoticeText(bool isChineseLanguage)
	{
		string keyText = $"<color={CompassLobbyNoticeKeyColor}>{GetCompassHotkeyLabel()}</color>";
		return isChineseLanguage ? $"按 {keyText} 生成指南针" : $"Press {keyText} to spawn compass";
	}

	private bool ShouldShowCompassLobbyNotice()
	{
		return IsModFeatureEnabled() && HasFogAuthority() && GetCompassHotkey() != KeyCode.None && IsAirportScene(SceneManager.GetActiveScene());
	}

	private void UpdateCompassLobbyNotice()
	{
		if (!ShouldShowCompassLobbyNotice())
		{
			CleanupCompassLobbyNotice();
			return;
		}
		Canvas targetCanvas = ResolveHudCanvas();
		if (!IsCanvasUsable(targetCanvas))
		{
			CleanupCompassLobbyNotice();
			return;
		}
		if (_compassLobbyNoticeRect == null || _compassLobbyNoticeText == null || _compassLobbyNoticeRect.parent != targetCanvas.transform)
		{
			CreateCompassLobbyNotice(targetCanvas);
			if (_compassLobbyNoticeRect == null || _compassLobbyNoticeText == null)
			{
				return;
			}
		}
		string noticeText = GetCompassLobbyNoticeText(DetectChineseLanguage());
		if (!string.Equals(_lastCompassLobbyNoticeText, noticeText, StringComparison.Ordinal))
		{
			_lastCompassLobbyNoticeText = noticeText;
			_compassLobbyNoticeText.text = noticeText;
		}
		ClampCompassLobbyNoticeToCanvas(targetCanvas);
		_compassLobbyNoticeRect.gameObject.SetActive(true);
	}

	private void CreateCompassLobbyNotice(Canvas targetCanvas = null)
	{
		CleanupCompassLobbyNotice();
		Canvas canvas = targetCanvas ?? ResolveHudCanvas();
		if (!IsCanvasUsable(canvas))
		{
			return;
		}
		GameObject noticeObject = new GameObject("FogClimbCompassLobbyNotice");
		noticeObject.transform.SetParent(canvas.transform, false);
		noticeObject.transform.SetAsLastSibling();
		_compassLobbyNoticeRect = noticeObject.AddComponent<RectTransform>();
		_compassLobbyNoticeRect.sizeDelta = new Vector2(CompassLobbyNoticeWidth, CompassLobbyNoticeHeight);
		_compassLobbyNoticeText = noticeObject.AddComponent<TextMeshProUGUI>();
		ApplyCompassLobbyNoticeStyle(_compassLobbyNoticeText);
		_lastCompassLobbyNoticeText = GetCompassLobbyNoticeText(DetectChineseLanguage());
		_compassLobbyNoticeText.text = _lastCompassLobbyNoticeText;
		ClampCompassLobbyNoticeToCanvas(canvas);
	}

	private void ApplyCompassLobbyNoticeStyle(TextMeshProUGUI target)
	{
		if (target == null)
		{
			return;
		}
		ApplyGameTextStyle(target, new Color(1f, 0.94f, 0.72f), CompassLobbyNoticeFontSizeMultiplier);
		target.fontStyle = FontStyles.Normal;
		target.alignment = TextAlignmentOptions.MidlineRight;
		target.textWrappingMode = TextWrappingModes.NoWrap;
		target.overflowMode = TextOverflowModes.Overflow;
		target.lineSpacing = CompassLobbyNoticeLineSpacing;
	}

	private void ClampCompassLobbyNoticeToCanvas(Canvas targetCanvas = null)
	{
		if (_compassLobbyNoticeRect == null)
		{
			return;
		}
		Canvas canvas = targetCanvas ?? ResolveHudCanvas() ?? _compassLobbyNoticeRect.GetComponentInParent<Canvas>();
		if (!IsCanvasUsable(canvas))
		{
			return;
		}
		if (_compassLobbyNoticeRect.parent != canvas.transform)
		{
			_compassLobbyNoticeRect.SetParent(canvas.transform, false);
		}
		ApplyRightMiddleAnchoredRect(_compassLobbyNoticeRect, CompassLobbyNoticeWidth, CompassLobbyNoticeHeight, CompassLobbyNoticeRightOffset, CompassLobbyNoticeDownOffset);
	}

	private void CleanupCompassLobbyNotice()
	{
		if (_compassLobbyNoticeRect != null)
		{
			UnityEngine.Object.Destroy(_compassLobbyNoticeRect.gameObject);
		}
		_compassLobbyNoticeRect = null;
		_compassLobbyNoticeText = null;
		_lastCompassLobbyNoticeText = string.Empty;
	}

	private bool ShouldShowFogUi(Canvas targetCanvas = null)
	{
		if (!IsModFeatureEnabled() || !(FogUiEnabled?.Value ?? true))
		{
			return false;
		}
		if (LoadingScreenHandler.loading || IsFogUiBlockedByOverlay() || !IsFogUiSceneAllowed())
		{
			return false;
		}
		Canvas canvas = targetCanvas ?? ResolveHudCanvas();
		return IsCanvasUsable(canvas);
	}

	private static bool IsFogUiBlockedByOverlay()
	{
		GUIManager instance = GUIManager.instance;
		return instance != null && instance.endScreen != null && instance.endScreen.isOpen;
	}

	private static bool IsFogUiSceneAllowed()
	{
		Scene activeScene = SceneManager.GetActiveScene();
		if (!activeScene.IsValid())
		{
			return GameHandler.IsOnIsland;
		}
		return IsAirportScene(activeScene) || IsGameplayFogScene(activeScene);
	}

	private static bool IsAirportScene(Scene scene)
	{
		return string.Equals(scene.name, "Airport", StringComparison.OrdinalIgnoreCase);
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

	private Canvas ResolveHudCanvas()
	{
		if (!IsFogUiSceneAllowed())
		{
			return null;
		}
		GUIManager instance = GUIManager.instance;
		if (instance != null)
		{
			if (IsCanvasUsable(instance.hudCanvas))
			{
				return instance.hudCanvas;
			}
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

	private static bool IsCanvasUsable(Canvas canvas)
	{
		return canvas != null && canvas.isRootCanvas && canvas.isActiveAndEnabled && canvas.gameObject.activeInHierarchy && canvas.renderMode != RenderMode.WorldSpace;
	}

	private bool NeedsFogUiRebuild(Canvas targetCanvas)
	{
		if (_fogUiRect == null || _fogUiText == null || !IsCanvasUsable(targetCanvas))
		{
			return true;
		}
		Canvas currentCanvas = _fogUiRect.GetComponentInParent<Canvas>();
		return !IsCanvasUsable(currentCanvas) || _fogUiRect.parent != targetCanvas.transform;
	}

	private TextMeshProUGUI ResolveFogStyleSource()
	{
		bool isChineseLanguage = DetectChineseLanguage();
		GUIManager instance = GUIManager.instance;
		if (instance != null)
		{
			TextMeshProUGUI[] candidates = new TextMeshProUGUI[3] { instance.itemPromptMain, instance.interactNameText, instance.interactPromptText };
			foreach (TextMeshProUGUI candidate in candidates)
			{
				if (IsTextSourceSuitable(candidate, isChineseLanguage))
				{
					return candidate;
				}
			}
			foreach (TextMeshProUGUI candidate2 in candidates)
			{
				if (candidate2 != null)
				{
					return candidate2;
				}
			}
		}
		TextMeshProUGUI localizedSource = FindLocalizedTextSource(isChineseLanguage);
		if (localizedSource != null)
		{
			return localizedSource;
		}
		return null;
	}

	private static TextMeshProUGUI FindLocalizedTextSource(bool isChineseLanguage)
	{
		TextMeshProUGUI bestSource = null;
		int bestScore = int.MinValue;
		TextMeshProUGUI[] texts = UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
		foreach (TextMeshProUGUI text in texts)
		{
			if (text == null || text.font == null || !text.isActiveAndEnabled || !text.gameObject.activeInHierarchy)
			{
				continue;
			}
			int score = 0;
			if (ContainsLocalizedCharacters(text.text, isChineseLanguage))
			{
				score += 100;
			}
			if (text.GetComponentInParent<Canvas>() != null)
			{
				score += 20;
			}
			if (text.fontSharedMaterial != null)
			{
				score += 5;
			}
			if (score > bestScore)
			{
				bestScore = score;
				bestSource = text;
			}
		}
		return bestScore > 0 ? bestSource : null;
	}

	private static bool IsTextSourceSuitable(TextMeshProUGUI source, bool isChineseLanguage)
	{
		if (source == null || source.font == null)
		{
			return false;
		}
		return !isChineseLanguage || ContainsLocalizedCharacters(source.text, isChineseLanguage: true);
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
				if (character >= '\u3400' && character <= '\u9fff')
				{
					return true;
				}
			}
			else if (character <= sbyte.MaxValue && char.IsLetter(character))
			{
				return true;
			}
		}
		return false;
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

	private void CreateFogUi(Canvas targetCanvas = null)
	{
		CleanupFogUi();
		Canvas canvas = targetCanvas ?? ResolveHudCanvas();
		if (!ShouldShowFogUi(canvas))
		{
			return;
		}
		try
		{
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
			_fogUiText.text = string.Empty;
			ClampFogUiToCanvas(canvas);
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
		Canvas targetCanvas = ResolveHudCanvas();
		if (!ShouldShowFogUi(targetCanvas))
		{
			SetFogUiVisible(false);
			return;
		}
		if (NeedsFogUiRebuild(targetCanvas))
		{
			CreateFogUi(targetCanvas);
			if (_fogUiRect == null || _fogUiText == null)
			{
				return;
			}
		}
		SetFogUiVisible(true);
		ClampFogUiToCanvas(targetCanvas);
		bool isChinese = DetectChineseLanguage();
		float speed = _orbFogHandler != null ? _orbFogHandler.speed : (FogSpeed?.Value ?? DefaultFogSpeed);
		string state = GetFogStateLabel(isChinese);
		string countdown = GetFogCountdownLabel(isChinese);
		if (_orbFogHandler == null)
		{
			float configuredDelay = FogDelay?.Value ?? DefaultFogDelaySeconds;
			_fogUiText.text = isChinese
				? $"Fog Climb  |  速度: {speed:F2}  |  延迟: {configuredDelay:F0}s  |  状态: {state}"
				: $"Fog Climb  |  Speed: {speed:F2}  |  Delay: {configuredDelay:F0}s  |  State: {state}";
			return;
		}
		_fogUiText.text = isChinese
			? $"Fog Climb  |  速度: {speed:F2}  |  状态: {state}{countdown}"
			: $"Fog Climb  |  Speed: {speed:F2}  |  State: {state}{countdown}";
	}

	private string GetFogStateLabel(bool isChinese)
	{
		if (!IsModFeatureEnabled())
		{
			return isChinese ? "关闭" : "OFF";
		}
		if (_orbFogHandler == null)
		{
			Scene activeScene = SceneManager.GetActiveScene();
			if (IsAirportScene(activeScene))
			{
				return isChinese ? "大厅" : "LOBBY";
			}
			if (IsGameplayFogScene(activeScene))
			{
				return isChinese ? "初始化中" : "SYNCING";
			}
			return isChinese ? "未就绪" : "INACTIVE";
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

	private void ClampFogUiToCanvas(Canvas targetCanvas = null)
	{
		if (_fogUiRect == null)
		{
			return;
		}
		Canvas canvas = targetCanvas ?? ResolveHudCanvas() ?? _fogUiRect.GetComponentInParent<Canvas>();
		if (!IsCanvasUsable(canvas))
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

	private static void ApplyRightMiddleAnchoredRect(RectTransform target, float width, float height, float rightOffset, float downOffset)
	{
		if (target == null)
		{
			return;
		}
		target.localScale = Vector3.one;
		target.anchorMin = new Vector2(1f, 0.5f);
		target.anchorMax = new Vector2(1f, 0.5f);
		target.pivot = new Vector2(1f, 0.5f);
		target.sizeDelta = new Vector2(width, height);
		target.anchoredPosition = new Vector2(0f - rightOffset, 0f - downOffset);
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

	private static string GetLocalizedModDisplayName(bool isChineseLanguage)
	{
		return isChineseLanguage ? "毒雾攀登" : "Fog Climb";
	}

	private static string GetKeyName(ConfigKey configKey, bool isChineseLanguage)
	{
		return configKey switch
		{
			ConfigKey.ModEnabled => isChineseLanguage ? "模组开关" : "Enable Mod",
			ConfigKey.FogSpeed => isChineseLanguage ? "毒雾移动速度" : "Fog Speed",
			ConfigKey.FogDelay => isChineseLanguage ? "毒雾延迟时间s" : "Fog Delay (s)",
			ConfigKey.CompassHotkey => isChineseLanguage ? "指南针生成按键" : "Compass Hotkey",
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
			ConfigKey.ModEnabled => isChineseLanguage ? "启用独立毒雾模组。仅主机需要安装；主机会同步毒雾进度、冷伤害压制与指南针奖励。" : "Enable the standalone fog mod. Only the host needs it; the host synchronizes fog progress, cold suppression, and compass rewards.",
			ConfigKey.FogSpeed => isChineseLanguage ? "毒雾移动速度，范围 0.3~5，默认 2。" : "Fog movement speed. Range 0.3 to 5, default 2.",
			ConfigKey.FogDelay => isChineseLanguage ? "首段毒雾开始移动前的延迟，范围 0~150 秒，默认 90 秒。" : "Delay before the first fog segment starts moving. Range 0 to 150 seconds, default 90 seconds.",
			ConfigKey.CompassHotkey => isChineseLanguage ? "按键在自己头顶生成普通指南针；设为 None 可禁用。" : "Spawn a normal compass above your head with a hotkey. Set to None to disable.",
			ConfigKey.FogUiEnabled => isChineseLanguage ? "显示毒雾 HUD 文本。" : "Show the fog HUD text.",
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

	private void TryCleanupGeneratedBackupFile()
	{
		try
		{
			string pluginPath = Info?.Location;
			if (string.IsNullOrWhiteSpace(pluginPath))
			{
				return;
			}
			string backupPath = pluginPath + "-unrpcpatched.old";
			if (!File.Exists(backupPath))
			{
				return;
			}
			File.Delete(backupPath);
			Logger.LogInfo($"[{PluginName}] Removed generated backup file: {Path.GetFileName(backupPath)}");
		}
		catch (Exception ex)
		{
			Logger.LogDebug($"[{PluginName}] Failed to clean generated backup file: {ex.Message}");
		}
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
