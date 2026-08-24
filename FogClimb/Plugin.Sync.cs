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

// FogClimb subsystem: Sync
public sealed partial class Plugin
{


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
			SetFogUiVisible(visible: false);
			SetCampfireLocatorUiVisible(visible: false);
			CleanupCompassLobbyNotice();
			return;
		}
		_lastFogStateSyncTime = -0.18f;
		_lastRemoteStatusSyncTime = -0.25f;
		_lastCompassGrantSyncTime = -0.75f;
		if (modEnabled)
		{
			_fogStateInitialized = false;
		}
	}


	private static bool HasRemotePlayers()
	{
		if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null)
		{
			return PhotonNetwork.CurrentRoom.PlayerCount > 1;
		}
		return false;
	}


	private void RefreshRemotePlayerJoinGraceState()
	{
		if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
		{
			_remotePlayerFirstSeenTimes.Clear();
			_remotePlayerCompassBaselineCounts.Clear();
			return;
		}
		int localActorNumber = PhotonNetwork.LocalPlayer?.ActorNumber ?? (-1);
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
			if (PhotonNetwork.IsMasterClient && IsModFeatureEnabled() && _pendingWeatherStateBroadcastTime < 0f)
			{
				_pendingWeatherStateBroadcastTime = Time.unscaledTime + 3f;
				_weatherStateBroadcastRetryCount = 0;
			}
			if (shouldLogJoinGrace)
			{
				if (_totalCompassGrantCount > 0)
				{
					base.Logger.LogInfo(string.Format("[{0}] Delaying host sync for remote player #{1} for {2:F1}s. Skipping {3} historical compass grants for late join safety.", "Fog&ColdControl", player.ActorNumber, 8f, _totalCompassGrantCount));
				}
				else
				{
					base.Logger.LogInfo(string.Format("[{0}] Delaying host sync for remote player #{1} for {2:F1}s.", "Fog&ColdControl", player.ActorNumber, 8f));
				}
			}
		}
		int[] array = _remotePlayerFirstSeenTimes.Keys.Where((int item) => !activeRemoteActors.Contains(item)).ToArray();
		foreach (int actorNumber in array)
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
		return _remotePlayerFirstSeenTimes.Values.Any((float firstSeenTime) => now - firstSeenTime < 8f);
	}


	private bool TryBroadcastWeatherStateToGuests()
	{
		if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || !IsModFeatureEnabled())
		{
			return true;
		}
		StormVisual[] visuals = ResolveCachedStormVisuals();
		if (visuals == null || visuals.Length == 0)
		{
			return false;
		}
		foreach (StormVisual visual in visuals)
		{
			if (visual == null)
			{
				continue;
			}
			WindChillZone zone = ReadStormVisualZone(visual);
			if (zone == null)
			{
				continue;
			}
			PhotonView photonView = zone.GetComponent<PhotonView>();
			if (photonView == null)
			{
				continue;
			}
			try
			{
				photonView.RPC("RPCA_ToggleWind", RpcTarget.Others, zone.windActive, ReadWindChillDirection(zone), ReadWindChillUntilSwitch(zone));
			}
			catch (Exception ex)
			{
				base.Logger.LogDebug("[Fog&ColdControl] Weather state broadcast skipped: " + ex.Message);
			}
		}
		return true;
	}


	private string GetCompactGuestSpeedBadge()
	{
		if (!TryGetGuestEstimatedFogSpeed(out var estimatedSpeed))
		{
			return string.Empty;
		}
		return BuildFogUiBadge("SPD", "#8EC5FF", GetGuestFogSpeedUiEntryText(estimatedSpeed));
	}
}
