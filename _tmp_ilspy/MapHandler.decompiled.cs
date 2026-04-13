using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Peak.Network;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Zorro.Core;
using Zorro.Core.CLI;
using Zorro.PhotonUtility;

public class MapHandler : Singleton<MapHandler>
{
	[Serializable]
	public class MapSegment
	{
		[SerializeField]
		private Biome.BiomeType _biome;

		[SerializeField]
		private GameObject _segmentParent;

		[SerializeField]
		private GameObject _segmentCampfire;

		public GameObject wallNext;

		public GameObject wallPrevious;

		public Transform reconnectSpawnPos;

		[SerializeField]
		private DayNightProfile _dayNightProfile;

		public bool hasVariant;

		public Biome.BiomeType variantBiome;

		public bool isVariant;

		public Biome.BiomeType biome
		{
			get
			{
				if (hasVariant && Singleton<MapHandler>.Instance.BiomeIsPresent(variantBiome))
				{
					return Singleton<MapHandler>.Instance.GetVariantSegmentFromBiome(variantBiome).biome;
				}
				return _biome;
			}
		}

		public GameObject segmentParent
		{
			get
			{
				if (hasVariant && Singleton<MapHandler>.Instance.BiomeIsPresent(variantBiome))
				{
					return Singleton<MapHandler>.Instance.GetVariantSegmentFromBiome(variantBiome).segmentParent;
				}
				return _segmentParent;
			}
		}

		public GameObject segmentCampfire
		{
			get
			{
				if (hasVariant && Singleton<MapHandler>.Instance.BiomeIsPresent(variantBiome))
				{
					return Singleton<MapHandler>.Instance.GetVariantSegmentFromBiome(variantBiome).segmentCampfire;
				}
				return _segmentCampfire;
			}
		}

		public DayNightProfile dayNightProfile
		{
			get
			{
				if (hasVariant && Singleton<MapHandler>.Instance.BiomeIsPresent(variantBiome))
				{
					return Singleton<MapHandler>.Instance.GetVariantSegmentFromBiome(variantBiome).dayNightProfile;
				}
				return _dayNightProfile;
			}
		}
	}

	public Transform globalParent;

	private int _lastRevivedSegment = int.MinValue;

	public MapSegment[] segments;

	public MapSegment[] variantSegments;

	public Transform respawnTheKiln;

	public Transform respawnThePeak;

	public LavaRising lavaRising;

	[SerializeField]
	private int currentSegment;

	private bool hasSpawnedInitialSpawners;

	private bool hasFinishedStartRoutine;

	private ListenerHandle debugCommandHandle;

	private bool hasEnded;

	private bool hasCutsceneEnded;

	private List<PhotonView> viewsToDestoryIfNotAlreadyWhenSwitchingSegments = new List<PhotonView>();

	public List<Biome.BiomeType> biomes = new List<Biome.BiomeType>();

	public int LastRevivedSegment
	{
		get
		{
			return _lastRevivedSegment;
		}
		private set
		{
			Debug.Log($"Revive segment updated to {value}");
			_lastRevivedSegment = value;
		}
	}

	public static bool Exists => Singleton<MapHandler>.Instance != null;

	public static bool ExistsAndInitialized
	{
		get
		{
			if (Exists)
			{
				return Singleton<MapHandler>.Instance.hasFinishedStartRoutine;
			}
			return false;
		}
	}

	public static MapSegment CurrentMapSegment => Singleton<MapHandler>.Instance.segments[Singleton<MapHandler>.Instance.currentSegment];

	public static bool LastSeenCampfireIsSafe
	{
		get
		{
			if (Singleton<MapHandler>.Instance.currentSegment < 4)
			{
				if (OrbFogHandler.IsFoggingCurrentSegment)
				{
					return CurrentCampfire.EveryoneInRange();
				}
				return true;
			}
			return !Singleton<LavaRising>.Instance.started;
		}
	}

	private static bool PreviousSegmentIsStillBaseCamp
	{
		get
		{
			if (Singleton<MapHandler>.Instance.currentSegment != 0)
			{
				if (Singleton<MapHandler>.Instance.currentSegment != 4)
				{
					return !CurrentCampfire.EveryoneInRange();
				}
				return true;
			}
			return false;
		}
	}

	public static RespawnChest BaseCampScoutStatue
	{
		get
		{
			if (!PreviousSegmentIsStillBaseCamp)
			{
				return CurrentScoutStatue;
			}
			return PreviousScoutStatue;
		}
	}

	public static bool BaseCampHasRevived
	{
		get
		{
			if (Singleton<MapHandler>.Instance.currentSegment != Singleton<MapHandler>.Instance.LastRevivedSegment)
			{
				if (PreviousSegmentIsStillBaseCamp)
				{
					return Singleton<MapHandler>.Instance.LastRevivedSegment == Singleton<MapHandler>.Instance.currentSegment - 1;
				}
				return false;
			}
			return true;
		}
	}

	public static Transform CurrentBaseCampSpawnPoint
	{
		get
		{
			if (!PreviousSegmentIsStillBaseCamp)
			{
				if (Singleton<MapHandler>.Instance.GetCurrentSegment() != Segment.Beach)
				{
					return CurrentCampfire.transform;
				}
				return SpawnPoint.LocalSpawnPoint.transform;
			}
			return PreviousCampfire.transform;
		}
	}

	public static Campfire PreviousCampfire
	{
		get
		{
			if (Singleton<MapHandler>.Instance.currentSegment != 0)
			{
				return GetCampfireRoot(Singleton<MapHandler>.Instance.currentSegment - 1).GetComponentInChildren<Campfire>(includeInactive: true);
			}
			return null;
		}
	}

	public static RespawnChest PreviousScoutStatue
	{
		get
		{
			if (Singleton<MapHandler>.Instance.currentSegment != 0)
			{
				if (Singleton<MapHandler>.Instance.GetCurrentSegment() != Segment.TheKiln)
				{
					return GetCampfireRoot(Singleton<MapHandler>.Instance.currentSegment - 1).GetComponentInChildren<RespawnChest>();
				}
				return Singleton<MapHandler>.Instance.segments[Singleton<MapHandler>.Instance.currentSegment - 1].segmentParent.GetComponentInChildren<RespawnChest>(includeInactive: true);
			}
			return null;
		}
	}

	public static Campfire CurrentCampfire => GetCampfireRoot(Singleton<MapHandler>.Instance.currentSegment).GetComponentInChildren<Campfire>();

	public static RespawnChest CurrentScoutStatue
	{
		get
		{
			if ((int)Singleton<MapHandler>.Instance.GetCurrentSegment() <= 3)
			{
				if (Singleton<MapHandler>.Instance.GetCurrentSegment() != Segment.Caldera)
				{
					return GetCampfireRoot(Singleton<MapHandler>.Instance.currentSegment).GetComponentInChildren<RespawnChest>();
				}
				return Singleton<MapHandler>.Instance.segments[Singleton<MapHandler>.Instance.currentSegment].segmentParent.GetComponentInChildren<RespawnChest>();
			}
			return null;
		}
	}

	public static Segment CurrentSegmentNumber => (Segment)Singleton<MapHandler>.Instance.currentSegment;

	private static GameObject GetCampfireRoot(int segmentIndex)
	{
		if (segmentIndex >= 0)
		{
			return Singleton<MapHandler>.Instance.segments[segmentIndex].segmentCampfire;
		}
		return null;
	}

	protected override void Awake()
	{
		hasFinishedStartRoutine = false;
		base.Awake();
		debugCommandHandle = CustomCommands<CustomCommandType>.RegisterListener<SyncMapHandlerDebugCommandPackage>(OnPackageHandle);
		if (Application.isEditor)
		{
			DetectBiomes();
		}
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		CustomCommands<CustomCommandType>.UnregisterListener(debugCommandHandle);
	}

	private IEnumerator Start()
	{
		yield return null;
		CurrentScoutStatue.SegmentNumber = Segment.Beach;
		CurrentScoutStatue.ReviveUsed += delegate(RespawnChest statue)
		{
			LastRevivedSegment = (int)statue.SegmentNumber;
		};
		for (int num = 1; num < segments.Length; num++)
		{
			segments[num].segmentParent.SetActive(value: false);
			if (segments[num].segmentCampfire != null)
			{
				segments[num].segmentCampfire.SetActive(value: false);
			}
			Debug.Log($"Disabling segment: {num} with parent: {segments[num].segmentParent.name}");
		}
		segments[0].wallNext.SetActive(value: true);
		hasFinishedStartRoutine = true;
	}

	internal void DetectBiomes()
	{
		biomes.Clear();
		for (int i = 0; i < base.transform.childCount; i++)
		{
			Transform child = base.transform.GetChild(i);
			for (int j = 0; j < child.childCount; j++)
			{
				if (child.GetChild(j).gameObject.activeInHierarchy && child.GetChild(j).TryGetComponent<Biome>(out var component))
				{
					biomes.Add(component.biomeType);
				}
			}
		}
	}

	private void Update()
	{
		if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient && !hasSpawnedInitialSpawners)
		{
			ISpawner[] componentsInChildren = segments[0].segmentParent.GetComponentsInChildren<ISpawner>();
			ISpawner[] componentsInChildren2 = segments[0].segmentCampfire.GetComponentsInChildren<ISpawner>();
			ISpawner[] componentsInChildren3 = globalParent.GetComponentsInChildren<ISpawner>();
			hasSpawnedInitialSpawners = true;
			ISpawner[] array = componentsInChildren;
			foreach (ISpawner spawner in array)
			{
				viewsToDestoryIfNotAlreadyWhenSwitchingSegments.AddRange(spawner.TrySpawnItems());
			}
			array = componentsInChildren2;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].TrySpawnItems();
			}
			array = componentsInChildren3;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].TrySpawnItems();
			}
		}
		else if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
		{
			hasSpawnedInitialSpawners = true;
		}
		bool flag = false;
		Player[] array2 = PlayerHandler.GetAllPlayers().ToArray();
		int num = array2.Length;
		if (array2.Length > 4)
		{
			num = array2.Length / 2;
		}
		else if (array2.Length == 4)
		{
			num = 3;
		}
		else if (array2.Length == 3)
		{
			num = 2;
		}
		if (array2.Count((Player player) => player.hasClosedEndScreen) >= num)
		{
			flag = true;
		}
		if (flag && array2.Length != 0 && !GameHandler.TryGetStatus<EndScreenStatus>(out var _) && !hasEnded)
		{
			bool num2 = Character.localCharacter.refs.stats.won || Character.localCharacter.refs.stats.somebodyElseWon;
			hasEnded = true;
			if (num2 && !RunSettings.isMiniRun)
			{
				GameHandler.AddStatus<EndScreenStatus>(new EndScreenStatus());
				Debug.LogError("Load credits");
				Singleton<PeakHandler>.Instance.EndScreenComplete();
			}
			else
			{
				Debug.LogError("Everyone has closed end screen.. Loading airport");
				Singleton<GameOverHandler>.Instance.LoadAirport();
			}
		}
		bool flag2 = false;
		Player[] array3 = array2;
		for (int i = 0; i < array3.Length; i++)
		{
			if (array3[i].doneWithCutscene)
			{
				flag2 = true;
				break;
			}
		}
		if (flag2 && array2.Length != 0 && !hasCutsceneEnded)
		{
			hasCutsceneEnded = true;
			Debug.Log("Everyone is done with cutscene, loading airport");
			GameHandler.AddStatus<SceneSwitchingStatus>(new SceneSwitchingStatus());
			RetrievableResourceSingleton<LoadingScreenHandler>.Instance.Load(LoadingScreen.LoadingScreenType.Basic, null, RetrievableResourceSingleton<LoadingScreenHandler>.Instance.LoadSceneProcess("Airport", networked: true, yieldForCharacterSpawn: true, 1f));
		}
	}

	public void GoToSegment(Segment s)
	{
		if ((int)s <= currentSegment)
		{
			Debug.LogError($"Trying to transition to segment already passed: {s}");
			return;
		}
		Debug.Log($"Going to segment: {s}");
		StartCoroutine(ShowNextSegmentCoroutine());
		IEnumerator ShowNextSegmentCoroutine()
		{
			Character.localCharacter.data.DestroyCheckpointFlags();
			int startSegment = currentSegment;
			currentSegment++;
			if (s == Segment.TheKiln && (bool)segments[currentSegment].wallPrevious)
			{
				segments[currentSegment].wallPrevious.SetActive(value: true);
			}
			OrbFogHandler orbFogHandler = Singleton<OrbFogHandler>.Instance;
			yield return orbFogHandler.WaitForFogCatchUp();
			yield return new WaitForSecondsRealtime(1f);
			segments[startSegment].segmentParent.SetActive(value: false);
			yield return null;
			segments[currentSegment].segmentParent.SetActive(value: true);
			if (CurrentScoutStatue != null)
			{
				CurrentScoutStatue.SegmentNumber = CurrentSegmentNumber;
				CurrentScoutStatue.ReviveUsed += delegate(RespawnChest statue)
				{
					LastRevivedSegment = (int)statue.SegmentNumber;
				};
			}
			EnablingSubstep[] array = (from enablingSubstep in segments[currentSegment].segmentParent.GetComponentsInChildren<EnablingSubstep>()
				where enablingSubstep.gameObject.activeSelf
				select enablingSubstep).ToArray();
			EnablingSubstep[] array2 = array;
			for (int num = 0; num < array2.Length; num++)
			{
				array2[num].gameObject.SetActive(value: false);
			}
			if ((bool)segments[startSegment].wallNext)
			{
				segments[startSegment].wallNext.SetActive(value: false);
			}
			if ((bool)segments[startSegment].wallPrevious)
			{
				segments[startSegment].wallPrevious.SetActive(value: false);
			}
			if ((bool)segments[currentSegment].wallNext)
			{
				segments[currentSegment].wallNext.SetActive(value: true);
			}
			if ((bool)segments[currentSegment].wallPrevious)
			{
				segments[currentSegment].wallPrevious.SetActive(value: true);
			}
			PhotonNetwork.IsMessageQueueRunning = false;
			segments[currentSegment].segmentParent.SetActive(value: true);
			PhotonNetwork.IsMessageQueueRunning = true;
			EnablingSubstep[] array3 = array;
			foreach (EnablingSubstep substep in array3)
			{
				yield return new WaitForSeconds(0.15f);
				PhotonNetwork.IsMessageQueueRunning = false;
				substep.gameObject.SetActive(value: true);
				Debug.Log($"Enabling substep: {substep}");
				PhotonNetwork.IsMessageQueueRunning = true;
			}
			if (PhotonNetwork.IsMasterClient)
			{
				foreach (PhotonView viewsToDestoryIfNotAlreadyWhenSwitchingSegment in viewsToDestoryIfNotAlreadyWhenSwitchingSegments)
				{
					if (!(viewsToDestoryIfNotAlreadyWhenSwitchingSegment == null))
					{
						bool flag = true;
						try
						{
							foreach (Character allPlayerCharacter in PlayerHandler.GetAllPlayerCharacters())
							{
								if (!(allPlayerCharacter == null) && Vector3.Distance(viewsToDestoryIfNotAlreadyWhenSwitchingSegment.transform.position, allPlayerCharacter.Center) < 60f)
								{
									flag = false;
								}
							}
							if (viewsToDestoryIfNotAlreadyWhenSwitchingSegment != null && flag)
							{
								Debug.Log("Removing: " + viewsToDestoryIfNotAlreadyWhenSwitchingSegment.gameObject.name);
								PhotonNetwork.Destroy(viewsToDestoryIfNotAlreadyWhenSwitchingSegment);
							}
						}
						catch (Exception ex)
						{
							Debug.LogError("Exception while trying to remove old item: " + ex.ToString());
						}
					}
				}
				viewsToDestoryIfNotAlreadyWhenSwitchingSegments.Clear();
			}
			if (segments[currentSegment].segmentCampfire != null)
			{
				segments[currentSegment].segmentCampfire.SetActive(value: true);
			}
			if (segments.WithinRange(startSegment - 1) && segments[startSegment - 1].segmentCampfire != null)
			{
				segments[startSegment - 1].segmentCampfire.SetActive(value: false);
			}
			if (PhotonNetwork.IsMasterClient)
			{
				Debug.Log("Spawning from parent: " + segments[currentSegment].segmentParent.gameObject.name);
				List<ISpawner> list = segments[currentSegment].segmentParent.GetComponentsInChildren<ISpawner>().ToList();
				if (s == Segment.TheKiln)
				{
					list.AddRange(Singleton<PeakHandler>.Instance.gameObject.GetComponentsInChildren<ISpawner>());
				}
				foreach (ISpawner item in list)
				{
					viewsToDestoryIfNotAlreadyWhenSwitchingSegments.AddRange(item.TrySpawnItems());
				}
				if ((bool)segments[currentSegment].segmentCampfire)
				{
					Debug.Log("Spawning items in " + segments[currentSegment].segmentCampfire.gameObject.name);
					ISpawner[] componentsInChildren = segments[currentSegment].segmentCampfire.GetComponentsInChildren<ISpawner>();
					for (int num = 0; num < componentsInChildren.Length; num++)
					{
						componentsInChildren[num].TrySpawnItems();
					}
				}
				else
				{
					Debug.Log("NO CAMPFIRE SEGMENT");
				}
			}
			if (segments[currentSegment].dayNightProfile != null)
			{
				DayNightManager.instance.BlendProfiles(segments[currentSegment].dayNightProfile);
			}
			yield return new WaitForSeconds(0.5f);
			StartCoroutine(ShowTitleText());
			yield return orbFogHandler.WaitForReveal();
			if (s != Segment.TheKiln)
			{
				orbFogHandler.SetFogOrigin(currentSegment);
			}
			PhotonNetwork.IsMessageQueueRunning = true;
			IEnumerator ShowTitleText()
			{
				yield return new WaitForSeconds(0.5f);
				Singleton<MountainProgressHandler>.Instance.SetSegmentComplete(startSegment + 1);
				PhotonNetwork.IsMessageQueueRunning = true;
			}
		}
	}

	[ConsoleCommand]
	public static void JumpToSegment(Segment segment)
	{
		if (PhotonNetwork.IsMasterClient)
		{
			JumpToSegmentLogic(segment, (from player in PlayerHandler.GetAllPlayers()
				select player.photonView.Owner.ActorNumber).ToHashSet(), !NetCode.Session.IsOffline, updateFog: true);
			PreviousCampfire?.LightWithoutReveal();
		}
		else
		{
			GameUtils.instance.photonView.RPC("RPC_WarpToSegment", RpcTarget.MasterClient, (int)segment);
		}
	}

	public static void SetSegmentOnSpawn(Segment segment, int lastRevivedSegment)
	{
		Singleton<MapHandler>.Instance.LastRevivedSegment = lastRevivedSegment;
		if ((int)segment < 4 && (Singleton<OrbFogHandler>.Instance == null || Singleton<OrbFogHandler>.Instance.currentID != (int)segment))
		{
			Debug.LogError("Uh oh! The fog handler needs to be initialized before we spawn or the spawn will be broken!!!");
		}
		if (segment != Segment.Beach)
		{
			JumpToSegmentLogic(segment, new HashSet<int> { NetCode.Session.SeatNumber }, sendToEveryone: false);
		}
	}

	private static void JumpToSegmentLogic(Segment segment, HashSet<int> playersToTeleport, bool sendToEveryone, bool updateFog = false)
	{
		Debug.Log($"Jumping to beginning of segment: {segment}");
		MapSegment[] array = Singleton<MapHandler>.Instance.segments;
		foreach (MapSegment mapSegment in array)
		{
			mapSegment.segmentParent.SetActive(value: false);
			if ((bool)mapSegment.segmentCampfire)
			{
				mapSegment.segmentCampfire.SetActive(value: false);
			}
			if ((bool)mapSegment.wallNext)
			{
				mapSegment.wallNext.gameObject.SetActive(value: false);
			}
			if ((bool)mapSegment.wallPrevious)
			{
				mapSegment.wallPrevious.gameObject.SetActive(value: false);
			}
		}
		Singleton<MapHandler>.Instance.currentSegment = (int)segment;
		int num = Singleton<MapHandler>.Instance.currentSegment;
		if (segment == Segment.Peak)
		{
			num--;
		}
		MapSegment mapSegment2 = Singleton<MapHandler>.Instance.segments[num];
		Debug.Log($"Setting up segment walls for {mapSegment2}");
		mapSegment2.segmentParent.SetActive(value: true);
		if ((bool)mapSegment2.segmentCampfire)
		{
			mapSegment2.segmentCampfire.SetActive(value: true);
		}
		if ((bool)mapSegment2.wallNext)
		{
			mapSegment2.wallNext.gameObject.SetActive(value: true);
		}
		if ((bool)mapSegment2.wallPrevious)
		{
			mapSegment2.wallPrevious.gameObject.SetActive(value: true);
		}
		Vector3 vector = CurrentBaseCampSpawnPoint.position + CharacterSpawner.RandomBaseCampOffset;
		if (segment == Segment.Peak)
		{
			vector = Singleton<MapHandler>.Instance.respawnThePeak.position;
		}
		if (num > 0)
		{
			MapSegment mapSegment3 = Singleton<MapHandler>.Instance.segments[num - 1];
			if (mapSegment3.segmentCampfire != null)
			{
				mapSegment3.segmentCampfire.SetActive(value: true);
			}
		}
		if (PhotonNetwork.IsMasterClient)
		{
			List<ISpawner> list = mapSegment2.segmentParent.GetComponentsInChildren<ISpawner>().ToList();
			Debug.Log($"Spawning items in {segment} from {list.Count} spawners. " + "Parent: " + mapSegment2.segmentParent.gameObject.name);
			int num2 = 0;
			if (segment == Segment.TheKiln)
			{
				list.AddRange(Singleton<PeakHandler>.Instance.gameObject.GetComponentsInChildren<ISpawner>());
			}
			foreach (ISpawner item in list)
			{
				item.TrySpawnItems();
				num2++;
			}
			if ((bool)mapSegment2.segmentCampfire)
			{
				Debug.Log("Spawning items in " + mapSegment2.segmentCampfire.gameObject.name);
				ISpawner[] componentsInChildren = mapSegment2.segmentCampfire.GetComponentsInChildren<ISpawner>();
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					componentsInChildren[i].TrySpawnItems();
				}
			}
			else
			{
				Debug.Log("NO CAMPFIRE SEGMENT");
			}
		}
		if (updateFog)
		{
			Debug.Log($"Moving fog to index {num}");
			Singleton<OrbFogHandler>.Instance.SetFogOrigin(num);
		}
		if (mapSegment2.dayNightProfile != null)
		{
			DayNightManager.instance.BlendProfiles(mapSegment2.dayNightProfile);
		}
		if (PhotonNetwork.IsMasterClient)
		{
			List<Character> allPlayerCharacters = PlayerHandler.GetAllPlayerCharacters();
			Debug.Log($"Teleporting {playersToTeleport.Count} out of {allPlayerCharacters.Count} players to {segment} campfire..");
			foreach (Character item2 in allPlayerCharacters)
			{
				if (playersToTeleport.Contains(item2.photonView.Owner.ActorNumber))
				{
					item2.photonView.RPC("WarpPlayerRPC", RpcTarget.All, vector, false);
				}
			}
		}
		if (sendToEveryone)
		{
			CustomCommands<CustomCommandType>.SendPackage(new SyncMapHandlerDebugCommandPackage(segment, Array.Empty<int>()), ReceiverGroup.Others);
		}
		if (CurrentScoutStatue != null)
		{
			CurrentScoutStatue.SegmentNumber = CurrentSegmentNumber;
			CurrentScoutStatue.ReviveUsed += delegate(RespawnChest statue)
			{
				Singleton<MapHandler>.Instance.LastRevivedSegment = (int)statue.SegmentNumber;
			};
		}
	}

	private void OnPackageHandle(SyncMapHandlerDebugCommandPackage p)
	{
		Debug.Log($"Sync map debug package received! Jumping {p.PlayerToTeleport.Length} players to {p.Segment}");
		JumpToSegmentLogic(p.Segment, p.PlayerToTeleport.ToHashSet(), sendToEveryone: false, updateFog: true);
	}

	public Segment GetCurrentSegment()
	{
		return (Segment)currentSegment;
	}

	public Biome.BiomeType GetCurrentBiome()
	{
		return segments[currentSegment].biome;
	}

	public bool BiomeIsPresent(Biome.BiomeType biomeType)
	{
		return biomes.Contains(biomeType);
	}

	public MapSegment GetVariantSegmentFromBiome(Biome.BiomeType biome)
	{
		for (int i = 0; i < variantSegments.Length; i++)
		{
			if (variantSegments[i].biome == biome)
			{
				return variantSegments[i];
			}
		}
		Debug.LogError("COULDNT FIND SEGMENT FROM BIOME. RETURNING SHORE SEGMENT");
		return segments[0];
	}
}
