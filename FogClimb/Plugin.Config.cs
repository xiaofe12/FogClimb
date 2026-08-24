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

// FogClimb subsystem: Config
public sealed partial class Plugin
{


	internal static ConfigEntry<bool> ModEnabled { get; private set; }


	internal static ConfigEntry<bool> FogColdSuppression { get; private set; }


	internal static ConfigEntry<bool> NightColdEnabled { get; private set; }


	internal static ConfigEntry<float> FogSpeed { get; private set; }


	internal static ConfigEntry<float> FogDelay { get; private set; }


	internal static ConfigEntry<KeyCode> FogPauseHotkey { get; private set; }


	internal static ConfigEntry<bool> FogUiEnabled { get; private set; }


	internal static ConfigEntry<bool> CampfireLocatorUiEnabled { get; private set; }


	internal static ConfigEntry<float> FogUiX { get; private set; }


	internal static ConfigEntry<float> FogUiY { get; private set; }


	internal static ConfigEntry<float> FogUiScale { get; private set; }


	internal static ConfigEntry<float> FogUiEntrySpacing { get; private set; }


	internal static ConfigEntry<FogUiLayoutKind> FogUiLayout { get; private set; }


	internal static ConfigEntry<float> FogUiBackgroundOpacity { get; private set; }


	internal static ConfigEntry<bool> FogStateUiEnabled { get; private set; }


	internal static ConfigEntry<bool> RunTimerUiEnabled { get; private set; }


	internal static ConfigEntry<bool> DayNightTimeUiEnabled { get; private set; }


	internal static ConfigEntry<bool> AltitudeUiEnabled { get; private set; }


	internal static ConfigEntry<bool> FogSpeedUiEnabled { get; private set; }


	internal static ConfigEntry<bool> CampfireDistanceUiEnabled { get; private set; }


	internal static ConfigEntry<bool> WeatherHazardUiEnabled { get; private set; }


	internal static ConfigEntry<bool> FogTimingUiEnabled { get; private set; }


	internal static ConfigEntry<bool> DirectStartUiEnabled { get; private set; }


	internal static ConfigEntry<bool> FogApproachUiEnabled { get; private set; }


	internal static ConfigEntry<bool> LavaApproachUiEnabled { get; private set; }


	internal static ConfigEntry<bool> NextBiomeUiEnabled { get; private set; }


	private void InitializeConfig(bool isChineseLanguage)
	{
		FogColdSuppression = base.Config.Bind(GetConfigSectionName(ConfigKey.FogColdSuppression), GetConfigKeyName(ConfigKey.FogColdSuppression), defaultValue: false, CreateConfigDescription(ConfigKey.FogColdSuppression, isChineseLanguage));
		NightColdEnabled = base.Config.Bind(GetConfigSectionName(ConfigKey.NightColdEnabled), GetConfigKeyName(ConfigKey.NightColdEnabled), defaultValue: true, CreateConfigDescription(ConfigKey.NightColdEnabled, isChineseLanguage));
		FogSpeed = base.Config.Bind(GetConfigSectionName(ConfigKey.FogSpeed), GetConfigKeyName(ConfigKey.FogSpeed), 0.4f, CreateConfigDescription(ConfigKey.FogSpeed, isChineseLanguage));
		FogDelay = base.Config.Bind(GetConfigSectionName(ConfigKey.FogDelay), GetConfigKeyName(ConfigKey.FogDelay), 900f, CreateConfigDescription(ConfigKey.FogDelay, isChineseLanguage));
		FogPauseHotkey = base.Config.Bind(GetConfigSectionName(ConfigKey.FogPauseHotkey), GetConfigKeyName(ConfigKey.FogPauseHotkey), KeyCode.Y, CreateConfigDescription(ConfigKey.FogPauseHotkey, isChineseLanguage));
		ModEnabled = base.Config.Bind(GetConfigSectionName(ConfigKey.ModEnabled), GetConfigKeyName(ConfigKey.ModEnabled), defaultValue: true, CreateConfigDescription(ConfigKey.ModEnabled, isChineseLanguage));
		FogUiEnabled = base.Config.Bind(GetConfigSectionName(ConfigKey.FogUiEnabled), GetConfigKeyName(ConfigKey.FogUiEnabled), defaultValue: true, CreateConfigDescription(ConfigKey.FogUiEnabled, isChineseLanguage));
		CampfireLocatorUiEnabled = base.Config.Bind(GetConfigSectionName(ConfigKey.CampfireLocatorUiEnabled), GetConfigKeyName(ConfigKey.CampfireLocatorUiEnabled), defaultValue: true, CreateConfigDescription(ConfigKey.CampfireLocatorUiEnabled, isChineseLanguage));
		CompassEnabled = base.Config.Bind(GetConfigSectionName(ConfigKey.CompassEnabled), GetConfigKeyName(ConfigKey.CompassEnabled), defaultValue: false, CreateConfigDescription(ConfigKey.CompassEnabled, isChineseLanguage));
		CompassHotkey = base.Config.Bind(GetConfigSectionName(ConfigKey.CompassHotkey), GetConfigKeyName(ConfigKey.CompassHotkey), KeyCode.G, CreateConfigDescription(ConfigKey.CompassHotkey, isChineseLanguage));
		FogUiX = base.Config.Bind(GetConfigSectionName(ConfigKey.FogUiX), GetConfigKeyName(ConfigKey.FogUiX), 60f, CreateConfigDescription(ConfigKey.FogUiX, isChineseLanguage));
		FogUiY = base.Config.Bind(GetConfigSectionName(ConfigKey.FogUiY), GetConfigKeyName(ConfigKey.FogUiY), 0f, CreateConfigDescription(ConfigKey.FogUiY, isChineseLanguage));
		FogUiScale = base.Config.Bind(GetConfigSectionName(ConfigKey.FogUiScale), GetConfigKeyName(ConfigKey.FogUiScale), 0.9f, CreateConfigDescription(ConfigKey.FogUiScale, isChineseLanguage));
		FogUiEntrySpacing = base.Config.Bind(GetConfigSectionName(ConfigKey.FogUiEntrySpacing), GetConfigKeyName(ConfigKey.FogUiEntrySpacing), DefaultFogUiEntrySpacing, CreateConfigDescription(ConfigKey.FogUiEntrySpacing, isChineseLanguage));
		FogUiLayout = base.Config.Bind(GetConfigSectionName(ConfigKey.FogUiLayout), GetConfigKeyName(ConfigKey.FogUiLayout), FogUiLayoutKind.Horizontal, CreateConfigDescription(ConfigKey.FogUiLayout, isChineseLanguage));
		FogUiBackgroundOpacity = base.Config.Bind(GetConfigSectionName(ConfigKey.FogUiBackgroundOpacity), GetConfigKeyName(ConfigKey.FogUiBackgroundOpacity), DefaultFogUiBackgroundOpacity, CreateConfigDescription(ConfigKey.FogUiBackgroundOpacity, isChineseLanguage));
		FogStateUiEnabled = base.Config.Bind(GetConfigSectionName(ConfigKey.FogStateUiEnabled), GetConfigKeyName(ConfigKey.FogStateUiEnabled), defaultValue: true, CreateConfigDescription(ConfigKey.FogStateUiEnabled, isChineseLanguage));
		RunTimerUiEnabled = base.Config.Bind(GetConfigSectionName(ConfigKey.RunTimerUiEnabled), GetConfigKeyName(ConfigKey.RunTimerUiEnabled), defaultValue: true, CreateConfigDescription(ConfigKey.RunTimerUiEnabled, isChineseLanguage));
		DayNightTimeUiEnabled = base.Config.Bind(GetConfigSectionName(ConfigKey.DayNightTimeUiEnabled), GetConfigKeyName(ConfigKey.DayNightTimeUiEnabled), defaultValue: true, CreateConfigDescription(ConfigKey.DayNightTimeUiEnabled, isChineseLanguage));
		AltitudeUiEnabled = base.Config.Bind(GetConfigSectionName(ConfigKey.AltitudeUiEnabled), GetConfigKeyName(ConfigKey.AltitudeUiEnabled), defaultValue: true, CreateConfigDescription(ConfigKey.AltitudeUiEnabled, isChineseLanguage));
		FogSpeedUiEnabled = base.Config.Bind(GetConfigSectionName(ConfigKey.FogSpeedUiEnabled), GetConfigKeyName(ConfigKey.FogSpeedUiEnabled), defaultValue: true, CreateConfigDescription(ConfigKey.FogSpeedUiEnabled, isChineseLanguage));
		CampfireDistanceUiEnabled = base.Config.Bind(GetConfigSectionName(ConfigKey.CampfireDistanceUiEnabled), GetConfigKeyName(ConfigKey.CampfireDistanceUiEnabled), defaultValue: true, CreateConfigDescription(ConfigKey.CampfireDistanceUiEnabled, isChineseLanguage));
		WeatherHazardUiEnabled = base.Config.Bind(GetConfigSectionName(ConfigKey.WeatherHazardUiEnabled), GetConfigKeyName(ConfigKey.WeatherHazardUiEnabled), defaultValue: true, CreateConfigDescription(ConfigKey.WeatherHazardUiEnabled, isChineseLanguage));
		FogTimingUiEnabled = base.Config.Bind(GetConfigSectionName(ConfigKey.FogTimingUiEnabled), GetConfigKeyName(ConfigKey.FogTimingUiEnabled), defaultValue: true, CreateConfigDescription(ConfigKey.FogTimingUiEnabled, isChineseLanguage));
		DirectStartUiEnabled = base.Config.Bind(GetConfigSectionName(ConfigKey.DirectStartUiEnabled), GetConfigKeyName(ConfigKey.DirectStartUiEnabled), defaultValue: true, CreateConfigDescription(ConfigKey.DirectStartUiEnabled, isChineseLanguage));
		FogApproachUiEnabled = base.Config.Bind(GetConfigSectionName(ConfigKey.FogApproachUiEnabled), GetConfigKeyName(ConfigKey.FogApproachUiEnabled), defaultValue: true, CreateConfigDescription(ConfigKey.FogApproachUiEnabled, isChineseLanguage));
		LavaApproachUiEnabled = base.Config.Bind(GetConfigSectionName(ConfigKey.LavaApproachUiEnabled), GetConfigKeyName(ConfigKey.LavaApproachUiEnabled), defaultValue: true, CreateConfigDescription(ConfigKey.LavaApproachUiEnabled, isChineseLanguage));
		NextBiomeUiEnabled = base.Config.Bind(GetConfigSectionName(ConfigKey.NextBiomeUiEnabled), GetConfigKeyName(ConfigKey.NextBiomeUiEnabled), defaultValue: true, CreateConfigDescription(ConfigKey.NextBiomeUiEnabled, isChineseLanguage));
		MigrateLocalizedConfigEntries();
		ClampConfigValues();
	}


	private void EnsurePreferredConfigFile()
	{
		try
		{
			string configDirectory = Paths.ConfigPath;
			if (!string.IsNullOrWhiteSpace(configDirectory))
			{
				Directory.CreateDirectory(configDirectory);
				string preferredConfigPath = Path.Combine(configDirectory, "Thanks.Fog&ColdControl.cfg");
				string legacyConfigPath = LegacyConfigFileNames.Select((string fileName) => Path.Combine(configDirectory, fileName)).Concat(new string[1] { Path.Combine(configDirectory, "com.github.Thanks.FogClimb.cfg") }).Distinct(StringComparer.OrdinalIgnoreCase)
					.ToArray()
					.FirstOrDefault(File.Exists);
				if (!string.IsNullOrWhiteSpace(legacyConfigPath) && !string.Equals(preferredConfigPath, legacyConfigPath, StringComparison.OrdinalIgnoreCase) && !File.Exists(preferredConfigPath))
				{
					File.Move(legacyConfigPath, preferredConfigPath);
					base.Logger.LogInfo("[Fog&ColdControl] Migrated config file to Thanks.Fog&ColdControl.cfg.");
				}
				if (!(BasePluginConfigBackingField == null) && (base.Config == null || !string.Equals(base.Config.ConfigFilePath, preferredConfigPath, StringComparison.OrdinalIgnoreCase)))
				{
					ConfigFile preferredConfig = new ConfigFile(preferredConfigPath, saveOnInit: true);
					BasePluginConfigBackingField.SetValue(this, preferredConfig);
				}
			}
		}
		catch (Exception ex)
		{
			base.Logger.LogWarning("[Fog&ColdControl] Failed to switch config path to Thanks.Fog&ColdControl.cfg: " + ex.Message);
		}
	}


	private ConfigDescription CreateConfigDescription(ConfigKey configKey, bool isChineseLanguage)
	{
		return configKey switch
		{
			ConfigKey.FogSpeed => new ConfigDescription(GetLocalizedDescription(configKey, isChineseLanguage), new AcceptableValueRange<float>(0.3f, 20f)), 
			ConfigKey.FogDelay => new ConfigDescription(GetLocalizedDescription(configKey, isChineseLanguage), new AcceptableValueRange<float>(20f, 1000f)), 
			ConfigKey.FogUiX => new ConfigDescription(GetLocalizedDescription(configKey, isChineseLanguage), new AcceptableValueRange<float>(0f, 3840f)),
			ConfigKey.FogUiY => new ConfigDescription(GetLocalizedDescription(configKey, isChineseLanguage), new AcceptableValueRange<float>(0f, 2160f)),
			ConfigKey.FogUiScale => new ConfigDescription(GetLocalizedDescription(configKey, isChineseLanguage), new AcceptableValueRange<float>(0.5f, 2.5f)),
			ConfigKey.FogUiEntrySpacing => new ConfigDescription(GetLocalizedDescription(configKey, isChineseLanguage), new AcceptableValueRange<float>(MinFogUiEntrySpacing, MaxFogUiEntrySpacing)),
			ConfigKey.FogUiBackgroundOpacity => new ConfigDescription(GetLocalizedDescription(configKey, isChineseLanguage), new AcceptableValueRange<float>(MinFogUiBackgroundOpacity, MaxFogUiBackgroundOpacity)),
			_ => new ConfigDescription(GetLocalizedDescription(configKey, isChineseLanguage), null), 
		};
	}


	private void MigrateLocalizedConfigEntries()
	{
		IDictionary orphanedEntries = GetOrphanedEntries(base.Config);
		if (orphanedEntries != null && orphanedEntries.Count != 0 && (0u | (TryMigrateLocalizedConfigValue(ModEnabled, ConfigKey.ModEnabled, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(FogColdSuppression, ConfigKey.FogColdSuppression, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(NightColdEnabled, ConfigKey.NightColdEnabled, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(FogSpeed, ConfigKey.FogSpeed, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(FogDelay, ConfigKey.FogDelay, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(CompassEnabled, ConfigKey.CompassEnabled, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(CompassHotkey, ConfigKey.CompassHotkey, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(FogPauseHotkey, ConfigKey.FogPauseHotkey, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(FogUiEnabled, ConfigKey.FogUiEnabled, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(CampfireLocatorUiEnabled, ConfigKey.CampfireLocatorUiEnabled, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(FogUiX, ConfigKey.FogUiX, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(FogUiY, ConfigKey.FogUiY, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(FogUiScale, ConfigKey.FogUiScale, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(FogUiEntrySpacing, ConfigKey.FogUiEntrySpacing, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(FogUiLayout, ConfigKey.FogUiLayout, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(FogUiBackgroundOpacity, ConfigKey.FogUiBackgroundOpacity, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(FogStateUiEnabled, ConfigKey.FogStateUiEnabled, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(RunTimerUiEnabled, ConfigKey.RunTimerUiEnabled, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(DayNightTimeUiEnabled, ConfigKey.DayNightTimeUiEnabled, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(AltitudeUiEnabled, ConfigKey.AltitudeUiEnabled, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(FogSpeedUiEnabled, ConfigKey.FogSpeedUiEnabled, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(CampfireDistanceUiEnabled, ConfigKey.CampfireDistanceUiEnabled, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(WeatherHazardUiEnabled, ConfigKey.WeatherHazardUiEnabled, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(FogTimingUiEnabled, ConfigKey.FogTimingUiEnabled, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(DirectStartUiEnabled, ConfigKey.DirectStartUiEnabled, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(FogApproachUiEnabled, ConfigKey.FogApproachUiEnabled, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(LavaApproachUiEnabled, ConfigKey.LavaApproachUiEnabled, orphanedEntries) ? 1u : 0u) | (TryMigrateLocalizedConfigValue(NextBiomeUiEnabled, ConfigKey.NextBiomeUiEnabled, orphanedEntries) ? 1u : 0u)) != 0)
		{
			base.Config.Save();
			MarkConfigFileLocalizationDirty(saveConfigFile: false, reorderConfigFile: true);
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
		string[] sectionAliases = (from value in new string[4]
			{
				GetConfigSectionName(configKey),
				GetSectionName(configKey, isChineseLanguage: true),
				GetLegacyConfigSectionName(),
				GetLegacySectionName(isChineseLanguage: true)
			}.Concat(GetLegacySectionNames(configKey))
			where !string.IsNullOrWhiteSpace(value)
			select value).Distinct(StringComparer.Ordinal).ToArray();
		string[] array = sectionAliases;
		foreach (string section in array)
		{
			yield return new ConfigDefinition(section, canonicalKey);
			yield return new ConfigDefinition(section, chineseKey);
			foreach (string legacyKey in GetLegacyKeyNames(configKey))
			{
				yield return new ConfigDefinition(section, legacyKey);
			}
		}
	}


	private static IEnumerable<string> GetLegacySectionNames(ConfigKey configKey)
	{
		if (HasBasicSectionLegacyAlias(configKey))
		{
			yield return GetBasicSectionName(isChineseLanguage: false);
			yield return GetBasicSectionName(isChineseLanguage: true);
		}
	}


	private static bool HasBasicSectionLegacyAlias(ConfigKey configKey)
	{
		if (configKey != ConfigKey.ModEnabled && configKey != ConfigKey.FogUiEnabled && configKey != ConfigKey.CampfireLocatorUiEnabled && configKey != ConfigKey.CompassEnabled)
		{
			return configKey == ConfigKey.CompassHotkey;
		}
		return true;
	}


	private static IEnumerable<string> GetLegacyKeyNames(ConfigKey configKey)
	{
		if (configKey == ConfigKey.FogColdSuppression)
		{
			yield return "毒雾寒冷";
		}
		if (configKey == ConfigKey.CampfireLocatorUiEnabled)
		{
			yield return "篭火位置 HUD";
		}
	}


	private static IDictionary GetOrphanedEntries(ConfigFile configFile)
	{
		return configFile?.GetType().GetProperty("OrphanedEntries", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(configFile) as IDictionary;
	}


	private static bool DefinitionsEqual(ConfigDefinition left, ConfigDefinition right)
	{
		if (string.Equals(left?.Section, right?.Section, StringComparison.Ordinal))
		{
			return string.Equals(left?.Key, right?.Key, StringComparison.Ordinal);
		}
		return false;
	}


	private static void ClampConfigValues()
	{
		NormalizeShootZombiesFogUiDefaults();
		if (FogSpeed != null)
		{
			FogSpeed.Value = Mathf.Clamp(FogSpeed.Value, 0.3f, 20f);
		}
		if (FogDelay != null)
		{
			FogDelay.Value = Mathf.Clamp(FogDelay.Value, 20f, 1000f);
		}
		if (FogUiX != null)
		{
			FogUiX.Value = Mathf.Clamp(FogUiX.Value, 0f, 3840f);
		}
		if (FogUiY != null)
		{
			FogUiY.Value = Mathf.Clamp(FogUiY.Value, 0f, 2160f);
		}
		if (FogUiScale != null)
		{
			FogUiScale.Value = Mathf.Clamp(FogUiScale.Value, 0.5f, 2.5f);
		}
		if (FogUiEntrySpacing != null)
		{
			FogUiEntrySpacing.Value = Mathf.Clamp(FogUiEntrySpacing.Value, MinFogUiEntrySpacing, MaxFogUiEntrySpacing);
		}
	}


	private void RegisterConfigChangeHandlers()
	{
		if (base.Config != null)
		{
			base.Config.SettingChanged -= OnConfigSettingChanged;
			base.Config.SettingChanged += OnConfigSettingChanged;
		}
	}


	private void UnregisterConfigChangeHandlers()
	{
		if (base.Config != null)
		{
			base.Config.SettingChanged -= OnConfigSettingChanged;
		}
	}


	private void OnConfigSettingChanged(object sender, SettingChangedEventArgs e)
	{
		MarkConfigFileLocalizationDirty(saveConfigFile: false);
	}


	private void MarkConfigFileLocalizationDirty(bool saveConfigFile, bool reorderConfigFile = false)
	{
		_pendingConfigFileLocalizationRefresh = true;
		_pendingConfigFileLocalizationSave |= saveConfigFile;
		_pendingConfigFileOrderRefresh |= reorderConfigFile;
	}


	private void HandlePendingConfigFileLocalizationRefresh()
	{
		if (_pendingConfigFileLocalizationRefresh && !_isRefreshingLanguage)
		{
			bool isChineseLanguage = _lastDetectedChineseLanguage;
			bool saveConfigFile = _pendingConfigFileLocalizationSave;
			bool reorderConfigFile = _pendingConfigFileOrderRefresh;
			_pendingConfigFileLocalizationRefresh = false;
			_pendingConfigFileLocalizationSave = false;
			_pendingConfigFileOrderRefresh = false;
			TryRefreshLocalizedConfigFile(isChineseLanguage, saveConfigFile, reorderConfigFile);
		}
	}


	private static bool HasAnyConfiguredFogDelay()
	{
		return true;
	}


	private bool ShouldEnforceConfiguredDelay(OrbFogHandler fogHandler)
	{
		if (_initialDelayCompleted || fogHandler == null || !HasAnyConfiguredFogDelay())
		{
			return false;
		}
		if (fogHandler.currentID != 0)
		{
			return IsCampfireDelayPendingForOrigin(fogHandler.currentID);
		}
		return true;
	}


	private bool ShouldSuppressConfiguredNightCold(Character character)
	{
		float currentNormalizedTime;
		if (character != null && character.data != null && !character.data.dead && !character.isBot && !character.isZombie && ShouldDisableNightColdFeatureLocally() && IsNightColdStageActive())
		{
			return TryIsNightTime(out currentNormalizedTime);
		}
		return false;
	}


	private void HandleFogUiConfigChanges()
	{
		bool fogUiEnabled = FogUiEnabled?.Value ?? true;
		bool campfireLocatorUiEnabled = CampfireLocatorUiEnabled?.Value ?? true;
		float fogUiX = FogUiX?.Value ?? 60f;
		float fogUiY = FogUiY?.Value ?? 0f;
		float fogUiScale = FogUiScale?.Value ?? 0.9f;
		FogUiLayoutKind fogUiLayout = FogUiLayout?.Value ?? FogUiLayoutKind.Horizontal;
		if (fogUiEnabled != _lastFogUiEnabledState || campfireLocatorUiEnabled != _lastCampfireLocatorUiEnabledState || !Approximately(fogUiX, _lastFogUiX) || !Approximately(fogUiY, _lastFogUiY) || !Approximately(fogUiScale, _lastFogUiScale) || fogUiLayout != _lastFogUiLayout)
		{
			_lastFogUiEnabledState = fogUiEnabled;
			_lastCampfireLocatorUiEnabledState = campfireLocatorUiEnabled;
			_lastFogUiX = fogUiX;
			_lastFogUiY = fogUiY;
			_lastFogUiScale = fogUiScale;
			_lastFogUiLayout = fogUiLayout;
			CreateFogUi();
			CreateCampfireLocatorUi();
		}
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
			base.Logger.LogWarning("[Fog&ColdControl] Failed to reinitialize localized config: " + ex.Message);
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
			ConfigEntryBase[] configEntriesSnapshot = GetConfigEntriesSnapshot(base.Config);
			foreach (ConfigEntryBase entry in configEntriesSnapshot)
			{
				if (!(entry?.Definition == null) && entry.Description != null && TryGetConfigKey(entry.Definition.Key, out var configKey))
				{
					string localizedDescription = GetLocalizedDescription(configKey, isChineseLanguage);
					if (!string.IsNullOrWhiteSpace(localizedDescription))
					{
						SetPrivateField(entry.Description, "<Description>k__BackingField", localizedDescription);
					}
				}
			}
		}
		catch (Exception ex)
		{
			base.Logger.LogWarning("[Fog&ColdControl] Failed to apply localized config metadata: " + ex.Message);
		}
	}


	private static ConfigEntryBase[] GetConfigEntriesSnapshot(ConfigFile configFile)
	{
		if (configFile == null)
		{
			return Array.Empty<ConfigEntryBase>();
		}
		if (!(ConfigFileEntriesProperty?.GetValue(configFile) is IDictionary { Count: not 0 } dictionary))
		{
			return Array.Empty<ConfigEntryBase>();
		}
		return (from entry in dictionary.Values.Cast<object>().OfType<ConfigEntryBase>()
			where entry != null
			select entry).ToArray();
	}


	private static bool TryGetConfigKey(string keyName, out ConfigKey configKey)
	{
		foreach (ConfigKey candidate in Enum.GetValues(typeof(ConfigKey)))
		{
			if (string.Equals(keyName, GetConfigKeyName(candidate), StringComparison.OrdinalIgnoreCase) || string.Equals(keyName, GetKeyName(candidate, isChineseLanguage: true), StringComparison.OrdinalIgnoreCase) || GetLegacyKeyNames(candidate).Any((string legacyKey) => string.Equals(keyName, legacyKey, StringComparison.OrdinalIgnoreCase)))
			{
				configKey = candidate;
				return true;
			}
		}
		configKey = ConfigKey.ModEnabled;
		return false;
	}


	private void TryRefreshLocalizedConfigFile(bool isChineseLanguage, bool saveConfigFile, bool reorderConfigFile)
	{
		try
		{
			if (base.Config != null && !string.IsNullOrWhiteSpace(base.Config.ConfigFilePath))
			{
				if (saveConfigFile)
				{
					base.Config.Save();
				}
				RewriteConfigFileLocalization(base.Config.ConfigFilePath, isChineseLanguage, reorderConfigFile);
			}
		}
		catch (Exception ex)
		{
			base.Logger.LogWarning("[Fog&ColdControl] Failed to refresh localized config file: " + ex.Message);
		}
	}


	private static void RewriteConfigFileLocalization(string configFilePath, bool isChineseLanguage, bool reorderConfigFile)
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
			string obj = originalLines[i] ?? string.Empty;
			if (!string.Equals(obj, rewrittenLines[i] = RewriteConfigFileLine(obj, isChineseLanguage), StringComparison.Ordinal))
			{
				changed = true;
			}
		}
		if (reorderConfigFile)
		{
			string[] orderedLines = ReorderConfigFileLines(rewrittenLines, isChineseLanguage);
			if (!Enumerable.SequenceEqual(rewrittenLines, orderedLines))
			{
				rewrittenLines = orderedLines;
				changed = true;
			}
		}
		if (changed)
		{
			File.WriteAllLines(configFilePath, rewrittenLines);
		}
	}


	private static string[] ReorderConfigFileLines(string[] lines, bool isChineseLanguage)
	{
		if (lines == null || lines.Length == 0)
		{
			return Array.Empty<string>();
		}
		string basicSectionName = GetBasicSectionName(isChineseLanguage);
		string adjustmentSectionName = GetAdjustmentSectionName(isChineseLanguage);
		List<string> preambleLines = new List<string>();
		List<string> pendingBlockLines = new List<string>();
		Dictionary<ConfigKey, ConfigFileEntryBlock> knownBlocks = new Dictionary<ConfigKey, ConfigFileEntryBlock>();
		Dictionary<string, List<ConfigFileEntryBlock>> unknownBlocksBySection = new Dictionary<string, List<ConfigFileEntryBlock>>(StringComparer.Ordinal);
		HashSet<string> emittedSections = new HashSet<string>(StringComparer.Ordinal);
		string currentSectionName = string.Empty;
		bool insideSection = false;
		for (int i = 0; i < lines.Length; i++)
		{
			string line = lines[i] ?? string.Empty;
			if (TryGetConfigSectionHeaderName(line, out var sectionName))
			{
				FlushConfigFileUnknownBlock(pendingBlockLines, currentSectionName, unknownBlocksBySection);
				currentSectionName = sectionName;
				insideSection = true;
				continue;
			}
			if (!insideSection)
			{
				preambleLines.Add(line);
				continue;
			}
			pendingBlockLines.Add(line);
			if (TryGetConfigKeyFromValueLine(line, out var configKey))
			{
				ConfigFileEntryBlock block = new ConfigFileEntryBlock
				{
					Key = configKey,
					HasKnownKey = true,
					SectionName = currentSectionName,
					Lines = new List<string>(pendingBlockLines)
				};
				if (!knownBlocks.ContainsKey(configKey) || IsPreferredConfigFileSection(currentSectionName, configKey, isChineseLanguage))
				{
					knownBlocks[configKey] = block;
				}
				pendingBlockLines.Clear();
			}
		}
		FlushConfigFileUnknownBlock(pendingBlockLines, currentSectionName, unknownBlocksBySection);
		List<string> orderedLines = new List<string>();
		foreach (string line2 in preambleLines)
		{
			orderedLines.Add(line2);
		}
		AppendConfigFileSection(orderedLines, basicSectionName, BasicConfigFileOrder, knownBlocks, unknownBlocksBySection, emittedSections);
		AppendConfigFileSection(orderedLines, adjustmentSectionName, AdjustmentConfigFileOrder, knownBlocks, unknownBlocksBySection, emittedSections);
		foreach (KeyValuePair<string, List<ConfigFileEntryBlock>> pair in unknownBlocksBySection)
		{
			if (emittedSections.Contains(pair.Key) || pair.Value == null || pair.Value.Count == 0)
			{
				continue;
			}
			AppendConfigFileSectionHeader(orderedLines, pair.Key);
			foreach (ConfigFileEntryBlock block2 in pair.Value)
			{
				AppendConfigFileBlock(orderedLines, block2.Lines);
			}
			emittedSections.Add(pair.Key);
		}
		return orderedLines.ToArray();
	}


	private static void FlushConfigFileUnknownBlock(List<string> pendingBlockLines, string currentSectionName, Dictionary<string, List<ConfigFileEntryBlock>> unknownBlocksBySection)
	{
		if (pendingBlockLines == null || pendingBlockLines.Count == 0 || string.IsNullOrWhiteSpace(currentSectionName))
		{
			pendingBlockLines?.Clear();
			return;
		}
		if (pendingBlockLines.All(string.IsNullOrWhiteSpace))
		{
			pendingBlockLines.Clear();
			return;
		}
		if (!unknownBlocksBySection.TryGetValue(currentSectionName, out var blocks))
		{
			blocks = (unknownBlocksBySection[currentSectionName] = new List<ConfigFileEntryBlock>());
		}
		blocks.Add(new ConfigFileEntryBlock
		{
			SectionName = currentSectionName,
			Lines = new List<string>(pendingBlockLines)
		});
		pendingBlockLines.Clear();
	}


	private static void AppendConfigFileSection(List<string> output, string sectionName, ConfigKey[] orderedKeys, Dictionary<ConfigKey, ConfigFileEntryBlock> knownBlocks, Dictionary<string, List<ConfigFileEntryBlock>> unknownBlocksBySection, HashSet<string> emittedSections)
	{
		bool num = orderedKeys?.Any((ConfigKey key2) => knownBlocks.ContainsKey(key2)) ?? false;
		List<ConfigFileEntryBlock> unknownBlocks;
		bool hasUnknownBlocks = unknownBlocksBySection.TryGetValue(sectionName, out unknownBlocks) && unknownBlocks != null && unknownBlocks.Count > 0;
		if (!num && !hasUnknownBlocks)
		{
			return;
		}
		AppendConfigFileSectionHeader(output, sectionName);
		if (orderedKeys != null)
		{
			foreach (ConfigKey key in orderedKeys)
			{
				if (knownBlocks.TryGetValue(key, out var block))
				{
					AppendConfigFileBlock(output, block.Lines);
				}
			}
		}
		if (hasUnknownBlocks)
		{
			foreach (ConfigFileEntryBlock block2 in unknownBlocks)
			{
				AppendConfigFileBlock(output, block2.Lines);
			}
		}
		emittedSections.Add(sectionName);
	}


	private static void AppendConfigFileSectionHeader(List<string> output, string sectionName)
	{
		if (output != null && !string.IsNullOrWhiteSpace(sectionName))
		{
			TrimTrailingBlankLines(output);
			if (output.Count > 0)
			{
				output.Add(string.Empty);
			}
			output.Add("[" + sectionName + "]");
		}
	}


	private static void AppendConfigFileBlock(List<string> output, List<string> lines)
	{
		if (output != null && lines != null && lines.Count != 0)
		{
			if (output.Count > 0 && !string.IsNullOrWhiteSpace(output[output.Count - 1]))
			{
				output.Add(string.Empty);
			}
			int startIndex;
			for (startIndex = 0; startIndex < lines.Count && string.IsNullOrWhiteSpace(lines[startIndex]); startIndex++)
			{
			}
			for (int i = startIndex; i < lines.Count; i++)
			{
				output.Add(lines[i] ?? string.Empty);
			}
		}
	}


	private static bool IsPreferredConfigFileSection(string sectionName, ConfigKey configKey, bool isChineseLanguage)
	{
		return string.Equals(sectionName, GetSectionName(configKey, isChineseLanguage), StringComparison.Ordinal);
	}


	private static bool TryGetConfigSectionHeaderName(string line, out string sectionName)
	{
		sectionName = string.Empty;
		if (string.IsNullOrWhiteSpace(line))
		{
			return false;
		}
		string trimmed = line.Trim();
		if (!trimmed.StartsWith("[", StringComparison.Ordinal) || !trimmed.EndsWith("]", StringComparison.Ordinal))
		{
			return false;
		}
		sectionName = trimmed.Substring(1, trimmed.Length - 2).Trim();
		return !string.IsNullOrWhiteSpace(sectionName);
	}


	private static bool TryGetConfigKeyFromValueLine(string line, out ConfigKey configKey)
	{
		configKey = ConfigKey.ModEnabled;
		if (string.IsNullOrWhiteSpace(line))
		{
			return false;
		}
		string trimmed = line.TrimStart(Array.Empty<char>());
		if (trimmed.StartsWith("#", StringComparison.Ordinal) || trimmed.StartsWith(";", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal))
		{
			return false;
		}
		int equalsIndex = line.IndexOf('=');
		if (equalsIndex <= 0)
		{
			return false;
		}
		int keyStart;
		for (keyStart = 0; keyStart < equalsIndex && char.IsWhiteSpace(line[keyStart]); keyStart++)
		{
		}
		int keyEnd = equalsIndex - 1;
		while (keyEnd >= keyStart && char.IsWhiteSpace(line[keyEnd]))
		{
			keyEnd--;
		}
		if (keyEnd < keyStart)
		{
			return false;
		}
		return TryGetConfigKey(line.Substring(keyStart, keyEnd - keyStart + 1), out configKey);
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
			if (!TryGetLocalizedSectionName(trimmed.Substring(1, trimmed.Length - 2).Trim(), isChineseLanguage, out var localizedSectionName))
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
		int keyStart;
		for (keyStart = 0; keyStart < equalsIndex && char.IsWhiteSpace(line[keyStart]); keyStart++)
		{
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
		if (!TryGetConfigKey(line.Substring(keyStart, keyEnd - keyStart + 1), out var configKey))
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
			localizedSectionName = GetAdjustmentSectionName(isChineseLanguage);
			return true;
		}
		if (MatchesBasicSectionName(sectionName))
		{
			localizedSectionName = GetBasicSectionName(isChineseLanguage);
			return true;
		}
		localizedSectionName = string.Empty;
		return false;
	}


	private static bool MatchesBasicSectionName(string sectionName)
	{
		if (!string.Equals(sectionName, "Basic", StringComparison.OrdinalIgnoreCase) && !string.Equals(sectionName, GetBasicSectionName(isChineseLanguage: true), StringComparison.Ordinal) && !string.Equals(sectionName, GetLegacyConfigSectionName(), StringComparison.OrdinalIgnoreCase))
		{
			return string.Equals(sectionName, GetLegacySectionName(isChineseLanguage: true), StringComparison.Ordinal);
		}
		return true;
	}


	private static bool MatchesAdjustmentSectionName(string sectionName)
	{
		if (!string.Equals(sectionName, "Adjustments", StringComparison.OrdinalIgnoreCase))
		{
			return string.Equals(sectionName, GetAdjustmentSectionName(isChineseLanguage: true), StringComparison.Ordinal);
		}
		return true;
	}


	private void TryLocalizeVisibleModConfigUi()
	{
		if (!TryGetModConfigMenuInstance(out var menuType, out var menuInstance) || !(menuInstance is Behaviour behaviour) || behaviour == null)
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
		if (!Chainloader.PluginInfos.TryGetValue("com.github.PEAKModding.PEAKLib.ModConfig", out var pluginInfo) || pluginInfo?.Instance == null)
		{
			return false;
		}
		Assembly assembly = pluginInfo.Instance.GetType().Assembly;
		menuType = assembly.GetType("PEAKLib.ModConfig.Components.ModdedSettingsMenu");
		menuInstance = menuType?.GetProperty("Instance", StaticBindingFlags)?.GetValue(null);
		if (menuType != null)
		{
			return menuInstance != null;
		}
		return false;
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


	private void ApplyTextLocalizationToRoot(Transform root, Dictionary<string, string> map)
	{
		if (root == null || map == null || map.Count == 0)
		{
			return;
		}
		TMP_Text[] componentsInChildren = root.GetComponentsInChildren<TMP_Text>(includeInactive: true);
		foreach (TMP_Text text in componentsInChildren)
		{
			if (!(text == null))
			{
				string trimmed = text.text?.Trim();
				if (!string.IsNullOrWhiteSpace(trimmed) && map.TryGetValue(trimmed, out var localized) && !string.Equals(text.text, localized, StringComparison.Ordinal))
				{
					text.text = localized;
				}
			}
		}
	}


	private Dictionary<string, string> BuildModConfigUiLocalizationMap(bool isChineseLanguage)
	{
		Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
		AddUiLocalizationPair(map, "Fog Climb", "Fog&ColdControl");
		AddUiLocalizationPair(map, "FogClimb", "Fog&ColdControl");
		AddUiLocalizationPair(map, "Fog&ColdControl", "Fog&ColdControl");
		AddUiLocalizationPair(map, "FogAndColdControl", "Fog&ColdControl");
		AddUiLocalizationPair(map, "毒雾攀登", GetLocalizedModDisplayName(isChineseLanguage));
		foreach (string sectionName in (from ConfigKey configKey2 in Enum.GetValues(typeof(ConfigKey))
			select GetConfigSectionName(configKey2)).Concat(from ConfigKey configKey2 in Enum.GetValues(typeof(ConfigKey))
			select GetSectionName(configKey2, isChineseLanguage: true)).Concat(new string[2]
		{
			GetLegacyConfigSectionName(),
			GetLegacySectionName(isChineseLanguage: true)
		}).Distinct(StringComparer.Ordinal))
		{
			if (string.Equals(sectionName, GetLegacyConfigSectionName(), StringComparison.Ordinal) || string.Equals(sectionName, GetLegacySectionName(isChineseLanguage: true), StringComparison.Ordinal))
			{
				AddUiLocalizationPair(map, sectionName, GetBasicSectionName(isChineseLanguage));
				continue;
			}
			ConfigKey matchedKey = Enum.GetValues(typeof(ConfigKey)).Cast<ConfigKey>().FirstOrDefault((ConfigKey candidate) => string.Equals(sectionName, GetConfigSectionName(candidate), StringComparison.OrdinalIgnoreCase) || string.Equals(sectionName, GetSectionName(candidate, isChineseLanguage: true), StringComparison.Ordinal));
			AddUiLocalizationPair(map, sectionName, GetSectionName(matchedKey, isChineseLanguage));
		}
		foreach (ConfigKey configKey in Enum.GetValues(typeof(ConfigKey)))
		{
			AddUiLocalizationPair(map, GetConfigKeyName(configKey), GetKeyName(configKey, isChineseLanguage));
			AddUiLocalizationPair(map, GetKeyName(configKey, isChineseLanguage: true), GetKeyName(configKey, isChineseLanguage));
			foreach (string legacyKey in GetLegacyKeyNames(configKey))
			{
				AddUiLocalizationPair(map, legacyKey, GetKeyName(configKey, isChineseLanguage));
			}
			AddUiLocalizationPair(map, GetLocalizedDescription(configKey, isChineseLanguage: false), GetLocalizedDescription(configKey, isChineseLanguage));
			AddUiLocalizationPair(map, GetLocalizedDescription(configKey, isChineseLanguage: true), GetLocalizedDescription(configKey, isChineseLanguage));
		}
		return map;
	}


	private static void AddUiLocalizationPair(Dictionary<string, string> map, string source, string localized)
	{
		if (map != null && !string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(localized))
		{
			string sourceTrimmed = source.Trim();
			string localizedTrimmed = (map[sourceTrimmed] = localized.Trim());
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
				isChineseLanguage = languageValue == 9;
				return true;
			}
			string languageText = PlayerPrefs.GetString("LanguageSetting", string.Empty);
			if (string.IsNullOrWhiteSpace(languageText))
			{
				return false;
			}
			if (int.TryParse(languageText, NumberStyles.Integer, CultureInfo.InvariantCulture, out languageValue))
			{
				isChineseLanguage = languageValue == 9;
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


	private static string GetConfigSectionName(ConfigKey configKey)
	{
		return GetSectionName(configKey, isChineseLanguage: false);
	}


	private static string GetSectionName(ConfigKey configKey, bool isChineseLanguage)
	{
		if (IsAdjustmentConfigKey(configKey))
		{
			return GetAdjustmentSectionName(isChineseLanguage);
		}
		return GetBasicSectionName(isChineseLanguage);
	}


	private static string GetBasicSectionName(bool isChineseLanguage)
	{
		if (!isChineseLanguage)
		{
			return "Basic";
		}
		return "基本";
	}


	private static string GetAdjustmentSectionName(bool isChineseLanguage)
	{
		if (!isChineseLanguage)
		{
			return "Adjustments";
		}
		return "调整";
	}


	private static string GetLegacyConfigSectionName()
	{
		return "Fog";
	}


	private static string GetLegacySectionName(bool isChineseLanguage)
	{
		if (!isChineseLanguage)
		{
			return "Fog";
		}
		return "毒雾";
	}


	private static bool IsAdjustmentConfigKey(ConfigKey configKey)
	{
		if (configKey != ConfigKey.ModEnabled && configKey != ConfigKey.FogUiEnabled && configKey != ConfigKey.CampfireLocatorUiEnabled && configKey != ConfigKey.CompassEnabled && configKey != ConfigKey.CompassHotkey && configKey != ConfigKey.FogUiX && configKey != ConfigKey.FogUiY && !IsHudEntryToggleConfigKey(configKey))
		{
			return configKey == ConfigKey.FogUiScale || configKey == ConfigKey.FogUiEntrySpacing || configKey == ConfigKey.FogUiLayout || configKey == ConfigKey.FogUiBackgroundOpacity;
		}
		return true;
	}


	private static bool IsHudEntryToggleConfigKey(ConfigKey configKey)
	{
		return configKey switch
		{
			ConfigKey.FogStateUiEnabled => true,
			ConfigKey.RunTimerUiEnabled => true,
			ConfigKey.DayNightTimeUiEnabled => true,
			ConfigKey.AltitudeUiEnabled => true,
			ConfigKey.FogSpeedUiEnabled => true,
			ConfigKey.CampfireDistanceUiEnabled => true,
			ConfigKey.WeatherHazardUiEnabled => true,
			ConfigKey.FogTimingUiEnabled => true,
			ConfigKey.DirectStartUiEnabled => true,
			ConfigKey.FogApproachUiEnabled => true,
			ConfigKey.LavaApproachUiEnabled => true,
			ConfigKey.NextBiomeUiEnabled => true,
			_ => false,
		};
	}


	private static string GetConfigKeyName(ConfigKey configKey)
	{
		return GetKeyName(configKey, isChineseLanguage: false);
	}


	private static string GetKeyName(ConfigKey configKey, bool isChineseLanguage)
	{
		return configKey switch
		{
			ConfigKey.ModEnabled => isChineseLanguage ? "模块开关" : "Enable Mod", 
			ConfigKey.FogColdSuppression => isChineseLanguage ? "毒雾寒冷阻止" : "Suppress Fog Cold", 
			ConfigKey.NightColdEnabled => isChineseLanguage ? "夜晚寒冷" : "Night Cold", 
			ConfigKey.FogSpeed => isChineseLanguage ? "毒雾移动速度" : "Fog Speed", 
			ConfigKey.FogDelay => isChineseLanguage ? "毒雾延迟时间s" : "Fog Delay (s)", 
			ConfigKey.CompassEnabled => isChineseLanguage ? "指南针功能开关" : "Compass Feature", 
			ConfigKey.CompassHotkey => isChineseLanguage ? "指南针生成按键" : "Compass Hotkey", 
			ConfigKey.FogPauseHotkey => isChineseLanguage ? "毒雾暂停按键" : "Pause Fog Hotkey", 
			ConfigKey.FogUiEnabled => isChineseLanguage ? "UI启用" : "Fog UI", 
			ConfigKey.CampfireLocatorUiEnabled => isChineseLanguage ? "篝火位置 HUD" : "Campfire Locator HUD", 
			ConfigKey.FogUiX => isChineseLanguage ? "UI X位置" : "UI X Position", 
			ConfigKey.FogUiY => isChineseLanguage ? "UI Y位置" : "UI Y Position", 
			ConfigKey.FogUiScale => isChineseLanguage ? "UI缩放" : "UI Scale", 
			ConfigKey.FogUiEntrySpacing => isChineseLanguage ? "UI间距" : "UI Entry Spacing", 
			ConfigKey.FogUiLayout => isChineseLanguage ? "UI排布" : "UI Layout", 
			ConfigKey.FogUiBackgroundOpacity => isChineseLanguage ? "UI背景透明度" : "UI Background Opacity",
			ConfigKey.FogStateUiEnabled => isChineseLanguage ? "状态图标显示" : "Fog State Icon",
			ConfigKey.RunTimerUiEnabled => isChineseLanguage ? "计时器显示" : "Run Timer",
			ConfigKey.DayNightTimeUiEnabled => isChineseLanguage ? "昼夜时间显示" : "Day/Night Time",
			ConfigKey.AltitudeUiEnabled => isChineseLanguage ? "当前高度显示" : "Altitude",
			ConfigKey.FogSpeedUiEnabled => isChineseLanguage ? "毒雾速度显示" : "Fog Speed Display",
			ConfigKey.CampfireDistanceUiEnabled => isChineseLanguage ? "篝火距离显示" : "Campfire Distance",
			ConfigKey.WeatherHazardUiEnabled => isChineseLanguage ? "天气预警显示" : "Weather Hazard",
			ConfigKey.FogTimingUiEnabled => isChineseLanguage ? "毒雾延迟倒计时显示" : "Fog Delay Countdown",
			ConfigKey.DirectStartUiEnabled => isChineseLanguage ? "直达进度显示" : "Direct Start Progress",
			ConfigKey.FogApproachUiEnabled => isChineseLanguage ? "毒雾接近显示" : "Fog Approach",
			ConfigKey.LavaApproachUiEnabled => isChineseLanguage ? "熔岩接近显示" : "Lava Approach",
			ConfigKey.NextBiomeUiEnabled => isChineseLanguage ? "下一生态区显示" : "Next Biome",
			_ => string.Empty, 
		};
	}


	private static string GetLocalizedDescription(ConfigKey configKey, bool isChineseLanguage)
	{
		return (configKey switch
		{
			ConfigKey.ModEnabled => isChineseLanguage ? "总开关。开启后由主机接管 Fog&ColdControl 的毒雾、联机同步和指南针奖励；关闭后不生效。" : "Master switch for Fog&ColdControl. When enabled, the host controls fog behavior, multiplayer sync, and compass rewards.", 
			ConfigKey.FogColdSuppression => isChineseLanguage ? "是否阻止毒雾带来的寒冷值。开启=阻止毒雾寒冷（UI 显示 OFF，橙红色）；关闭=保留毒雾寒冷（UI 显示 ON，绿色）。只影响毒雾寒冷，不影响夜晚寒冷。默认关闭。" : "Whether to block cold caused by fog. On = blocks fog cold (UI shows OFF, orange); Off = keeps fog cold (UI shows ON, green). Only affects fog cold, not night cold. Default: Off.",
			ConfigKey.NightColdEnabled => isChineseLanguage ? "控制夜晚寒冷功能。只在天阶 5 及以上生效，行为与原版一致。默认开启。" : "Controls night cold. Only applies on Ascent 5 and above, matching vanilla behavior. Default: On.", 
			ConfigKey.FogSpeed => isChineseLanguage ? "控制毒雾推进速度。范围 0.3~20，数值越大，毒雾推进越快。默认 0.4。" : "Controls how fast the fog moves. Range: 0.3 to 20. Higher values make the fog move faster. Default: 0.4.", 
			ConfigKey.FogDelay => isChineseLanguage ? "控制首段毒雾开始移动前的等待时间。范围 20~1000 秒。默认 900 秒。" : "Controls how long the first fog segment waits before moving. Range: 20 to 1000 seconds. Default: 900 seconds.", 
			ConfigKey.CompassEnabled => isChineseLanguage ? "指南针功能总开关。控制自动发放、按键生成和大厅提示。默认关闭。" : "Master switch for compass features, including automatic rewards, hotkey spawning, and the lobby prompt. Default: Off.", 
			ConfigKey.CompassHotkey => isChineseLanguage ? "按下后在玩家正前方生成一个普通指南针。设为 None 可禁用该按键。" : "Spawns a normal compass in front of the player. Set this to None to disable the hotkey.", 
			ConfigKey.FogPauseHotkey => isChineseLanguage ? "主机专用按键，用于暂停或继续毒雾推进。设为 None 可禁用。默认 Y。" : "Host-only hotkey used to pause or resume fog movement. Set this to None to disable it. Default: Y.", 
			ConfigKey.FogUiEnabled => isChineseLanguage ? "控制是否显示毒雾相关 HUD。" : "Shows or hides the Fog&ColdControl HUD.", 
			ConfigKey.CampfireLocatorUiEnabled => isChineseLanguage ? "控制是否显示屏幕顶部的篝火位置 HUD。默认开启。" : "Shows or hides the top-screen campfire locator HUD. Default: On.", 
			ConfigKey.FogUiX => isChineseLanguage ? "毒雾 HUD 水平位置（基于屏幕左下角向右偏移，单位像素）。运行时会按当前分辨率自动约束在屏幕内。范围 0~3840，默认 60。" : "Horizontal position of the Fog&ColdControl HUD (offset right from the bottom-left corner, in pixels). Automatically clamped to the screen at runtime based on the current resolution. Range: 0 to 3840. Default: 60.",
			ConfigKey.FogUiY => isChineseLanguage ? "毒雾 HUD 垂直位置（基于屏幕左下角向上偏移，单位像素）。运行时会按当前分辨率自动约束在屏幕内。范围 0~2160，默认 0。" : "Vertical position of the Fog&ColdControl HUD (offset up from the bottom-left corner, in pixels). Automatically clamped to the screen at runtime based on the current resolution. Range: 0 to 2160. Default: 0.",
			ConfigKey.FogUiScale => isChineseLanguage ? "调整毒雾 HUD 的整体缩放。默认 0.9。" : "Adjusts the overall scale of the Fog&ColdControl HUD. Default: 0.9.",
			ConfigKey.FogUiEntrySpacing => isChineseLanguage ? "调整毒雾 HUD 各条目之间的间距（像素）。数值越小越紧凑。范围 0~40，默认 8。" : "Adjusts the spacing between Fog&ColdControl HUD entries (in pixels). Smaller values pack entries tighter. Range: 0 to 40. Default: 8.",
			ConfigKey.FogUiLayout => isChineseLanguage ? "毒雾 HUD 条目的排布方式。Horizontal=横向排列（默认），Vertical=垂直排列。" : "Arrangement of Fog&ColdControl HUD entries. Horizontal = side by side (default), Vertical = stacked.",
			ConfigKey.FogUiBackgroundOpacity => isChineseLanguage ? "毒雾 HUD 背景的黑色圆角半透明背景透明度。范围 0~1，0=完全透明（不显示背景），1=完全不透明。默认 0。" : "Opacity of the Fog&ColdControl HUD's rounded semi-transparent black background. Range: 0 to 1. 0 = fully transparent (no background), 1 = fully opaque. Default: 0.",
			ConfigKey.FogStateUiEnabled => isChineseLanguage ? "控制是否显示毒雾运行状态图标条目。默认开启。" : "Shows or hides the fog runtime state icon entry. Default: On.",
			ConfigKey.RunTimerUiEnabled => isChineseLanguage ? "控制是否显示本局计时器条目。默认开启。" : "Shows or hides the run timer entry. Default: On.",
			ConfigKey.DayNightTimeUiEnabled => isChineseLanguage ? "控制是否显示昼夜时间条目（白天/夜晚图标与天数）。默认开启。" : "Shows or hides the day/night time entry (day/night icon and day count). Default: On.",
			ConfigKey.AltitudeUiEnabled => isChineseLanguage ? "控制是否显示当前高度条目。默认开启。" : "Shows or hides the current altitude entry. Default: On.",
			ConfigKey.FogSpeedUiEnabled => isChineseLanguage ? "控制是否显示毒雾速度条目。默认开启。" : "Shows or hides the fog speed entry. Default: On.",
			ConfigKey.CampfireDistanceUiEnabled => isChineseLanguage ? "控制是否显示下一个篝火距离条目。默认开启。" : "Shows or hides the next campfire distance entry. Default: On.",
			ConfigKey.WeatherHazardUiEnabled => isChineseLanguage ? "控制是否显示天气预警条目（风/雪/雨图标与剩余时间）。默认开启。" : "Shows or hides the weather hazard entries (wind/snow/rain icon with remaining time). Default: On.",
			ConfigKey.FogTimingUiEnabled => isChineseLanguage ? "控制是否显示毒雾延迟倒计时条目。默认开启。" : "Shows or hides the fog delay countdown entry. Default: On.",
			ConfigKey.DirectStartUiEnabled => isChineseLanguage ? "控制是否显示直达进度条目。默认开启。" : "Shows or hides the direct start progress entry. Default: On.",
			ConfigKey.FogApproachUiEnabled => isChineseLanguage ? "控制是否显示毒雾接近条目（距离与预计到达时间）。默认开启。" : "Shows or hides the fog approach entry (distance and ETA). Default: On.",
			ConfigKey.LavaApproachUiEnabled => isChineseLanguage ? "控制是否显示熔岩/石化接近条目。默认开启。" : "Shows or hides the lava/petrify approach entry. Default: On.",
			ConfigKey.NextBiomeUiEnabled => isChineseLanguage ? "控制是否显示下一生态区条目。默认开启。" : "Shows or hides the next biome entry. Default: On.",
			_ => string.Empty, 
		}).Replace("FogClimb", "Fog&ColdControl").Replace("Fog Climb", "Fog&ColdControl");
	}
}
