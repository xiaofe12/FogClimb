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
using UnityEngine.UI;
using Zorro.Core;

namespace FogColdControl;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("com.github.PEAKModding.PEAKLib.ModConfig", BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BaseUnityPlugin
{
	private enum ConfigKey
	{
		ModEnabled,
		FogColdSuppression,
		NightColdEnabled,
		FogSpeed,
		FogDelay,
		CompassEnabled,
		CompassHotkey,
		FogPauseHotkey,
		FogUiEnabled,
		CampfireLocatorUiEnabled,
		FogUiX,
		FogUiY,
		FogUiScale
	}

	private enum VanillaProgressStartUiState
	{
		Hidden,
		Unavailable,
		Tracking
	}

	private enum FogUiIconKind
	{
		State,
		Speed,
		Buffer,
		Delay,
		Distance,
		Eta,
		Direct,
		Pause,
		FogHandling,
		Night
	}

	private sealed class FogUiEntryView
	{
		public RectTransform Root;

		public Image Icon;

		public TextMeshProUGUI Text;

		public string LastText = string.Empty;

		public FogUiIconKind LastIconKind;

		public Color LastIconColor = Color.clear;
	}

	private readonly struct FogUiDisplayEntry
	{
		public readonly FogUiIconKind Kind;

		public readonly Color IconColor;

		public readonly string Text;

		public FogUiDisplayEntry(FogUiIconKind kind, Color iconColor, string text)
		{
			Kind = kind;
			IconColor = iconColor;
			Text = text ?? string.Empty;
		}
	}

	public const string PluginGuid = "com.github.Thanks.FogClimb";

	public const string PluginName = "Fog&ColdControl";

	public const string PluginVersion = "1.0.0";

	private const string PreferredConfigFileName = "Thanks.Fog&ColdControl.cfg";

	private const string PreferredPluginFileName = "Thanks.Fog&ColdControl.dll";

	private static readonly string[] LegacyConfigFileNames = new string[2]
	{
		"Thanks.FogClimb.cfg",
		"com.github.Thanks.FogClimb.cfg"
	};

	private static readonly string[] LegacyPluginFileNames = new string[2]
	{
		"Thanks.FogClimb.dll",
		"com.github.Thanks.FogClimb.dll"
	};

	private const float DefaultVanillaFogSpeed = 0.3f;

	private const float DefaultFogSpeed = 0.4f;

	private const float DefaultFogDelaySeconds = 900f;

	private const float MinFogSpeed = 0.3f;

	private const float MaxFogSpeed = 20f;

	private const float MinFogDelaySeconds = 20f;

	private const float MaxFogDelaySeconds = 1000f;

	private const float DefaultFogUiX = 60f;

	private const float DefaultFogUiY = 0f;

	private const float DefaultFogUiScale = 0.9f;

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

	private const float FogSporesPerSecond = 0.005f;

	private const float RemoteFogSuppressionSafetyMultiplier = 1.12f;

	private const float NightColdPerSecond = 0.008f;

	private const float StatusChunkSize = 0.025f;

	private const float StalledFogResumeDelayRatio = 0.02f;

	private const float MinStalledFogResumeDelaySeconds = 2f;

	private const float MaxStalledFogResumeDelaySeconds = 8f;

	private const float HiddenFogDelayBufferSeconds = 10f;

	private const float FogEtaMinSampleIntervalSeconds = 0.1f;

	private const float FogEtaMinSizeDelta = 0.05f;

	private const float FogEtaMinReliableRate = 0.02f;

	private const float FogEtaRateSmoothing = 0.35f;

	private const float FogEtaDangerWindowSeconds = 45f;

	private const float FogEtaWarningWindowSeconds = 90f;

	private const float FogEtaDisplayStepSeconds = 0.5f;

	private const float FogDistanceDangerWindowUnits = 120f;

	private const float FogDistanceWarningWindowUnits = 300f;

	private const float FogDistanceEtaRefreshIntervalSeconds = 0.8f;

	private const string FogUiDistanceLabelColor = "#79E2D0";

	private const string FogUiDistanceValueColor = "#D9FFF5";

	private const string FogUiDistanceWarningLabelColor = "#FFB864";

	private const string FogUiDistanceWarningValueColor = "#FFE6BF";

	private const float PeakFogMinStartSize = 650f;

	private const float PeakFogMaxStartSize = 1400f;

	private const float PeakVerticalFogStopHeight = 1800f;

	private const float FogArrivalStopSize = 30f;

	private const KeyCode HiddenNightTestHotkey = KeyCode.LeftBracket;

	private const float HiddenNightTestHoldSeconds = 5f;

	private const float FallbackNightTimeNormalized = 0.85f;

	private const float RemotePlayerJoinGraceSeconds = 8f;

	private const int MaxCompassGrantsPerPlayerPerSync = 1;

	private const float CampfireCompassGrantDelaySeconds = 0.9f;

	private const float FogUiWidth = 1360f;

	private const float FogUiHeight = 34f;

	private const float FogUiHorizontalPadding = 10f;

	private const float FogUiIconSize = 19f;

	private const float FogUiIconVerticalOffset = -1f;

	private const float FogUiEntrySpacing = 14f;

	private const float FogUiEntryIconTextSpacing = 3f;

	private const float FogUiEntryTextSizeMultiplier = 0.9f;

	private const float CampfireLocatorUiWidth = 372f;

	private const float CampfireLocatorUiHeight = 24f;

	private const float CampfireLocatorTopOffset = 54f;

	private const float CampfireLocatorLineWidth = 360f;

	private const float CampfireLocatorLineHeight = 2f;

	private const float CampfireLocatorDotSize = 18f;

	private const float CampfireLocatorDotSmoothing = 12f;

	private const float CompassLobbyNoticeRightOffset = 28f;

	private const float CompassLobbyNoticeDownOffset = 0f;

	private const float CompassLobbyNoticeWidth = 735f;

	private const float CompassLobbyNoticeHeight = 81f;

	private const float CompassLobbyNoticeFontSizeMultiplier = 1.2f;

	private const float CompassLobbyNoticeLineSpacing = 1.125f;

	private const string CompassLobbyNoticeKeyColor = "#FF3B30";

	private const string FogUiSpeedLabelColor = "#8EC5FF";

	private const string FogUiSpeedValueColor = "#D6F1FF";

	private const string FogUiWaitLabelColor = "#F2C75C";

	private const string FogUiWaitValueColor = "#FFE8A3";

	private const string FogUiCountdownLabelColor = "#FF8A5B";

	private const string FogUiCountdownValueColor = "#FFD2B8";

	private const string FogUiCountdownDangerLabelColor = "#FF2D2D";

	private const string FogUiCountdownDangerValueColor = "#FFC0C0";

	private const string FogUiDelayCountdownStartLabelColor = "#FF8A8A";

	private const string FogUiDelayCountdownStartValueColor = "#FFD6D6";

	private const string FogUiDelayCountdownEndLabelColor = "#7A0000";

	private const string FogUiDelayCountdownEndValueColor = "#B31212";

	private const string FogUiStateLabelColor = "#A8E0A0";

	private const string FogUiStateRunningColor = "#B5FFB8";

	private const string FogUiStatePausedColor = "#FFC37D";

	private const string FogUiStateWaitingColor = "#FFE08A";

	private const string FogUiStateSyncingColor = "#A3D2FF";

	private const string FogUiHintLabelColor = "#B7C0CC";

	private const string FogUiHintValueColor = "#E2EAF3";

	private const string FogUiNightEnabledColor = "#9FFFA8";

	private const string FogUiNightDisabledColor = "#FFB3B3";

	// Manual entry point for adjusting the granted compass item when auto-detection is wrong.
	private static readonly ushort CompassItemIdOverride = 0;

	private const string CompassNameKeyword = "Compass";

	private const string ModConfigPluginGuid = "com.github.PEAKModding.PEAKLib.ModConfig";

	private const string LegacyCanonicalConfigSectionName = "Fog";

	private const string CanonicalBasicConfigSectionName = "Basic";

	private const string CanonicalAdjustmentConfigSectionName = "Adjustments";

	private const string NetworkInstallStateKey = "FogClimb.Enabled";

	private const int SimplifiedChineseLanguageIndex = 9;

	private static readonly BindingFlags InstanceBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

	private static readonly BindingFlags StaticBindingFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

	private static readonly FieldInfo ConfigEntryDescriptionField = typeof(ConfigEntryBase).GetField("<Description>k__BackingField", InstanceBindingFlags);

	private static readonly PropertyInfo ConfigFileEntriesProperty = typeof(ConfigFile).GetProperty("Entries", InstanceBindingFlags);

	private static readonly FieldInfo BasePluginConfigBackingField = typeof(BaseUnityPlugin).GetField("<Config>k__BackingField", InstanceBindingFlags);

	private static readonly FieldInfo OrbFogHandlerOriginsField = typeof(OrbFogHandler).GetField("origins", InstanceBindingFlags);

	private static readonly FieldInfo OrbFogHandlerSphereField = typeof(OrbFogHandler).GetField("sphere", InstanceBindingFlags);

	private static readonly int StatusTypeCount = Enum.GetNames(typeof(CharacterAfflictions.STATUSTYPE)).Length;

	private static readonly string[] DayNightTimeMemberCandidates = new string[5] { "currentTime", "time", "timeOfDay", "timeNormalized", "cycleTime" };

	private static readonly Color CampfireLocatorLineColor = new Color(1f, 1f, 1f, 0.95f);

	private static readonly Color CampfireLocatorDotColor = new Color(1f, 0.2f, 0.2f, 1f);

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

	private bool _lastCampfireLocatorUiEnabledState;

	private bool _lastDetectedChineseLanguage;

	private bool _isRefreshingLanguage;

	private bool _pendingConfigFileLocalizationRefresh;

	private bool _pendingConfigFileLocalizationSave;

	private float _lastFogUiX;

	private float _lastFogUiY;

	private float _lastFogUiScale;

	private bool _hasAdvertisedInstallState;

	private bool _lastAdvertisedInstallState;

	private float _hiddenNightTestHoldTimer;

	private bool _hiddenNightTestTriggeredThisHold;

	private bool _fogPaused;

	private readonly HashSet<int> _grantedCampfireCompassIds = new HashSet<int>();

	private readonly HashSet<int> _restoredCheckpointCampfireIds = new HashSet<int>();

	private readonly Dictionary<int, int> _playerCompassGrantCounts = new Dictionary<int, int>();

	private readonly Dictionary<int, float> _remoteFogSuppressionDebt = new Dictionary<int, float>();

	private readonly Dictionary<int, float> _remoteFogSporesSuppressionDebt = new Dictionary<int, float>();

	private readonly Dictionary<int, float> _remotePlayerFirstSeenTimes = new Dictionary<int, float>();

	private readonly Dictionary<int, int> _remotePlayerCompassBaselineCounts = new Dictionary<int, int>();

	private readonly Dictionary<int, float> _pendingCampfireCompassGrantTimes = new Dictionary<int, float>();

	private Item _compassItem;

	private RectTransform _fogUiRect;

	private TextMeshProUGUI _fogUiText;

	private string _lastFogUiRenderedText = string.Empty;

	private RectTransform _fogUiEntriesRect;

	private readonly List<FogUiEntryView> _fogUiEntryViews = new List<FogUiEntryView>();

	private readonly List<FogUiDisplayEntry> _fogUiDisplayEntries = new List<FogUiDisplayEntry>(8);

	private readonly Dictionary<FogUiIconKind, Sprite> _fogUiIconSprites = new Dictionary<FogUiIconKind, Sprite>();

	private Sprite _campfireLocatorDotSprite;

	private RectTransform _campfireLocatorUiRect;

	private RectTransform _campfireLocatorDotRect;

	private float _campfireLocatorCurrentDotX;

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

	private int _fogEtaTrackedOriginId = -1;

	private float _fogEtaLastObservedSize = float.NaN;

	private float _fogEtaLastObservedTime = -1f;

	private float _fogEtaEstimatedUnitsPerSecond;

	private bool _fogEtaHasReliableRate;

	private float _fogDistanceEtaLastRefreshTime = -1f;

	private bool _fogDistanceEtaHasSnapshot;

	private bool _fogDistanceEtaHasEta;

	private float _fogDistanceEtaRemainingDistance;

	private float _fogDistanceEtaSeconds;

	internal static Plugin Instance { get; private set; }

	internal static ConfigEntry<bool> ModEnabled { get; private set; }

	internal static ConfigEntry<bool> FogColdSuppression { get; private set; }

	internal static ConfigEntry<bool> NightColdEnabled { get; private set; }

	internal static ConfigEntry<float> FogSpeed { get; private set; }

	internal static ConfigEntry<float> FogDelay { get; private set; }

	internal static ConfigEntry<bool> CompassEnabled { get; private set; }

	internal static ConfigEntry<KeyCode> CompassHotkey { get; private set; }

	internal static ConfigEntry<KeyCode> FogPauseHotkey { get; private set; }

	internal static ConfigEntry<bool> FogUiEnabled { get; private set; }

	internal static ConfigEntry<bool> CampfireLocatorUiEnabled { get; private set; }

	internal static ConfigEntry<float> FogUiX { get; private set; }

	internal static ConfigEntry<float> FogUiY { get; private set; }

	internal static ConfigEntry<float> FogUiScale { get; private set; }

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
		_lastFogUiX = FogUiX?.Value ?? DefaultFogUiX;
		_lastFogUiY = FogUiY?.Value ?? DefaultFogUiY;
		_lastFogUiScale = FogUiScale?.Value ?? DefaultFogUiScale;
		MarkConfigFileLocalizationDirty(saveConfigFile: true);
		_harmony = new Harmony(PluginGuid);
		_harmony.PatchAll(Assembly.GetExecutingAssembly());
		SceneManager.sceneLoaded += OnSceneLoaded;
		Logger.LogInfo($"[{PluginName}] Loaded.");
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

	private void InitializeConfig(bool isChineseLanguage)
	{
		ModEnabled = Config.Bind(GetConfigSectionName(ConfigKey.ModEnabled), GetConfigKeyName(ConfigKey.ModEnabled), true, CreateConfigDescription(ConfigKey.ModEnabled, isChineseLanguage));
		FogColdSuppression = Config.Bind(GetConfigSectionName(ConfigKey.FogColdSuppression), GetConfigKeyName(ConfigKey.FogColdSuppression), false, CreateConfigDescription(ConfigKey.FogColdSuppression, isChineseLanguage));
		NightColdEnabled = Config.Bind(GetConfigSectionName(ConfigKey.NightColdEnabled), GetConfigKeyName(ConfigKey.NightColdEnabled), true, CreateConfigDescription(ConfigKey.NightColdEnabled, isChineseLanguage));
		FogSpeed = Config.Bind(GetConfigSectionName(ConfigKey.FogSpeed), GetConfigKeyName(ConfigKey.FogSpeed), DefaultFogSpeed, CreateConfigDescription(ConfigKey.FogSpeed, isChineseLanguage));
		FogDelay = Config.Bind(GetConfigSectionName(ConfigKey.FogDelay), GetConfigKeyName(ConfigKey.FogDelay), DefaultFogDelaySeconds, CreateConfigDescription(ConfigKey.FogDelay, isChineseLanguage));
		CompassEnabled = Config.Bind(GetConfigSectionName(ConfigKey.CompassEnabled), GetConfigKeyName(ConfigKey.CompassEnabled), false, CreateConfigDescription(ConfigKey.CompassEnabled, isChineseLanguage));
		CompassHotkey = Config.Bind(GetConfigSectionName(ConfigKey.CompassHotkey), GetConfigKeyName(ConfigKey.CompassHotkey), KeyCode.G, CreateConfigDescription(ConfigKey.CompassHotkey, isChineseLanguage));
		FogPauseHotkey = Config.Bind(GetConfigSectionName(ConfigKey.FogPauseHotkey), GetConfigKeyName(ConfigKey.FogPauseHotkey), KeyCode.Y, CreateConfigDescription(ConfigKey.FogPauseHotkey, isChineseLanguage));
		FogUiEnabled = Config.Bind(GetConfigSectionName(ConfigKey.FogUiEnabled), GetConfigKeyName(ConfigKey.FogUiEnabled), true, CreateConfigDescription(ConfigKey.FogUiEnabled, isChineseLanguage));
		CampfireLocatorUiEnabled = Config.Bind(GetConfigSectionName(ConfigKey.CampfireLocatorUiEnabled), GetConfigKeyName(ConfigKey.CampfireLocatorUiEnabled), true, CreateConfigDescription(ConfigKey.CampfireLocatorUiEnabled, isChineseLanguage));
		FogUiX = Config.Bind(GetConfigSectionName(ConfigKey.FogUiX), GetConfigKeyName(ConfigKey.FogUiX), DefaultFogUiX, CreateConfigDescription(ConfigKey.FogUiX, isChineseLanguage));
		FogUiY = Config.Bind(GetConfigSectionName(ConfigKey.FogUiY), GetConfigKeyName(ConfigKey.FogUiY), DefaultFogUiY, CreateConfigDescription(ConfigKey.FogUiY, isChineseLanguage));
		FogUiScale = Config.Bind(GetConfigSectionName(ConfigKey.FogUiScale), GetConfigKeyName(ConfigKey.FogUiScale), DefaultFogUiScale, CreateConfigDescription(ConfigKey.FogUiScale, isChineseLanguage));
		MigrateLocalizedConfigEntries();
		ClampConfigValues();
	}

	private void EnsurePreferredConfigFile()
	{
		try
		{
			string configDirectory = Paths.ConfigPath;
			if (string.IsNullOrWhiteSpace(configDirectory))
			{
				return;
			}
			Directory.CreateDirectory(configDirectory);
			string preferredConfigPath = Path.Combine(configDirectory, PreferredConfigFileName);
			string[] legacyConfigPaths = LegacyConfigFileNames
				.Select(fileName => Path.Combine(configDirectory, fileName))
				.Concat(new[] { Path.Combine(configDirectory, $"{PluginGuid}.cfg") })
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();
			string legacyConfigPath = legacyConfigPaths.FirstOrDefault(File.Exists);
			if (!string.IsNullOrWhiteSpace(legacyConfigPath)
				&& !string.Equals(preferredConfigPath, legacyConfigPath, StringComparison.OrdinalIgnoreCase)
				&& !File.Exists(preferredConfigPath))
			{
				File.Move(legacyConfigPath, preferredConfigPath);
				Logger.LogInfo($"[{PluginName}] Migrated config file to {PreferredConfigFileName}.");
			}
			if (BasePluginConfigBackingField == null)
			{
				return;
			}
			if (Config != null && string.Equals(Config.ConfigFilePath, preferredConfigPath, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			ConfigFile preferredConfig = new ConfigFile(preferredConfigPath, true);
			BasePluginConfigBackingField.SetValue(this, preferredConfig);
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"[{PluginName}] Failed to switch config path to {PreferredConfigFileName}: {ex.Message}");
		}
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
		migratedAnyValue |= TryMigrateLocalizedConfigValue(NightColdEnabled, ConfigKey.NightColdEnabled, orphanedEntries);
		migratedAnyValue |= TryMigrateLocalizedConfigValue(FogSpeed, ConfigKey.FogSpeed, orphanedEntries);
		migratedAnyValue |= TryMigrateLocalizedConfigValue(FogDelay, ConfigKey.FogDelay, orphanedEntries);
		migratedAnyValue |= TryMigrateLocalizedConfigValue(CompassEnabled, ConfigKey.CompassEnabled, orphanedEntries);
		migratedAnyValue |= TryMigrateLocalizedConfigValue(CompassHotkey, ConfigKey.CompassHotkey, orphanedEntries);
		migratedAnyValue |= TryMigrateLocalizedConfigValue(FogPauseHotkey, ConfigKey.FogPauseHotkey, orphanedEntries);
		migratedAnyValue |= TryMigrateLocalizedConfigValue(FogUiEnabled, ConfigKey.FogUiEnabled, orphanedEntries);
		migratedAnyValue |= TryMigrateLocalizedConfigValue(CampfireLocatorUiEnabled, ConfigKey.CampfireLocatorUiEnabled, orphanedEntries);
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
		bool migrated = false;
		foreach (ConfigDefinition aliasDefinition in GetAliasDefinitions(configKey))
		{
			if (DefinitionsEqual(aliasDefinition, entry.Definition) || !orphanedEntries.Contains(aliasDefinition))
			{
				continue;
			}
			if (!migrated)
			{
				object orphanedValue = orphanedEntries[aliasDefinition];
				if (orphanedValue != null)
				{
					entry.SetSerializedValue(orphanedValue.ToString());
				}
				migrated = true;
			}
			orphanedEntries.Remove(aliasDefinition);
		}
		return migrated;
	}

	private static IEnumerable<ConfigDefinition> GetAliasDefinitions(ConfigKey configKey)
	{
		string canonicalKey = GetConfigKeyName(configKey);
		string chineseKey = GetKeyName(configKey, isChineseLanguage: true);
		string[] sectionAliases = new string[]
		{
			GetConfigSectionName(configKey),
			GetSectionName(configKey, isChineseLanguage: true),
			GetLegacyConfigSectionName(),
			GetLegacySectionName(isChineseLanguage: true)
		}
			.Where(section => !string.IsNullOrWhiteSpace(section))
			.Distinct(StringComparer.Ordinal)
			.ToArray();

		foreach (string section in sectionAliases)
		{
			yield return new ConfigDefinition(section, canonicalKey);
			yield return new ConfigDefinition(section, chineseKey);
		}
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
		bool matchesOlderFogClimbDefault = Approximately(FogUiX.Value, 60f) && Approximately(FogUiY.Value, 16f) && Approximately(FogUiScale.Value, 1.2f);
		bool matchesCurrentFogClimbDefault = Approximately(FogUiX.Value, 60f) && Approximately(FogUiY.Value, 0f) && Approximately(FogUiScale.Value, 1.2f);
		if (matchesLegacyPresetA || matchesLegacyPresetB || matchesLegacyPresetC || matchesLegacyPresetD || matchesPreviousFogClimbDefault || matchesOlderFogClimbDefault || matchesCurrentFogClimbDefault)
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

	private void RegisterConfigChangeHandlers()
	{
		if (Config == null)
		{
			return;
		}
		Config.SettingChanged -= OnConfigSettingChanged;
		Config.SettingChanged += OnConfigSettingChanged;
	}

	private void UnregisterConfigChangeHandlers()
	{
		if (Config == null)
		{
			return;
		}
		Config.SettingChanged -= OnConfigSettingChanged;
	}

	private void OnConfigSettingChanged(object sender, SettingChangedEventArgs e)
	{
		MarkConfigFileLocalizationDirty(saveConfigFile: false);
	}

	private void MarkConfigFileLocalizationDirty(bool saveConfigFile)
	{
		_pendingConfigFileLocalizationRefresh = true;
		_pendingConfigFileLocalizationSave |= saveConfigFile;
	}

	private void HandlePendingConfigFileLocalizationRefresh()
	{
		if (!_pendingConfigFileLocalizationRefresh || _isRefreshingLanguage)
		{
			return;
		}
		bool isChineseLanguage = _lastDetectedChineseLanguage;
		bool saveConfigFile = _pendingConfigFileLocalizationSave;
		_pendingConfigFileLocalizationRefresh = false;
		_pendingConfigFileLocalizationSave = false;
		TryRefreshLocalizedConfigFile(isChineseLanguage, saveConfigFile);
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
		HandlePendingConfigFileLocalizationRefresh();
		HandleFogUiConfigChanges();
		HandleManualCompassHotkey();
		HandleFogPauseHotkey();
		HandleHiddenNightTestHotkey();
		RefreshRemotePlayerJoinGraceState();
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
			SyncRemoteStatusSuppressionIfNeeded();
			SyncCompassGrantsToPlayersIfNeeded();
		}
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
		CleanupFogUi();
		CleanupCampfireLocatorUi();
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
		ResetFogArrivalEstimate();
	}

	private static bool HasAnyConfiguredFogDelay()
	{
		return HiddenFogDelayBufferSeconds > 0f || (FogDelay != null && FogDelay.Value > 0f);
	}

	private static bool TryGetVanillaProgressStartThresholds(OrbFogHandler fogHandler, out float requiredHeight, out float requiredForward)
	{
		requiredHeight = 0f;
		requiredForward = 0f;
		if (fogHandler == null)
		{
			return false;
		}
		requiredHeight = fogHandler.currentStartHeight;
		requiredForward = fogHandler.currentStartForward;
		return !float.IsNaN(requiredHeight)
			&& !float.IsInfinity(requiredHeight)
			&& !float.IsNaN(requiredForward)
			&& !float.IsInfinity(requiredForward);
	}

	private static bool TryGetVanillaProgressStartProgress(OrbFogHandler fogHandler, out int passedCount, out int totalCount, out float requiredHeight, out float requiredForward)
	{
		passedCount = 0;
		totalCount = 0;
		requiredHeight = 0f;
		requiredForward = 0f;
		if (Ascents.currentAscent < 0 || !TryGetVanillaProgressStartThresholds(fogHandler, out requiredHeight, out requiredForward))
		{
			return false;
		}
		foreach (Character character in Character.AllCharacters)
		{
			if (character?.data == null || character.data.dead)
			{
				continue;
			}
			totalCount++;
			if (character.Center.y >= requiredHeight && character.Center.z >= requiredForward)
			{
				passedCount++;
			}
		}
		return totalCount > 0;
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
		if (IsFogRemovedInCurrentScene())
		{
			ClearSyntheticFogStage();
			_pendingSyntheticFogSegmentId = -1;
			_delayedFogOriginId = -1;
			ApplyRemovedFogState();
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
			if (!ShouldEnforceConfiguredDelay(_orbFogHandler))
			{
				return;
			}
			if (TryStartFogFromVanillaProgressTrigger(_orbFogHandler))
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
		if (_fogPaused)
		{
			ApplyPausedFogState(syncImmediately: false);
			return;
		}
		_orbFogHandler.speed = FogSpeed.Value;
		TryAutoStartCustomRunFogIfNeeded();
		TryAutoResumeStalledFogMovement();
		TryGrantInitialCompassIfNeeded();
	}

	private bool TryStartFogFromVanillaProgressTrigger(OrbFogHandler fogHandler)
	{
		if (!TryGetVanillaProgressStartProgress(fogHandler, out int passedCount, out int totalCount, out float requiredHeight, out float requiredForward))
		{
			return false;
		}
		if (passedCount < totalCount)
		{
			return false;
		}
		float remainingBuffer = Mathf.Max(HiddenFogDelayBufferSeconds - _fogHiddenBufferTimer, 0f);
		float configuredDelay = Mathf.Max(FogDelay?.Value ?? DefaultFogDelaySeconds, 0f);
		float remainingDelay = Mathf.Max(configuredDelay - _fogDelayTimer, 0f);
		_initialDelayCompleted = true;
		_fogHiddenBufferTimer = HiddenFogDelayBufferSeconds;
		_fogDelayTimer = configuredDelay;
		Logger.LogInfo($"[{PluginName}] Starting fog early from vanilla progress trigger at origin {fogHandler.currentID}. progress={passedCount}/{totalCount}, thresholdY={requiredHeight:F1}, thresholdZ={requiredForward:F1}, remainingBuffer={remainingBuffer:F1}s, remainingDelay={remainingDelay:F1}s.");
		StartFogMovement();
		return true;
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

	private static bool ShouldUseCustomFogPositionForSegment(Segment segment)
	{
		return segment >= Segment.Peak;
	}

	private static bool ShouldRemoveFogForSegment(Segment segment)
	{
		return segment == Segment.Caldera || segment == Segment.TheKiln;
	}

	private static bool TryGetCurrentGameplaySegment(out Segment segment)
	{
		segment = Segment.Beach;
		if (!IsGameplayFogScene(SceneManager.GetActiveScene()) || LoadingScreenHandler.loading)
		{
			return false;
		}
		MapHandler mapHandler = Singleton<MapHandler>.Instance;
		if (mapHandler == null)
		{
			return false;
		}
		segment = MapHandler.CurrentSegmentNumber;
		return true;
	}

	private static bool IsFogRemovedInCurrentScene()
	{
		return TryGetCurrentGameplaySegment(out Segment segment) && ShouldRemoveFogForSegment(segment);
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
		if (sphere == null)
		{
			return;
		}
		_fogSphere = sphere;
		sphere.currentSize = 0f;
		if (sphere.gameObject.activeSelf)
		{
			sphere.gameObject.SetActive(false);
		}
	}

	private bool IsLateGameFogColdSuppressionActive()
	{
		if (!IsGameplayFogScene(SceneManager.GetActiveScene()) || LoadingScreenHandler.loading)
		{
			return _activeSyntheticFogSegmentId >= (int)Segment.Caldera;
		}
		MapHandler mapHandler = Singleton<MapHandler>.Instance;
		if (mapHandler != null && (int)MapHandler.CurrentSegmentNumber >= (int)Segment.Caldera)
		{
			return true;
		}
		return _activeSyntheticFogSegmentId >= (int)Segment.Caldera;
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
		if (_orbFogHandler == null || !HasFogAuthority() || _orbFogHandler.isMoving || IsFogRemovedInCurrentScene() || !TryGetTargetFogOriginId(out int expectedOriginId))
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

	private bool TryBuildPeakVerticalFogStage(Segment syntheticSegment, out Vector3 fogPoint, out float fogSize, out string anchorDescription)
	{
		fogPoint = Vector3.zero;
		fogSize = 0f;
		anchorDescription = string.Empty;
		if (syntheticSegment < Segment.Peak)
		{
			return false;
		}
		if (!TryResolvePeakVerticalFogTargetAnchor(syntheticSegment, out Vector3 targetAnchor, out string targetDescription))
		{
			return false;
		}
		fogPoint = new Vector3(targetAnchor.x, PeakVerticalFogStopHeight + FogArrivalStopSize, targetAnchor.z);
		fogSize = ComputePeakVerticalFogStartSize(fogPoint, targetAnchor);
		if (fogSize <= 0f)
		{
			return false;
		}
		anchorDescription = $"{targetDescription} vertical-up stop@{PeakVerticalFogStopHeight:F0}";
		return true;
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

	private bool TryResolvePeakVerticalFogAnchor(Segment syntheticSegment, out Vector3 fogPoint, out string anchorDescription)
	{
		fogPoint = Vector3.zero;
		anchorDescription = string.Empty;
		if (syntheticSegment < Segment.Peak)
		{
			return false;
		}
		if (!TryResolvePeakVerticalFogTargetAnchor(syntheticSegment, out Vector3 targetAnchor, out string targetDescription))
		{
			return false;
		}
		fogPoint = new Vector3(targetAnchor.x, PeakVerticalFogStopHeight + FogArrivalStopSize, targetAnchor.z);
		anchorDescription = $"{targetDescription} vertical-up stop@{PeakVerticalFogStopHeight:F0}";
		return true;
	}

	private bool TryResolvePeakVerticalFogTargetAnchor(Segment syntheticSegment, out Vector3 targetAnchor, out string targetDescription)
	{
		targetAnchor = Vector3.zero;
		targetDescription = string.Empty;
		if (syntheticSegment < Segment.Peak)
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
		Character fallbackCharacter = Character.AllCharacters.FirstOrDefault(character => character != null);
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
		return ComputeFogStartSizeForStage(syntheticSegment, fogPoint, baseSize, PeakFogMinStartSize, PeakFogMaxStartSize, 130f, 95f);
	}

	private float ComputePeakVerticalFogStartSize(Vector3 fogPoint, Vector3 targetAnchor)
	{
		float previousOriginSize = 900f;
		TryGetPreviousRealFogOriginSize(out previousOriginSize);
		float computedSize = Mathf.Max(previousOriginSize * 0.68f, PeakFogMinStartSize);
		float lowestRelevantY = Mathf.Min(targetAnchor.y, PeakVerticalFogStopHeight - 180f);
		foreach (Character character in Character.AllCharacters)
		{
			if (character == null || character.data == null || character.data.dead)
			{
				continue;
			}
			lowestRelevantY = Mathf.Min(lowestRelevantY, character.Center.y);
			computedSize = Mathf.Max(computedSize, Vector3.Distance(fogPoint, character.Center) + 70f);
		}
		computedSize = Mathf.Max(computedSize, fogPoint.y - lowestRelevantY + 120f);
		if (TryResolveFogStageCoverageAnchor(Segment.Peak, out Vector3 stageAnchor, out _))
		{
			computedSize = Mathf.Max(computedSize, Vector3.Distance(fogPoint, stageAnchor) + 90f);
		}
		return Mathf.Clamp(computedSize, PeakFogMinStartSize, PeakFogMaxStartSize);
	}

	private void TryAutoStartCustomRunFogIfNeeded()
	{
		if (_orbFogHandler == null || !HasFogAuthority() || _fogPaused || !RunSettings.IsCustomRun)
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
		if (_orbFogHandler == null || !HasFogAuthority() || _fogPaused || !_initialDelayCompleted)
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
		if (_orbFogHandler == null || _fogPaused || IsFogRemovedInCurrentScene())
		{
			return;
		}
		Logger.LogInfo($"[{PluginName}] Starting fog movement. currentOrigin={_orbFogHandler.currentID}, syntheticStage={_activeSyntheticFogSegmentId}, pendingOrigin={_delayedFogOriginId}, currentSize={_orbFogHandler.currentSize:F1}.");
		ClearPendingCampfireDelayForOrigin(_orbFogHandler.currentID);
		bool shouldGrantInitialCompass = !_initialCompassGranted && _orbFogHandler.currentID == 0;
		if (!TryInvokeFogStartMovement(_orbFogHandler, out string invocationPath))
		{
			Logger.LogError($"[{PluginName}] Failed to start fog movement because no compatible OrbFogHandler start method was found. currentOrigin={_orbFogHandler.currentID}.");
			return;
		}
		Logger.LogInfo($"[{PluginName}] Fog movement invoked via {invocationPath}.");
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
			Logger.LogWarning($"[{PluginName}] Fog start method {method.Name} failed: {ex.InnerException?.Message ?? ex.Message}");
			return false;
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"[{PluginName}] Fog start method {method.Name} failed: {ex.Message}");
			return false;
		}
	}

	private void ApplyPausedFogState(bool syncImmediately)
	{
		if (_orbFogHandler == null)
		{
			return;
		}
		_orbFogHandler.speed = 0f;
		_orbFogHandler.isMoving = false;
		if (syncImmediately)
		{
			ForceSyncFogStateToGuests();
		}
	}

	private void ForceSyncFogStateToGuests()
	{
		if (_orbFogHandler == null || !HasRemotePlayers() || !PhotonNetwork.IsMasterClient)
		{
			return;
		}
		if (HasRemotePlayersInJoinGracePeriod())
		{
			return;
		}
		_lastFogStateSyncTime = -FogStateSyncIntervalSeconds;
		ResetFogStateSyncSnapshot();
		SyncFogStateToGuestsIfNeeded();
	}

	private void UpdateFogArrivalEstimate()
	{
		if (_orbFogHandler == null || _fogPaused || IsFogRemovedInCurrentScene() || !_orbFogHandler.isMoving || _orbFogHandler.hasArrived)
		{
			ResetFogArrivalEstimate();
			return;
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
		if (elapsed < FogEtaMinSampleIntervalSeconds)
		{
			return;
		}
		float sizeDelta = _fogEtaLastObservedSize - observedSize;
		if (sizeDelta <= -FogEtaMinSizeDelta)
		{
			InitializeFogArrivalEstimate(originId, observedSize, now);
			return;
		}
		if (Mathf.Abs(sizeDelta) < FogEtaMinSizeDelta)
		{
			return;
		}
		_fogEtaLastObservedSize = observedSize;
		_fogEtaLastObservedTime = now;
		float measuredRate = sizeDelta / elapsed;
		if (measuredRate < FogEtaMinReliableRate)
		{
			UpdateFogArrivalMetricsCache(force: false);
			return;
		}
		_fogEtaEstimatedUnitsPerSecond = _fogEtaHasReliableRate ? Mathf.Lerp(_fogEtaEstimatedUnitsPerSecond, measuredRate, FogEtaRateSmoothing) : measuredRate;
		_fogEtaHasReliableRate = true;
		UpdateFogArrivalMetricsCache(force: false);
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
		Character localCharacter = Character.localCharacter;
		if (localCharacter == null || localCharacter.data == null || localCharacter.data.dead)
		{
			return false;
		}
		playerPoint = localCharacter.Center;
		return TryResolveFogPointForCurrentOrigin(out fogPoint, out _);
	}

	private float GetObservedFogEtaSize()
	{
		if (_orbFogHandler == null)
		{
			return FogArrivalStopSize;
		}
		float rawSize = _orbFogHandler.currentSize;
		if (IsReadOnlyFogUiViewer() && TryGetGuestFogSyncOverrideSize(out float overrideSize))
		{
			rawSize = overrideSize;
		}
		return Mathf.Max(rawSize, FogArrivalStopSize);
	}

	private float GetDisplayedFogEtaSize()
	{
		float observedSize = GetObservedFogEtaSize();
		if (!_fogEtaHasReliableRate || float.IsNaN(_fogEtaLastObservedSize) || _fogEtaLastObservedTime < 0f)
		{
			return observedSize;
		}
		if (Mathf.Abs(observedSize - _fogEtaLastObservedSize) >= FogEtaMinSizeDelta)
		{
			return observedSize;
		}
		float extrapolatedSize = _fogEtaLastObservedSize - _fogEtaEstimatedUnitsPerSecond * Mathf.Max(Time.unscaledTime - _fogEtaLastObservedTime, 0f);
		return Mathf.Clamp(Mathf.Min(observedSize, extrapolatedSize), FogArrivalStopSize, observedSize);
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
		if (!force && _fogDistanceEtaLastRefreshTime >= 0f && now - _fogDistanceEtaLastRefreshTime < FogDistanceEtaRefreshIntervalSeconds)
		{
			return;
		}
		if (!TryGetDisplayedFogEtaGeometry(out float currentSize, out Vector3 fogPoint, out Vector3 playerPoint))
		{
			ResetFogArrivalMetricsCache();
			return;
		}
		float remainingDistance = Mathf.Max(currentSize - Vector3.Distance(fogPoint, playerPoint), 0f);
		_fogDistanceEtaLastRefreshTime = now;
		_fogDistanceEtaHasSnapshot = true;
		_fogDistanceEtaRemainingDistance = remainingDistance;
		if (remainingDistance <= 0.05f)
		{
			_fogDistanceEtaHasEta = true;
			_fogDistanceEtaSeconds = 0f;
			return;
		}
		if (!TryResolveFogEtaRate(out float etaRate))
		{
			_fogDistanceEtaHasEta = false;
			_fogDistanceEtaSeconds = 0f;
			return;
		}
		_fogDistanceEtaHasEta = true;
		_fogDistanceEtaSeconds = remainingDistance / etaRate;
	}

	private bool TryResolveFogEtaRate(out float etaRate)
	{
		etaRate = 0f;
		if (_fogEtaHasReliableRate && _fogEtaEstimatedUnitsPerSecond >= FogEtaMinReliableRate)
		{
			etaRate = _fogEtaEstimatedUnitsPerSecond;
		}
		else if (HasFogAuthority() && _orbFogHandler != null && _orbFogHandler.speed >= FogEtaMinReliableRate)
		{
			etaRate = _orbFogHandler.speed;
		}
		return etaRate >= FogEtaMinReliableRate;
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
		MapHandler mapHandler = Singleton<MapHandler>.Instance;
		if (mapHandler == null)
		{
			return false;
		}
		if (ShouldRemoveFogForSegment(MapHandler.CurrentSegmentNumber))
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
		_remoteFogSporesSuppressionDebt.Clear();
		if (!hasAuthority && PhotonNetwork.InRoom)
		{
			RestoreVanillaFogSpeed();
			ResetFogRuntimeState();
			SetFogUiVisible(false);
			SetCampfireLocatorUiVisible(false);
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

	private void RefreshRemotePlayerJoinGraceState()
	{
		if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
		{
			_remotePlayerFirstSeenTimes.Clear();
			_remotePlayerCompassBaselineCounts.Clear();
			return;
		}
		int localActorNumber = PhotonNetwork.LocalPlayer?.ActorNumber ?? -1;
		HashSet<int> activeRemoteActors = new HashSet<int>();
		bool shouldLogJoinGrace = HasFogAuthority();
		foreach (Photon.Realtime.Player player in PhotonNetwork.CurrentRoom.Players.Values)
		{
			if (player == null || player.ActorNumber <= 0 || player.ActorNumber == localActorNumber)
			{
				continue;
			}
			activeRemoteActors.Add(player.ActorNumber);
			if (_remotePlayerFirstSeenTimes.ContainsKey(player.ActorNumber))
			{
				continue;
			}
			_remotePlayerFirstSeenTimes[player.ActorNumber] = Time.unscaledTime;
			_remotePlayerCompassBaselineCounts[player.ActorNumber] = _totalCompassGrantCount;
			if (!shouldLogJoinGrace)
			{
				continue;
			}
			if (_totalCompassGrantCount > 0)
			{
				Logger.LogInfo($"[{PluginName}] Delaying host sync for remote player #{player.ActorNumber} for {RemotePlayerJoinGraceSeconds:F1}s. Skipping { _totalCompassGrantCount } historical compass grants for late join safety.");
			}
			else
			{
				Logger.LogInfo($"[{PluginName}] Delaying host sync for remote player #{player.ActorNumber} for {RemotePlayerJoinGraceSeconds:F1}s.");
			}
		}
		int[] staleActors = _remotePlayerFirstSeenTimes.Keys.Where(actorNumber => !activeRemoteActors.Contains(actorNumber)).ToArray();
		foreach (int actorNumber in staleActors)
		{
			_remotePlayerFirstSeenTimes.Remove(actorNumber);
			_remotePlayerCompassBaselineCounts.Remove(actorNumber);
		}
	}

	private bool HasRemotePlayersInJoinGracePeriod()
	{
		if (!HasRemotePlayers())
		{
			return false;
		}
		float now = Time.unscaledTime;
		return _remotePlayerFirstSeenTimes.Values.Any(firstSeenTime => now - firstSeenTime < RemotePlayerJoinGraceSeconds);
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
		return TryGetGuestFogSyncOverrideSize(out float overrideSize) ? overrideSize : _orbFogHandler.currentSize;
	}

	private bool TryGetGuestFogSyncOverrideSize(out float overrideSize)
	{
		overrideSize = 0f;
		if (_orbFogHandler == null)
		{
			return false;
		}
		if (!TryGetActiveFogSyncReference(out Vector3 customFogPoint, out Segment fogSegment))
		{
			return false;
		}
		if (!TryResolveFogOriginPoint(_orbFogHandler.currentID, out Vector3 vanillaFogPoint))
		{
			return false;
		}
		if (Vector3.Distance(vanillaFogPoint, customFogPoint) <= 0.05f)
		{
			return false;
		}
		if (!TryResolveFogStageCoverageAnchor(fogSegment, out Vector3 syncReferencePoint, out _))
		{
			return false;
		}
		float hostReferenceDistance = Vector3.Distance(customFogPoint, syncReferencePoint);
		float vanillaReferenceDistance = Vector3.Distance(vanillaFogPoint, syncReferencePoint);
		overrideSize = Mathf.Clamp(vanillaReferenceDistance + (_orbFogHandler.currentSize - hostReferenceDistance), FogArrivalStopSize, 6000f);
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
		if (origins.Length <= 0)
		{
			return false;
		}
		int resolvedOriginId = Mathf.Clamp(originId, 0, origins.Length - 1);
		FogSphereOrigin origin = origins[resolvedOriginId] ?? origins.LastOrDefault(candidate => candidate != null);
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
			return;
		}
		if (HasRemotePlayersInJoinGracePeriod())
		{
			return;
		}
		PhotonView photonView = _orbFogHandler.GetComponent<PhotonView>();
		if (photonView == null)
		{
			return;
		}
		try
		{
			float syncSize = GetFogStateSyncSizeForGuests();
			photonView.RPC("RPC_InitFog", RpcTarget.Others, new object[]
			{
				_orbFogHandler.currentID,
				syncSize,
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
		if (HasRemotePlayersInJoinGracePeriod())
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
		if (HasRemotePlayersInJoinGracePeriod())
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
				photonView.RPC("RPC_InitFog", RpcTarget.Others, new object[]
				{
					_orbFogHandler.currentID,
					syncSize,
					_orbFogHandler.hasArrived,
					_orbFogHandler.isMoving
				});
			}
			else
			{
				photonView.RPC("RPCA_SyncFog", RpcTarget.Others, new object[]
				{
					syncSize,
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
			_remoteFogSporesSuppressionDebt.Clear();
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
		int[] staleSporeKeys = _remoteFogSporesSuppressionDebt.Keys.Where(key => !activeKeys.Contains(key)).ToArray();
		foreach (int staleKey in staleSporeKeys)
		{
			_remoteFogSporesSuppressionDebt.Remove(staleKey);
		}
	}

	private bool ShouldDisableNightColdInCurrentStage()
	{
		return !IsNightColdFeatureEnabled()
			&& IsNightColdStageActive()
			&& TryIsNightTime(out _);
	}

	private bool IsNightColdStageActive()
	{
		if (!LoadingScreenHandler.loading && IsGameplayFogScene(SceneManager.GetActiveScene()))
		{
			return true;
		}
		return _activeSyntheticFogSegmentId >= 0 || GameHandler.IsOnIsland;
	}

	private bool ShouldSuppressConfiguredNightCold(Character character)
	{
		return character != null
			&& character.data != null
			&& !character.data.dead
			&& !character.isBot
			&& !character.isZombie
			&& !IsNightColdFeatureEnabled()
			&& IsNightColdStageActive()
			&& TryIsNightTime(out _);
	}

	private static bool TryIsNightTime(out float currentNormalizedTime)
	{
		currentNormalizedTime = 0f;
		DayNightManager dayNightManager = DayNightManager.instance ?? UnityEngine.Object.FindAnyObjectByType<DayNightManager>();
		if (dayNightManager == null)
		{
			return false;
		}
		if (TryGetIsDayFactor(dayNightManager, out float isDayFactor))
		{
			currentNormalizedTime = Mathf.Clamp01(isDayFactor);
			return isDayFactor < 0.5f;
		}
		if (!TryGetCurrentDayNightTimeNormalized(dayNightManager, out currentNormalizedTime))
		{
			return false;
		}
		float dayStart = Mathf.Repeat(dayNightManager.dayStart, 1f);
		float dayEnd = Mathf.Repeat(dayNightManager.dayEnd, 1f);
		bool isDay = IsTimeWithinWrappedRange(currentNormalizedTime, dayStart, dayEnd);
		return !isDay;
	}

	private static bool TryGetIsDayFactor(DayNightManager dayNightManager, out float isDayFactor)
	{
		isDayFactor = 0f;
		if (dayNightManager == null)
		{
			return false;
		}
		Type managerType = dayNightManager.GetType();
		PropertyInfo isDayProperty = managerType.GetProperty("isDay", InstanceBindingFlags);
		if (TryReadSingleValue(isDayProperty?.GetValue(dayNightManager), out float propertyValue))
		{
			isDayFactor = Mathf.Clamp01(propertyValue);
			return true;
		}
		FieldInfo isDayField = managerType.GetField("isDay", InstanceBindingFlags);
		if (TryReadSingleValue(isDayField?.GetValue(dayNightManager), out float fieldValue))
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
			return value >= rangeStart && value < rangeEnd;
		}
		return value >= rangeStart || value < rangeEnd;
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
			PropertyInfo property = managerType.GetProperty(memberName, InstanceBindingFlags);
			if (TryReadSingleValue(property?.GetValue(dayNightManager), out float propertyValue))
			{
				currentNormalizedTime = Mathf.Repeat(propertyValue, 1f);
				return true;
			}
			FieldInfo field = managerType.GetField(memberName, InstanceBindingFlags);
			if (TryReadSingleValue(field?.GetValue(dayNightManager), out float fieldValue))
			{
				currentNormalizedTime = Mathf.Repeat(fieldValue, 1f);
				return true;
			}
		}
		return false;
	}

	private static bool TryReadSingleValue(object rawValue, out float value)
	{
		value = 0f;
		switch (rawValue)
		{
		case bool boolValue:
			value = boolValue ? 1f : 0f;
			return true;
		case float floatValue:
			value = floatValue;
			return true;
		case double doubleValue:
			value = (float)doubleValue;
			return true;
		case int intValue:
			value = intValue;
			return true;
		default:
			return false;
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
			&& !character.data.dead
			&& !HasRemoteFogSuppressionSupport(character.photonView.Owner)
			&& (Instance == null || Instance.IsCharacterPastJoinGrace(character));
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
		float pendingColdDebt = _remoteFogSuppressionDebt.TryGetValue(playerKey, out float storedColdDebt) ? storedColdDebt : 0f;
		float pendingSporeDebt = _remoteFogSporesSuppressionDebt.TryGetValue(playerKey, out float storedSporeDebt) ? storedSporeDebt : 0f;
		if (TryGetCurrentStatusSuppressionRates(character, out float coldRatePerSecond, out float sporeRatePerSecond))
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
			// Remote affliction mirrors can lag behind the owning client after game updates,
			// so spend suppression debt directly instead of waiting for the host mirror.
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
			if (sporeTransferAmount > 0f && (int)CharacterAfflictions.STATUSTYPE.Spores < payload.Length)
			{
				payload[(int)CharacterAfflictions.STATUSTYPE.Spores] -= sporeTransferAmount;
				hasPayload = true;
			}
		}
		else
		{
			_remoteFogSporesSuppressionDebt.Remove(playerKey);
		}
		return hasPayload ? payload : null;
	}

	private bool TryGetCurrentStatusSuppressionRates(Character character, out float coldRatePerSecond, out float sporeRatePerSecond)
	{
		coldRatePerSecond = 0f;
		sporeRatePerSecond = 0f;
		if (!ShouldSuppressFogColdDamage() || character?.data == null)
		{
			return false;
		}
		float fogBaseRatePerSecond = 0f;
		float fogSporeRatePerSecond = 0f;
		if (IsCharacterInsideFogSphere(character))
		{
			fogBaseRatePerSecond += FogColdPerSecond;
			fogSporeRatePerSecond += FogSporesPerSecond;
		}
		if (IsCharacterInsideLegacyFog(character, out float legacyRate))
		{
			fogBaseRatePerSecond += legacyRate;
			fogSporeRatePerSecond = Mathf.Max(fogSporeRatePerSecond, legacyRate > 0f ? legacyRate : FogSporesPerSecond);
		}
		if (fogBaseRatePerSecond > 0f)
		{
			coldRatePerSecond += ConvertColdBaseRateToSuppressionRate(character, fogBaseRatePerSecond) * RemoteFogSuppressionSafetyMultiplier;
		}
		if (fogSporeRatePerSecond > 0f)
		{
			sporeRatePerSecond += fogSporeRatePerSecond * RemoteFogSuppressionSafetyMultiplier;
		}
		if (ShouldSuppressConfiguredNightCold(character))
		{
			coldRatePerSecond += ConvertColdBaseRateToSuppressionRate(character, NightColdPerSecond);
		}
		return coldRatePerSecond > 0f || sporeRatePerSecond > 0f;
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
		foreach (Player player2 in players)
		{
			if (player2 != null && yieldedPlayerIds.Add(player2.GetInstanceID()))
			{
				yield return player2;
			}
		}
	}

	private void GrantCompassToAllPlayers(string reason)
	{
		if (!IsModFeatureEnabled() || !HasFogAuthority() || !IsCompassFeatureEnabled())
		{
			return;
		}
		_totalCompassGrantCount++;
		Logger.LogDebug($"[{PluginName}] Queued compass grant #{_totalCompassGrantCount} ({reason}).");
		SyncCompassGrantsToPlayers(force: true, reason);
	}

	private void SyncCompassGrantsToPlayersIfNeeded()
	{
		if (!IsModFeatureEnabled() || !HasFogAuthority() || !IsCompassFeatureEnabled() || _totalCompassGrantCount <= 0)
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
		if (!IsModFeatureEnabled() || !HasFogAuthority() || !IsCompassFeatureEnabled() || _totalCompassGrantCount <= 0)
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
		Dictionary<int, Player> playersByActorNumber = new Dictionary<int, Player>();
		foreach (Player player in EnumerateTargetPlayers())
		{
			if (!IsPlayerPastJoinGrace(player))
			{
				continue;
			}
			int playerKey = GetPlayerCompassGrantKey(player);
			activePlayerKeys.Add(playerKey);
			PhotonView playerPhotonView = player.GetComponent<PhotonView>();
			int actorNumber = playerPhotonView?.Owner?.ActorNumber ?? -1;
			if (actorNumber > 0 && !playersByActorNumber.ContainsKey(actorNumber))
			{
				playersByActorNumber[actorNumber] = player;
			}
			int deliveredCount = GetDeliveredCompassGrantCount(player);
			int grantsIssuedThisPass = 0;
			while (deliveredCount < _totalCompassGrantCount)
			{
				if (!GrantCompassToPlayer(player, $"{reason}-{deliveredCount + 1}/{_totalCompassGrantCount}"))
				{
					break;
				}
				deliveredCount++;
				grantsIssuedThisPass++;
				if (grantsIssuedThisPass >= MaxCompassGrantsPerPlayerPerSync)
				{
					break;
				}
			}
			_playerCompassGrantCounts[playerKey] = deliveredCount;
		}
		TrySyncCompassGrantsToMissingActorPlayers(reason, activePlayerKeys, playersByActorNumber);
		int[] staleKeys = _playerCompassGrantCounts.Keys.Where(key => !activePlayerKeys.Contains(key)).ToArray();
		foreach (int staleKey in staleKeys)
		{
			_playerCompassGrantCounts.Remove(staleKey);
		}
	}

	private void TrySyncCompassGrantsToMissingActorPlayers(string reason, HashSet<int> activePlayerKeys, Dictionary<int, Player> playersByActorNumber)
	{
		if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
		{
			return;
		}
		int localActorNumber = PhotonNetwork.LocalPlayer?.ActorNumber ?? -1;
		foreach (Photon.Realtime.Player roomPlayer in PhotonNetwork.CurrentRoom.Players.Values.OrderBy(player => player?.ActorNumber ?? int.MaxValue))
		{
			int actorNumber = roomPlayer?.ActorNumber ?? -1;
			if (actorNumber <= 0 || actorNumber == localActorNumber)
			{
				continue;
			}
			if (playersByActorNumber.ContainsKey(actorNumber))
			{
				continue;
			}
			activePlayerKeys.Add(actorNumber);
			if (!IsActorPastJoinGrace(actorNumber))
			{
				continue;
			}
			int deliveredCount = GetDeliveredCompassGrantCountForActor(actorNumber);
			int grantsIssuedThisPass = 0;
			while (deliveredCount < _totalCompassGrantCount)
			{
				if (!GrantCompassToActor(actorNumber, $"{reason}-{deliveredCount + 1}/{_totalCompassGrantCount}"))
				{
					break;
				}
				deliveredCount++;
				grantsIssuedThisPass++;
				if (grantsIssuedThisPass >= MaxCompassGrantsPerPlayerPerSync)
				{
					break;
				}
			}
			_playerCompassGrantCounts[actorNumber] = deliveredCount;
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

	private int GetDeliveredCompassGrantCount(Player player)
	{
		int playerKey = GetPlayerCompassGrantKey(player);
		if (_playerCompassGrantCounts.TryGetValue(playerKey, out int deliveredCount))
		{
			return deliveredCount;
		}
		PhotonView photonView = player != null ? player.GetComponent<PhotonView>() : null;
		if (photonView?.Owner != null && _remotePlayerCompassBaselineCounts.TryGetValue(photonView.Owner.ActorNumber, out int baselineCount))
		{
			return baselineCount;
		}
		return 0;
	}

	private int GetDeliveredCompassGrantCountForActor(int actorNumber)
	{
		if (_playerCompassGrantCounts.TryGetValue(actorNumber, out int deliveredCount))
		{
			return deliveredCount;
		}
		if (_remotePlayerCompassBaselineCounts.TryGetValue(actorNumber, out int baselineCount))
		{
			return baselineCount;
		}
		return 0;
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
		if (!_remotePlayerFirstSeenTimes.TryGetValue(photonView.Owner.ActorNumber, out float firstSeenTime))
		{
			return true;
		}
		return Time.unscaledTime - firstSeenTime >= RemotePlayerJoinGraceSeconds;
	}

	private bool IsActorPastJoinGrace(int actorNumber)
	{
		if (actorNumber <= 0)
		{
			return false;
		}
		int localActorNumber = PhotonNetwork.LocalPlayer?.ActorNumber ?? -1;
		if (actorNumber == localActorNumber)
		{
			return true;
		}
		if (!_remotePlayerFirstSeenTimes.TryGetValue(actorNumber, out float firstSeenTime))
		{
			return true;
		}
		return Time.unscaledTime - firstSeenTime >= RemotePlayerJoinGraceSeconds;
	}

	private void ProcessPendingCampfireCompassGrants()
	{
		if (!IsModFeatureEnabled() || !HasFogAuthority() || !IsCompassFeatureEnabled() || _pendingCampfireCompassGrantTimes.Count <= 0)
		{
			return;
		}
		if (LoadingScreenHandler.loading)
		{
			return;
		}
		float now = Time.unscaledTime;
		int[] readyCampfireIds = _pendingCampfireCompassGrantTimes
			.Where(entry => now >= entry.Value)
			.Select(entry => entry.Key)
			.ToArray();
		foreach (int campfireId in readyCampfireIds)
		{
			_pendingCampfireCompassGrantTimes.Remove(campfireId);
			Logger.LogInfo($"[{PluginName}] Delivering delayed campfire compass grant for campfire #{campfireId}.");
			GrantCompassToAllPlayers($"campfire-{campfireId}");
		}
	}

	private bool GrantCompassToPlayer(Player player, string reason)
	{
		if (player == null || _compassItem == null || !IsPlayerPastJoinGrace(player))
		{
			return false;
		}
		PhotonView photonView = player.GetComponent<PhotonView>();
		int actorNumber = photonView?.Owner?.ActorNumber ?? -1;
		if (photonView?.Owner != null && !photonView.IsMine)
		{
			Character remoteCharacter = player.character;
			if (remoteCharacter == null || remoteCharacter.data == null || remoteCharacter.data.dead)
			{
				remoteCharacter = Character.AllCharacters.FirstOrDefault((Character character) =>
					character != null
					&& character.photonView != null
					&& character.photonView.Owner != null
					&& character.photonView.Owner.ActorNumber == actorNumber
					&& !character.isBot
					&& !character.isZombie
					&& character.data != null
					&& !character.data.dead);
			}
			if (remoteCharacter != null)
			{
				return DropCompassInFrontOfCharacter(remoteCharacter, actorNumber, reason);
			}
		}
		return DropCompassInFrontOfPlayer(player, reason, preferLocalViewAnchor: true);
	}

	private bool GrantCompassToActor(int actorNumber, string reason)
	{
		if (actorNumber <= 0 || _compassItem == null || !IsActorPastJoinGrace(actorNumber))
		{
			return false;
		}
		Player actorPlayer = EnumerateTargetPlayers().FirstOrDefault((Player player) =>
		{
			PhotonView photonView = player != null ? player.GetComponent<PhotonView>() : null;
			return photonView?.Owner?.ActorNumber == actorNumber;
		});
		if (actorPlayer != null)
		{
			return GrantCompassToPlayer(actorPlayer, reason);
		}
		Character actorCharacter = Character.AllCharacters.FirstOrDefault((Character character) =>
			character != null
			&& character.photonView != null
			&& character.photonView.Owner != null
			&& character.photonView.Owner.ActorNumber == actorNumber
			&& !character.isBot
			&& !character.isZombie
			&& character.data != null
			&& !character.data.dead);
		if (actorCharacter == null)
		{
			return false;
		}
		return DropCompassInFrontOfCharacter(actorCharacter, actorNumber, reason);
	}

	private bool DropCompassInFrontOfPlayer(Player player, string reason)
	{
		return DropCompassInFrontOfPlayer(player, reason, preferLocalViewAnchor: false);
	}

	private bool DropCompassInFrontOfPlayer(Player player, string reason, bool preferLocalViewAnchor)
	{
		if (player == null || _compassItem == null)
		{
			return false;
		}
		ResolveCompassSpawnPose(player, preferLocalViewAnchor, out Vector3 spawnPosition, out Quaternion spawnRotation);
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

	private bool DropCompassInFrontOfCharacter(Character character, int actorNumber, string reason)
	{
		if (character == null || _compassItem == null)
		{
			return false;
		}
		ResolveCompassSpawnPose(character, out Vector3 spawnPosition, out Quaternion spawnRotation);
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
			Logger.LogDebug($"[{PluginName}] Spawned compass near actor #{actorNumber} ({reason}).");
			return true;
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"[{PluginName}] Failed to spawn compass near actor #{actorNumber} ({reason}): {ex.Message}");
			return false;
		}
	}

	private static Vector3 GetCompassDropPosition(Player player)
	{
		ResolveCompassSpawnPose(player, out Vector3 spawnPosition, out _);
		return spawnPosition;
	}

	private static Quaternion GetCompassDropRotation(Player player)
	{
		ResolveCompassSpawnPose(player, out _, out Quaternion spawnRotation);
		return spawnRotation;
	}

	private static void ResolveCompassSpawnPose(Player player, out Vector3 spawnPosition, out Quaternion spawnRotation)
	{
		ResolveCompassSpawnPose(player, preferLocalViewAnchor: false, out spawnPosition, out spawnRotation);
	}

	private static void ResolveCompassSpawnPose(Player player, bool preferLocalViewAnchor, out Vector3 spawnPosition, out Quaternion spawnRotation)
	{
		if (preferLocalViewAnchor && TryResolveLocalViewCompassSpawnPose(player, out spawnPosition, out spawnRotation))
		{
			return;
		}
		Vector3 anchorPosition = ResolveCompassFaceAnchorPosition(player);
		Vector3 rawForward = ResolveCompassFacingForward(player);
		if (rawForward.sqrMagnitude < 0.01f)
		{
			rawForward = Vector3.forward;
		}
		rawForward = rawForward.normalized;
		Vector3 flatForward = Vector3.ProjectOnPlane(rawForward, Vector3.up);
		if (flatForward.sqrMagnitude < 0.01f)
		{
			flatForward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up);
		}
		if (flatForward.sqrMagnitude < 0.01f)
		{
			flatForward = Vector3.forward;
		}
		flatForward = flatForward.normalized;
		spawnPosition = anchorPosition + rawForward * 0.85f;
		spawnRotation = Quaternion.LookRotation(flatForward, Vector3.up);
	}

	private static void ResolveCompassSpawnPose(Character character, out Vector3 spawnPosition, out Quaternion spawnRotation)
	{
		Vector3 anchorPosition = character.Center + Vector3.up * 0.5f;
		Vector3 rawForward = character.transform.forward;
		if (rawForward.sqrMagnitude < 0.01f)
		{
			rawForward = Vector3.forward;
		}
		rawForward = rawForward.normalized;
		Vector3 flatForward = Vector3.ProjectOnPlane(rawForward, Vector3.up);
		if (flatForward.sqrMagnitude < 0.01f)
		{
			flatForward = Vector3.forward;
		}
		flatForward = flatForward.normalized;
		spawnPosition = anchorPosition + rawForward * 0.85f;
		spawnRotation = Quaternion.LookRotation(flatForward, Vector3.up);
	}

	private static bool TryResolveLocalViewCompassSpawnPose(Player player, out Vector3 spawnPosition, out Quaternion spawnRotation)
	{
		spawnPosition = default;
		spawnRotation = Quaternion.identity;
		Player localPlayer = Player.localPlayer ?? Character.localCharacter?.player;
		if (player == null || localPlayer == null || player != localPlayer)
		{
			return false;
		}
		Camera viewCamera = Camera.main;
		if (viewCamera == null || !viewCamera.isActiveAndEnabled)
		{
			viewCamera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault((Camera candidate) => candidate != null && candidate.isActiveAndEnabled && candidate.gameObject.activeInHierarchy);
		}
		if (viewCamera == null)
		{
			return false;
		}
		Transform cameraTransform = viewCamera.transform;
		Vector3 rawForward = cameraTransform.forward;
		if (rawForward.sqrMagnitude < 0.01f)
		{
			return false;
		}
		rawForward = rawForward.normalized;
		Vector3 flatForward = Vector3.ProjectOnPlane(rawForward, Vector3.up);
		if (flatForward.sqrMagnitude < 0.01f)
		{
			flatForward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up);
		}
		if (flatForward.sqrMagnitude < 0.01f)
		{
			flatForward = Vector3.forward;
		}
		flatForward = flatForward.normalized;
		spawnPosition = cameraTransform.position + rawForward * 0.9f;
		spawnRotation = Quaternion.LookRotation(flatForward, Vector3.up);
		return true;
	}

	private static Vector3 ResolveCompassFaceAnchorPosition(Player player)
	{
		if (TryGetCompassFaceAnchor(player, out Transform anchor))
		{
			Vector3 offset = anchor.name.IndexOf("camera", StringComparison.OrdinalIgnoreCase) >= 0 ? Vector3.down * 0.03f : Vector3.zero;
			return anchor.position + offset;
		}
		Character character = player.character;
		if (character != null)
		{
			Vector3 center = character.Center;
			return new Vector3(center.x, center.y + 0.5f, center.z);
		}
		return player.transform.position + Vector3.up * 1.45f;
	}

	private static Vector3 ResolveCompassFacingForward(Player player)
	{
		if (TryGetCompassFaceAnchor(player, out Transform anchor) && ShouldUseCompassAnchorForward(anchor))
		{
			Vector3 anchorForward = anchor.forward;
			if (anchorForward.sqrMagnitude >= 0.01f)
			{
				return anchorForward;
			}
		}
		Character character = player.character;
		if (character != null && character.transform.forward.sqrMagnitude >= 0.01f)
		{
			return character.transform.forward;
		}
		if (player.transform.forward.sqrMagnitude >= 0.01f)
		{
			return player.transform.forward;
		}
		if (TryGetCompassFaceAnchor(player, out anchor) && anchor.forward.sqrMagnitude >= 0.01f)
		{
			return anchor.forward;
		}
		return Vector3.forward;
	}

	private static bool TryGetCompassFaceAnchor(Player player, out Transform anchor)
	{
		anchor = null;
		if (player == null)
		{
			return false;
		}
		Transform[] roots = player.character != null && player.character.transform != player.transform
			? new Transform[2] { player.character.transform, player.transform }
			: new Transform[1] { player.transform };
		int bestScore = int.MaxValue;
		float bestHeight = float.MinValue;
		foreach (Transform root in roots)
		{
			if (root == null)
			{
				continue;
			}
			Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
			foreach (Transform candidate in transforms)
			{
				int score = GetCompassFaceAnchorScore(candidate);
				if (score == int.MaxValue)
				{
					continue;
				}
				float height = candidate.position.y;
				if (score < bestScore || (score == bestScore && height > bestHeight))
				{
					bestScore = score;
					bestHeight = height;
					anchor = candidate;
				}
			}
		}
		return anchor != null;
	}

	private static int GetCompassFaceAnchorScore(Transform candidate)
	{
		if (candidate == null)
		{
			return int.MaxValue;
		}
		string name = candidate.name ?? string.Empty;
		if (name.Equals("MainCamera", StringComparison.OrdinalIgnoreCase))
		{
			return 0;
		}
		if (name.IndexOf("maincamera", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return 1;
		}
		if (name.IndexOf("camera", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return 2;
		}
		if (name.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return 3;
		}
		if (name.IndexOf("face", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return 4;
		}
		if (name.IndexOf("look", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return 5;
		}
		if (name.IndexOf("jaw", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return 6;
		}
		return int.MaxValue;
	}

	private static bool ShouldUseCompassAnchorForward(Transform anchor)
	{
		if (anchor == null)
		{
			return false;
		}
		string name = anchor.name ?? string.Empty;
		return name.IndexOf("camera", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("look", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private void HandleManualCompassHotkey()
	{
		if (!IsModFeatureEnabled() || !HasFogAuthority() || !IsCompassFeatureEnabled())
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
		if (instance != null && instance.windowBlockingInput)
		{
			return;
		}
		if (!Input.GetKeyDown(hotkey))
		{
			return;
		}
		_fogPaused = !_fogPaused;
		if (_fogPaused)
		{
			ApplyPausedFogState(syncImmediately: true);
			Logger.LogInfo($"[{PluginName}] Fog paused by hotkey {hotkey}.");
			return;
		}
		Logger.LogInfo($"[{PluginName}] Fog resumed by hotkey {hotkey}.");
		if (_orbFogHandler != null && _initialDelayCompleted && !ShouldHoldFogUntilCampfireActivation(_orbFogHandler) && !_orbFogHandler.isMoving)
		{
			StartFogMovement();
		}
		ForceSyncFogStateToGuests();
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
			Logger.LogInfo($"[{PluginName}] Hidden night-test hotkey forced night at {nightTime:F3}{(PhotonNetwork.InRoom ? " and synced it to guests." : ".")}");
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
		if (!IsCompassFeatureEnabled())
		{
			return;
		}
		if (!TryResolveCompassItem())
		{
			Logger.LogWarning($"[{PluginName}] Failed to resolve compass item for manual hotkey spawn.");
			return;
		}
		if (!TryGetLocalPlayablePlayer(out Player localPlayer))
		{
			return;
		}
		if (!DropCompassInFrontOfPlayer(localPlayer, "manual-hotkey", preferLocalViewAnchor: true))
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
		bool campfireLocatorUiEnabled = CampfireLocatorUiEnabled?.Value ?? true;
		float fogUiX = FogUiX?.Value ?? DefaultFogUiX;
		float fogUiY = FogUiY?.Value ?? DefaultFogUiY;
		float fogUiScale = FogUiScale?.Value ?? DefaultFogUiScale;
		if (fogUiEnabled == _lastFogUiEnabledState
			&& campfireLocatorUiEnabled == _lastCampfireLocatorUiEnabledState
			&& Approximately(fogUiX, _lastFogUiX)
			&& Approximately(fogUiY, _lastFogUiY)
			&& Approximately(fogUiScale, _lastFogUiScale))
		{
			return;
		}
		_lastFogUiEnabledState = fogUiEnabled;
		_lastCampfireLocatorUiEnabledState = campfireLocatorUiEnabled;
		_lastFogUiX = fogUiX;
		_lastFogUiY = fogUiY;
		_lastFogUiScale = fogUiScale;
		CreateFogUi();
		CreateCampfireLocatorUi();
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
			MarkConfigFileLocalizationDirty(saveConfigFile: true);
			if (_fogUiText != null)
			{
				ApplyGameTextStyle(_fogUiText, Color.white);
			}
			if (_compassLobbyNoticeText != null)
			{
				ApplyCompassLobbyNoticeStyle(_compassLobbyNoticeText);
			}
			RefreshFogUiEntryStyles();
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
			if (string.Equals(keyName, GetConfigKeyName(candidate), StringComparison.OrdinalIgnoreCase) || string.Equals(keyName, GetKeyName(candidate, isChineseLanguage: true), StringComparison.OrdinalIgnoreCase))
			{
				configKey = candidate;
				return true;
			}
		}
		configKey = default;
		return false;
	}

	private void TryRefreshLocalizedConfigFile(bool isChineseLanguage, bool saveConfigFile)
	{
		try
		{
			if (Config == null || string.IsNullOrWhiteSpace(Config.ConfigFilePath))
			{
				return;
			}
			if (saveConfigFile)
			{
				Config.Save();
			}
			RewriteConfigFileLocalization(Config.ConfigFilePath, isChineseLanguage);
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"[{PluginName}] Failed to refresh localized config file: {ex.Message}");
		}
	}

	private static void RewriteConfigFileLocalization(string configFilePath, bool isChineseLanguage)
	{
		if (string.IsNullOrWhiteSpace(configFilePath) || !File.Exists(configFilePath))
		{
			return;
		}
		string[] originalLines = File.ReadAllLines(configFilePath);
		string[] rewrittenLines = new string[originalLines.Length];
		bool changed = false;
		for (int i = 0; i < originalLines.Length; i++)
		{
			string line = originalLines[i] ?? string.Empty;
			string rewrittenLine = RewriteConfigFileLine(line, isChineseLanguage);
			rewrittenLines[i] = rewrittenLine;
			if (!string.Equals(line, rewrittenLine, StringComparison.Ordinal))
			{
				changed = true;
			}
		}
		if (!changed)
		{
			return;
		}
		File.WriteAllLines(configFilePath, rewrittenLines);
	}

	private static string RewriteConfigFileLine(string line, bool isChineseLanguage)
	{
		if (string.IsNullOrWhiteSpace(line))
		{
			return line ?? string.Empty;
		}
		string trimmed = line.Trim();
		if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
		{
			string sectionName = trimmed.Substring(1, trimmed.Length - 2).Trim();
			if (!TryGetLocalizedSectionName(sectionName, isChineseLanguage, out string localizedSectionName))
			{
				return line;
			}
			int openIndex = line.IndexOf('[');
			int closeIndex = line.LastIndexOf(']');
			if (openIndex < 0 || closeIndex < openIndex)
			{
				return line;
			}
			return line.Substring(0, openIndex + 1) + localizedSectionName + line.Substring(closeIndex);
		}
		if (trimmed.StartsWith("#", StringComparison.Ordinal) || trimmed.StartsWith(";", StringComparison.Ordinal))
		{
			return line;
		}
		int equalsIndex = line.IndexOf('=');
		if (equalsIndex <= 0)
		{
			return line;
		}
		int keyStart = 0;
		while (keyStart < equalsIndex && char.IsWhiteSpace(line[keyStart]))
		{
			keyStart++;
		}
		int keyEnd = equalsIndex - 1;
		while (keyEnd >= keyStart && char.IsWhiteSpace(line[keyEnd]))
		{
			keyEnd--;
		}
		if (keyEnd < keyStart)
		{
			return line;
		}
		string keyName = line.Substring(keyStart, keyEnd - keyStart + 1);
		if (!TryGetConfigKey(keyName, out ConfigKey configKey))
		{
			return line;
		}
		string localizedKeyName = GetKeyName(configKey, isChineseLanguage);
		return line.Substring(0, keyStart) + localizedKeyName + line.Substring(keyEnd + 1);
	}

	private static bool TryGetLocalizedSectionName(string sectionName, bool isChineseLanguage, out string localizedSectionName)
	{
		if (MatchesAdjustmentSectionName(sectionName))
		{
			localizedSectionName = GetSectionName(ConfigKey.FogUiX, isChineseLanguage);
			return true;
		}
		if (MatchesBasicSectionName(sectionName))
		{
			localizedSectionName = GetSectionName(ConfigKey.ModEnabled, isChineseLanguage);
			return true;
		}
		localizedSectionName = string.Empty;
		return false;
	}

	private static bool MatchesBasicSectionName(string sectionName)
	{
		return string.Equals(sectionName, CanonicalBasicConfigSectionName, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(sectionName, GetSectionName(ConfigKey.ModEnabled, isChineseLanguage: true), StringComparison.Ordinal)
			|| string.Equals(sectionName, GetLegacyConfigSectionName(), StringComparison.OrdinalIgnoreCase)
			|| string.Equals(sectionName, GetLegacySectionName(isChineseLanguage: true), StringComparison.Ordinal);
	}

	private static bool MatchesAdjustmentSectionName(string sectionName)
	{
		return string.Equals(sectionName, CanonicalAdjustmentConfigSectionName, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(sectionName, GetSectionName(ConfigKey.FogUiX, isChineseLanguage: true), StringComparison.Ordinal);
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
		AddUiLocalizationPair(map, "Fog Climb", PluginName);
		AddUiLocalizationPair(map, "FogClimb", PluginName);
		AddUiLocalizationPair(map, "Fog&ColdControl", PluginName);
		AddUiLocalizationPair(map, "FogAndColdControl", PluginName);
		AddUiLocalizationPair(map, "\u6BD2\u96FE\u6500\u767B", GetLocalizedModDisplayName(isChineseLanguage));
		foreach (string sectionName in Enum.GetValues(typeof(ConfigKey))
			.Cast<ConfigKey>()
			.Select(configKey => GetConfigSectionName(configKey))
			.Concat(Enum.GetValues(typeof(ConfigKey)).Cast<ConfigKey>().Select(configKey => GetSectionName(configKey, isChineseLanguage: true)))
			.Concat(new[] { GetLegacyConfigSectionName(), GetLegacySectionName(isChineseLanguage: true) })
			.Distinct(StringComparer.Ordinal))
		{
			if (string.Equals(sectionName, GetLegacyConfigSectionName(), StringComparison.Ordinal) || string.Equals(sectionName, GetLegacySectionName(isChineseLanguage: true), StringComparison.Ordinal))
			{
				AddUiLocalizationPair(map, sectionName, GetSectionName(ConfigKey.ModEnabled, isChineseLanguage));
				continue;
			}

			ConfigKey matchedKey = Enum.GetValues(typeof(ConfigKey))
				.Cast<ConfigKey>()
				.FirstOrDefault(candidate => string.Equals(sectionName, GetConfigSectionName(candidate), StringComparison.OrdinalIgnoreCase)
					|| string.Equals(sectionName, GetSectionName(candidate, isChineseLanguage: true), StringComparison.Ordinal));
			AddUiLocalizationPair(map, sectionName, GetSectionName(matchedKey, isChineseLanguage));
		}
		foreach (ConfigKey configKey in Enum.GetValues(typeof(ConfigKey)))
		{
			AddUiLocalizationPair(map, GetConfigKeyName(configKey), GetKeyName(configKey, isChineseLanguage));
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
		return isChineseLanguage ? $"\u6309 {keyText} \u751F\u6210\u6307\u5357\u9488" : $"Press {keyText} to spawn compass";
	}

	private bool ShouldShowCompassLobbyNotice()
	{
		return IsModFeatureEnabled()
			&& HasFogAuthority()
			&& IsCompassFeatureEnabled()
			&& GetCompassHotkey() != KeyCode.None
			&& IsAirportScene(SceneManager.GetActiveScene());
	}

	private string GetCompassLobbyNoticeTextSafe(bool isChineseLanguage)
	{
		string keyText = $"<color={CompassLobbyNoticeKeyColor}>{GetCompassHotkeyLabel()}</color>";
		return isChineseLanguage ? $"\u6309 {keyText} \u751F\u6210\u6307\u5357\u9488" : $"Press {keyText} to spawn compass";
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
		string noticeText = GetCompassLobbyNoticeTextSafe(DetectChineseLanguage());
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
		GameObject noticeObject = new GameObject("FogClimbCompassLobbyNotice", typeof(RectTransform));
		_compassLobbyNoticeRect = noticeObject.GetComponent<RectTransform>();
		_compassLobbyNoticeRect.SetParent(canvas.transform, false);
		_compassLobbyNoticeRect.SetAsLastSibling();
		_compassLobbyNoticeRect.sizeDelta = new Vector2(CompassLobbyNoticeWidth, CompassLobbyNoticeHeight);
		_compassLobbyNoticeText = noticeObject.AddComponent<TextMeshProUGUI>();
		ApplyCompassLobbyNoticeStyle(_compassLobbyNoticeText);
		_lastCompassLobbyNoticeText = GetCompassLobbyNoticeTextSafe(DetectChineseLanguage());
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
		if (LoadingScreenHandler.loading || IsFogUiBlockedByOverlay() || !IsFogUiSceneAllowed() || ShouldHideFogUiForCurrentStage())
		{
			return false;
		}
		Canvas canvas = targetCanvas ?? ResolveHudCanvas();
		return IsCanvasUsable(canvas);
	}

	private bool ShouldHideFogUiForCurrentStage()
	{
		if (!IsGameplayFogScene(SceneManager.GetActiveScene()) || LoadingScreenHandler.loading)
		{
			return false;
		}
		if (TryGetCurrentGameplaySegment(out Segment segment))
		{
			return segment >= Segment.Caldera;
		}
		return _activeSyntheticFogSegmentId >= (int)Segment.Caldera;
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
			return false;
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
		if (_fogUiRect == null || _fogUiText == null || _fogUiEntriesRect == null || !IsCanvasUsable(targetCanvas))
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

	private bool TryResolveNextCampfireTarget(out Vector3 targetPosition)
	{
		targetPosition = Vector3.zero;
		if (!IsGameplayFogScene(SceneManager.GetActiveScene()) || LoadingScreenHandler.loading)
		{
			return false;
		}
		MapHandler mapHandler = Singleton<MapHandler>.Instance;
		if (mapHandler == null || mapHandler.segments == null || mapHandler.segments.Length <= 0)
		{
			return false;
		}
		int startIndex = Mathf.Clamp((int)MapHandler.CurrentSegmentNumber, 0, mapHandler.segments.Length - 1);
		for (int segmentIndex = startIndex; segmentIndex < mapHandler.segments.Length; segmentIndex++)
		{
			if (!TryResolveSegmentCampfire(mapHandler.segments[segmentIndex], out Campfire campfire) || campfire.Lit)
			{
				continue;
			}
			targetPosition = campfire.transform.position;
			return true;
		}
		return false;
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

	private bool IsCharacterInsideVisibleFog(Character character)
	{
		if (character == null || character.data == null || character.data.dead)
		{
			return false;
		}
		if (IsCharacterInsideFogSphere(character))
		{
			return true;
		}
		if (IsCharacterInsideLegacyFog(character, out _))
		{
			return true;
		}
		if (TryGetDisplayedFogEtaGeometry(out float currentSize, out Vector3 fogPoint, out _))
		{
			return Vector3.Distance(fogPoint, character.Center) > currentSize;
		}
		return _orbFogHandler != null
			&& _orbFogHandler.isMoving
			&& TryGetFogArrivalRemainingDistance(out float remainingDistance)
			&& remainingDistance <= 0.05f;
	}

	private bool ShouldShowCampfireLocatorUi(Canvas targetCanvas = null)
	{
		if (!IsModFeatureEnabled() || !(FogUiEnabled?.Value ?? true) || !(CampfireLocatorUiEnabled?.Value ?? true))
		{
			return false;
		}
		if (LoadingScreenHandler.loading || IsFogUiBlockedByOverlay() || !IsGameplayFogScene(SceneManager.GetActiveScene()))
		{
			return false;
		}
		Character localCharacter = Character.localCharacter;
		if (!IsCharacterInsideVisibleFog(localCharacter))
		{
			return false;
		}
		Canvas canvas = targetCanvas ?? ResolveHudCanvas();
		return IsCanvasUsable(canvas) && TryResolveNextCampfireTarget(out _);
	}

	private bool NeedsCampfireLocatorUiRebuild(Canvas targetCanvas)
	{
		if (_campfireLocatorUiRect == null || _campfireLocatorDotRect == null || !IsCanvasUsable(targetCanvas))
		{
			return true;
		}
		Canvas currentCanvas = _campfireLocatorUiRect.GetComponentInParent<Canvas>();
		return !IsCanvasUsable(currentCanvas) || _campfireLocatorUiRect.parent != targetCanvas.transform;
	}

	private void CreateCampfireLocatorUi(Canvas targetCanvas = null)
	{
		CleanupCampfireLocatorUi();
		Canvas canvas = targetCanvas ?? ResolveHudCanvas();
		if (!ShouldShowCampfireLocatorUi(canvas))
		{
			return;
		}
		try
		{
			GameObject container = new GameObject("FogClimbCampfireLocatorUI", typeof(RectTransform));
			_campfireLocatorUiRect = container.GetComponent<RectTransform>();
			_campfireLocatorUiRect.SetParent(canvas.transform, false);
			_campfireLocatorUiRect.SetAsLastSibling();
			_campfireLocatorUiRect.sizeDelta = new Vector2(CampfireLocatorUiWidth, CampfireLocatorUiHeight);

			GameObject lineObject = new GameObject("Line", typeof(RectTransform), typeof(Image));
			RectTransform lineRect = lineObject.GetComponent<RectTransform>();
			lineRect.SetParent(_campfireLocatorUiRect, false);
			lineRect.anchorMin = new Vector2(0.5f, 0.5f);
			lineRect.anchorMax = new Vector2(0.5f, 0.5f);
			lineRect.pivot = new Vector2(0.5f, 0.5f);
			lineRect.anchoredPosition = Vector2.zero;
			lineRect.sizeDelta = new Vector2(CampfireLocatorLineWidth, CampfireLocatorLineHeight);
			lineObject.GetComponent<Image>().color = CampfireLocatorLineColor;

			GameObject dotObject = new GameObject("Dot", typeof(RectTransform), typeof(Image));
			_campfireLocatorDotRect = dotObject.GetComponent<RectTransform>();
			_campfireLocatorDotRect.SetParent(_campfireLocatorUiRect, false);
			_campfireLocatorDotRect.anchorMin = new Vector2(0.5f, 0.5f);
			_campfireLocatorDotRect.anchorMax = new Vector2(0.5f, 0.5f);
			_campfireLocatorDotRect.pivot = new Vector2(0.5f, 0.5f);
			_campfireLocatorDotRect.anchoredPosition = Vector2.zero;
			_campfireLocatorDotRect.sizeDelta = new Vector2(CampfireLocatorDotSize, CampfireLocatorDotSize);
			Image dotImage = dotObject.GetComponent<Image>();
			dotImage.raycastTarget = false;
			dotImage.preserveAspect = true;
			dotImage.sprite = GetCampfireLocatorDotSprite();
			dotImage.color = Color.white;

			_campfireLocatorCurrentDotX = 0f;
			ClampCampfireLocatorUiToCanvas(canvas);
			SetCampfireLocatorUiVisible(true);
		}
		catch
		{
			CleanupCampfireLocatorUi();
		}
	}

	private void UpdateCampfireLocatorUi()
	{
		if ((FogUiEnabled != null && !FogUiEnabled.Value) || (CampfireLocatorUiEnabled != null && !CampfireLocatorUiEnabled.Value))
		{
			SetCampfireLocatorUiVisible(false);
			return;
		}
		Canvas targetCanvas = ResolveHudCanvas();
		if (!ShouldShowCampfireLocatorUi(targetCanvas))
		{
			SetCampfireLocatorUiVisible(false);
			return;
		}
		if (NeedsCampfireLocatorUiRebuild(targetCanvas))
		{
			CreateCampfireLocatorUi(targetCanvas);
			if (_campfireLocatorUiRect == null || _campfireLocatorDotRect == null)
			{
				return;
			}
		}
		if (!TryResolveNextCampfireTarget(out Vector3 targetPosition))
		{
			SetCampfireLocatorUiVisible(false);
			return;
		}
		SetCampfireLocatorUiVisible(true);
		ClampCampfireLocatorUiToCanvas(targetCanvas);
		UpdateCampfireLocatorDot(targetPosition);
	}

	private void UpdateCampfireLocatorDot(Vector3 targetPosition)
	{
		if (_campfireLocatorDotRect == null)
		{
			return;
		}
		float halfTravelDistance = Mathf.Max(0f, (CampfireLocatorLineWidth - CampfireLocatorDotSize) * 0.5f);
		float targetDotX = 0f;
		Camera viewCamera = ResolveActiveViewCamera();
		if (viewCamera != null)
		{
			Vector3 viewportPosition = viewCamera.WorldToViewportPoint(targetPosition);
			if (viewportPosition.z > 0.05f)
			{
				targetDotX = Mathf.Lerp(0f - halfTravelDistance, halfTravelDistance, Mathf.Clamp01(viewportPosition.x));
			}
			else
			{
				Vector3 toTarget = Vector3.ProjectOnPlane(targetPosition - viewCamera.transform.position, Vector3.up);
				Vector3 right = Vector3.ProjectOnPlane(viewCamera.transform.right, Vector3.up);
				float side = 0f;
				if (toTarget.sqrMagnitude >= 0.01f && right.sqrMagnitude >= 0.01f)
				{
					side = Vector3.Dot(toTarget.normalized, right.normalized);
				}
				targetDotX = side >= 0f ? halfTravelDistance : 0f - halfTravelDistance;
			}
		}
		float smoothing = 1f - Mathf.Exp(0f - CampfireLocatorDotSmoothing * Mathf.Max(Time.unscaledDeltaTime, 0f));
		_campfireLocatorCurrentDotX = Mathf.Lerp(_campfireLocatorCurrentDotX, targetDotX, smoothing);
		_campfireLocatorDotRect.anchoredPosition = new Vector2(_campfireLocatorCurrentDotX, 0f);
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
			GameObject container = new GameObject("FogClimbUI", typeof(RectTransform));
			_fogUiRect = container.GetComponent<RectTransform>();
			_fogUiRect.SetParent(canvas.transform, false);
			_fogUiRect.anchorMin = Vector2.zero;
			_fogUiRect.anchorMax = Vector2.zero;
			_fogUiRect.pivot = Vector2.zero;
			_fogUiRect.sizeDelta = new Vector2(FogUiWidth, FogUiHeight);
			_fogUiRect.SetAsLastSibling();
			GameObject labelObject = new GameObject("FogClimbUILabel", typeof(RectTransform));
			RectTransform labelRect = labelObject.GetComponent<RectTransform>();
			labelRect.SetParent(_fogUiRect, false);
			labelRect.anchorMin = Vector2.zero;
			labelRect.anchorMax = Vector2.zero;
			labelRect.pivot = Vector2.zero;
			labelRect.anchoredPosition = Vector2.zero;
			labelRect.sizeDelta = new Vector2(FogUiWidth, FogUiHeight);
			_fogUiText = labelObject.AddComponent<TextMeshProUGUI>();
			ApplyGameTextStyle(_fogUiText, Color.white);
			_fogUiText.richText = true;
			_fogUiText.alignment = TextAlignmentOptions.MidlineLeft;
			_fogUiText.margin = new Vector4(FogUiHorizontalPadding, 0f, FogUiHorizontalPadding, 0f);
			_fogUiText.text = string.Empty;
			_fogUiText.enabled = false;
			GameObject entriesObject = new GameObject("FogClimbUIEntries", typeof(RectTransform), typeof(HorizontalLayoutGroup));
			_fogUiEntriesRect = entriesObject.GetComponent<RectTransform>();
			_fogUiEntriesRect.SetParent(_fogUiRect, false);
			_fogUiEntriesRect.anchorMin = Vector2.zero;
			_fogUiEntriesRect.anchorMax = Vector2.zero;
			_fogUiEntriesRect.pivot = Vector2.zero;
			_fogUiEntriesRect.anchoredPosition = Vector2.zero;
			_fogUiEntriesRect.sizeDelta = new Vector2(FogUiWidth, FogUiHeight);
		HorizontalLayoutGroup rowLayout = entriesObject.GetComponent<HorizontalLayoutGroup>();
		rowLayout.childControlWidth = true;
		rowLayout.childControlHeight = true;
		rowLayout.childForceExpandWidth = false;
			rowLayout.childForceExpandHeight = false;
			rowLayout.spacing = FogUiEntrySpacing;
			rowLayout.padding = new RectOffset((int)FogUiHorizontalPadding, (int)FogUiHorizontalPadding, 0, 0);
			_lastFogUiRenderedText = string.Empty;
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
		bool isChineseLanguage = DetectChineseLanguage();
		bool isLobbyScene = IsAirportScene(SceneManager.GetActiveScene());
		if (_fogUiEntriesRect != null)
		{
			PopulateFogUiEntries(isChineseLanguage, isLobbyScene);
			return;
		}
		if (IsReadOnlyFogUiViewer())
		{
			SetFogUiText(BuildReadOnlyFogUiText(isChineseLanguage));
			return;
		}
		float compactSpeed = _fogPaused ? 0f : (_orbFogHandler != null ? _orbFogHandler.speed : (FogSpeed?.Value ?? DefaultFogSpeed));
		string stateBadge = BuildFogUiBadge("ST", FogUiStateLabelColor, GetFogRuntimeStateLabel(isChineseLanguage));
		string speedBadge = BuildFogUiBadge("SPD", FogUiSpeedLabelColor, Colorize(compactSpeed.ToString("0.##"), FogUiSpeedValueColor));
		if (isLobbyScene)
		{
			SetFogUiText(BuildFogUiLine(stateBadge, speedBadge, GetCompactFogHandlingBadge(), GetCompactNightBadge()));
			return;
		}
		string timingBadge = GetCompactTimingBadge();
		string distanceBadge = GetCompactDistanceBadge();
		string etaBadge = GetCompactEtaBadge(isChineseLanguage);
		string directBadge = GetCompactDirectStartBadge(isChineseLanguage);
		if (!string.IsNullOrWhiteSpace(timingBadge))
		{
			SetFogUiText(BuildFogUiLine(stateBadge, speedBadge, timingBadge, directBadge));
			return;
		}
		SetFogUiText(BuildFogUiLine(stateBadge, speedBadge, distanceBadge, etaBadge, directBadge));
		if (!ShouldUseLegacyFogUiLayout())
		{
			return;
		}
		if (IsReadOnlyFogUiViewer())
		{
			SetFogUiText(BuildReadOnlyFogUiText(isChineseLanguage));
			return;
		}
		float speed = _fogPaused ? 0f : (_orbFogHandler != null ? _orbFogHandler.speed : (FogSpeed?.Value ?? DefaultFogSpeed));
		string speedText = isChineseLanguage ? "毒雾速度:" : "FogSpeed:";
		string stateText = isChineseLanguage ? "当前状态:" : "State:";
		string speedLabel = $"{Colorize(speedText, FogUiSpeedLabelColor)} {Colorize(speed.ToString("0.##"), FogUiSpeedValueColor)}";
		string stateLabel = $"{Colorize(stateText, FogUiStateLabelColor)} {GetFogRuntimeStateLabel(isChineseLanguage)}";
		string timingLabel = GetFogTimingLabel(isChineseLanguage);
		string distanceLabel = GetResolvedFogDistanceLabel(isChineseLanguage);
		string arrivalLabel = GetResolvedFogArrivalEtaLabel(isChineseLanguage);
		string primaryLine = BuildFogUiLine(speedLabel, stateLabel, timingLabel, distanceLabel, arrivalLabel);
		string directStartHint = GetVanillaProgressStartUiLabel(isChineseLanguage);
		if (!string.IsNullOrWhiteSpace(directStartHint))
		{
			primaryLine = BuildFogUiLine(primaryLine, directStartHint);
		}
		if (!isLobbyScene)
		{
			SetFogUiText(primaryLine);
			return;
		}
			string fogHandlingHint = GetFogHandlingUiLabel(isChineseLanguage);
			string nightHint = GetNightColdUiLabel(isChineseLanguage);
			SetFogUiText(BuildFogUiLine(primaryLine, fogHandlingHint, nightHint));
	}

	private string BuildReadOnlyFogUiText(bool isChineseLanguage)
	{
		string stateBadge = BuildFogUiBadge("ST", FogUiStateLabelColor, GetGuestFogRuntimeStateLabel(isChineseLanguage));
		return BuildFogUiLine(stateBadge, GetCompactDistanceBadge(), GetCompactEtaBadge(isChineseLanguage));
	}

	private static string BuildFogUiLine(params string[] parts)
	{
		if (parts == null || parts.Length == 0)
		{
			return string.Empty;
		}
		return string.Join("  |  ", parts.Where((string part) => !string.IsNullOrWhiteSpace(part)).Select((string part) => part.Trim()));
	}

	private static bool ShouldUseLegacyFogUiLayout()
	{
		return false;
	}

	private void PopulateFogUiEntries(bool isChineseLanguage, bool isLobbyScene)
	{
		_fogUiDisplayEntries.Clear();
		if (IsReadOnlyFogUiViewer())
		{
			_fogUiDisplayEntries.Add(new FogUiDisplayEntry(FogUiIconKind.State, ParseColorOrDefault(FogUiStateLabelColor), GetGuestFogStateUiEntryText(isChineseLanguage)));
			TryAppendFogDistanceEntry(_fogUiDisplayEntries, isChineseLanguage);
			TryAppendFogEtaEntry(_fogUiDisplayEntries, isChineseLanguage);
			ApplyFogUiEntries();
			return;
		}
		_fogUiDisplayEntries.Add(new FogUiDisplayEntry(FogUiIconKind.State, ParseColorOrDefault(FogUiStateLabelColor), GetFogStateUiEntryText(isChineseLanguage)));
		_fogUiDisplayEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Speed, ParseColorOrDefault(FogUiSpeedLabelColor), GetFogSpeedUiEntryText(isChineseLanguage)));
		if (isLobbyScene)
		{
			_fogUiDisplayEntries.Add(new FogUiDisplayEntry(FogUiIconKind.FogHandling, ParseColorOrDefault(FogUiHintLabelColor), GetFogHandlingUiEntryText(isChineseLanguage)));
			_fogUiDisplayEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Night, ParseColorOrDefault(FogUiHintLabelColor), GetNightColdUiEntryText(isChineseLanguage)));
			ApplyFogUiEntries();
			return;
		}
		if (TryAppendFogTimingEntry(_fogUiDisplayEntries, isChineseLanguage))
		{
			TryAppendDirectStartEntry(_fogUiDisplayEntries, isChineseLanguage);
			ApplyFogUiEntries();
			return;
		}
		TryAppendFogDistanceEntry(_fogUiDisplayEntries, isChineseLanguage);
		TryAppendFogEtaEntry(_fogUiDisplayEntries, isChineseLanguage);
		TryAppendDirectStartEntry(_fogUiDisplayEntries, isChineseLanguage);
		ApplyFogUiEntries();
	}

	private bool TryAppendFogTimingEntry(List<FogUiDisplayEntry> targetEntries, bool isChineseLanguage)
	{
		float configuredDelay = Mathf.Max(FogDelay?.Value ?? DefaultFogDelaySeconds, 0f);
		if (_orbFogHandler == null || IsFogRemovedInCurrentScene() || _fogPaused || _orbFogHandler.isMoving || _orbFogHandler.hasArrived || _initialDelayCompleted)
		{
			return false;
		}
		float hiddenRemaining = Mathf.Max(HiddenFogDelayBufferSeconds - _fogHiddenBufferTimer, 0f);
		if (hiddenRemaining > 0.05f)
		{
			float hiddenDanger = HiddenFogDelayBufferSeconds <= 0.01f ? 1f : Mathf.Clamp01(1f - hiddenRemaining / HiddenFogDelayBufferSeconds);
			string labelColor = LerpHexColor(FogUiCountdownLabelColor, FogUiCountdownDangerLabelColor, hiddenDanger);
			targetEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Buffer, ParseColorOrDefault(labelColor), GetFogTimingUiEntryText(isChineseLanguage)));
			return true;
		}
		float delayRemaining = Mathf.Max(configuredDelay - _fogDelayTimer, 0f);
		if (delayRemaining <= 0.05f)
		{
			return false;
		}
		float danger = configuredDelay <= 0.01f ? 1f : Mathf.Clamp01(1f - delayRemaining / configuredDelay);
		string delayLabelColor = LerpHexColor(FogUiDelayCountdownStartLabelColor, FogUiDelayCountdownEndLabelColor, danger);
		targetEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Delay, ParseColorOrDefault(delayLabelColor), GetFogTimingUiEntryText(isChineseLanguage)));
		return true;
	}

	private void TryAppendFogDistanceEntry(List<FogUiDisplayEntry> targetEntries, bool isChineseLanguage)
	{
		if (!TryGetFogArrivalRemainingDistance(out float remainingDistance) || remainingDistance <= 0.05f)
		{
			return;
		}
		GetFogDistanceColors(remainingDistance, out string labelColor, out string valueColor);
		targetEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Distance, ParseColorOrDefault(labelColor), GetFogDistanceUiEntryText(isChineseLanguage)));
	}

	private void TryAppendFogEtaEntry(List<FogUiDisplayEntry> targetEntries, bool isChineseLanguage)
	{
		if (_orbFogHandler == null || IsFogRemovedInCurrentScene() || _fogPaused || !_orbFogHandler.isMoving || _orbFogHandler.hasArrived)
		{
			return;
		}
		if (!TryGetFogArrivalEtaSeconds(out float etaSeconds))
		{
			targetEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Eta, ParseColorOrDefault(FogUiHintLabelColor), GetFogEtaUiEntryText(isChineseLanguage)));
			return;
		}
		if (etaSeconds <= 0.05f)
		{
			return;
		}
		float displayedEtaSeconds = QuantizeFogEtaSeconds(etaSeconds);
		GetFogEtaColors(displayedEtaSeconds, out string labelColor, out string valueColor);
		targetEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Eta, ParseColorOrDefault(labelColor), GetFogEtaUiEntryText(isChineseLanguage)));
	}

	private void TryAppendDirectStartEntry(List<FogUiDisplayEntry> targetEntries, bool isChineseLanguage)
	{
		if (_orbFogHandler == null || IsAirportScene(SceneManager.GetActiveScene()) || IsFogRemovedInCurrentScene() || _fogPaused)
		{
			return;
		}
		if (_orbFogHandler.isMoving || _orbFogHandler.hasArrived || _initialDelayCompleted || !ShouldEnforceConfiguredDelay(_orbFogHandler))
		{
			return;
		}
		VanillaProgressStartUiState uiState = GetVanillaProgressStartUiState();
		if (uiState == VanillaProgressStartUiState.Unavailable)
		{
			targetEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Direct, ParseColorOrDefault(FogUiHintLabelColor), GetDirectStartUiEntryText(isChineseLanguage)));
			return;
		}
		if (uiState != VanillaProgressStartUiState.Tracking)
		{
			return;
		}
		if (!TryGetVanillaProgressStartProgress(_orbFogHandler, out int passedCount, out int totalCount, out _, out _))
		{
			targetEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Direct, ParseColorOrDefault(FogUiHintLabelColor), GetDirectStartUiEntryText(isChineseLanguage)));
			return;
		}
		float progress = totalCount <= 0 ? 0f : Mathf.Clamp01((float)passedCount / totalCount);
		string valueColor = LerpHexColor(FogUiHintValueColor, FogUiStateRunningColor, progress);
		targetEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Direct, ParseColorOrDefault(valueColor), GetDirectStartUiEntryText(isChineseLanguage)));
	}

	private void ApplyFogUiEntries()
	{
		if (_fogUiEntriesRect == null)
		{
			return;
		}
		EnsureFogUiEntryViewCount(_fogUiDisplayEntries.Count);
		for (int i = 0; i < _fogUiEntryViews.Count; i++)
		{
			FogUiEntryView view = _fogUiEntryViews[i];
			bool shouldBeVisible = i < _fogUiDisplayEntries.Count;
			if (view?.Root == null)
			{
				continue;
			}
			view.Root.gameObject.SetActive(shouldBeVisible);
			if (!shouldBeVisible)
			{
				continue;
			}
			FogUiDisplayEntry entry = _fogUiDisplayEntries[i];
			if (view.Icon != null && (view.LastIconKind != entry.Kind || view.LastIconColor != entry.IconColor))
			{
				view.Icon.sprite = GetFogUiIconSprite(entry.Kind);
				view.Icon.color = entry.IconColor;
				view.LastIconKind = entry.Kind;
				view.LastIconColor = entry.IconColor;
			}
			if (view.Text != null && !string.Equals(view.LastText, entry.Text, StringComparison.Ordinal))
			{
				view.LastText = entry.Text;
				view.Text.text = entry.Text;
			}
		}
	}

	private static string BuildFogUiBadge(string tag, string tagColor, string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		string normalizedTag = string.IsNullOrWhiteSpace(tag) ? string.Empty : $"[{tag.Trim().ToUpperInvariant()}]";
		if (string.IsNullOrEmpty(normalizedTag))
		{
			return value.Trim();
		}
		return $"{Colorize(normalizedTag, tagColor)} {value.Trim()}";
	}

	private static Color ParseColorOrDefault(string hexColor, string fallbackHex = "#FFFFFF")
	{
		if (TryParseHtmlColor(hexColor, out Color parsedColor))
		{
			return parsedColor;
		}
		if (TryParseHtmlColor(fallbackHex, out Color fallbackColor))
		{
			return fallbackColor;
		}
		return Color.white;
	}

	private static string GetCompactFogHandlingValue()
	{
		return IsFogColdSuppressionEnabled() ? Colorize("BLK", FogUiNightEnabledColor) : Colorize("VAN", FogUiStatePausedColor);
	}

	private static string GetCompactNightValue()
	{
		return IsNightColdFeatureEnabled() ? Colorize("ON", FogUiNightEnabledColor) : Colorize("OFF", FogUiNightDisabledColor);
	}

	private string GetCompactTimingBadge()
	{
		float configuredDelay = Mathf.Max(FogDelay?.Value ?? DefaultFogDelaySeconds, 0f);
		if (_orbFogHandler == null || IsFogRemovedInCurrentScene() || _fogPaused || _orbFogHandler.isMoving || _orbFogHandler.hasArrived || _initialDelayCompleted)
		{
			return string.Empty;
		}
		float hiddenRemaining = Mathf.Max(HiddenFogDelayBufferSeconds - _fogHiddenBufferTimer, 0f);
		if (hiddenRemaining > 0.05f)
		{
			float hiddenDanger = HiddenFogDelayBufferSeconds <= 0.01f ? 1f : Mathf.Clamp01(1f - hiddenRemaining / HiddenFogDelayBufferSeconds);
			string labelColor = LerpHexColor(FogUiCountdownLabelColor, FogUiCountdownDangerLabelColor, hiddenDanger);
			string valueColor = LerpHexColor(FogUiCountdownValueColor, FogUiCountdownDangerValueColor, hiddenDanger);
			return BuildFogUiBadge("BUF", labelColor, Colorize($"{hiddenRemaining:F1}s", valueColor));
		}
		float delayRemaining = Mathf.Max(configuredDelay - _fogDelayTimer, 0f);
		if (delayRemaining <= 0.05f)
		{
			return string.Empty;
		}
		float danger = configuredDelay <= 0.01f ? 1f : Mathf.Clamp01(1f - delayRemaining / configuredDelay);
		string delayLabelColor = LerpHexColor(FogUiDelayCountdownStartLabelColor, FogUiDelayCountdownEndLabelColor, danger);
		string delayValueColor = LerpHexColor(FogUiDelayCountdownStartValueColor, FogUiDelayCountdownEndValueColor, danger);
		return BuildFogUiBadge("DLY", delayLabelColor, Colorize($"{delayRemaining:F1}s", delayValueColor));
	}

	private string GetCompactDistanceBadge()
	{
		if (!TryGetFogArrivalRemainingDistance(out float remainingDistance) || remainingDistance <= 0.05f)
		{
			return string.Empty;
		}
		GetFogDistanceColors(remainingDistance, out string labelColor, out string valueColor);
		return BuildFogUiBadge("DIS", labelColor, Colorize(FormatFogDistanceValue(remainingDistance), valueColor));
	}

	private string GetCompactEtaBadge(bool isChineseLanguage)
	{
		if (_orbFogHandler == null || IsFogRemovedInCurrentScene() || _fogPaused || !_orbFogHandler.isMoving || _orbFogHandler.hasArrived)
		{
			return string.Empty;
		}
		if (!TryGetFogArrivalEtaSeconds(out float etaSeconds))
		{
			return BuildFogUiBadge("ETA", FogUiHintLabelColor, Colorize(isChineseLanguage ? "\u4F30\u7B97\u4E2D" : "...", FogUiHintValueColor));
		}
		if (etaSeconds <= 0.05f)
		{
			return string.Empty;
		}
		float displayedEtaSeconds = QuantizeFogEtaSeconds(etaSeconds);
		GetFogEtaColors(displayedEtaSeconds, out string labelColor, out string valueColor);
		return BuildFogUiBadge("ETA", labelColor, Colorize($"{displayedEtaSeconds:F1}s", valueColor));
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
		VanillaProgressStartUiState uiState = GetVanillaProgressStartUiState();
		if (uiState == VanillaProgressStartUiState.Unavailable)
		{
			return BuildFogUiBadge("DIR", FogUiHintLabelColor, Colorize("N/A", FogUiNightDisabledColor));
		}
		if (uiState != VanillaProgressStartUiState.Tracking)
		{
			return string.Empty;
		}
		if (!TryGetVanillaProgressStartProgress(_orbFogHandler, out int passedCount, out int totalCount, out _, out _))
		{
			return BuildFogUiBadge("DIR", FogUiHintLabelColor, Colorize(isChineseLanguage ? "进度" : "PRG", FogUiHintValueColor));
		}
		float progress = totalCount <= 0 ? 0f : Mathf.Clamp01((float)passedCount / totalCount);
		string valueColor = LerpHexColor(FogUiHintValueColor, FogUiStateRunningColor, progress);
		return BuildFogUiBadge("DIR", FogUiHintLabelColor, Colorize($"{passedCount}/{totalCount}", valueColor));
	}

	private static string GetCompactPauseBadge()
	{
		return BuildFogUiBadge("PAU", FogUiHintLabelColor, Colorize(GetFogPauseHotkeyLabel(), FogUiHintValueColor));
	}

	private static string GetCompactFogHandlingBadge()
	{
		if (IsFogColdSuppressionEnabled())
		{
			return BuildFogUiBadge("FOG", FogUiHintLabelColor, Colorize("BLK", FogUiNightEnabledColor));
		}
		return BuildFogUiBadge("FOG", FogUiHintLabelColor, Colorize("VAN", FogUiStatePausedColor));
	}

	private static string GetCompactNightBadge()
	{
		if (IsNightColdFeatureEnabled())
		{
			return BuildFogUiBadge("NGT", FogUiHintLabelColor, Colorize("ON", FogUiNightEnabledColor));
		}
		return BuildFogUiBadge("NGT", FogUiHintLabelColor, Colorize("OFF", FogUiNightDisabledColor));
	}

	private string GetGuestFogRuntimeStateLabel(bool isChineseLanguage)
	{
		if (!IsModFeatureEnabled())
		{
			return Colorize(isChineseLanguage ? "关闭" : "OFF", FogUiStatePausedColor);
		}
		if (IsAirportScene(SceneManager.GetActiveScene()))
		{
			return Colorize(isChineseLanguage ? "大厅" : "LOBBY", FogUiStateSyncingColor);
		}
		if (IsFogRemovedInCurrentScene())
		{
			return Colorize(isChineseLanguage ? "\u5DF2\u7981\u7528" : "DISABLED", FogUiStatePausedColor);
		}
		if (_orbFogHandler == null)
		{
			return Colorize(isChineseLanguage ? "\u540C\u6B65\u4E2D" : "SYNCING", FogUiStateSyncingColor);
		}
		if (_orbFogHandler.isMoving)
		{
			return Colorize(isChineseLanguage ? "\u8FD0\u884C\u4E2D" : "RUNNING", FogUiStateRunningColor);
		}
		if (_orbFogHandler.hasArrived)
		{
			return Colorize(isChineseLanguage ? "\u5DF2\u5230\u8FBE" : "ARRIVED", FogUiStateRunningColor);
		}
		if (ShouldEnforceConfiguredDelay(_orbFogHandler))
		{
			return Colorize(isChineseLanguage ? "\u7B49\u5F85\u4E2D" : "WAITING", FogUiStateWaitingColor);
		}
		return Colorize(isChineseLanguage ? "待机" : "IDLE", FogUiStateSyncingColor);
	}

	private string GetFogTimingLabel(bool isChineseLanguage)
	{
		float configuredDelay = Mathf.Max(FogDelay?.Value ?? DefaultFogDelaySeconds, 0f);
		string fogDelayText = isChineseLanguage ? "毒雾延迟:" : "Fog Delay:";
		if (_orbFogHandler == null)
		{
			return $"{Colorize(fogDelayText, FogUiWaitLabelColor)} {Colorize($"{configuredDelay:F1}s", FogUiWaitValueColor)}";
		}
		if (IsFogRemovedInCurrentScene())
		{
			return string.Empty;
		}
		if (_fogPaused)
		{
			return string.Empty;
		}
		if (_orbFogHandler.isMoving || _orbFogHandler.hasArrived || _initialDelayCompleted)
		{
			return string.Empty;
		}
		float hiddenRemaining = Mathf.Max(HiddenFogDelayBufferSeconds - _fogHiddenBufferTimer, 0f);
		float delayRemaining = Mathf.Max(configuredDelay - _fogDelayTimer, 0f);
		if (hiddenRemaining > 0.05f)
		{
			float hiddenDanger = HiddenFogDelayBufferSeconds <= 0.01f ? 1f : Mathf.Clamp01(1f - hiddenRemaining / HiddenFogDelayBufferSeconds);
			string bufferLabelColor = LerpHexColor(FogUiCountdownLabelColor, FogUiCountdownDangerLabelColor, hiddenDanger);
			string bufferValueColor = LerpHexColor(FogUiCountdownValueColor, FogUiCountdownDangerValueColor, hiddenDanger);
			string bufferText = isChineseLanguage ? "缓冲:" : "Buffer:";
			return $"{Colorize(bufferText, bufferLabelColor)} {Colorize($"{hiddenRemaining:F1}s", bufferValueColor)}";
		}
		if (delayRemaining <= 0.05f)
		{
			return string.Empty;
		}
		float danger = configuredDelay <= 0.01f ? 1f : Mathf.Clamp01(1f - delayRemaining / configuredDelay);
		string fogDelayLabelColor = LerpHexColor(FogUiDelayCountdownStartLabelColor, FogUiDelayCountdownEndLabelColor, danger);
		string fogDelayValueColor = LerpHexColor(FogUiDelayCountdownStartValueColor, FogUiDelayCountdownEndValueColor, danger);
		return $"{Colorize(fogDelayText, fogDelayLabelColor)} {Colorize($"{delayRemaining:F1}s", fogDelayValueColor)}";
	}

	private string GetFogArrivalEtaLabel(bool isChineseLanguage)
	{
		if (_orbFogHandler == null || IsFogRemovedInCurrentScene() || _fogPaused || !_orbFogHandler.isMoving || _orbFogHandler.hasArrived)
		{
			return string.Empty;
		}
		string etaText = isChineseLanguage ? "到达:" : "ETA:";
		if (!TryGetFogArrivalEtaSeconds(out float etaSeconds))
		{
			string estimatingText = isChineseLanguage ? "\u4F30\u7B97\u4E2D" : "Estimating";
			return $"{Colorize(etaText, FogUiHintLabelColor)} {Colorize(estimatingText, FogUiHintValueColor)}";
		}
		if (etaSeconds <= 0.05f)
		{
			return string.Empty;
		}
		GetFogEtaColors(etaSeconds, out string labelColor, out string valueColor);
		return $"{Colorize(etaText, labelColor)} {Colorize($"{etaSeconds:F1}s", valueColor)}";
	}

	private string GetFogDistanceLabel(bool isChineseLanguage)
	{
		if (!TryGetFogArrivalRemainingDistance(out float remainingDistance))
		{
			return string.Empty;
		}
		if (remainingDistance <= 0.05f)
		{
			return string.Empty;
		}
		GetFogDistanceColors(remainingDistance, out string displayLabelColor, out string displayValueColor);
		string displayLabel = isChineseLanguage ? "\u8DDD\u79BB:" : "Distance:";
		return $"{Colorize(displayLabel, displayLabelColor)} {Colorize(FormatFogDistanceValue(remainingDistance), displayValueColor)}";
	}

	private string GetResolvedFogArrivalEtaLabel(bool isChineseLanguage)
	{
		if (_orbFogHandler == null || IsFogRemovedInCurrentScene() || _fogPaused || !_orbFogHandler.isMoving || _orbFogHandler.hasArrived)
		{
			return string.Empty;
		}
		string etaText = isChineseLanguage ? "\u5230\u8FBE:" : "ETA:";
		if (!TryGetFogArrivalEtaSeconds(out float etaSeconds))
		{
			string estimatingText = isChineseLanguage ? "\u4F30\u7B97\u4E2D" : "Estimating";
			return $"{Colorize(etaText, FogUiHintLabelColor)} {Colorize(estimatingText, FogUiHintValueColor)}";
		}
		if (etaSeconds <= 0.05f)
		{
			return string.Empty;
		}
		float displayedEtaSeconds = QuantizeFogEtaSeconds(etaSeconds);
		GetFogEtaColors(displayedEtaSeconds, out string labelColor, out string valueColor);
		return $"{Colorize(etaText, labelColor)} {Colorize($"{displayedEtaSeconds:F1}s", valueColor)}";
	}

	private string GetResolvedFogDistanceLabel(bool isChineseLanguage)
	{
		if (!TryGetFogArrivalRemainingDistance(out float remainingDistance))
		{
			return string.Empty;
		}
		if (remainingDistance <= 0.05f)
		{
			return string.Empty;
		}
		GetFogDistanceColors(remainingDistance, out string labelColor, out string valueColor);
		string label = isChineseLanguage ? "\u8DDD\u79BB:" : "Distance:";
		return $"{Colorize(label, labelColor)} {Colorize(FormatFogDistanceValue(remainingDistance), valueColor)}";
	}

	private static void GetFogEtaColors(float etaSeconds, out string labelColor, out string valueColor)
	{
		float warning = Mathf.Clamp01(1f - etaSeconds / FogEtaWarningWindowSeconds);
		labelColor = LerpHexColor(FogUiDistanceLabelColor, FogUiDistanceWarningLabelColor, warning);
		valueColor = LerpHexColor(FogUiDistanceValueColor, FogUiDistanceWarningValueColor, warning);
		if (etaSeconds > FogEtaDangerWindowSeconds)
		{
			return;
		}
		float danger = FogEtaDangerWindowSeconds <= 0.01f ? 1f : Mathf.Clamp01(1f - etaSeconds / FogEtaDangerWindowSeconds);
		labelColor = LerpHexColor(labelColor, FogUiCountdownDangerLabelColor, danger);
		valueColor = LerpHexColor(valueColor, FogUiCountdownDangerValueColor, danger);
	}

	private static void GetFogDistanceColors(float remainingDistance, out string labelColor, out string valueColor)
	{
		float warning = Mathf.Clamp01(1f - remainingDistance / FogDistanceWarningWindowUnits);
		labelColor = LerpHexColor(FogUiDistanceLabelColor, FogUiDistanceWarningLabelColor, warning);
		valueColor = LerpHexColor(FogUiDistanceValueColor, FogUiDistanceWarningValueColor, warning);
		if (remainingDistance > FogDistanceDangerWindowUnits)
		{
			return;
		}
		float danger = FogDistanceDangerWindowUnits <= 0.01f ? 1f : Mathf.Clamp01(1f - remainingDistance / FogDistanceDangerWindowUnits);
		labelColor = LerpHexColor(labelColor, FogUiCountdownDangerLabelColor, danger);
		valueColor = LerpHexColor(valueColor, FogUiCountdownDangerValueColor, danger);
	}

	private static string FormatFogDistanceValue(float remainingDistance)
	{
		return $"{Mathf.RoundToInt(Mathf.Max(remainingDistance, 0f))}m";
	}

	private static float QuantizeFogEtaSeconds(float etaSeconds)
	{
		if (etaSeconds <= 0f)
		{
			return 0f;
		}
		float step = Mathf.Max(FogEtaDisplayStepSeconds, 0.1f);
		return Mathf.Ceil(etaSeconds / step) * step;
	}

	private string GetVanillaProgressStartUiLabel(bool isChineseLanguage)
	{
		if (_orbFogHandler == null || IsAirportScene(SceneManager.GetActiveScene()) || IsFogRemovedInCurrentScene() || _fogPaused)
		{
			return string.Empty;
		}
		if (_orbFogHandler.isMoving || _orbFogHandler.hasArrived || _initialDelayCompleted || !ShouldEnforceConfiguredDelay(_orbFogHandler))
		{
			return string.Empty;
		}
		string label = isChineseLanguage ? "切入:" : "DirectStart:";
		VanillaProgressStartUiState uiState = GetVanillaProgressStartUiState();
		if (uiState == VanillaProgressStartUiState.Unavailable)
		{
			return $"{Colorize(label, FogUiHintLabelColor)} {Colorize(isChineseLanguage ? "\u5F53\u524D\u6BB5\u65E0\u539F\u7248\u9608\u503C" : "N/A", FogUiNightDisabledColor)}";
		}
		if (uiState != VanillaProgressStartUiState.Tracking)
		{
			return string.Empty;
		}
		if (!TryGetVanillaProgressStartProgress(_orbFogHandler, out int passedCount, out int totalCount, out _, out _))
		{
			return $"{Colorize(label, FogUiHintLabelColor)} {Colorize(isChineseLanguage ? "进度触发" : "Progress", FogUiHintValueColor)}";
		}
		float progress = totalCount <= 0 ? 0f : Mathf.Clamp01((float)passedCount / totalCount);
		string valueColor = LerpHexColor(FogUiHintValueColor, FogUiStateRunningColor, progress);
		string valueText = isChineseLanguage ? $"推进 {passedCount}/{totalCount}" : $"{passedCount}/{totalCount}";
		return $"{Colorize(label, FogUiHintLabelColor)} {Colorize(valueText, valueColor)}";
	}

	private string GetFogStateUiEntryText(bool isChineseLanguage)
	{
		return GetFogRuntimeStateLabel(isChineseLanguage);
	}

	private string GetGuestFogStateUiEntryText(bool isChineseLanguage)
	{
		return GetGuestFogRuntimeStateLabel(isChineseLanguage);
	}

	private string GetFogSpeedUiEntryText(bool isChineseLanguage)
	{
		float displaySpeed = _fogPaused ? 0f : (_orbFogHandler != null ? _orbFogHandler.speed : (FogSpeed?.Value ?? DefaultFogSpeed));
		return Colorize(displaySpeed.ToString("0.##"), FogUiSpeedValueColor);
	}

	private string GetLobbyPauseUiEntryText(bool isChineseLanguage)
	{
		return Colorize(GetFogPauseHotkeyLabel(), FogUiHintValueColor);
	}

	private string GetFogHandlingUiEntryText(bool isChineseLanguage)
	{
		if (IsFogColdSuppressionEnabled())
		{
			return isChineseLanguage
				? Colorize("\u963B\u6B62\u6BD2\u96FE\u5BD2\u51B7", FogUiNightEnabledColor)
				: Colorize("Block Fog Cold", FogUiNightEnabledColor);
		}
		return isChineseLanguage
			? Colorize("\u6BD2\u96FE\u5BD2\u51B7", FogUiStatePausedColor)
			: Colorize("Fog Cold", FogUiStatePausedColor);
	}

	private string GetNightColdUiEntryText(bool isChineseLanguage)
	{
		if (IsNightColdFeatureEnabled())
		{
			return isChineseLanguage
				? Colorize("\u591C\u665A\u5BD2\u51B7\u5F00\u542F", FogUiNightEnabledColor)
				: Colorize("Night Cold On", FogUiNightEnabledColor);
		}
		return isChineseLanguage
			? Colorize("\u591C\u665A\u5BD2\u51B7\u5173\u95ED", FogUiNightDisabledColor)
			: Colorize("Night Cold Off", FogUiNightDisabledColor);
	}

	private string GetFogTimingUiEntryText(bool isChineseLanguage)
	{
		float configuredDelay = Mathf.Max(FogDelay?.Value ?? DefaultFogDelaySeconds, 0f);
		if (_orbFogHandler == null)
		{
			return Colorize($"{configuredDelay:F1}s", FogUiWaitValueColor);
		}
		if (IsFogRemovedInCurrentScene() || _fogPaused || _orbFogHandler.isMoving || _orbFogHandler.hasArrived || _initialDelayCompleted)
		{
			return string.Empty;
		}
		float hiddenRemaining = Mathf.Max(HiddenFogDelayBufferSeconds - _fogHiddenBufferTimer, 0f);
		if (hiddenRemaining > 0.05f)
		{
			float hiddenDanger = HiddenFogDelayBufferSeconds <= 0.01f ? 1f : Mathf.Clamp01(1f - hiddenRemaining / HiddenFogDelayBufferSeconds);
			string valueColor = LerpHexColor(FogUiCountdownValueColor, FogUiCountdownDangerValueColor, hiddenDanger);
			return Colorize($"{hiddenRemaining:F1}s", valueColor);
		}
		float delayRemaining = Mathf.Max(configuredDelay - _fogDelayTimer, 0f);
		if (delayRemaining <= 0.05f)
		{
			return string.Empty;
		}
		float danger = configuredDelay <= 0.01f ? 1f : Mathf.Clamp01(1f - delayRemaining / configuredDelay);
		string fogDelayValueColor = LerpHexColor(FogUiDelayCountdownStartValueColor, FogUiDelayCountdownEndValueColor, danger);
		return Colorize($"{delayRemaining:F1}s", fogDelayValueColor);
	}

	private string GetFogDistanceUiEntryText(bool isChineseLanguage)
	{
		if (!TryGetFogArrivalRemainingDistance(out float remainingDistance) || remainingDistance <= 0.05f)
		{
			return string.Empty;
		}
		GetFogDistanceColors(remainingDistance, out _, out string valueColor);
		return Colorize(FormatFogDistanceValue(remainingDistance), valueColor);
	}

	private string GetFogEtaUiEntryText(bool isChineseLanguage)
	{
		if (_orbFogHandler == null || IsFogRemovedInCurrentScene() || _fogPaused || !_orbFogHandler.isMoving || _orbFogHandler.hasArrived)
		{
			return string.Empty;
		}
		if (!TryGetFogArrivalEtaSeconds(out float etaSeconds))
		{
			return Colorize(isChineseLanguage ? "\u4F30\u7B97\u4E2D" : "Estimating", FogUiHintValueColor);
		}
		if (etaSeconds <= 0.05f)
		{
			return string.Empty;
		}
		float displayedEtaSeconds = QuantizeFogEtaSeconds(etaSeconds);
		GetFogEtaColors(displayedEtaSeconds, out _, out string valueColor);
		return Colorize($"{displayedEtaSeconds:F1}s", valueColor);
	}

	private string GetDirectStartUiEntryText(bool isChineseLanguage)
	{
		if (_orbFogHandler == null || IsAirportScene(SceneManager.GetActiveScene()) || IsFogRemovedInCurrentScene() || _fogPaused)
		{
			return string.Empty;
		}
		if (_orbFogHandler.isMoving || _orbFogHandler.hasArrived || _initialDelayCompleted || !ShouldEnforceConfiguredDelay(_orbFogHandler))
		{
			return string.Empty;
		}
		VanillaProgressStartUiState uiState = GetVanillaProgressStartUiState();
		if (uiState == VanillaProgressStartUiState.Unavailable)
		{
			return Colorize("N/A", FogUiNightDisabledColor);
		}
		if (uiState != VanillaProgressStartUiState.Tracking)
		{
			return string.Empty;
		}
		if (!TryGetVanillaProgressStartProgress(_orbFogHandler, out int passedCount, out int totalCount, out _, out _))
		{
			return Colorize(isChineseLanguage ? "进度触发" : "Progress", FogUiHintValueColor);
		}
		float progress = totalCount <= 0 ? 0f : Mathf.Clamp01((float)passedCount / totalCount);
		string valueColor = LerpHexColor(FogUiHintValueColor, FogUiStateRunningColor, progress);
		string valueText = isChineseLanguage ? $"推进 {passedCount}/{totalCount}" : $"{passedCount}/{totalCount}";
		return Colorize(valueText, valueColor);
	}

	private VanillaProgressStartUiState GetVanillaProgressStartUiState()
	{
		if (_orbFogHandler == null || _orbFogHandler.isMoving || _orbFogHandler.hasArrived || _initialDelayCompleted || !ShouldEnforceConfiguredDelay(_orbFogHandler))
		{
			return VanillaProgressStartUiState.Hidden;
		}
		if (!TryGetVanillaProgressStartThresholds(_orbFogHandler, out _, out _) || Ascents.currentAscent < 0)
		{
			return VanillaProgressStartUiState.Unavailable;
		}
		return VanillaProgressStartUiState.Tracking;
	}

	private string GetFogRuntimeStateLabel(bool isChineseLanguage)
	{
		if (!IsModFeatureEnabled())
		{
			return Colorize(isChineseLanguage ? "关闭" : "OFF", FogUiStatePausedColor);
		}
		if (IsAirportScene(SceneManager.GetActiveScene()))
		{
			return Colorize(isChineseLanguage ? "大厅" : "LOBBY", FogUiStateSyncingColor);
		}
		if (IsFogRemovedInCurrentScene())
		{
			return Colorize(isChineseLanguage ? "\u5DF2\u7981\u7528" : "DISABLED", FogUiStatePausedColor);
		}
		if (_fogPaused)
		{
			return Colorize(isChineseLanguage ? "\u623F\u4E3B\u5DF2\u6682\u505C" : "HOST PAUSED", FogUiStatePausedColor);
		}
		if (_orbFogHandler == null)
		{
			return Colorize(isChineseLanguage ? "\u540C\u6B65\u4E2D" : "SYNCING", FogUiStateSyncingColor);
		}
		if (!_initialDelayCompleted || ShouldEnforceConfiguredDelay(_orbFogHandler))
		{
			return Colorize(isChineseLanguage ? "\u7B49\u5F85\u4E2D" : "WAITING", FogUiStateWaitingColor);
		}
		if (_orbFogHandler.isMoving)
		{
			return Colorize(isChineseLanguage ? "\u8FD0\u884C\u4E2D" : "RUNNING", FogUiStateRunningColor);
		}
		if (_orbFogHandler.hasArrived)
		{
			return Colorize(isChineseLanguage ? "\u5DF2\u5230\u8FBE" : "ARRIVED", FogUiStateRunningColor);
		}
		return Colorize(isChineseLanguage ? "待机" : "IDLE", FogUiStateSyncingColor);
	}

	private static string GetLobbyPauseHintLabel(bool isChineseLanguage)
	{
		string hotkeyLabel = GetFogPauseHotkeyLabel();
		return isChineseLanguage
			? $"{Colorize("\u6682\u505C\u952E:", FogUiHintLabelColor)} {Colorize(hotkeyLabel, FogUiHintValueColor)}"
			: $"{Colorize("Pause:", FogUiHintLabelColor)} {Colorize(hotkeyLabel, FogUiHintValueColor)}";
	}

	private static string GetLobbyCompassHintLabel(bool isChineseLanguage)
	{
		if (!IsCompassFeatureEnabled())
		{
			return string.Empty;
		}
		string label = isChineseLanguage ? "\u6307\u5357\u9488:" : "Compass:";
		string value = isChineseLanguage ? $"\u6309 {GetCompassHotkeyLabel()} \u751F\u6210" : $"Press {GetCompassHotkeyLabel()} to spawn";
		return $"{Colorize(label, FogUiHintLabelColor)} {Colorize(value, FogUiHintValueColor)}";
	}

	private static string GetFogHandlingUiLabel(bool isChineseLanguage)
	{
		string label = isChineseLanguage ? "\u6BD2\u96FE\u5BD2\u51B7:" : "Fog Cold:";
		if (IsFogColdSuppressionEnabled())
		{
			return isChineseLanguage
				? $"{Colorize(label, FogUiHintLabelColor)} {Colorize("\u963B\u6B62\u6BD2\u96FE\u5BD2\u51B7", FogUiNightEnabledColor)}"
				: $"{Colorize(label, FogUiHintLabelColor)} {Colorize("Block Fog Cold", FogUiNightEnabledColor)}";
		}
		return isChineseLanguage
			? $"{Colorize(label, FogUiHintLabelColor)} {Colorize("\u6BD2\u96FE\u5BD2\u51B7", FogUiStatePausedColor)}"
			: $"{Colorize(label, FogUiHintLabelColor)} {Colorize("Fog Cold", FogUiStatePausedColor)}";
	}

	private static string GetNightColdUiLabel(bool isChineseLanguage)
	{
		string label = isChineseLanguage ? "\u591C\u665A\u5BD2\u51B7:" : "Night Cold:";
		if (IsNightColdFeatureEnabled())
		{
			return isChineseLanguage
				? $"{Colorize(label, FogUiHintLabelColor)} {Colorize("\u591C\u665A\u5BD2\u51B7\u5F00\u542F", FogUiNightEnabledColor)}"
				: $"{Colorize(label, FogUiHintLabelColor)} {Colorize("Night Cold On", FogUiNightEnabledColor)}";
		}
		return isChineseLanguage
			? $"{Colorize(label, FogUiHintLabelColor)} {Colorize("\u591C\u665A\u5BD2\u51B7\u5173\u95ED", FogUiNightDisabledColor)}"
			: $"{Colorize(label, FogUiHintLabelColor)} {Colorize("Night Cold Off", FogUiNightDisabledColor)}";
	}

	private static string Colorize(string text, string hexColor)
	{
		if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(hexColor))
		{
			return text ?? string.Empty;
		}
		return $"<color={hexColor}>{text}</color>";
	}

	private static string LerpHexColor(string fromHex, string toHex, float t)
	{
		if (!TryParseHtmlColor(fromHex, out Color fromColor) || !TryParseHtmlColor(toHex, out Color toColor))
		{
			return t >= 0.5f ? toHex : fromHex;
		}
		Color mixed = Color.Lerp(fromColor, toColor, Mathf.Clamp01(t));
		return $"#{ColorUtility.ToHtmlStringRGB(mixed)}";
	}

	private static bool TryParseHtmlColor(string hexColor, out Color color)
	{
		color = Color.white;
		if (string.IsNullOrWhiteSpace(hexColor))
		{
			return false;
		}
		string normalized = hexColor.StartsWith("#", StringComparison.Ordinal) ? hexColor : $"#{hexColor}";
		return ColorUtility.TryParseHtmlString(normalized, out color);
	}

	private bool HasFogCountdownLabel()
	{
		return HasFogAuthority() && _orbFogHandler != null && ShouldEnforceConfiguredDelay(_orbFogHandler) && HasVisibleFogDelayCountdown();
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

	private void ClampCampfireLocatorUiToCanvas(Canvas targetCanvas = null)
	{
		if (_campfireLocatorUiRect == null)
		{
			return;
		}
		Canvas canvas = targetCanvas ?? ResolveHudCanvas() ?? _campfireLocatorUiRect.GetComponentInParent<Canvas>();
		if (!IsCanvasUsable(canvas))
		{
			return;
		}
		if (_campfireLocatorUiRect.parent != canvas.transform)
		{
			_campfireLocatorUiRect.SetParent(canvas.transform, false);
		}
		RectTransform canvasRect = canvas.GetComponent<RectTransform>();
		if (canvasRect == null)
		{
			return;
		}
		ApplyTopCenterAnchoredRect(_campfireLocatorUiRect, canvasRect, CampfireLocatorUiWidth, CampfireLocatorUiHeight, 1f, CampfireLocatorTopOffset);
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

	private static void ApplyTopCenterAnchoredRect(RectTransform target, RectTransform canvasRect, float width, float height, float scale, float topOffset)
	{
		if (target == null || canvasRect == null)
		{
			return;
		}
		target.localScale = Vector3.one * Mathf.Clamp(scale, MinFogUiScale, MaxFogUiScale);
		target.anchorMin = new Vector2(0.5f, 1f);
		target.anchorMax = new Vector2(0.5f, 1f);
		target.pivot = new Vector2(0.5f, 1f);
		target.sizeDelta = new Vector2(width, height);
		target.anchoredPosition = new Vector2(0f, 0f - topOffset);
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

	private void SetFogUiText(string text)
	{
		if (_fogUiText == null)
		{
			return;
		}
		string nextText = text ?? string.Empty;
		if (string.Equals(_lastFogUiRenderedText, nextText, StringComparison.Ordinal))
		{
			return;
		}
		_lastFogUiRenderedText = nextText;
		_fogUiText.text = nextText;
	}

	private void EnsureFogUiEntryViewCount(int targetCount)
	{
		if (_fogUiEntriesRect == null)
		{
			return;
		}
		while (_fogUiEntryViews.Count < targetCount)
		{
			_fogUiEntryViews.Add(CreateFogUiEntryView(_fogUiEntryViews.Count));
		}
	}

	private FogUiEntryView CreateFogUiEntryView(int index)
	{
		GameObject entryObject = new GameObject($"Entry{index}", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
		RectTransform entryRect = entryObject.GetComponent<RectTransform>();
		entryRect.SetParent(_fogUiEntriesRect, false);
		HorizontalLayoutGroup entryLayout = entryObject.GetComponent<HorizontalLayoutGroup>();
		entryLayout.childControlWidth = true;
		entryLayout.childControlHeight = true;
		entryLayout.childForceExpandWidth = false;
		entryLayout.childForceExpandHeight = false;
		entryLayout.spacing = FogUiEntryIconTextSpacing;
		ContentSizeFitter entryFitter = entryObject.GetComponent<ContentSizeFitter>();
		entryFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
		entryFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		LayoutElement entryElement = entryObject.GetComponent<LayoutElement>();
		entryElement.minHeight = FogUiHeight;
		entryElement.preferredHeight = FogUiHeight;

		GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
		RectTransform iconRect = iconObject.GetComponent<RectTransform>();
		iconRect.SetParent(entryRect, false);
		iconRect.sizeDelta = new Vector2(FogUiIconSize, FogUiIconSize);
		iconRect.anchoredPosition = new Vector2(0f, FogUiIconVerticalOffset);
		LayoutElement iconLayout = iconObject.GetComponent<LayoutElement>();
		iconLayout.minWidth = FogUiIconSize;
		iconLayout.preferredWidth = FogUiIconSize;
		iconLayout.minHeight = FogUiIconSize;
		iconLayout.preferredHeight = FogUiIconSize;
		iconLayout.flexibleWidth = 0f;
		Image iconImage = iconObject.GetComponent<Image>();
		iconImage.raycastTarget = false;
		iconImage.preserveAspect = true;

		GameObject textObject = new GameObject("Text", typeof(RectTransform));
		RectTransform textRect = textObject.GetComponent<RectTransform>();
		textRect.SetParent(entryRect, false);
		TextMeshProUGUI textComponent = textObject.AddComponent<TextMeshProUGUI>();
		ApplyGameTextStyle(textComponent, Color.white, FogUiEntryTextSizeMultiplier);
		textComponent.richText = true;
		textComponent.alignment = TextAlignmentOptions.MidlineLeft;
		textComponent.overflowMode = TextOverflowModes.Overflow;
		textComponent.enableAutoSizing = false;
		textComponent.text = string.Empty;

		return new FogUiEntryView
		{
			Root = entryRect,
			Icon = iconImage,
			Text = textComponent
		};
	}

	private void RefreshFogUiEntryStyles()
	{
		foreach (FogUiEntryView entryView in _fogUiEntryViews)
		{
			if (entryView?.Text == null)
			{
				continue;
			}
			ApplyGameTextStyle(entryView.Text, Color.white, FogUiEntryTextSizeMultiplier);
			entryView.Text.richText = true;
			entryView.Text.alignment = TextAlignmentOptions.MidlineLeft;
			entryView.Text.overflowMode = TextOverflowModes.Overflow;
		}
	}

	private Sprite GetFogUiIconSprite(FogUiIconKind iconKind)
	{
		if (_fogUiIconSprites.TryGetValue(iconKind, out Sprite sprite) && sprite != null)
		{
			return sprite;
		}
		sprite = CreateFogUiIconSprite(iconKind);
		_fogUiIconSprites[iconKind] = sprite;
		return sprite;
	}

	private Sprite GetCampfireLocatorDotSprite()
	{
		if (_campfireLocatorDotSprite != null)
		{
			return _campfireLocatorDotSprite;
		}
		_campfireLocatorDotSprite = CreateCampfireLocatorDotSprite();
		return _campfireLocatorDotSprite;
	}

	private static Sprite CreateCampfireLocatorDotSprite()
	{
		const int size = 32;
		Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
		texture.filterMode = FilterMode.Bilinear;
		texture.wrapMode = TextureWrapMode.Clamp;
		Color32[] pixels = Enumerable.Repeat(new Color32(255, 255, 255, 0), size * size).ToArray();
		Color32 outline = new Color32(255, 255, 255, 255);
		Color32 outer = new Color32(170, 24, 24, 255);
		Color32 inner = new Color32(255, 62, 62, 255);

		FillCircle(pixels, size, 16f, 16f, 11.8f, outline);
		FillCircle(pixels, size, 16f, 16f, 9.8f, outer);
		FillCircle(pixels, size, 16f, 16f, 6.8f, inner);

		texture.SetPixels32(pixels);
		texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
		return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
	}

	private static Sprite CreateFogUiIconSprite(FogUiIconKind iconKind)
	{
		const int size = 24;
		Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
		texture.filterMode = FilterMode.Point;
		texture.wrapMode = TextureWrapMode.Clamp;
		Color32[] pixels = Enumerable.Repeat(new Color32(255, 255, 255, 0), size * size).ToArray();
		switch (iconKind)
		{
		case FogUiIconKind.State:
			DrawCircleOutline(pixels, size, 12f, 8f, 4.5f, 2f);
			DrawLine(pixels, size, 12f, 12f, 12f, 20f, 2f);
			DrawLine(pixels, size, 9f, 18f, 15f, 18f, 2f);
			break;
		case FogUiIconKind.Speed:
			DrawLine(pixels, size, 5f, 8f, 16f, 8f, 2f);
			DrawLine(pixels, size, 3f, 12f, 18f, 12f, 2f);
			DrawLine(pixels, size, 7f, 16f, 20f, 16f, 2f);
			break;
		case FogUiIconKind.Buffer:
			DrawLine(pixels, size, 7f, 4f, 17f, 4f, 2f);
			DrawLine(pixels, size, 7f, 20f, 17f, 20f, 2f);
			DrawLine(pixels, size, 8f, 6f, 16f, 12f, 2f);
			DrawLine(pixels, size, 16f, 12f, 8f, 18f, 2f);
			break;
		case FogUiIconKind.Delay:
		case FogUiIconKind.Eta:
			DrawCircleOutline(pixels, size, 12f, 12f, 8f, 2f);
			DrawLine(pixels, size, 12f, 12f, 12f, 7f, 2f);
			DrawLine(pixels, size, 12f, 12f, 16f, 14f, 2f);
			break;
		case FogUiIconKind.Distance:
			DrawLine(pixels, size, 5f, 12f, 19f, 12f, 2f);
			DrawLine(pixels, size, 5f, 8f, 5f, 16f, 2f);
			DrawLine(pixels, size, 19f, 8f, 19f, 16f, 2f);
			DrawLine(pixels, size, 10f, 10f, 10f, 14f, 2f);
			DrawLine(pixels, size, 14f, 10f, 14f, 14f, 2f);
			break;
		case FogUiIconKind.Direct:
			DrawCircleOutline(pixels, size, 9f, 15f, 3.2f, 1.8f);
			DrawLine(pixels, size, 11f, 13f, 19f, 6f, 2f);
			DrawLine(pixels, size, 14f, 6f, 19f, 6f, 2f);
			DrawLine(pixels, size, 19f, 6f, 19f, 11f, 2f);
			break;
		case FogUiIconKind.Pause:
			DrawRectOutline(pixels, size, 5f, 5f, 14f, 14f, 2f);
			DrawLine(pixels, size, 9f, 12f, 15f, 12f, 2f);
			break;
		case FogUiIconKind.FogHandling:
			DrawLine(pixels, size, 12f, 4f, 12f, 20f, 1.8f);
			DrawLine(pixels, size, 4f, 12f, 20f, 12f, 1.8f);
			DrawLine(pixels, size, 6f, 6f, 18f, 18f, 1.8f);
			DrawLine(pixels, size, 18f, 6f, 6f, 18f, 1.8f);
			DrawCircleOutline(pixels, size, 12f, 12f, 3f, 1.2f);
			break;
		case FogUiIconKind.Night:
			FillCircle(pixels, size, 12f, 12f, 8f, new Color32(255, 255, 255, 255));
			FillCircle(pixels, size, 15f, 10f, 8f, new Color32(255, 255, 255, 0));
			break;
		}
		texture.SetPixels32(pixels);
		texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
		return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
	}

	private static void DrawCircleOutline(Color32[] pixels, int size, float centerX, float centerY, float radius, float thickness)
	{
		float inner = Mathf.Max(radius - thickness, 0f);
		float outer = radius + thickness;
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(centerX, centerY));
				if (distance >= inner && distance <= outer)
				{
					pixels[y * size + x] = new Color32(255, 255, 255, 255);
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
				float dx = x + 0.5f - centerX;
				float dy = y + 0.5f - centerY;
				if (dx * dx + dy * dy <= radiusSquared)
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
				float dx = (x + 0.5f - centerX) / safeRadiusX;
				float dy = (y + 0.5f - centerY) / safeRadiusY;
				if (dx * dx + dy * dy <= 1f)
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
				if (x < 0 || x >= size)
				{
					continue;
				}
				pixels[y * size + x] = color;
			}
		}
	}

	private static void DrawLine(Color32[] pixels, int size, float x0, float y0, float x1, float y1, float thickness)
	{
		DrawLine(pixels, size, x0, y0, x1, y1, thickness, new Color32(255, 255, 255, 255));
	}

	private static void DrawLine(Color32[] pixels, int size, float x0, float y0, float x1, float y1, float thickness, Color32 color)
	{
		Vector2 start = new Vector2(x0, y0);
		Vector2 end = new Vector2(x1, y1);
		float radius = Mathf.Max(thickness * 0.5f, 0.5f);
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
				if (DistanceToSegment(point, start, end) <= radius)
				{
					pixels[y * size + x] = color;
				}
			}
		}
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

	private void SetCampfireLocatorUiVisible(bool visible)
	{
		if (_campfireLocatorUiRect != null)
		{
			_campfireLocatorUiRect.gameObject.SetActive(visible);
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
		_fogUiEntriesRect = null;
		_fogUiEntryViews.Clear();
		_fogUiDisplayEntries.Clear();
		_lastFogUiRenderedText = string.Empty;
	}

	private void CleanupCampfireLocatorUi()
	{
		if (_campfireLocatorUiRect != null)
		{
			UnityEngine.Object.Destroy(_campfireLocatorUiRect.gameObject);
		}
		_campfireLocatorUiRect = null;
		_campfireLocatorDotRect = null;
		_campfireLocatorCurrentDotX = 0f;
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
		_remoteFogSporesSuppressionDebt.Clear();
		_remotePlayerCompassBaselineCounts.Clear();
		_pendingCampfireCompassGrantTimes.Clear();
		_fogPaused = false;
		_localFogStatusSuppressionDepth = 0;
		ResetHiddenNightTestHotkeyState();
		ResetFogArrivalEstimate();
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

	private static string GetConfigSectionName(ConfigKey configKey)
	{
		return GetSectionName(configKey, isChineseLanguage: false);
	}

	private static string GetSectionName(ConfigKey configKey, bool isChineseLanguage)
	{
		if (IsAdjustmentConfigKey(configKey))
		{
			return isChineseLanguage ? "\u8C03\u6574" : CanonicalAdjustmentConfigSectionName;
		}
		return isChineseLanguage ? "\u57FA\u672C" : CanonicalBasicConfigSectionName;
	}

	private static string GetLegacyConfigSectionName()
	{
		return LegacyCanonicalConfigSectionName;
	}

	private static string GetLegacySectionName(bool isChineseLanguage)
	{
		return isChineseLanguage ? "\u6BD2\u96FE" : LegacyCanonicalConfigSectionName;
	}

	private static bool IsAdjustmentConfigKey(ConfigKey configKey)
	{
		return configKey == ConfigKey.FogUiX || configKey == ConfigKey.FogUiY || configKey == ConfigKey.FogUiScale;
	}

	private static string GetLocalizedModDisplayName(bool isChineseLanguage)
	{
		return PluginName;
	}

	private static string GetConfigKeyName(ConfigKey configKey)
	{
		return GetKeyName(configKey, isChineseLanguage: false);
	}

	private static string GetKeyName(ConfigKey configKey, bool isChineseLanguage)
	{
		return configKey switch
		{
			ConfigKey.ModEnabled => isChineseLanguage ? "\u6A21\u5757\u5F00\u5173" : "Enable Mod",
			ConfigKey.FogColdSuppression => isChineseLanguage ? "\u6BD2\u96FE\u5BD2\u51B7" : "Suppress Fog Cold",
			ConfigKey.NightColdEnabled => isChineseLanguage ? "\u591C\u665A\u5BD2\u51B7" : "Night Cold",
			ConfigKey.FogSpeed => isChineseLanguage ? "\u6BD2\u96FE\u79FB\u52A8\u901F\u5EA6" : "Fog Speed",
			ConfigKey.FogDelay => isChineseLanguage ? "\u6BD2\u96FE\u5EF6\u8FDF\u65F6\u95F4s" : "Fog Delay (s)",
			ConfigKey.CompassEnabled => isChineseLanguage ? "\u6307\u5357\u9488\u529F\u80FD\u5F00\u5173" : "Compass Feature",
			ConfigKey.CompassHotkey => isChineseLanguage ? "\u6307\u5357\u9488\u751F\u6210\u6309\u952E" : "Compass Hotkey",
			ConfigKey.FogPauseHotkey => isChineseLanguage ? "\u6BD2\u96FE\u6682\u505C\u6309\u952E" : "Pause Fog Hotkey",
			ConfigKey.FogUiEnabled => isChineseLanguage ? "UI\u542F\u7528" : "Fog UI",
			ConfigKey.CampfireLocatorUiEnabled => isChineseLanguage ? "\u7BED\u706B\u4F4D\u7F6E HUD" : "Campfire Locator HUD",
			ConfigKey.FogUiX => isChineseLanguage ? "UI X\u4F4D\u7F6E" : "UI X Position",
			ConfigKey.FogUiY => isChineseLanguage ? "UI Y\u4F4D\u7F6E" : "UI Y Position",
			ConfigKey.FogUiScale => isChineseLanguage ? "UI\u7F29\u653E" : "UI Scale",
			_ => string.Empty
		};
	}

	private static string GetLocalizedDescription(ConfigKey configKey, bool isChineseLanguage)
	{
		string description = configKey switch
		{
			ConfigKey.ModEnabled => isChineseLanguage ? "\u603B\u5F00\u5173\u3002\u5F00\u542F\u540E\u7531\u4E3B\u673A\u63A5\u7BA1 Fog&ColdControl \u7684\u6BD2\u96FE\u3001\u8054\u673A\u540C\u6B65\u548C\u6307\u5357\u9488\u5956\u52B1\uFF1B\u5173\u95ED\u540E\u4E0D\u751F\u6548\u3002" : "Master switch for Fog&ColdControl. When enabled, the host controls fog behavior, multiplayer sync, and compass rewards.",
			ConfigKey.FogColdSuppression => isChineseLanguage ? "\u63A7\u5236\u662F\u5426\u963B\u6B62\u6BD2\u96FE\u5E26\u6765\u7684\u5BD2\u51B7\u503C\u3002\u53EA\u5F71\u54CD\u6BD2\u96FE\u5BD2\u51B7\uFF0C\u4E0D\u5F71\u54CD\u591C\u665A\u5BD2\u51B7\u3002\u9ED8\u8BA4\u5173\u95ED\u3002" : "Blocks cold caused by fog only. This does not affect night cold. Default: Off.",
			ConfigKey.NightColdEnabled => isChineseLanguage ? "\u63A7\u5236\u591C\u665A\u5BD2\u51B7\u529F\u80FD\u3002\u53EA\u5728\u5929\u9636 5 \u53CA\u4EE5\u4E0A\u751F\u6548\uFF0C\u884C\u4E3A\u4E0E\u539F\u7248\u4E00\u81F4\u3002\u9ED8\u8BA4\u5F00\u542F\u3002" : "Controls night cold. Only applies on Ascent 5 and above, matching vanilla behavior. Default: On.",
			ConfigKey.FogSpeed => isChineseLanguage ? "\u63A7\u5236\u6BD2\u96FE\u63A8\u8FDB\u901F\u5EA6\u3002\u8303\u56F4 0.3~20\uFF0C\u6570\u503C\u8D8A\u5927\uFF0C\u6BD2\u96FE\u63A8\u8FDB\u8D8A\u5FEB\u3002\u9ED8\u8BA4 0.4\u3002" : "Controls how fast the fog moves. Range: 0.3 to 20. Higher values make the fog move faster. Default: 0.4.",
			ConfigKey.FogDelay => isChineseLanguage ? "\u63A7\u5236\u9996\u6BB5\u6BD2\u96FE\u5F00\u59CB\u79FB\u52A8\u524D\u7684\u7B49\u5F85\u65F6\u95F4\u3002\u8303\u56F4 20~1000 \u79D2\u3002\u9ED8\u8BA4 900 \u79D2\u3002" : "Controls how long the first fog segment waits before moving. Range: 20 to 1000 seconds. Default: 900 seconds.",
			ConfigKey.CompassEnabled => isChineseLanguage ? "\u6307\u5357\u9488\u529F\u80FD\u603B\u5F00\u5173\u3002\u63A7\u5236\u81EA\u52A8\u53D1\u653E\u3001\u6309\u952E\u751F\u6210\u548C\u5927\u5385\u63D0\u793A\u3002\u9ED8\u8BA4\u5173\u95ED\u3002" : "Master switch for compass features, including automatic rewards, hotkey spawning, and the lobby prompt. Default: Off.",
			ConfigKey.CompassHotkey => isChineseLanguage ? "\u6309\u4E0B\u540E\u5728\u73A9\u5BB6\u6B63\u524D\u65B9\u751F\u6210\u4E00\u4E2A\u666E\u901A\u6307\u5357\u9488\u3002\u8BBE\u4E3A None \u53EF\u7981\u7528\u8BE5\u6309\u952E\u3002" : "Spawns a normal compass in front of the player. Set this to None to disable the hotkey.",
			ConfigKey.FogPauseHotkey => isChineseLanguage ? "\u4E3B\u673A\u4E13\u7528\u6309\u952E\uFF0C\u7528\u4E8E\u6682\u505C\u6216\u7EE7\u7EED\u6BD2\u96FE\u63A8\u8FDB\u3002\u8BBE\u4E3A None \u53EF\u7981\u7528\u3002\u9ED8\u8BA4 Y\u3002" : "Host-only hotkey used to pause or resume fog movement. Set this to None to disable it. Default: Y.",
			ConfigKey.FogUiEnabled => isChineseLanguage ? "\u63A7\u5236\u662F\u5426\u663E\u793A\u6BD2\u96FE\u76F8\u5173 HUD\u3002" : "Shows or hides the Fog&ColdControl HUD.",
			ConfigKey.CampfireLocatorUiEnabled => isChineseLanguage ? "\u63A7\u5236\u662F\u5426\u663E\u793A\u5C4F\u5E55\u9876\u90E8\u7684\u7BED\u706B\u4F4D\u7F6E HUD\u3002\u9ED8\u8BA4\u5F00\u542F\u3002" : "Shows or hides the top-screen campfire locator HUD. Default: On.",
			ConfigKey.FogUiX => isChineseLanguage ? "\u8C03\u6574\u6BD2\u96FE HUD \u7684\u6C34\u5E73\u4F4D\u7F6E\u3002\u9ED8\u8BA4 60\u3002" : "Adjusts the horizontal position of the Fog&ColdControl HUD. Default: 60.",
			ConfigKey.FogUiY => isChineseLanguage ? "\u8C03\u6574\u6BD2\u96FE HUD \u7684\u5782\u76F4\u4F4D\u7F6E\u3002\u9ED8\u8BA4 0\u3002" : "Adjusts the vertical position of the Fog&ColdControl HUD. Default: 0.",
			ConfigKey.FogUiScale => isChineseLanguage ? "\u8C03\u6574\u6BD2\u96FE HUD \u7684\u6574\u4F53\u7F29\u653E\u3002\u9ED8\u8BA4 0.9\u3002" : "Adjusts the overall scale of the Fog&ColdControl HUD. Default: 0.9.",
			_ => string.Empty
		};
		return description.Replace("FogClimb", PluginName).Replace("Fog Climb", PluginName);
	}

	internal static bool IsModFeatureEnabled()
	{
		return ModEnabled == null || ModEnabled.Value;
	}

	private static bool HasFogAuthority()
	{
		return !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;
	}

	private static bool IsReadOnlyFogUiViewer()
	{
		return PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient;
	}

	internal static bool IsCompassFeatureEnabled()
	{
		return CompassEnabled == null || CompassEnabled.Value;
	}


	internal static bool IsFogColdSuppressionEnabled()
	{
		return FogColdSuppression == null || FogColdSuppression.Value;
	}

	internal static bool IsNightColdFeatureEnabled()
	{
		return NightColdEnabled == null || NightColdEnabled.Value;
	}

	internal static bool ShouldSuppressFogColdLocally()
	{
		return IsModFeatureEnabled() && IsFogColdSuppressionEnabled();
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
		if (ShouldSuppressFogColdLocally())
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
		if (afflictions == null)
		{
			return false;
		}
		Character character = afflictions.character;
		if (character == null || character != Character.localCharacter || character.isBot || character.isZombie || character.data == null)
		{
			return false;
		}
		bool isColdLikeStatus = character.data.isSkeleton ? statusType == CharacterAfflictions.STATUSTYPE.Injury : statusType == CharacterAfflictions.STATUSTYPE.Cold;
		bool isFogSporesStatus = statusType == CharacterAfflictions.STATUSTYPE.Spores;
		if (!isColdLikeStatus && !isFogSporesStatus)
		{
			return false;
		}
		if (_localFogStatusSuppressionDepth > 0 && ShouldSuppressFogColdLocally())
		{
			return true;
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
		int coldLikeIndex = character.data.isSkeleton ? (int)CharacterAfflictions.STATUSTYPE.Injury : (int)CharacterAfflictions.STATUSTYPE.Cold;
		if (coldLikeIndex >= 0 && coldLikeIndex < deltas.Length && deltas[coldLikeIndex] > 0f && (suppressNightCold || suppressFogCold || preserveLateGameNoCold))
		{
			deltas[coldLikeIndex] = 0f;
		}
		if (suppressFogCold)
		{
			int sporesIndex = (int)CharacterAfflictions.STATUSTYPE.Spores;
			if (sporesIndex >= 0 && sporesIndex < deltas.Length && deltas[sporesIndex] > 0f)
			{
				deltas[sporesIndex] = 0f;
			}
		}
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
		return IsFogRemovedInCurrentScene() || Instance.ShouldEnforceConfiguredDelay(fogHandler) || Instance.ShouldHoldFogUntilCampfireActivation(fogHandler);
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
		bool isEnabled = ShouldSuppressFogColdLocally();
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

	private bool IsCharacterPastJoinGrace(Character character)
	{
		if (character?.photonView?.Owner == null || character.photonView.IsMine)
		{
			return true;
		}
		if (!_remotePlayerFirstSeenTimes.TryGetValue(character.photonView.Owner.ActorNumber, out float firstSeenTime))
		{
			return true;
		}
		return Time.unscaledTime - firstSeenTime >= RemotePlayerJoinGraceSeconds;
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
		if (!ScheduleFogDelayAfterCampfire(currentCampfire, out _))
		{
			return;
		}
		_restoredCheckpointCampfireIds.Add(campfireId);
		Logger.LogInfo($"[{PluginName}] Restored fog delay from lit checkpoint campfire: {currentCampfire.name}.");
	}

	private static CharacterAfflictions.STATUSTYPE GetFogSuppressionStatusType(Character character)
	{
		return character?.data != null && character.data.isSkeleton ? CharacterAfflictions.STATUSTYPE.Injury : CharacterAfflictions.STATUSTYPE.Cold;
	}

	private static float GetSuppressionTransferAmount(float pendingDebt)
	{
		if (pendingDebt <= 0f)
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
			List<string> backupPaths = new List<string>
			{
				pluginPath + "-unrpcpatched.old"
			};
			string pluginDirectory = Path.GetDirectoryName(pluginPath);
			if (!string.IsNullOrWhiteSpace(pluginDirectory))
			{
				foreach (string legacyPluginFileName in LegacyPluginFileNames)
				{
					backupPaths.Add(Path.Combine(pluginDirectory, legacyPluginFileName + "-unrpcpatched.old"));
				}
			}
			foreach (string backupPath in backupPaths.Distinct(StringComparer.OrdinalIgnoreCase))
			{
				if (!File.Exists(backupPath))
				{
					continue;
				}
				File.Delete(backupPath);
				Logger.LogInfo($"[{PluginName}] Removed generated backup file: {Path.GetFileName(backupPath)}");
			}
		}
		catch (Exception ex)
		{
			Logger.LogDebug($"[{PluginName}] Failed to clean generated backup file: {ex.Message}");
		}
	}

	private void TryCleanupLegacyPluginFile()
	{
		try
		{
			string pluginPath = Info?.Location;
			if (string.IsNullOrWhiteSpace(pluginPath))
			{
				return;
			}
			string currentFileName = Path.GetFileName(pluginPath);
			if (!string.Equals(currentFileName, PreferredPluginFileName, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			string pluginDirectory = Path.GetDirectoryName(pluginPath);
			if (string.IsNullOrWhiteSpace(pluginDirectory))
			{
				return;
			}
			foreach (string legacyPluginFileName in LegacyPluginFileNames)
			{
				string legacyPluginPath = Path.Combine(pluginDirectory, legacyPluginFileName);
				if (!File.Exists(legacyPluginPath))
				{
					continue;
				}
				File.Delete(legacyPluginPath);
				Logger.LogInfo($"[{PluginName}] Removed legacy plugin file: {legacyPluginFileName}");
			}
		}
		catch (Exception ex)
		{
			Logger.LogDebug($"[{PluginName}] Failed to clean legacy plugin file: {ex.Message}");
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
		bool scheduledFogDelay = ScheduleFogDelayAfterCampfire(campfire, out int delayedOriginId);
		if (!scheduledFogDelay)
		{
			Logger.LogInfo($"[{PluginName}] Skipping campfire compass grant for {campfire.name} because no fog segment is scheduled after this campfire.");
			return;
		}
		if (scheduledFogDelay && delayedOriginId == 0 && !_initialCompassGranted)
		{
			Logger.LogInfo($"[{PluginName}] Skipping campfire compass grant for the opening campfire ({campfire.name}) because the initial fog-rise grant is still pending.");
			return;
		}
		if (!IsCompassFeatureEnabled())
		{
			return;
		}
		_pendingCampfireCompassGrantTimes[campfireId] = Time.unscaledTime + CampfireCompassGrantDelaySeconds;
		Logger.LogInfo($"[{PluginName}] Scheduled campfire compass grant for {campfire.name} in {CampfireCompassGrantDelaySeconds:F1}s.");
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
			if (_orbFogHandler.currentID == delayedOriginId && _initialDelayCompleted)
			{
				StartFogMovement();
			}
		}
		float configuredDelay = FogDelay?.Value ?? DefaultFogDelaySeconds;
		Logger.LogInfo($"[{PluginName}] Applied hidden fog buffer {HiddenFogDelayBufferSeconds:F1}s and configured delay {configuredDelay:F1}s after lighting campfire. Next origin: {delayedOriginId}.");
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
			Logger.LogWarning($"[{PluginName}] Unable to resolve fog origin after lighting campfire because no fog origins were found.");
			return false;
		}
		Segment advancedSegment = campfire.advanceToSegment;
		if (ShouldRemoveFogForSegment(advancedSegment))
		{
			_pendingSyntheticFogSegmentId = -1;
			Logger.LogInfo($"[{PluginName}] Skipping fog scheduling for removed segment {(int)advancedSegment} ({advancedSegment}). Campfire={campfire.name}.");
			return false;
		}
		if ((int)advancedSegment >= availableOriginCount)
		{
			if (!ShouldUseCustomFogPositionForSegment(advancedSegment))
			{
				_pendingSyntheticFogSegmentId = -1;
				Logger.LogInfo($"[{PluginName}] No synthetic fog stage is defined for segment {(int)advancedSegment} ({advancedSegment}). Campfire={campfire.name}.");
				return false;
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
