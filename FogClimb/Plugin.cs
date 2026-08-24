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

[BepInPlugin("com.github.Thanks.FogClimb", "Fog&ColdControl", "1.1.9")]
[BepInDependency("com.github.PEAKModding.PEAKLib.ModConfig", BepInDependency.DependencyFlags.SoftDependency)]
public sealed partial class Plugin : BaseUnityPlugin
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
		FogUiScale,
		FogUiEntrySpacing,
		FogUiLayout,
		FogUiBackgroundOpacity,
		FogStateUiEnabled,
		RunTimerUiEnabled,
		DayNightTimeUiEnabled,
		AltitudeUiEnabled,
		FogSpeedUiEnabled,
		CampfireDistanceUiEnabled,
		WeatherHazardUiEnabled,
		FogTimingUiEnabled,
		DirectStartUiEnabled,
		FogApproachUiEnabled,
		LavaApproachUiEnabled,
		NextBiomeUiEnabled
	}


	internal enum FogUiLayoutKind
	{
		Horizontal,
		Vertical
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
		StateRunning,
		StateWaiting,
		StatePaused,
		StateArrived,
		StateSyncing,
		StateOff,
		StateLobby,
		StateIdle,
		Speed,
		Buffer,
		Delay,
		Timer,
		Campfire,
		Biome,
		Altitude,
		Lava,
		Petrify,
		Distance,
		Eta,
		Direct,
		Pause,
		FogHandling,
		Night,
		DayTime,
		NightTime,
		Wind,
		Snowstorm,
		Rain,
		Separator
	}


	private enum FogUiThemeKind
	{
		Day,
		Night,
		Fog
	}


	private sealed class FogUiEntryView
	{
		public RectTransform Root;

		public Image Icon;

		public TextMeshProUGUI Text;

		public RectTransform BarRoot;

		public Image BarBackground;

		public Image BarFill;

		public TextMeshProUGUI BarText;

		public string LastText = string.Empty;

		public FogUiIconKind LastIconKind;

		public Color LastIconColor = Color.clear;

		public Color LastTextColor = Color.clear;

		public float LastProgress = -1f;

		public Color LastBarFillColor = Color.clear;

		public Color LastBarBackgroundColor = Color.clear;

		public string LastBarText = string.Empty;

		public LayoutElement EntryLayoutElement;

		public LayoutElement TextLayoutElement;

		public float LastContentWidth = -1f;

		public bool LastHasProgressBar;

		public bool LastHasTextGradient;

		public Color LastGradientLeft = Color.clear;

		public Color LastGradientRight = Color.clear;
	}


	private sealed class ConfigFileEntryBlock
	{
		public ConfigKey Key;

		public bool HasKnownKey;

		public string SectionName = string.Empty;

		public List<string> Lines = new List<string>();
	}


	private readonly struct FogUiDisplayEntry
	{
		public readonly FogUiIconKind Kind;

		public readonly Color IconColor;

		public readonly string Text;

		public readonly bool HasProgressBar;

		public readonly float Progress;

		public readonly Color BarFillColor;

		public readonly Color BarBackgroundColor;

		public readonly string BarText;

		public readonly bool HasTextGradient;

		public readonly Color TextGradientLeft;

		public readonly Color TextGradientRight;

		public FogUiDisplayEntry(FogUiIconKind kind, Color iconColor, string text)
			: this(kind, iconColor, text, hasProgressBar: false, 0f, Color.white, Color.clear, string.Empty, hasTextGradient: false, Color.white, Color.white)
		{
		}

		public FogUiDisplayEntry(FogUiIconKind kind, Color iconColor, string text, float progress, Color barFillColor, Color barBackgroundColor, string barText)
			: this(kind, iconColor, text, hasProgressBar: true, progress, barFillColor, barBackgroundColor, barText, hasTextGradient: false, Color.white, Color.white)
		{
		}

		public FogUiDisplayEntry(FogUiIconKind kind, Color iconColor, string text, Color gradientLeft, Color gradientRight)
			: this(kind, iconColor, text, hasProgressBar: false, 0f, Color.white, Color.clear, string.Empty, hasTextGradient: true, gradientLeft, gradientRight)
		{
		}

		private FogUiDisplayEntry(FogUiIconKind kind, Color iconColor, string text, bool hasProgressBar, float progress, Color barFillColor, Color barBackgroundColor, string barText, bool hasTextGradient, Color gradientLeft, Color gradientRight)
		{
			Kind = kind;
			IconColor = iconColor;
			Text = text ?? string.Empty;
			HasProgressBar = hasProgressBar;
			Progress = Mathf.Clamp01(progress);
			BarFillColor = barFillColor;
			BarBackgroundColor = barBackgroundColor;
			BarText = barText ?? string.Empty;
			HasTextGradient = hasTextGradient;
			TextGradientLeft = gradientLeft;
			TextGradientRight = gradientRight;
		}
	}


	public const string PluginGuid = "com.github.Thanks.FogClimb";


	public const string PluginName = "Fog&ColdControl";


	public const string PluginVersion = "1.1.9";


	private const string PreferredConfigFileName = "Thanks.Fog&ColdControl.cfg";


	private const string PreferredPluginFileName = "Thanks.Fog&ColdControl.dll";


	private static readonly string[] LegacyConfigFileNames = new string[2] { "Thanks.FogClimb.cfg", "com.github.Thanks.FogClimb.cfg" };


	private static readonly string[] LegacyPluginFileNames = new string[2] { "Thanks.FogClimb.dll", "com.github.Thanks.FogClimb.dll" };


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


	private const float MinFogUiX = 0f;


	private const float MaxFogUiX = 3840f;


	private const float MinFogUiY = 0f;


	private const float MaxFogUiY = 2160f;


	private const float MinFogUiScale = 0.5f;


	private const float MaxFogUiScale = 2.5f;


	private const float DefaultFogUiEntrySpacing = 8f;


	private const float MinFogUiEntrySpacing = 0f;


	private const float MaxFogUiEntrySpacing = 40f;


	private const float DefaultFogUiBackgroundOpacity = 0f;


	private const float MinFogUiBackgroundOpacity = 0f;


	private const float MaxFogUiBackgroundOpacity = 1f;


	private const int FogUiBackgroundSpriteSize = 64;


	private const float FogUiBackgroundCornerRadiusX = 12f;


	private const float FogUiBackgroundCornerRadiusY = 10f;


	private const float FogUiBackgroundSliceBorderX = 12f;


	private const float FogUiBackgroundSliceBorderY = 10f;


	private const float FogUiVerticalContainerWidth = 176f;


	private const float FogUiVerticalMinHeight = 34f;


	private const float FogUiEntryHeight = 34f;


	private const float FogUiHorizontalWidth = 1180f;


	private const float FogUiHorizontalHeight = 34f;


	private const float FogUiBackgroundVerticalPadding = 2f;


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


	private const float FogEtaRateSmoothing = 0.15f;


	private const float FogEtaDangerWindowSeconds = 45f;


	private const float FogEtaWarningWindowSeconds = 90f;


	private const float FogEtaDisplayStepSeconds = 0.5f;


	private const float FogEtaIncreaseNoiseToleranceSeconds = 2f;


	private const float FogEtaMaxDisplayIncreasePerSecond = 1.5f;


	private const float FogEtaMaxExtraDisplayDecreasePerSecond = 3f;


	private const float FogDistanceDangerWindowUnits = 120f;


	private const float FogDistanceWarningWindowUnits = 300f;


	private const float FogDistanceEtaRefreshIntervalSeconds = 0.8f;


	private const float GuestFogSpeedDisplaySmoothing = 0.03f;


	private const float GuestFogSpeedStableTolerance = 0.001f;


	private const float GuestFogSpeedStableConfirmSeconds = 3f;


	private const float GuestFogSpeedDisplayStep = 0.1f;


	private const float GuestFogSpeedMinUpdateIntervalSeconds = 1.5f;


	private const int FogUiDelaySecondsIntegerDigits = 2;


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


	private const float FogUiWidth = 1180f;


	private const float FogUiRightPadding = 18f;


	private const float FogUiHeight = 34f;


	private const float FogUiHorizontalPadding = 10f;


	private const float FogUiIconSize = 20f;


	private const float FogUiIconVerticalOffset = 0f;


	private const float FogUiCompactEntrySpacing = 6f;


	private const float FogUiCompactWidthThreshold = 1040f;


	private const float FogUiEntryIconTextSpacing = 4f;


	private const float FogUiEntryWidthQuantum = 4f;


	private const float FogUiEntryTextSizeMultiplier = 0.9f;


	private const float FogUiEntryTextTrailingPadding = 2f;


	private const int FogUiUnitTextSizePercent = 78;


	private const float CampfireLocatorUiWidth = 420f;


	private const float CampfireLocatorUiHeight = 52f;


	private const float CampfireLocatorTopOffset = 54f;


	private const float CampfireLocatorLineWidth = 400f;


	private const float CampfireLocatorLineHeight = 2f;


	private const float CampfireLocatorDotSize = 22f;


	private const float CampfireLocatorDotSmoothing = 12f;


	private const float CampfireLocatorTickHeight = 8f;


	private const float CampfireLocatorTickWidth = 1.5f;


	private const float CampfireLocatorCenterTickHeight = 12f;


	private const float CampfireLocatorCenterTickWidth = 2.5f;


	private const float CampfireLocatorDistanceFontSize = 14f;


	private const float WeatherIncomingWarningSeconds = 5f;


	private const float FogUiWeatherGraceSeconds = 0.8f;


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


	private const string FogUiCountdownDangerLabelColor = "#FF6B5F";


	private const string FogUiCountdownDangerValueColor = "#FFD0C8";


	private const string FogUiDelayCountdownStartLabelColor = "#FF9A8A";


	private const string FogUiDelayCountdownStartValueColor = "#FFD6D6";


	private const string FogUiDelayCountdownEndLabelColor = "#FF5A4D";


	private const string FogUiDelayCountdownEndValueColor = "#FFB0A8";


	private const string FogUiStateLabelColor = "#A8E0A0";


	private const string FogUiStateRunningColor = "#B5FFB8";


	private const string FogUiStatePausedColor = "#FFA64D";


	private const string FogUiStateWaitingColor = "#FFE08A";


	private const string FogUiStateSyncingColor = "#A3D2FF";


	private const string FogUiStateArrivedColor = "#7BD4FF";


	private const string FogUiStateOffColor = "#FF6B6B";


	private const string FogUiStateIdleColor = "#C8C8C8";


	private const string FogUiHintLabelColor = "#B7C0CC";


	private const string FogUiHintValueColor = "#E2EAF3";


	private const string FogUiBiomeLabelColor = "#9FD7A6";


	private const string FogUiBiomeValueColor = "#E0F5E2";


	private const string FogUiCampfireLabelColor = "#FFB864";


	private const string FogUiCampfireValueColor = "#FFE6BF";


	private const string FogUiLavaLabelColor = "#FF7A3D";


	private const string FogUiLavaValueColor = "#FFD1A6";


	private const string FogUiNightEnabledColor = "#9FFFA8";


	private const string FogUiNightDisabledColor = "#FFB3B3";


	private const string FogUiDayTimeColor = "#FFE38A";


	private const string FogUiNightTimeColor = "#AFC6FF";


	private const string FogUiTimeBarEmptyColor = "#56606C";


	private const string FogUiTimeBarTextColor = "#FFFFFF";


	private const float FogUiTimeProgressBarWidth = 128f;


	private const float FogUiTimeProgressBarHeight = 17f;


	private const float FogUiEntryMaxWidth = 220f;


	private static readonly ushort CompassItemIdOverride = 0;


	private const string CompassNameKeyword = "Compass";


	private const string ModConfigPluginGuid = "com.github.PEAKModding.PEAKLib.ModConfig";


	private const string LegacyCanonicalConfigSectionName = "Fog";


	private const string CanonicalBasicConfigSectionName = "Basic";


	private const string CanonicalAdjustmentConfigSectionName = "Adjustments";


	private const string ChineseBasicConfigSectionName = "基本";


	private const string ChineseAdjustmentConfigSectionName = "调整";


	private const string NetworkInstallStateKey = "FogClimb.Enabled";


	private const int SimplifiedChineseLanguageIndex = 9;


	private static readonly BindingFlags InstanceBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;


	private static readonly BindingFlags StaticBindingFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;


	private static readonly FieldInfo ConfigEntryDescriptionField = typeof(ConfigEntryBase).GetField("<Description>k__BackingField", InstanceBindingFlags);


	private static readonly PropertyInfo ConfigFileEntriesProperty = typeof(ConfigFile).GetProperty("Entries", InstanceBindingFlags);


	private static readonly FieldInfo BasePluginConfigBackingField = typeof(BaseUnityPlugin).GetField("<Config>k__BackingField", InstanceBindingFlags);


	private static readonly FieldInfo OrbFogHandlerOriginsField = typeof(OrbFogHandler).GetField("origins", InstanceBindingFlags);


	private static readonly FieldInfo OrbFogHandlerSphereField = typeof(OrbFogHandler).GetField("sphere", InstanceBindingFlags);


	private static readonly FieldInfo CharacterAfflictionsCurrentStatusesField = typeof(CharacterAfflictions).GetField("currentStatuses", InstanceBindingFlags);


	private static readonly FieldInfo CharacterAfflictionsCurrentIncrementalStatusesField = typeof(CharacterAfflictions).GetField("currentIncrementalStatuses", InstanceBindingFlags);


	private static readonly FieldInfo CharacterAfflictionsCurrentDecrementalStatusesField = typeof(CharacterAfflictions).GetField("currentDecrementalStatuses", InstanceBindingFlags);


	private static readonly FieldInfo RunManagerRunStartedField = typeof(RunManager).GetField("runStarted", InstanceBindingFlags);


	private static readonly FieldInfo RunManagerTimerActiveField = typeof(RunManager).GetField("timerActive", InstanceBindingFlags);


	private static readonly int StatusTypeCount = Enum.GetNames(typeof(CharacterAfflictions.STATUSTYPE)).Length;


	private static readonly string[] DayNightTimeMemberCandidates = new string[6] { "timeOfDayNormalized", "currentTime", "time", "timeOfDay", "timeNormalized", "cycleTime" };


	private static readonly string[] DayNightDayCountMemberCandidates = new string[7] { "dayCount", "dayIndex", "currentDay", "dayNumber", "daysPassed", "totalDays", "day" };


	private static readonly ConfigKey[] BasicConfigFileOrder = new ConfigKey[5]

	{
		ConfigKey.FogColdSuppression,
		ConfigKey.NightColdEnabled,
		ConfigKey.FogSpeed,
		ConfigKey.FogDelay,
		ConfigKey.FogPauseHotkey
	};


	private static readonly ConfigKey[] AdjustmentConfigFileOrder = new ConfigKey[23]
	{
		ConfigKey.ModEnabled,
		ConfigKey.FogUiEnabled,
		ConfigKey.CampfireLocatorUiEnabled,
		ConfigKey.FogStateUiEnabled,
		ConfigKey.RunTimerUiEnabled,
		ConfigKey.DayNightTimeUiEnabled,
		ConfigKey.AltitudeUiEnabled,
		ConfigKey.FogSpeedUiEnabled,
		ConfigKey.CampfireDistanceUiEnabled,
		ConfigKey.WeatherHazardUiEnabled,
		ConfigKey.FogTimingUiEnabled,
		ConfigKey.DirectStartUiEnabled,
		ConfigKey.FogApproachUiEnabled,
		ConfigKey.LavaApproachUiEnabled,
		ConfigKey.NextBiomeUiEnabled,
		ConfigKey.CompassEnabled,
		ConfigKey.CompassHotkey,
		ConfigKey.FogUiX,
		ConfigKey.FogUiY,
		ConfigKey.FogUiScale,
		ConfigKey.FogUiEntrySpacing,
		ConfigKey.FogUiLayout,
		ConfigKey.FogUiBackgroundOpacity
	};


	private static readonly Color CampfireLocatorLineColor = new Color(1f, 1f, 1f, 0.95f);


	private static readonly Color CampfireLocatorDotColor = new Color(1f, 0.2f, 0.2f, 1f);


	private static int _localFogStatusSourceDepth;


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


	private float _lastFogStateSyncTime = -0.18f;


	private float _lastRemoteStatusSyncTime = -0.25f;


	private float _lastCompassGrantSyncTime = -0.75f;


	private bool _lastHadFogAuthority;


	private bool _lastModEnabledState;


	private bool _lastFogUiEnabledState;


	private bool _lastCampfireLocatorUiEnabledState;


	private bool _lastDetectedChineseLanguage;


	private bool _isRefreshingLanguage;


	private bool _pendingConfigFileLocalizationRefresh;


	private bool _pendingConfigFileLocalizationSave;


	private bool _pendingConfigFileOrderRefresh;


	private float _lastFogUiX;


	private float _lastFogUiY;


	private float _lastFogUiScale;


	private FogUiLayoutKind _lastFogUiLayout;


	private bool _hasAdvertisedInstallState;


	private bool _lastAdvertisedInstallState;


	private string _lastObservedRoomKey = string.Empty;


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


	private bool _fogUiLayoutDirty;


	private int _lastFogUiEntryLayoutCount = -1;


	private float _lastFogUiResolvedWidth = -1f;


	private RectTransform _fogUiEntriesRect;


	private Image _fogUiBackgroundImage;


	private Sprite _fogUiBackgroundSprite;


	private bool _fogUiThemeInitialized;


	private FogUiThemeKind _fogUiThemeKind;


	private readonly List<FogUiEntryView> _fogUiEntryViews = new List<FogUiEntryView>();


	private readonly List<FogUiDisplayEntry> _fogUiDisplayEntries = new List<FogUiDisplayEntry>(9);


	private readonly Dictionary<FogUiIconKind, Sprite> _fogUiIconSprites = new Dictionary<FogUiIconKind, Sprite>();


	private Sprite _campfireLocatorDotSprite;


	private Sprite _campfireLocatorLineSprite;


	private Sprite _nativeCampfireIconSprite;


	private Sprite _nativePoisonIconSprite;


	private Sprite _nativeHotIconSprite;


	private Sprite _nativeDrowsyIconSprite;


	private Sprite _nativePetrifyIconSprite;


	private Sprite _nativeAltitudeIconSprite;


	private float _lastNativeIconAttemptTime = -999f;


	private StormVisual[] _cachedStormVisuals;


	private float _lastStormVisualScanTime = -999f;


	private WindChillZone _weatherUiShownZone;


	private FogUiIconKind _weatherUiShownKind = FogUiIconKind.Wind;


	private string _weatherUiShownColorHex = "#A8D8F0";


	private float _weatherUiShownRemaining;


	private bool _weatherUiShownIsWarning;


	private float _weatherUiGraceUntil = -1f;


private float _lastLavaDiagLogTime = -999f;


	private static readonly bool EnableDebugDiagnostics = false;


	private static LavaRising _cachedSceneLavaRising;


	private static Segment _cachedSceneLavaSegment = Segment.Beach;


	private static bool _cachedSceneLavaValid;


	private static float _cachedSceneLavaScanTime = -999f;


	private float _pendingWeatherStateBroadcastTime = -1f;


	private int _weatherStateBroadcastRetryCount;


	private RectTransform _campfireLocatorUiRect;


	private RectTransform _campfireLocatorDotRect;


	private Image _campfireLocatorDotImage;


	private TextMeshProUGUI _campfireLocatorDistanceText;


	private float _campfireLocatorCurrentDotX;


	private RectTransform _compassLobbyNoticeRect;


	private TextMeshProUGUI _compassLobbyNoticeText;


	private string _lastCompassLobbyNoticeText = string.Empty;


	private float _fogUiStateAnimTime;


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


	private float _fogEtaLastAppliedHandlerSpeed = -1f;


	private float _fogEtaLastRateResetTime = -999f;


	private float _lastAdvertisedFogSpeed = -1f;


	private float _lastFogSpeedAdvertiseTime = -999f;


	private float _syncedHostFogSpeed = -1f;


	private float _guestAppliedFogSpeed = -1f;


	private float _fogDistanceEtaLastRefreshTime = -1f;


	private bool _fogDistanceEtaHasSnapshot;


	private bool _fogDistanceEtaHasEta;


	private float _fogDistanceEtaRemainingDistance;


	private float _fogDistanceEtaSeconds;


	private int _guestFogSpeedDisplayOriginId = -1;


	private float _guestFogSpeedDisplayValue;


	private float _guestFogSpeedStableCandidate;


	private float _guestFogSpeedStableSince = -1f;


	private bool _guestFogSpeedHasDisplay;


	private float _guestFogSpeedLastUpdateTime = -1f;


	private float _lastGuestFogDiagLogTime = -999f;


	private float _lastHostFogSyncDiagLogTime = -999f;


	private float _lastInstallAdvertiseDiagLogTime = -999f;


	private bool _windChillFieldDiagLogged;


	private static System.Reflection.FieldInfo _stormVisualZoneField;


	private static System.Reflection.FieldInfo _windChillDirectionField;


	private static System.Reflection.FieldInfo _windChillUntilSwitchField;
}
