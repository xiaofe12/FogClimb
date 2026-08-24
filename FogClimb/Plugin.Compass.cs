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

// FogClimb subsystem: Compass
public sealed partial class Plugin
{


	internal static ConfigEntry<bool> CompassEnabled { get; private set; }


	internal static ConfigEntry<KeyCode> CompassHotkey { get; private set; }


	private void TryGrantInitialCompassIfNeeded()
	{
		if (!_initialCompassGranted && !(_orbFogHandler == null) && _orbFogHandler.currentID == 0 && _initialDelayCompleted && _orbFogHandler.isMoving && !HasRemotePlayersInJoinGracePeriod())
		{
			_initialCompassGranted = true;
			GrantCompassToAllPlayers("initial-delay-ended");
		}
	}


	private bool TryResolveCompassItem()
	{
		if (_compassItem != null)
		{
			return true;
		}
		if (TryResolveCompassItemOverride(out var overriddenCompass))
		{
			_compassItem = overriddenCompass;
			return true;
		}
		ItemDatabase database = SingletonAsset<ItemDatabase>.Instance;
		if ((UnityEngine.Object)(object)database == null || database.Objects == null)
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
			CompassPointer compassPointer = item.GetComponentInChildren<CompassPointer>(includeInactive: true);
			if (!(compassPointer == null) && compassPointer.compassType == CompassPointer.CompassType.Normal)
			{
				normalCompassCandidates.Add(DescribeCompassItem(item));
				if (LooksLikeStandardCompassItem(item))
				{
					_compassItem = item;
					base.Logger.LogInfo("[Fog&ColdControl] Using normal compass item: " + DescribeCompassItem(item));
					return true;
				}
			}
		}
		if (normalCompassCandidates.Count > 0)
		{
			base.Logger.LogWarning("[Fog&ColdControl] Failed to identify the standard compass automatically. Set CompassItemIdOverride manually. Candidates: " + string.Join(", ", normalCompassCandidates));
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
		if (!ItemDatabase.TryGetItem(CompassItemIdOverride, out var resolvedItem) || resolvedItem == null)
		{
			base.Logger.LogWarning(string.Format("[{0}] CompassItemIdOverride={1} is invalid.", "Fog&ColdControl", CompassItemIdOverride));
			return false;
		}
		item = resolvedItem;
		base.Logger.LogInfo("[Fog&ColdControl] Using compass item override: " + DescribeCompassItem(item));
		return true;
	}


	private static bool LooksLikeStandardCompassItem(Item item)
	{
		if (item == null)
		{
			return false;
		}
		string obj = item.name ?? string.Empty;
		string uiName = item.UIData?.itemName ?? string.Empty;
		if (obj.IndexOf("Compass", StringComparison.OrdinalIgnoreCase) < 0)
		{
			return uiName.IndexOf("Compass", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		return true;
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


	private void GrantCompassToAllPlayers(string reason)
	{
		if (IsModFeatureEnabled() && HasFogAuthority() && IsCompassFeatureEnabled())
		{
			_totalCompassGrantCount++;
			base.Logger.LogDebug(string.Format("[{0}] Queued compass grant #{1} ({2}).", "Fog&ColdControl", _totalCompassGrantCount, reason));
			SyncCompassGrantsToPlayers(force: true, reason);
		}
	}


	private void SyncCompassGrantsToPlayersIfNeeded()
	{
		if (IsModFeatureEnabled() && HasFogAuthority() && IsCompassFeatureEnabled() && _totalCompassGrantCount > 0 && !(Time.unscaledTime - _lastCompassGrantSyncTime < 0.75f))
		{
			SyncCompassGrantsToPlayers(force: false, "periodic-sync");
		}
	}


	private void SyncCompassGrantsToPlayers(bool force, string reason)
	{
		if (!IsModFeatureEnabled() || !HasFogAuthority() || !IsCompassFeatureEnabled() || _totalCompassGrantCount <= 0)
		{
			return;
		}
		if (!TryResolveCompassItem())
		{
			base.Logger.LogWarning("[Fog&ColdControl] Failed to resolve compass item while syncing grants (" + reason + ").");
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
			int actorNumber = player.GetComponent<PhotonView>()?.Owner?.ActorNumber ?? (-1);
			if (actorNumber > 0 && !playersByActorNumber.ContainsKey(actorNumber))
			{
				playersByActorNumber[actorNumber] = player;
			}
			int deliveredCount = GetDeliveredCompassGrantCount(player);
			int grantsIssuedThisPass = 0;
			while (deliveredCount < _totalCompassGrantCount && GrantCompassToPlayer(player, $"{reason}-{deliveredCount + 1}/{_totalCompassGrantCount}"))
			{
				deliveredCount++;
				grantsIssuedThisPass++;
				if (grantsIssuedThisPass >= 1)
				{
					break;
				}
			}
			_playerCompassGrantCounts[playerKey] = deliveredCount;
		}
		TrySyncCompassGrantsToMissingActorPlayers(reason, activePlayerKeys, playersByActorNumber);
		int[] array = _playerCompassGrantCounts.Keys.Where((int key) => !activePlayerKeys.Contains(key)).ToArray();
		foreach (int staleKey in array)
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
		int localActorNumber = PhotonNetwork.LocalPlayer?.ActorNumber ?? (-1);
		foreach (Photon.Realtime.Player item in PhotonNetwork.CurrentRoom.Players.Values.OrderBy((Photon.Realtime.Player player) => player?.ActorNumber ?? int.MaxValue))
		{
			int actorNumber = item?.ActorNumber ?? (-1);
			if (actorNumber <= 0 || actorNumber == localActorNumber || playersByActorNumber.ContainsKey(actorNumber))
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
			while (deliveredCount < _totalCompassGrantCount && GrantCompassToActor(actorNumber, $"{reason}-{deliveredCount + 1}/{_totalCompassGrantCount}"))
			{
				deliveredCount++;
				grantsIssuedThisPass++;
				if (grantsIssuedThisPass >= 1)
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
		if (_playerCompassGrantCounts.TryGetValue(playerKey, out var deliveredCount))
		{
			return deliveredCount;
		}
		PhotonView photonView = ((player != null) ? player.GetComponent<PhotonView>() : null);
		if (photonView?.Owner != null && _remotePlayerCompassBaselineCounts.TryGetValue(photonView.Owner.ActorNumber, out var baselineCount))
		{
			return baselineCount;
		}
		return 0;
	}


	private int GetDeliveredCompassGrantCountForActor(int actorNumber)
	{
		if (_playerCompassGrantCounts.TryGetValue(actorNumber, out var deliveredCount))
		{
			return deliveredCount;
		}
		if (_remotePlayerCompassBaselineCounts.TryGetValue(actorNumber, out var baselineCount))
		{
			return baselineCount;
		}
		return 0;
	}


	private void ProcessPendingCampfireCompassGrants()
	{
		if (IsModFeatureEnabled() && HasFogAuthority() && IsCompassFeatureEnabled() && _pendingCampfireCompassGrantTimes.Count > 0 && !LoadingScreenHandler.loading)
		{
			float now = Time.unscaledTime;
			int[] array = (from entry in _pendingCampfireCompassGrantTimes
				where now >= entry.Value
				select entry.Key).ToArray();
			foreach (int campfireId in array)
			{
				_pendingCampfireCompassGrantTimes.Remove(campfireId);
				base.Logger.LogInfo(string.Format("[{0}] Delivering delayed campfire compass grant for campfire #{1}.", "Fog&ColdControl", campfireId));
				GrantCompassToAllPlayers($"campfire-{campfireId}");
			}
		}
	}


	private bool GrantCompassToPlayer(Player player, string reason)
	{
		if (player == null || _compassItem == null || !IsPlayerPastJoinGrace(player))
		{
			return false;
		}
		PhotonView photonView = player.GetComponent<PhotonView>();
		int actorNumber = photonView?.Owner?.ActorNumber ?? (-1);
		if (photonView?.Owner != null && !photonView.IsMine)
		{
			Character remoteCharacter = player.character;
			if (remoteCharacter == null || remoteCharacter.data == null || remoteCharacter.data.dead)
			{
				remoteCharacter = Character.AllCharacters.FirstOrDefault((Character character) => character != null && character.photonView != null && character.photonView.Owner != null && character.photonView.Owner.ActorNumber == actorNumber && !character.isBot && !character.isZombie && character.data != null && !character.data.dead);
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
		Player actorPlayer = EnumerateTargetPlayers().FirstOrDefault(delegate(Player player)
		{
			PhotonView obj = ((player != null) ? player.GetComponent<PhotonView>() : null);
			return (object)obj != null && obj.Owner?.ActorNumber == actorNumber;
		});
		if (actorPlayer != null)
		{
			return GrantCompassToPlayer(actorPlayer, reason);
		}
		Character actorCharacter = Character.AllCharacters.FirstOrDefault((Character character) => character != null && character.photonView != null && character.photonView.Owner != null && character.photonView.Owner.ActorNumber == actorNumber && !character.isBot && !character.isZombie && character.data != null && !character.data.dead);
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
		ResolveCompassSpawnPose(player, preferLocalViewAnchor, out var spawnPosition, out var spawnRotation);
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
			base.Logger.LogDebug("[Fog&ColdControl] Spawned compass in front of " + player.name + " (" + reason + ").");
			return true;
		}
		catch (Exception ex)
		{
			base.Logger.LogWarning("[Fog&ColdControl] Failed to spawn compass in front of " + player.name + " (" + reason + "): " + ex.Message);
			return false;
		}
	}


	private bool DropCompassInFrontOfCharacter(Character character, int actorNumber, string reason)
	{
		if (character == null || _compassItem == null)
		{
			return false;
		}
		ResolveCompassSpawnPose(character, out var spawnPosition, out var spawnRotation);
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
			base.Logger.LogDebug(string.Format("[{0}] Spawned compass near actor #{1} ({2}).", "Fog&ColdControl", actorNumber, reason));
			return true;
		}
		catch (Exception ex)
		{
			base.Logger.LogWarning(string.Format("[{0}] Failed to spawn compass near actor #{1} ({2}): {3}", "Fog&ColdControl", actorNumber, reason, ex.Message));
			return false;
		}
	}


	private static Vector3 GetCompassDropPosition(Player player)
	{
		ResolveCompassSpawnPose(player, out var spawnPosition, out var _);
		return spawnPosition;
	}


	private static Quaternion GetCompassDropRotation(Player player)
	{
		ResolveCompassSpawnPose(player, out var _, out var spawnRotation);
		return spawnRotation;
	}


	private static void ResolveCompassSpawnPose(Player player, out Vector3 spawnPosition, out Quaternion spawnRotation)
	{
		ResolveCompassSpawnPose(player, preferLocalViewAnchor: false, out spawnPosition, out spawnRotation);
	}


	private static void ResolveCompassSpawnPose(Player player, bool preferLocalViewAnchor, out Vector3 spawnPosition, out Quaternion spawnRotation)
	{
		if (!preferLocalViewAnchor || !TryResolveLocalViewCompassSpawnPose(player, out spawnPosition, out spawnRotation))
		{
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
		spawnPosition = default(Vector3);
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
		if (TryGetCompassFaceAnchor(player, out var anchor))
		{
			Vector3 offset = ((anchor.name.IndexOf("camera", StringComparison.OrdinalIgnoreCase) >= 0) ? (Vector3.down * 0.03f) : Vector3.zero);
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
		if (TryGetCompassFaceAnchor(player, out var anchor) && ShouldUseCompassAnchorForward(anchor))
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
		Transform[] obj = ((!(player.character != null) || !(player.character.transform != player.transform)) ? new Transform[1] { player.transform } : new Transform[2]
		{
			player.character.transform,
			player.transform
		});
		int bestScore = int.MaxValue;
		float bestHeight = float.MinValue;
		Transform[] array = obj;
		foreach (Transform root in array)
		{
			if (root == null)
			{
				continue;
			}
			Transform[] componentsInChildren = root.GetComponentsInChildren<Transform>(includeInactive: true);
			foreach (Transform candidate in componentsInChildren)
			{
				int score = GetCompassFaceAnchorScore(candidate);
				if (score != int.MaxValue)
				{
					float height = candidate.position.y;
					if (score < bestScore || (score == bestScore && height > bestHeight))
					{
						bestScore = score;
						bestHeight = height;
						anchor = candidate;
					}
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
		if (name.IndexOf("camera", StringComparison.OrdinalIgnoreCase) < 0)
		{
			return name.IndexOf("look", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		return true;
	}


	private void HandleManualCompassHotkey()
	{
		if (!IsModFeatureEnabled() || !HasFogAuthority() || !IsCompassFeatureEnabled())
		{
			return;
		}
		KeyCode hotkey = GetCompassHotkey();
		if (hotkey != KeyCode.None)
		{
			GUIManager instance = GUIManager.instance;
			if ((!(instance != null) || !instance.windowBlockingInput) && Input.GetKeyDown(hotkey))
			{
				TrySpawnManualCompassForLocalPlayer();
			}
		}
	}


	private void TrySpawnManualCompassForLocalPlayer()
	{
		if (IsCompassFeatureEnabled())
		{
			Player localPlayer;
			if (!TryResolveCompassItem())
			{
				base.Logger.LogWarning("[Fog&ColdControl] Failed to resolve compass item for manual hotkey spawn.");
			}
			else if (TryGetLocalPlayablePlayer(out localPlayer) && !DropCompassInFrontOfPlayer(localPlayer, "manual-hotkey", preferLocalViewAnchor: true))
			{
				base.Logger.LogWarning("[Fog&ColdControl] Manual compass hotkey spawn failed for " + localPlayer.name + ".");
			}
		}
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
		string keyText = "<color=#FF3B30>" + GetCompassHotkeyLabel() + "</color>";
		if (!isChineseLanguage)
		{
			return "Press " + keyText + " to spawn compass";
		}
		return "按 " + keyText + " 生成指南针";
	}


	private bool ShouldShowCompassLobbyNotice()
	{
		if (IsModFeatureEnabled() && HasFogAuthority() && IsCompassFeatureEnabled() && GetCompassHotkey() != KeyCode.None)
		{
			return IsAirportScene(SceneManager.GetActiveScene());
		}
		return false;
	}


	private string GetCompassLobbyNoticeTextSafe(bool isChineseLanguage)
	{
		string keyText = "<color=#FF3B30>" + GetCompassHotkeyLabel() + "</color>";
		if (!isChineseLanguage)
		{
			return "Press " + keyText + " to spawn compass";
		}
		return "按 " + keyText + " 生成指南针";
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
		_compassLobbyNoticeRect.gameObject.SetActive(value: true);
	}


	private void CreateCompassLobbyNotice(Canvas targetCanvas = null)
	{
		CleanupCompassLobbyNotice();
		Canvas canvas = targetCanvas ?? ResolveHudCanvas();
		if (IsCanvasUsable(canvas))
		{
			GameObject noticeObject = new GameObject("FogClimbCompassLobbyNotice", typeof(RectTransform));
			_compassLobbyNoticeRect = noticeObject.GetComponent<RectTransform>();
			_compassLobbyNoticeRect.SetParent(canvas.transform, worldPositionStays: false);
			_compassLobbyNoticeRect.SetAsLastSibling();
			_compassLobbyNoticeRect.sizeDelta = new Vector2(735f, 81f);
			_compassLobbyNoticeText = noticeObject.AddComponent<TextMeshProUGUI>();
			ApplyCompassLobbyNoticeStyle(_compassLobbyNoticeText);
			_lastCompassLobbyNoticeText = GetCompassLobbyNoticeTextSafe(DetectChineseLanguage());
			_compassLobbyNoticeText.text = _lastCompassLobbyNoticeText;
			ClampCompassLobbyNoticeToCanvas(canvas);
		}
	}


	private void ApplyCompassLobbyNoticeStyle(TextMeshProUGUI target)
	{
		if (!(target == null))
		{
			ApplyGameTextStyle(target, new Color(1f, 0.94f, 0.72f), 1.2f);
			target.fontStyle = FontStyles.Normal;
			target.alignment = TextAlignmentOptions.MidlineRight;
			target.textWrappingMode = TextWrappingModes.NoWrap;
			target.overflowMode = TextOverflowModes.Overflow;
			target.lineSpacing = 1.125f;
		}
	}


	private void ClampCompassLobbyNoticeToCanvas(Canvas targetCanvas = null)
	{
		if (_compassLobbyNoticeRect == null)
		{
			return;
		}
		Canvas canvas = targetCanvas ?? ResolveHudCanvas() ?? _compassLobbyNoticeRect.GetComponentInParent<Canvas>();
		if (IsCanvasUsable(canvas))
		{
			if (_compassLobbyNoticeRect.parent != canvas.transform)
			{
				_compassLobbyNoticeRect.SetParent(canvas.transform, worldPositionStays: false);
			}
			ApplyRightMiddleAnchoredRect(_compassLobbyNoticeRect, 735f, 81f, 28f, 0f);
		}
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


	private static string GetLobbyCompassHintLabel(bool isChineseLanguage)
	{
		if (!IsCompassFeatureEnabled())
		{
			return string.Empty;
		}
		string text = (isChineseLanguage ? "指南针:" : "Compass:");
		return string.Concat(str2: Colorize(isChineseLanguage ? ("按 " + GetCompassHotkeyLabel() + " 生成") : ("Press " + GetCompassHotkeyLabel() + " to spawn"), "#E2EAF3"), str0: Colorize(text, "#B7C0CC"), str1: " ");
	}


	internal static bool IsCompassFeatureEnabled()
	{
		if (CompassEnabled != null)
		{
			return CompassEnabled.Value;
		}
		return true;
	}
}
