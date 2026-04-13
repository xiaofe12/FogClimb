using System.Collections;
using ExitGames.Client.Photon;
using Peak.Network;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Zorro.Core;

public class OrbFogHandler : Singleton<OrbFogHandler>, IInRoomCallbacks
{
	public float speed = 0.3f;

	public float maxWaitTime = 500f;

	public float currentWaitTime;

	public bool hasArrived;

	public bool isMoving;

	public float currentSize;

	public float currentStartHeight;

	public float currentStartForward;

	public float dispelFogAmount;

	private FogSphere sphere;

	private FogSphereOrigin[] origins;

	private float syncCounter;

	private PhotonView photonView;

	public AnimationCurve fogRevealCurve;

	public AnimationCurve fogFadeCurve;

	public float currentCloseFog = 1f;

	public bool PlayersAreResting { get; set; }

	public int currentID { get; private set; }

	public static bool IsFoggingCurrentSegment
	{
		get
		{
			if (Singleton<OrbFogHandler>.Instance != null)
			{
				if (!Singleton<OrbFogHandler>.Instance.isMoving)
				{
					return Singleton<OrbFogHandler>.Instance.hasArrived;
				}
				return true;
			}
			return false;
		}
	}

	protected override void Awake()
	{
		base.Awake();
		photonView = GetComponent<PhotonView>();
	}

	private void Start()
	{
		sphere = GetComponentInChildren<FogSphere>();
		origins = base.transform.root.GetComponentsInChildren<FogSphereOrigin>();
		InitNewSphere(origins[currentID]);
	}

	private void OnEnable()
	{
		PhotonNetwork.AddCallbackTarget(this);
	}

	private void OnDisable()
	{
		PhotonNetwork.RemoveCallbackTarget(this);
		Shader.SetGlobalFloat("FakeMountainEnabled", 1f);
	}

	private void Update()
	{
		_ = sphere != null;
		if (!hasArrived)
		{
			bool flag = false;
			flag = currentID >= origins.Length || origins[currentID].disableFog;
			if (Ascents.fogEnabled && !flag)
			{
				if (isMoving)
				{
					Move();
				}
				else
				{
					WaitToMove();
				}
			}
		}
		if (PhotonNetwork.IsMasterClient)
		{
			Sync();
		}
		ApplyMeshEffects();
		float b = Mathf.Lerp(1f, 5f, dispelFogAmount);
		currentCloseFog = Mathf.Lerp(currentCloseFog, b, Time.deltaTime * 1f);
		Shader.SetGlobalFloat("CloseDistanceMod", currentCloseFog);
	}

	private void Sync()
	{
		syncCounter += Time.deltaTime;
		if (syncCounter > 5f)
		{
			syncCounter = 0f;
			photonView.RPC("RPCA_SyncFog", RpcTarget.Others, currentSize, isMoving);
		}
	}

	[PunRPC]
	public void RPCA_SyncFog(float s, bool moving)
	{
		currentSize = s;
		isMoving = moving;
	}

	[PunRPC]
	public void RPC_InitFog(int originId, float s, bool arrived, bool moving, PhotonMessageInfo info)
	{
		if (info.Sender.ActorNumber == NetCode.Session.HostId)
		{
			SetFogOrigin(originId);
			currentSize = s;
			hasArrived = arrived;
			isMoving = moving;
		}
	}

	public IEnumerator WaitForFogCatchUp()
	{
		isMoving = true;
		while (currentSize > 30f && isMoving && !hasArrived)
		{
			currentSize = Mathf.Lerp(currentSize, 29.5f, Time.deltaTime);
			currentSize = Mathf.MoveTowards(currentSize, 29.5f, Time.deltaTime);
			yield return null;
		}
	}

	public IEnumerator WaitForReveal()
	{
		float c = 0f;
		float t = 5f;
		sphere.ENABLE = 1f;
		while (c < t)
		{
			c += Time.deltaTime;
			sphere.REVEAL_AMOUNT = fogRevealCurve.Evaluate(c / t);
			sphere.ENABLE = fogFadeCurve.Evaluate(c / t);
			yield return null;
		}
		sphere.REVEAL_AMOUNT = 1f;
		sphere.ENABLE = 0f;
		currentSize = 800f;
	}

	public IEnumerator DisableFog()
	{
		float c = 0f;
		float t = 1f;
		while (c < t)
		{
			c += Time.deltaTime;
			sphere.ENABLE = 1f - c / t;
			yield return null;
		}
		sphere.ENABLE = 0f;
		sphere.REVEAL_AMOUNT = 0f;
		currentSize = 800f;
	}

	private void Move()
	{
		sphere.REVEAL_AMOUNT = 0f;
		sphere.ENABLE = Mathf.MoveTowards(sphere.ENABLE, 1f, Time.deltaTime * 0.1f);
		currentSize -= speed * Time.deltaTime;
		if (currentSize <= 30f)
		{
			Stop();
		}
	}

	private void Stop()
	{
		hasArrived = true;
		isMoving = false;
	}

	private void WaitToMove()
	{
		if (!PlayersAreResting)
		{
			currentWaitTime += Time.deltaTime;
		}
		if ((PlayersHaveMovedOn() || TimeToMove()) && PhotonNetwork.IsMasterClient)
		{
			photonView.RPC("StartMovingRPC", RpcTarget.All);
		}
	}

	private bool TimeToMove()
	{
		if (Ascents.currentAscent < 0)
		{
			return false;
		}
		if (currentWaitTime > maxWaitTime)
		{
			return currentID > 0;
		}
		return false;
	}

	private bool PlayersHaveMovedOn()
	{
		if (Character.AllCharacters.Count == 0)
		{
			return false;
		}
		if (Ascents.currentAscent < 0)
		{
			return false;
		}
		for (int i = 0; i < Character.AllCharacters.Count; i++)
		{
			if (!Character.AllCharacters[i].data.dead && (Character.AllCharacters[i].Center.y < currentStartHeight || Character.AllCharacters[i].Center.z < currentStartForward))
			{
				return false;
			}
		}
		Debug.Log("Players have moved on");
		return true;
	}

	private void ApplyMeshEffects()
	{
		sphere.currentSize = currentSize;
	}

	public void InitNewSphere(FogSphereOrigin newOrigin)
	{
		sphere.fogPoint = newOrigin.transform.position;
		currentSize = newOrigin.size;
		currentStartHeight = newOrigin.moveOnHeight;
		currentStartForward = newOrigin.moveOnForward;
	}

	[PunRPC]
	public void StartMovingRPC()
	{
		currentWaitTime = 0f;
		hasArrived = false;
		isMoving = true;
		GUIManager.instance.TheFogRises();
	}

	public void SetFogOrigin(int id)
	{
		currentID = id;
		if (currentID < origins.Length)
		{
			hasArrived = false;
			sphere.gameObject.SetActive(value: true);
			InitNewSphere(origins[currentID]);
		}
		else
		{
			hasArrived = true;
			Debug.Log("Last section, disabling fog sphere");
			sphere.gameObject.SetActive(value: false);
		}
	}

	public static void InitFogIfExists(Photon.Realtime.Player newPlayer)
	{
		if ((bool)Singleton<OrbFogHandler>.Instance)
		{
			Singleton<OrbFogHandler>.Instance.InitFogForPlayer(newPlayer);
		}
	}

	private void InitFogForPlayer(Photon.Realtime.Player newPlayer)
	{
		photonView.RPC("RPC_InitFog", newPlayer, currentID, currentSize, hasArrived, isMoving);
	}

	public void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
	{
	}

	public void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
	{
	}

	public void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
	{
	}

	public void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
	{
	}

	public void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
	{
	}
}
