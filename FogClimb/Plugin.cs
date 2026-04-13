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
		FogColdSuppression,
		FogSpeed,
		FogDelay,
		CalderaFogPosition,
		KilnFogPosition,
		CompassHotkey,
		FogUiEnabled,
		FogUiX,
		FogUiY,
		FogUiScale
	}

	public const string PluginGuid = "com.github.Thanks.FogClimb";

	public const string PluginName = "FogClimb";

	public const string PluginVersion = "1.0.1";

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

	private const float FogStateSyncIntervalSeconds = 0.18f;

	private const float FogStateSyncSizeThreshold = 0.35f;

	private const float RemoteStatusSyncIntervalSeconds = 0.25f;

	private const float LateGameRemoteStatusSyncIntervalSeconds = 0.1f;

	private const float CompassGrantSyncIntervalSeconds = 0.75f;

	private const float FogColdPerSecond = 0.0105f;

	private const float StatusChunkSize = 0.025f;

	private const float StalledFogResumeDelayRatio = 0.02f;

	private const float MinStalledFogResumeDelaySeconds = 2f;

	private const float MaxStalledFogResumeDelaySeconds = 8f;

	private const float HiddenFogDelayBufferSeconds = 5f;

	private const float OversizedFogSizeThreshold = 3500f;

	private const float CalderaFogMinStartSize = 1100f;

	private const float CalderaFogMaxStartSize = 2200f;

	private const float CalderaFogMinVerticalOffset = 520f;

	private const float CalderaFogMaxVerticalOffset = 760f;

	private const float CalderaFogVerticalOffsetRatio = 0.4f;

	private const float TheKilnFogMinStartSize = 850f;

	private const float TheKilnFogMaxStartSize = 1800f;

	private const float PeakFogMinStartSize = 650f;

	private const float PeakFogMaxStartSize = 1400f;

	private const KeyCode HiddenNightTestHotkey = KeyCode.F8;

	private const float HiddenNightTestHoldSeconds = 5f;

	private const float FallbackNightTimeNormalized = 0.85f;

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

	private const string NetworkInstallStateKey = "FogClimb.Enabled";

	private const int SimplifiedChineseLanguageIndex = 9;

	private static readonly BindingFlags InstanceBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

	private static readonly BindingFlags StaticBindingFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

	private static readonly FieldInfo ConfigEntryDescriptionField = typeof(ConfigEntryBase).GetField("<Description>k__BackingField", InstanceBindingFlags);

	private static readonly PropertyInfo ConfigFileEntriesProperty = typeof(ConfigFile).GetProperty("Entries", InstanceBindingFlags);

	private static readonly FieldInfo OrbFogHandlerOriginsField = typeof(OrbFogHandler).GetField("origins", InstanceBindingFlags);

	private static readonly FieldInfo OrbFogHandlerSphereField = typeof(OrbFogHandler).GetField("sphere", InstanceBindingFlags);

	private static readonly int StatusTypeCount = Enum.GetNames(typeof(CharacterAfflictions.STATUSTYPE)).Length;

	private static int _localFogStatusSuppressionDepth;

	private Harmony _harmony;

	private OrbFogHandler _orbFogHandler;

	private FogSphere _fogSphere;

	private Fog _legacyFog;

	private bool _fogStateInitialized;

	private bool _initialDelayCompleted;

	private int _trackedFogOriginId = -1;

	private float _fogDelayTimer;

	private float _fogHiddenBufferTimer;

	private float _fogHandlerSearchTimer;

	private float _lastFogStateSyncTime = -FogStateSyncIntervalSeconds;

	private float _lastRemoteStatusSyncTime = -RemoteStatusSyncIntervalSeconds;

	private float _lastCompassGrantSyncTime = -CompassGrantSyncIntervalSeconds;

	private bool _lastHadFogAuthority;

	private bool _lastModEnabledState;

	private bool _lastFogUiEnabledState;

	private bool _lastDetectedChineseLanguage;

	private bool _isRefreshingLanguage;

	private float _lastFogUiX;

	private float _lastFogUiY;

	private float _lastFogUiScale;

	private bool _hasAdvertisedInstallState;

	private bool _lastAdvertisedInstallState;

	private float _hiddenNightTestHoldTimer;

	private bool _hiddenNightTestTriggeredThisHold;

	private readonly HashSet<int> _grantedCampfireCompassIds = new HashSet<int>();

	private readonly HashSet<int> _restoredCheckpointCampfireIds = new HashSet<int>();

	private readonly Dictionary<int, int> _playerCompassGrantCounts = new Dictionary<int, int>();

	private readonly Dictionary<int, float> _remoteFogSuppressionDebt = new Dictionary<int, float>();

	private Item _compassItem;

	private RectTransform _fogUiRect;

	private TextMeshProUGUI _fogUiText;

	private RectTransform _compassLobbyNoticeRect;

	private TextMeshProUGUI _compassLobbyNoticeText;

	private string _lastCompassLobbyNoticeText = string.Empty;

	private bool _initialCompassGranted;

	private int _totalCompassGrantCount;

	private bool _hasSyncedFogStateSnapshot;

	private int _lastSyncedFogOriginId = -1;

	private float _lastSyncedFogSize = float.NaN;

	private bool _lastSyncedFogIsMoving;

	private bool _lastSyncedFogHasArrived;

	private int _delayedFogOriginId = -1;

	private int _pendingSyntheticFogSegmentId = -1;

	private int _activeSyntheticFogSegmentId = -1;

	private Vector3 _syntheticFogPoint;

	private float _syntheticFogStartSize;

	internal static Plugin Instance { get; private set; }

	internal static ConfigEntry<bool> ModEnabled { get; private set; }

	internal static ConfigEntry<bool> FogColdSuppression { get; private set; }

	internal static ConfigEntry<float> FogSpeed { get; private set; }

	internal static ConfigEntry<float> FogDelay { get; private set; }

	internal static ConfigEntry<bool> CalderaFogPosition { get; private set; }

	internal static ConfigEntry<bool> KilnFogPosition { get; private set; }

	internal static ConfigEntry<KeyCode> CompassHotkey { get; private set; }

	internal static ConfigEntry<bool> FogUiEnabled { get; private set; }

	internal static ConfigEntry<float> FogUiX { get; private set; }

	internal static ConfigEntry<float> FogUiY { get; private set; }

	internal static ConfigEntry<float> FogUiScale { get; private set; }

	private void Awake()
	{
		Instance = this;
		_lastHadFogAuthority = HasFogAuthority();
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
		FogColdSuppression = Config.Bind(sectionName, GetKeyName(ConfigKey.FogColdSuppression, isChineseLanguage), true, CreateConfigDescription(ConfigKey.FogColdSuppression, isChineseLanguage));
		FogSpeed = Config.Bind(sectionName, GetKeyName(ConfigKey.FogSpeed, isChineseLanguage), DefaultFogSpeed, CreateConfigDescription(ConfigKey.FogSpeed, isChineseLanguage));
		FogDelay = Config.Bind(sectionName, GetKeyName(ConfigKey.FogDelay, isChineseLanguage), DefaultFogDelaySeconds, CreateConfigDescription(ConfigKey.FogDelay, isChineseLanguage));
		CalderaFogPosition = Config.Bind(sectionName, GetKeyName(ConfigKey.CalderaFogPosition, isChineseLanguage), false, CreateConfigDescription(ConfigKey.CalderaFogPosition, isChineseLanguage));
		KilnFogPosition = Config.Bind(sectionName, GetKeyName(ConfigKey.KilnFogPosition, isChineseLanguage), false, CreateConfigDescription(ConfigKey.KilnFogPosition, isChineseLanguage));
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
		migratedAnyValue |= TryMigrateLocalizedConfigValue(FogColdSuppression, ConfigKey.FogColdSuppression, orphanedEntries);
		migratedAnyValue |= TryMigrateLocalizedConfigValue(FogSpeed, ConfigKey.FogSpeed, orphanedEntries);
		migratedAnyValue |= TryMigrateLocalizedConfigValue(FogDelay, ConfigKey.FogDelay, orphanedEntries);
		migratedAnyValue |= TryMigrateLocalizedConfigValue(CalderaFogPosition, ConfigKey.CalderaFogPosition, orphanedEntries);
		migratedAnyValue |= TryMigrateLocalizedConfigValue(KilnFogPosition, ConfigKey.KilnFogPosition, orphanedEntries);
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
		HandleAuthorityChangeIfNeeded(modEnabled);
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
		if (modEnabled)
		{
			EnsureFogCoverageAcrossAllLevels();
		}
		UpdateLocalInstallStateAdvertisement();
		HandleLanguageChangeIfNeeded();
		HandleFogUiConfigChanges();
		HandleManualCompassHotkey();
		HandleHiddenNightTestHotkey();
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
			TryRestoreCheckpointCampfireDelay();
			UpdateAuthorityFogMode();
			SyncFogStateToGuestsIfNeeded();
			SyncRemoteStatusSuppressionIfNeeded();
			SyncCompassGrantsToPlayersIfNeeded();
		}
		UpdateFogUi();
		UpdateCompassLobbyNotice();
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		ResetFogRuntimeState();
		_orbFogHandler = null;
		_fogSphere = null;
		_legacyFog = null;
		_compassItem = null;
		CleanupFogUi();
		CleanupCompassLobbyNotice();
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
		if (needLegacyFog)
		{
			_legacyFog = UnityEngine.Object.FindAnyObjectByType<Fog>();
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
			FogSphereOrigin origin = origins[i];
			if (origin == null || !origin.disableFog)
			{
				continue;
			}
			origin.disableFog = false;
		}
		FogSphere sphere = ResolveFogSphere(fogHandler);
		if (sphere == null)
		{
			return;
		}
		_fogSphere = sphere;
		if (!sphere.gameObject.activeSelf)
		{
			sphere.gameObject.SetActive(true);
		}
		if (fogHandler.currentSize > 0f && sphere.currentSize <= 0f)
		{
			sphere.currentSize = fogHandler.currentSize;
		}
	}

	private static FogSphereOrigin[] ResolveFogOrigins(OrbFogHandler fogHandler)
	{
		if (fogHandler == null)
		{
			return Array.Empty<FogSphereOrigin>();
		}
		FogSphereOrigin[] origins = OrbFogHandlerOriginsField?.GetValue(fogHandler) as FogSphereOrigin[];
		if (origins != null && origins.Length > 0)
		{
			return origins;
		}
		origins = fogHandler.GetComponentsInChildren<FogSphereOrigin>(includeInactive: true);
		if (origins != null && origins.Length > 0)
		{
			try
			{
				OrbFogHandlerOriginsField?.SetValue(fogHandler, origins);
			}
			catch
			{
			}
			return origins;
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
		_trackedFogOriginId = fogHandler != null ? fogHandler.currentID : -1;
		_initialDelayCompleted = ShouldSkipInitialDelay(fogHandler);
		_fogDelayTimer = _initialDelayCompleted ? (FogDelay?.Value ?? 0f) : 0f;
		_fogHiddenBufferTimer = _initialDelayCompleted ? HiddenFogDelayBufferSeconds : 0f;
	}

	private static bool HasAnyConfiguredFogDelay()
	{
		return HiddenFogDelayBufferSeconds > 0f || (FogDelay != null && FogDelay.Value > 0f);
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
			return fogHandler.isMoving || fogHandler.currentWaitTime > 1f || fogHandler.hasArrived;
		}
		if (ShouldHoldFogUntilCampfireActivation(fogHandler))
		{
			return false;
		}
		return fogHandler.isMoving || fogHandler.currentWaitTime > 1f || fogHandler.hasArrived;
	}

	private void UpdateAuthorityFogMode()
	{
		if (_orbFogHandler == null)
		{
			return;
		}
		ClampConfigValues();
		TryAlignFogOriginToCurrentSegment();
		TryUpdateSyntheticFogStage();
		TryNormalizeCalderaFogStage(forceResetCurrentSize: false);
		UpdateTrackedFogOrigin();
		if (ShouldAutoCompleteDelayForCurrentOrigin(_orbFogHandler))
		{
			_initialDelayCompleted = true;
		}
		if (!_initialDelayCompleted)
		{
			_orbFogHandler.speed = 0f;
			if (!ShouldEnforceConfiguredDelay(_orbFogHandler))
			{
				return;
			}
			if (_fogHiddenBufferTimer < HiddenFogDelayBufferSeconds)
			{
				_fogHiddenBufferTimer = Mathf.Min(_fogHiddenBufferTimer + Time.unscaledDeltaTime, HiddenFogDelayBufferSeconds);
				if (_fogHiddenBufferTimer < HiddenFogDelayBufferSeconds)
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
			return;
		}
		_orbFogHandler.speed = FogSpeed.Value;
		TryAutoStartCustomRunFogIfNeeded();
		TryAutoResumeStalledFogMovement();
		TryGrantInitialCompassIfNeeded();
	}

	private bool IsCampfireDelayPendingForOrigin(int originId)
	{
		return originId >= 0 && _delayedFogOriginId == originId;
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

	private bool ShouldEnforceConfiguredDelay(OrbFogHandler fogHandler)
	{
		if (_initialDelayCompleted || fogHandler == null || !HasAnyConfiguredFogDelay())
		{
			return false;
		}
		return fogHandler.currentID == 0 || IsCampfireDelayPendingForOrigin(fogHandler.currentID);
	}

	private bool ShouldHoldFogUntilCampfireActivation(OrbFogHandler fogHandler)
	{
		return fogHandler != null
			&& fogHandler.currentID > 0
			&& !IsCampfireDelayPendingForOrigin(fogHandler.currentID)
			&& !fogHandler.isMoving
			&& !fogHandler.hasArrived
			&& !_initialDelayCompleted;
	}

	private bool HasVisibleFogDelayCountdown()
	{
		return _fogHiddenBufferTimer >= HiddenFogDelayBufferSeconds;
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
		return _orbFogHandler == null ? 0 : ResolveFogOrigins(_orbFogHandler).Length;
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
		if (origins.Length <= 0)
		{
			return false;
		}
		int originId = Mathf.Clamp(_orbFogHandler.currentID, 0, origins.Length - 1);
		FogSphereOrigin origin = origins[originId] ?? origins.LastOrDefault(candidate => candidate != null);
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
		if (origins.Length <= 0)
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
		if (fogStageSegment >= Segment.Peak && mapHandler != null && mapHandler.respawnThePeak != null)
		{
			stageAnchor = mapHandler.respawnThePeak.position;
			anchorDescription = "respawnThePeak";
			return true;
		}
		if (fogStageSegment >= Segment.TheKiln && mapHandler != null && mapHandler.respawnTheKiln != null)
		{
			stageAnchor = mapHandler.respawnTheKiln.position;
			anchorDescription = "respawnTheKiln";
			return true;
		}
		if (mapHandler != null && mapHandler.segments != null && mapHandler.segments.Length > 0)
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
		Character fallbackCharacter = Character.AllCharacters.FirstOrDefault(character => character != null && character.data != null && !character.data.dead);
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
		if (TryResolveFogStageCoverageAnchor(fogStageSegment, out Vector3 stageAnchor, out _))
		{
			computedSize = Mathf.Max(computedSize, Vector3.Distance(fogPoint, stageAnchor) + anchorMargin);
		}
		foreach (Character character in Character.AllCharacters)
		{
			if (character == null || character.data == null || character.data.dead)
			{
				continue;
			}
			computedSize = Mathf.Max(computedSize, Vector3.Distance(fogPoint, character.Center) + playerMargin);
		}
		return Mathf.Clamp(computedSize, minSize, maxSize);
	}

	private float ComputeCalderaFogStartSize(Vector3 fogPoint)
	{
		float previousOriginSize = 900f;
		if (!TryGetPreviousRealFogOriginSize(out previousOriginSize))
		{
			previousOriginSize = 900f;
		}
		float baseSize = previousOriginSize * 1.05f;
		return ComputeFogStartSizeForStage(Segment.Caldera, fogPoint, baseSize, CalderaFogMinStartSize, CalderaFogMaxStartSize, 120f, 95f);
	}

	private bool TryResolveCalderaFogPoint(out Vector3 fogPoint, out string pointDescription)
	{
		fogPoint = Vector3.zero;
		pointDescription = string.Empty;
		if (!TryResolveFogPointForCurrentOrigin(out Vector3 originalFogPoint, out string originalDescription))
		{
			return false;
		}
		if (!TryResolveFogStageCoverageAnchor(Segment.Caldera, out Vector3 stageAnchor, out string anchorDescription))
		{
			fogPoint = originalFogPoint;
			pointDescription = originalDescription;
			return true;
		}
		float sourceVerticalOffset = originalFogPoint.y - stageAnchor.y;
		float verticalDirection = sourceVerticalOffset >= 0f ? 1f : -1f;
		float targetVerticalOffset = Mathf.Clamp(Mathf.Abs(sourceVerticalOffset) * CalderaFogVerticalOffsetRatio, CalderaFogMinVerticalOffset, CalderaFogMaxVerticalOffset);
		fogPoint = new Vector3(stageAnchor.x, stageAnchor.y + verticalDirection * targetVerticalOffset, stageAnchor.z);
		pointDescription = $"{anchorDescription} vertical";
		return true;
	}

	private void TryNormalizeCalderaFogStage(bool forceResetCurrentSize)
	{
		if (_orbFogHandler == null || !HasFogAuthority())
		{
			return;
		}
		if (!IsCalderaFogPositionEnabled())
		{
			return;
		}
		if ((int)MapHandler.CurrentSegmentNumber != (int)Segment.Caldera || _activeSyntheticFogSegmentId >= GetAvailableFogOriginCount())
		{
			return;
		}
		int availableOriginCount = GetAvailableFogOriginCount();
		if (availableOriginCount <= 0)
		{
			return;
		}
		int reusableOriginId = availableOriginCount - 1;
		if (_orbFogHandler.currentID != reusableOriginId)
		{
			return;
		}
		if (!forceResetCurrentSize && _orbFogHandler.currentSize > 30f && _orbFogHandler.currentSize < OversizedFogSizeThreshold)
		{
			return;
		}
		if (!TryResolveCalderaFogPoint(out Vector3 fogPoint, out string pointDescription))
		{
			return;
		}
		float desiredSize = ComputeCalderaFogStartSize(fogPoint);
		FogSphere sphere = _fogSphere ?? ResolveFogSphere(_orbFogHandler);
		if (sphere != null)
		{
			_fogSphere = sphere;
			if (!sphere.gameObject.activeSelf)
			{
				sphere.gameObject.SetActive(true);
			}
			sphere.fogPoint = fogPoint;
			sphere.currentSize = desiredSize;
		}
		_orbFogHandler.currentStartHeight = float.NegativeInfinity;
		_orbFogHandler.currentStartForward = float.NegativeInfinity;
		_orbFogHandler.currentSize = desiredSize;
		_orbFogHandler.hasArrived = false;
		if (forceResetCurrentSize)
		{
			_orbFogHandler.currentWaitTime = 0f;
		}
		Logger.LogInfo($"[{PluginName}] Normalized Caldera fog size to {desiredSize:F1} using {pointDescription}. forceReset={forceResetCurrentSize}, moving={_orbFogHandler.isMoving}.");
	}

	private static bool IsCalderaFogPositionEnabled()
	{
		return CalderaFogPosition != null && CalderaFogPosition.Value;
	}

	private static bool IsKilnFogPositionEnabled()
	{
		return KilnFogPosition != null && KilnFogPosition.Value;
	}

	private static bool ShouldUseCustomFogPositionForSegment(Segment segment)
	{
		if (segment >= Segment.TheKiln)
		{
			return IsKilnFogPositionEnabled();
		}
		if (segment == Segment.Caldera)
		{
			return IsCalderaFogPositionEnabled();
		}
		return false;
	}

	private bool IsLateGameFogColdSuppressionActive()
	{
		if (!IsGameplayFogScene(SceneManager.GetActiveScene()) || LoadingScreenHandler.loading)
		{
			return _activeSyntheticFogSegmentId >= (int)Segment.TheKiln;
		}
		MapHandler mapHandler = Singleton<MapHandler>.Instance;
		if (mapHandler != null && (int)MapHandler.CurrentSegmentNumber >= (int)Segment.TheKiln)
		{
			return true;
		}
		return _activeSyntheticFogSegmentId >= (int)Segment.TheKiln;
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
		if (_orbFogHandler == null || !HasFogAuthority() || _orbFogHandler.isMoving || !TryGetTargetFogOriginId(out int expectedOriginId))
		{
			return;
		}
		if (_orbFogHandler.currentID == expectedOriginId)
		{
			return;
		}
		int previousOriginId = _orbFogHandler.currentID;
		_orbFogHandler.SetFogOrigin(expectedOriginId);
		EnsureFogCoverage(_orbFogHandler);
		SyncFogOriginToGuests();
		Logger.LogInfo($"[{PluginName}] Aligned fog origin from {previousOriginId} to {expectedOriginId}.");
	}

	private void TryUpdateSyntheticFogStage()
	{
		if (_orbFogHandler == null || !HasFogAuthority())
		{
			return;
		}
		MapHandler mapHandler = Singleton<MapHandler>.Instance;
		int availableOriginCount = GetAvailableFogOriginCount();
		if (mapHandler == null || availableOriginCount <= 0)
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
			return;
		}
		if (!TryBuildSyntheticFogStage(targetSyntheticSegment, out Vector3 fogPoint, out float fogSize, out string anchorDescription))
		{
			return;
		}
		bool stageChanged = _activeSyntheticFogSegmentId != targetSyntheticSegmentId;
		bool allowRuntimeRefresh = stageChanged || !_orbFogHandler.isMoving || _orbFogHandler.hasArrived || _syntheticFogStartSize <= 0f;
		bool pointChanged = stageChanged || (allowRuntimeRefresh && Vector3.Distance(_syntheticFogPoint, fogPoint) > 0.1f);
		bool sizeChanged = stageChanged || (allowRuntimeRefresh && Mathf.Abs(_syntheticFogStartSize - fogSize) > 0.1f);
		_activeSyntheticFogSegmentId = targetSyntheticSegmentId;
		if (pointChanged)
		{
			_syntheticFogPoint = fogPoint;
		}
		if (sizeChanged)
		{
			_syntheticFogStartSize = fogSize;
		}
		bool dataChanged = pointChanged || sizeChanged;
		ApplySyntheticFogStageToHandler(resetCurrentSize: sizeChanged);
		if (!dataChanged)
		{
			return;
		}
		Logger.LogInfo($"[{PluginName}] Activated synthetic fog stage {targetSyntheticSegmentId} using {anchorDescription}. Point={fogPoint}, size={fogSize:F1}, delayCompleted={_initialDelayCompleted}, moving={_orbFogHandler.isMoving}.");
		if (_initialDelayCompleted && !_orbFogHandler.isMoving)
		{
			StartFogMovement();
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
				sphere.gameObject.SetActive(true);
			}
			sphere.fogPoint = _syntheticFogPoint;
			if (resetCurrentSize)
			{
				sphere.currentSize = _syntheticFogStartSize;
			}
		}
		_orbFogHandler.currentStartHeight = float.NegativeInfinity;
		_orbFogHandler.currentStartForward = float.NegativeInfinity;
		if (!resetCurrentSize)
		{
			return;
		}
		_orbFogHandler.currentSize = _syntheticFogStartSize;
		_orbFogHandler.currentWaitTime = 0f;
		_orbFogHandler.hasArrived = false;
	}

	private bool TryBuildSyntheticFogStage(Segment syntheticSegment, out Vector3 fogPoint, out float fogSize, out string anchorDescription)
	{
		fogPoint = Vector3.zero;
		fogSize = 0f;
		anchorDescription = string.Empty;
		if (!TryResolveSyntheticFogAnchor(syntheticSegment, out fogPoint, out anchorDescription))
		{
			return false;
		}
		fogSize = ComputeSyntheticFogStartSize(syntheticSegment, fogPoint);
		return fogSize > 0f;
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
		if (syntheticSegment >= Segment.Peak && mapHandler.respawnThePeak != null)
		{
			targetAnchor = mapHandler.respawnThePeak.position;
			targetDescription = "respawnThePeak";
			return true;
		}
		if (syntheticSegment >= Segment.TheKiln)
		{
			if (mapHandler.segments != null && mapHandler.segments.Length > (int)Segment.TheKiln)
			{
				MapHandler.MapSegment kilnSegment = mapHandler.segments[(int)Segment.TheKiln];
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
			if (mapHandler.respawnTheKiln != null)
			{
				targetAnchor = mapHandler.respawnTheKiln.position;
				targetDescription = "respawnTheKiln";
				return true;
			}
		}
		return false;
	}

	private bool TryResolveSyntheticFogAnchorFromOrigins(Segment syntheticSegment, out Vector3 fogPoint, out string anchorDescription)
	{
		fogPoint = Vector3.zero;
		anchorDescription = string.Empty;
		FogSphereOrigin[] origins = ResolveFogOrigins(_orbFogHandler);
		FogSphereOrigin lastOrigin = origins.LastOrDefault(origin => origin != null);
		if (lastOrigin == null)
		{
			return false;
		}
		FogSphereOrigin previousOrigin = origins.Where(origin => origin != null).Reverse().Skip(1).FirstOrDefault();
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
		if (TryResolveSyntheticTargetAnchor(syntheticSegment, out Vector3 targetAnchor, out string targetDescription))
		{
			float projectedDistance = Vector3.Dot(targetAnchor - lastOrigin.transform.position, direction);
			float desiredDistance = Mathf.Max(projectedDistance + 180f, pathStep.magnitude * extraStageIndex);
			extrapolatedPoint = lastOrigin.transform.position + direction * desiredDistance;
			Vector3 lateralOffset = Vector3.ProjectOnPlane(targetAnchor - extrapolatedPoint, direction);
			extrapolatedPoint += lateralOffset * 0.25f;
			anchorDescription = $"origin-extrapolated + {targetDescription}";
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
		MapHandler mapHandler = Singleton<MapHandler>.Instance;
		if (mapHandler == null)
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
		Character fallbackCharacter = Character.AllCharacters.FirstOrDefault(character => character != null);
		if (fallbackCharacter != null)
		{
			fogPoint = fallbackCharacter.Center;
			anchorDescription = "firstCharacter";
			return true;
		}
		return false;
	}

	private float ComputeSyntheticFogStartSize(Segment syntheticSegment, Vector3 fogPoint)
	{
		float previousOriginSize = 900f;
		TryGetPreviousRealFogOriginSize(out previousOriginSize);
		if (syntheticSegment >= Segment.Peak)
		{
			float baseSize = previousOriginSize * 0.72f;
			return ComputeFogStartSizeForStage(syntheticSegment, fogPoint, baseSize, PeakFogMinStartSize, PeakFogMaxStartSize, 130f, 95f);
		}
		float kilnBaseSize = previousOriginSize * 0.95f;
		return ComputeFogStartSizeForStage(syntheticSegment, fogPoint, kilnBaseSize, TheKilnFogMinStartSize, TheKilnFogMaxStartSize, 140f, 100f);
	}

	private void TryAutoStartCustomRunFogIfNeeded()
	{
		if (_orbFogHandler == null || !HasFogAuthority() || !RunSettings.IsCustomRun)
		{
			return;
		}
		if (ShouldHoldFogUntilCampfireActivation(_orbFogHandler))
		{
			return;
		}
		if (_orbFogHandler.isMoving || _orbFogHandler.hasArrived)
		{
			return;
		}
		if (_orbFogHandler.currentWaitTime > 0.2f)
		{
			return;
		}
		StartFogMovement();
	}

	private void TryAutoResumeStalledFogMovement()
	{
		if (_orbFogHandler == null || !HasFogAuthority() || !_initialDelayCompleted)
		{
			return;
		}
		if (ShouldHoldFogUntilCampfireActivation(_orbFogHandler))
		{
			return;
		}
		if (_orbFogHandler.currentID <= 0 || _orbFogHandler.isMoving || _orbFogHandler.hasArrived)
		{
			return;
		}
		if (!TryGetTargetFogOriginId(out int expectedOriginId) || expectedOriginId != _orbFogHandler.currentID)
		{
			return;
		}
		float stalledThreshold = GetStalledFogResumeDelaySeconds(_orbFogHandler);
		if (_orbFogHandler.currentWaitTime < stalledThreshold)
		{
			return;
		}
		Logger.LogInfo($"[{PluginName}] Resuming stalled fog at origin {_orbFogHandler.currentID} after waiting {_orbFogHandler.currentWaitTime:F1}s.");
		StartFogMovement();
	}

	private static float GetStalledFogResumeDelaySeconds(OrbFogHandler fogHandler)
	{
		if (fogHandler == null)
		{
			return MaxStalledFogResumeDelaySeconds;
		}
		return Mathf.Clamp(fogHandler.maxWaitTime * StalledFogResumeDelayRatio, MinStalledFogResumeDelaySeconds, MaxStalledFogResumeDelaySeconds);
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
				_initialDelayCompleted = HiddenFogDelayBufferSeconds <= 0f && (FogDelay == null || FogDelay.Value <= 0f);
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
				bool isOriginActive = _orbFogHandler.isMoving || _orbFogHandler.hasArrived || _orbFogHandler.currentWaitTime > 0.2f;
				_initialDelayCompleted = isOriginActive;
				_fogHiddenBufferTimer = isOriginActive ? HiddenFogDelayBufferSeconds : 0f;
				_fogDelayTimer = isOriginActive ? (FogDelay?.Value ?? 0f) : 0f;
			}
			return;
		}
		if (returnedToFirstOrigin)
		{
			_fogHiddenBufferTimer = 0f;
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
		Logger.LogInfo($"[{PluginName}] Starting fog movement. currentOrigin={_orbFogHandler.currentID}, syntheticStage={_activeSyntheticFogSegmentId}, pendingOrigin={_delayedFogOriginId}, currentSize={_orbFogHandler.currentSize:F1}.");
		ClearPendingCampfireDelayForOrigin(_orbFogHandler.currentID);
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
		MapHandler mapHandler = Singleton<MapHandler>.Instance;
		if (mapHandler == null)
		{
			return false;
		}
		int availableOriginCount = GetAvailableFogOriginCount();
		return TryMapSegmentToFogOriginId(MapHandler.CurrentSegmentNumber, availableOriginCount, out expectedOriginId);
	}

	private void HandleAuthorityChangeIfNeeded(bool modEnabled)
	{
		bool hasAuthority = HasFogAuthority();
		if (hasAuthority == _lastHadFogAuthority)
		{
			return;
		}
		_lastHadFogAuthority = hasAuthority;
		ResetFogStateSyncSnapshot();
		_remoteFogSuppressionDebt.Clear();
		if (!hasAuthority && PhotonNetwork.InRoom)
		{
			RestoreVanillaFogSpeed();
			ResetFogRuntimeState();
			SetFogUiVisible(false);
			CleanupCompassLobbyNotice();
			return;
		}
		_lastFogStateSyncTime = -FogStateSyncIntervalSeconds;
		_lastRemoteStatusSyncTime = -RemoteStatusSyncIntervalSeconds;
		_lastCompassGrantSyncTime = -CompassGrantSyncIntervalSeconds;
		if (modEnabled)
		{
			_fogStateInitialized = false;
		}
	}

	private static bool HasRemotePlayers()
	{
		return PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.PlayerCount > 1;
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
		return !_hasSyncedFogStateSnapshot
			|| _orbFogHandler == null
			|| _orbFogHandler.currentID != _lastSyncedFogOriginId
			|| _orbFogHandler.hasArrived != _lastSyncedFogHasArrived;
	}

	private bool NeedsFogStateDeltaSync()
	{
		return !_hasSyncedFogStateSnapshot
			|| _orbFogHandler == null
			|| _orbFogHandler.isMoving != _lastSyncedFogIsMoving
			|| float.IsNaN(_lastSyncedFogSize)
			|| Mathf.Abs(_orbFogHandler.currentSize - _lastSyncedFogSize) >= FogStateSyncSizeThreshold;
	}

	private void SyncFogOriginToGuests()
	{
		if (_orbFogHandler == null || !HasRemotePlayers() || !PhotonNetwork.IsMasterClient)
		{
			ResetFogStateSyncSnapshot();
			return;
		}
		PhotonView photonView = _orbFogHandler.GetComponent<PhotonView>();
		if (photonView == null)
		{
			return;
		}
		try
		{
			photonView.RPC("RPC_InitFog", RpcTarget.Others, new object[]
			{
				_orbFogHandler.currentID,
				_orbFogHandler.currentSize,
				_orbFogHandler.hasArrived,
				_orbFogHandler.isMoving
			});
			_lastFogStateSyncTime = Time.unscaledTime;
			CacheFogStateSyncSnapshot();
		}
		catch (Exception ex)
		{
			Logger.LogDebug($"[{PluginName}] Fog origin sync skipped: {ex.Message}");
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
		if (_orbFogHandler == null || !HasRemotePlayers() || !PhotonNetwork.IsMasterClient)
		{
			ResetFogStateSyncSnapshot();
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
		bool needsInitSync = NeedsFogStateInitSync();
		bool needsDeltaSync = NeedsFogStateDeltaSync();
		if (!needsInitSync && !needsDeltaSync)
		{
			return;
		}
		try
		{
			if (needsInitSync)
			{
				photonView.RPC("RPC_InitFog", RpcTarget.Others, new object[]
				{
					_orbFogHandler.currentID,
					_orbFogHandler.currentSize,
					_orbFogHandler.hasArrived,
					_orbFogHandler.isMoving
				});
			}
			else
			{
				photonView.RPC("RPCA_SyncFog", RpcTarget.Others, new object[]
				{
					_orbFogHandler.currentSize,
					_orbFogHandler.isMoving
				});
			}
			CacheFogStateSyncSnapshot();
		}
		catch (Exception ex)
		{
			Logger.LogDebug($"[{PluginName}] Fog sync skipped: {ex.Message}");
		}
	}

	private void SyncRemoteStatusSuppressionIfNeeded()
	{
		if (!ShouldSuppressFogColdDamage() || !HasRemotePlayers() || !PhotonNetwork.IsMasterClient)
		{
			_remoteFogSuppressionDebt.Clear();
			return;
		}
		float syncInterval = GetRemoteStatusSyncIntervalSeconds();
		float now = Time.unscaledTime;
		float elapsed = _lastRemoteStatusSyncTime < 0f ? syncInterval : now - _lastRemoteStatusSyncTime;
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
		int[] staleKeys = _remoteFogSuppressionDebt.Keys.Where(key => !activeKeys.Contains(key)).ToArray();
		foreach (int staleKey in staleKeys)
		{
			_remoteFogSuppressionDebt.Remove(staleKey);
		}
	}

	private static bool ShouldSendRemoteStatusSuppression(Character character)
	{
		return ShouldSuppressFogColdDamage()
			&& HasFogAuthority()
			&& character != null
			&& character.photonView != null
			&& character.photonView.Owner != null
			&& !character.photonView.IsMine
			&& !character.isBot
			&& !character.isZombie
			&& character.data != null
			&& !character.data.dead;
	}

	private static float GetRemoteStatusSyncIntervalSeconds()
	{
		return ShouldPreserveVanillaLateGameNoCold() ? LateGameRemoteStatusSyncIntervalSeconds : RemoteStatusSyncIntervalSeconds;
	}

	private float[] BuildRemoteStatusSuppressionPayload(Character character, float elapsed)
	{
		if (!ShouldSuppressFogColdDamage())
		{
			if (character != null)
			{
				_remoteFogSuppressionDebt.Remove(GetRemoteStatusSuppressionKey(character));
			}
			return null;
		}
		if (character?.data == null)
		{
			return null;
		}
		int playerKey = GetRemoteStatusSuppressionKey(character);
		float pendingDebt = _remoteFogSuppressionDebt.TryGetValue(playerKey, out float storedDebt) ? storedDebt : 0f;
		if (TryGetCurrentFogSuppressionRate(character, out float ratePerSecond))
		{
			pendingDebt += Mathf.Max(ratePerSecond, 0f) * Mathf.Max(elapsed, 0f);
		}
		if (pendingDebt <= 0f)
		{
			_remoteFogSuppressionDebt.Remove(playerKey);
			return null;
		}
		CharacterAfflictions.STATUSTYPE statusType = GetFogSuppressionStatusType(character);
		float currentStatus = character.refs?.afflictions != null ? character.refs.afflictions.GetCurrentStatus(statusType) : 0f;
		float transferAmount = GetSuppressionTransferAmount(pendingDebt, currentStatus);
		_remoteFogSuppressionDebt[playerKey] = Mathf.Max(pendingDebt - transferAmount, 0f);
		if (_remoteFogSuppressionDebt[playerKey] <= 0.0001f)
		{
			_remoteFogSuppressionDebt.Remove(playerKey);
		}
		if (transferAmount <= 0f)
		{
			return null;
		}
		float[] payload = new float[StatusTypeCount];
		payload[(int)statusType] = 0f - transferAmount;
		return payload;
	}

	private bool TryGetCurrentFogSuppressionRate(Character character, out float ratePerSecond)
	{
		ratePerSecond = 0f;
		if (!ShouldSuppressFogColdDamage() || character == null)
		{
			return false;
		}
		if (IsCharacterInsideFogSphere(character))
		{
			ratePerSecond += FogColdPerSecond;
		}
		if (IsCharacterInsideLegacyFog(character, out float legacyRate))
		{
			ratePerSecond += legacyRate;
		}
		return ratePerSecond > 0f;
	}

	private bool IsCharacterInsideAnyFog(Character character)
	{
		return IsCharacterInsideFogSphere(character) || IsCharacterInsideLegacyFog(character, out _);
	}

	private bool IsCharacterInsideFogSphere(Character character)
	{
		return _fogSphere != null
			&& character != null
			&& Mathf.Approximately(_fogSphere.ENABLE, 1f)
			&& Vector3.Distance(_fogSphere.fogPoint, character.Center) > _fogSphere.currentSize;
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

	private bool DropCompassInFrontOfPlayer(Player player, string reason)
	{
		if (player == null || _compassItem == null)
		{
			return false;
		}
		Vector3 spawnPosition = GetCompassDropPosition(player);
		Quaternion spawnRotation = GetCompassDropRotation(player);
		try
		{
			if (PhotonNetwork.InRoom)
			{
				PhotonNetwork.InstantiateItemRoom(_compassItem.name, spawnPosition, spawnRotation);
			}
			else
			{
				UnityEngine.Object.Instantiate(_compassItem.gameObject, spawnPosition, spawnRotation);
			}
			Logger.LogDebug($"[{PluginName}] Spawned compass in front of {player.name} ({reason}).");
			return true;
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"[{PluginName}] Failed to spawn compass in front of {player.name} ({reason}): {ex.Message}");
			return false;
		}
	}

	private bool DropCompassNearPlayer(Player player, string reason)
	{
		return DropCompassInFrontOfPlayer(player, reason);
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

	private static Quaternion GetCompassDropRotation(Player player)
	{
		Character character = player.character;
		Vector3 forward = character != null ? Vector3.ProjectOnPlane(character.transform.forward, Vector3.up) : player.transform.forward;
		if (forward.sqrMagnitude < 0.01f)
		{
			forward = Vector3.forward;
		}
		return Quaternion.LookRotation(forward.normalized, Vector3.up);
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

	private void HandleHiddenNightTestHotkey()
	{
		if (!IsModFeatureEnabled() || !HasFogAuthority() || HiddenNightTestHotkey == KeyCode.None)
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
			return;
		}
		if (!Input.GetKey(HiddenNightTestHotkey))
		{
			ResetHiddenNightTestHotkeyState();
			return;
		}
		if (_hiddenNightTestTriggeredThisHold)
		{
			return;
		}
		_hiddenNightTestHoldTimer += Time.unscaledDeltaTime;
		if (_hiddenNightTestHoldTimer < HiddenNightTestHoldSeconds)
		{
			return;
		}
		_hiddenNightTestTriggeredThisHold = true;
		SwitchToNightForTesting();
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
			Logger.LogDebug($"[{PluginName}] Hidden night-test hotkey skipped because DayNightManager is unavailable.");
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
				photonView.RPC("RPCA_SyncTime", RpcTarget.Others, new object[] { nightTime });
			}
			Logger.LogInfo($"[{PluginName}] Hidden night-test hotkey forced night at {nightTime:F3}.");
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"[{PluginName}] Hidden night-test hotkey failed: {ex.Message}");
		}
	}

	private static float CalculateNightTestTime(DayNightManager dayNightManager)
	{
		if (dayNightManager == null)
		{
			return FallbackNightTimeNormalized;
		}
		float dayStart = Mathf.Repeat(dayNightManager.dayStart, 1f);
		float dayEnd = Mathf.Repeat(dayNightManager.dayEnd, 1f);
		float nightDuration = Mathf.Repeat(dayStart - dayEnd, 1f);
		if (nightDuration < 0.01f)
		{
			return FallbackNightTimeNormalized;
		}
		return Mathf.Repeat(dayEnd + nightDuration * 0.5f, 1f);
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
		if (!DropCompassInFrontOfPlayer(localPlayer, "manual-hotkey"))
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
		if (!IsModFeatureEnabled() || !(FogUiEnabled?.Value ?? true) || !HasFogAuthority())
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
		if (HasFogAuthority() && ShouldEnforceConfiguredDelay(_orbFogHandler))
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
		if (!HasFogAuthority() || _orbFogHandler == null || !ShouldEnforceConfiguredDelay(_orbFogHandler) || !HasVisibleFogDelayCountdown())
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
		_delayedFogOriginId = -1;
		_trackedFogOriginId = -1;
		ClearSyntheticFogStage();
		_fogDelayTimer = 0f;
		_fogHiddenBufferTimer = 0f;
		_lastFogStateSyncTime = -FogStateSyncIntervalSeconds;
		_lastRemoteStatusSyncTime = -RemoteStatusSyncIntervalSeconds;
		_lastCompassGrantSyncTime = -CompassGrantSyncIntervalSeconds;
		_fogHandlerSearchTimer = 0f;
		_grantedCampfireCompassIds.Clear();
		_restoredCheckpointCampfireIds.Clear();
		_playerCompassGrantCounts.Clear();
		_remoteFogSuppressionDebt.Clear();
		_localFogStatusSuppressionDepth = 0;
		ResetHiddenNightTestHotkeyState();
		ResetFogStateSyncSnapshot();
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
			ConfigKey.FogColdSuppression => isChineseLanguage ? "毒雾寒冷值开关" : "Suppress Fog Cold",
			ConfigKey.FogSpeed => isChineseLanguage ? "毒雾移动速度" : "Fog Speed",
			ConfigKey.FogDelay => isChineseLanguage ? "毒雾延迟时间s" : "Fog Delay (s)",
			ConfigKey.CalderaFogPosition => isChineseLanguage ? "火山毒雾位置开关" : "Caldera Fog Position",
			ConfigKey.KilnFogPosition => isChineseLanguage ? "熔炉毒雾位置开关" : "Kiln Fog Position",
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
			ConfigKey.ModEnabled => isChineseLanguage ? "启用独立毒雾模组。仅主机需要安装；主机会同步毒雾进度、寒冷修正和指南针奖励。毒雾寒冷是否抵消由下方开关单独控制，但熔炉和山顶会保留原版“不再因毒雾受寒”的规则，不影响正常夜晚寒冷或游戏自定义设置。" : "Enable the standalone fog mod. Only the host needs it; the host synchronizes fog progress, cold fixes, and compass rewards. Fog-only cold suppression is controlled separately below, but kiln and peak still preserve vanilla no-fog-cold behavior without touching normal night cold or unrelated custom settings.",
			ConfigKey.FogColdSuppression => isChineseLanguage ? "控制是否抵消毒雾带来的寒冷值。关闭后前半程会恢复原版毒雾寒冷；熔炉和山顶仍保留原版“不再因毒雾受寒”的逻辑，不影响夜晚寒冷、毒雾推进和游戏中的其他自定义设置。" : "Toggle fog-only cold suppression. When off, earlier stages can use vanilla fog cold again; kiln and peak still preserve vanilla no-fog-cold behavior. This does not affect night cold, fog progression, or other in-game custom settings.",
			ConfigKey.FogSpeed => isChineseLanguage ? "毒雾移动速度，范围 0.3~5，默认 2。" : "Fog movement speed. Range 0.3 to 5, default 2.",
			ConfigKey.FogDelay => isChineseLanguage ? "首段毒雾开始移动前的延迟，范围 0~150 秒，默认 90 秒。" : "Delay before the first fog segment starts moving. Range 0 to 150 seconds, default 90 seconds.",
			ConfigKey.CalderaFogPosition => isChineseLanguage ? "启用后使用 FogClimb 的火山毒雾位置修正，让火山阶段的毒雾更容易看到。默认关闭。" : "Use FogClimb's Caldera fog position override so the fog is easier to see in the volcano stage. Disabled by default.",
			ConfigKey.KilnFogPosition => isChineseLanguage ? "启用后使用 FogClimb 的熔炉/山顶毒雾位置修正。关闭时不接管后两关的自定义毒雾位置。默认关闭。" : "Use FogClimb's kiln and peak fog position override. When disabled, the mod does not take over the custom fog positions for the last two stages. Disabled by default.",
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

	internal static bool IsFogColdSuppressionEnabled()
	{
		return FogColdSuppression == null || FogColdSuppression.Value;
	}

	internal static bool ShouldPreserveVanillaLateGameNoCold()
	{
		return HasFogAuthority() && IsModFeatureEnabled() && Instance != null && Instance.IsLateGameFogColdSuppressionActive();
	}

	internal static bool ShouldSuppressFogColdDamage()
	{
		return HasFogAuthority() && IsModFeatureEnabled() && (IsFogColdSuppressionEnabled() || ShouldPreserveVanillaLateGameNoCold());
	}

	internal static bool ShouldForceFogCoverageEverywhere()
	{
		return HasFogAuthority() && IsModFeatureEnabled();
	}

	internal static void NotifyFogHandlerChanged(OrbFogHandler fogHandler)
	{
		Instance?.EnsureFogCoverage(fogHandler);
	}

	internal static void BeginLocalFogStatusSuppression()
	{
		if (ShouldSuppressFogColdDamage())
		{
			_localFogStatusSuppressionDepth++;
		}
	}

	internal static void EndLocalFogStatusSuppression()
	{
		if (_localFogStatusSuppressionDepth > 0)
		{
			_localFogStatusSuppressionDepth--;
		}
	}

	internal static bool ShouldSuppressLocalFogSourceStatus(CharacterAfflictions afflictions, CharacterAfflictions.STATUSTYPE statusType)
	{
		if (!ShouldSuppressFogColdDamage() || afflictions == null)
		{
			return false;
		}
		Character character = afflictions.character;
		if (character == null || character != Character.localCharacter || character.isBot || character.isZombie || character.data == null)
		{
			return false;
		}
		bool isFogColdStatus = character.data.isSkeleton ? statusType == CharacterAfflictions.STATUSTYPE.Injury : statusType == CharacterAfflictions.STATUSTYPE.Cold;
		if (!isFogColdStatus)
		{
			return false;
		}
		if (_localFogStatusSuppressionDepth > 0)
		{
			return true;
		}
		return ShouldPreserveVanillaLateGameNoCold() && Instance != null && Instance.IsCharacterInsideAnyFog(character);
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
		return Instance.ShouldEnforceConfiguredDelay(fogHandler) || Instance.ShouldHoldFogUntilCampfireActivation(fogHandler);
	}

	internal static void NotifyCampfireLit(Campfire campfire)
	{
		Instance?.HandleCampfireLit(campfire);
	}

	private void UpdateLocalInstallStateAdvertisement()
	{
		if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
		{
			_hasAdvertisedInstallState = false;
			return;
		}
		bool isEnabled = ShouldSuppressFogColdDamage();
		if (_hasAdvertisedInstallState && _lastAdvertisedInstallState == isEnabled && LocalInstallStateMatches(isEnabled))
		{
			return;
		}
		try
		{
			PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
			{
				{
					NetworkInstallStateKey,
					isEnabled
				}
			});
			_lastAdvertisedInstallState = isEnabled;
			_hasAdvertisedInstallState = true;
		}
		catch (Exception ex)
		{
			_hasAdvertisedInstallState = false;
			Logger.LogDebug($"[{PluginName}] Failed to publish install-state metadata: {ex.Message}");
		}
	}

	private static bool LocalInstallStateMatches(bool expected)
	{
		Photon.Realtime.Player localPlayer = PhotonNetwork.LocalPlayer;
		if (localPlayer?.CustomProperties == null || !localPlayer.CustomProperties.ContainsKey(NetworkInstallStateKey))
		{
			return false;
		}
		return TryReadInstallState(localPlayer, out bool currentState) && currentState == expected;
	}

	private static bool HasRemoteFogSuppressionSupport(Photon.Realtime.Player player)
	{
		return TryReadInstallState(player, out bool isEnabled) && isEnabled;
	}

	private static bool TryReadInstallState(Photon.Realtime.Player player, out bool isEnabled)
	{
		isEnabled = false;
		if (player?.CustomProperties == null || !player.CustomProperties.ContainsKey(NetworkInstallStateKey))
		{
			return false;
		}
		object rawValue = player.CustomProperties[NetworkInstallStateKey];
		switch (rawValue)
		{
		case bool boolValue:
			isEnabled = boolValue;
			return true;
		case string stringValue:
			return bool.TryParse(stringValue, out isEnabled);
		default:
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
		}
	}

	private void TryRestoreCheckpointCampfireDelay()
	{
		if (!IsModFeatureEnabled() || !HasFogAuthority() || _orbFogHandler == null || Character.localCharacter == null)
		{
			return;
		}
		if (!IsGameplayFogScene(SceneManager.GetActiveScene()) || LoadingScreenHandler.loading)
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
		if (_grantedCampfireCompassIds.Contains(campfireId) || _restoredCheckpointCampfireIds.Contains(campfireId))
		{
			return;
		}
		if (_orbFogHandler.isMoving || _orbFogHandler.currentWaitTime > 0.2f)
		{
			_restoredCheckpointCampfireIds.Add(campfireId);
			return;
		}
		ScheduleFogDelayAfterCampfire(currentCampfire);
		_restoredCheckpointCampfireIds.Add(campfireId);
		Logger.LogInfo($"[{PluginName}] Restored fog delay from lit checkpoint campfire: {currentCampfire.name}.");
	}

	private static CharacterAfflictions.STATUSTYPE GetFogSuppressionStatusType(Character character)
	{
		return character?.data != null && character.data.isSkeleton ? CharacterAfflictions.STATUSTYPE.Injury : CharacterAfflictions.STATUSTYPE.Cold;
	}

	private static float GetSuppressionTransferAmount(float pendingDebt, float currentStatus)
	{
		if (pendingDebt <= 0f || currentStatus <= 0f)
		{
			return 0f;
		}
		float chunkTransfer = Mathf.Floor(pendingDebt / StatusChunkSize) * StatusChunkSize;
		if (chunkTransfer >= StatusChunkSize)
		{
			return chunkTransfer;
		}
		return pendingDebt < StatusChunkSize ? pendingDebt : 0f;
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
		ScheduleFogDelayAfterCampfire(campfire);
		GrantCompassToAllPlayers($"campfire-{campfireId}");
	}

	private void ScheduleFogDelayAfterCampfire(Campfire campfire)
	{
		if (campfire == null)
		{
			return;
		}
		if (!TryResolveCampfireFogOriginId(campfire, out int delayedOriginId))
		{
			return;
		}
		_delayedFogOriginId = delayedOriginId;
		_fogHiddenBufferTimer = 0f;
		_fogDelayTimer = 0f;
		_initialDelayCompleted = HiddenFogDelayBufferSeconds <= 0f && (FogDelay == null || FogDelay.Value <= 0f);
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
			TryNormalizeCalderaFogStage(forceResetCurrentSize: true);
			if (_orbFogHandler.currentID == delayedOriginId && _initialDelayCompleted)
			{
				StartFogMovement();
			}
		}
		float configuredDelay = FogDelay?.Value ?? DefaultFogDelaySeconds;
		Logger.LogInfo($"[{PluginName}] Applied hidden fog buffer {HiddenFogDelayBufferSeconds:F1}s and configured delay {configuredDelay:F1}s after lighting campfire. Next origin: {delayedOriginId}.");
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
			Logger.LogWarning($"[{PluginName}] Unable to resolve fog origin after lighting campfire because no fog origins were found.");
			return false;
		}
		if ((int)campfire.advanceToSegment >= availableOriginCount)
		{
			Segment advancedSegment = campfire.advanceToSegment;
			if (!ShouldUseCustomFogPositionForSegment(advancedSegment))
			{
				_pendingSyntheticFogSegmentId = -1;
				delayedOriginId = availableOriginCount - 1;
				Logger.LogInfo($"[{PluginName}] Synthetic fog position for segment {(int)advancedSegment} ({advancedSegment}) is disabled by config. Campfire={campfire.name}, fallbackOrigin={delayedOriginId}.");
				return delayedOriginId >= 0;
			}
			_pendingSyntheticFogSegmentId = (int)advancedSegment;
			delayedOriginId = availableOriginCount - 1;
			Logger.LogInfo($"[{PluginName}] Armed synthetic fog stage for segment {(int)advancedSegment} ({advancedSegment}) because only {availableOriginCount} real fog origins are available. Campfire={campfire.name}, fallbackOrigin={delayedOriginId}.");
			return delayedOriginId >= 0;
		}
		_pendingSyntheticFogSegmentId = -1;
		int currentOriginId = _orbFogHandler != null ? _orbFogHandler.currentID : -1;
		TryMapSegmentToFogOriginId(campfire.advanceToSegment, availableOriginCount, out int advanceOriginId);
		delayedOriginId = advanceOriginId;
		int currentSceneSegmentId = -1;
		int sceneOriginId = -1;
		MapHandler mapHandler = Singleton<MapHandler>.Instance;
		if (mapHandler != null)
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
			Logger.LogWarning($"[{PluginName}] Fog origin did not advance after lighting campfire. availableOrigins={availableOriginCount}, currentOrigin={currentOriginId}, sceneSegment={currentSceneSegmentId}, campfireAdvance={(int)campfire.advanceToSegment} ({campfire.advanceToSegment}), resolvedOrigin={delayedOriginId}, campfireName={campfire.name}.");
		}
		else
		{
			Logger.LogInfo($"[{PluginName}] Resolved fog origin after lighting campfire. availableOrigins={availableOriginCount}, currentOrigin={currentOriginId}, sceneSegment={currentSceneSegmentId}, campfireAdvance={(int)campfire.advanceToSegment} ({campfire.advanceToSegment}), resolvedOrigin={delayedOriginId}, campfireName={campfire.name}.");
		}
		return delayedOriginId >= 0;
	}
}
