using Photon.Pun;
using UnityEngine;
using Zorro.Core;
using Zorro.PhotonUtility;

public class LavaRising : Singleton<LavaRising>
{
	public Rigidbody lava;

	public Transform topTransform;

	public float initialWaitTime = 1f;

	public float travelTime = 60f;

	public bool debug;

	public float debugInitialWaitTime = 1f;

	public float debugTravelTime = 60f;

	private bool shownLavaRisingMessage;

	private ListenerHandle debugCommandHandle;

	private float syncTime;

	[SerializeField]
	private float startHeight;

	public float timeTraveled { get; set; }

	public bool started { get; set; }

	public bool ended { get; set; }

	public float secondsWaitedToStart { get; set; }

	protected override void Awake()
	{
		base.Awake();
		if (debug)
		{
			initialWaitTime = debugInitialWaitTime;
			travelTime = debugTravelTime;
		}
	}

	private void Start()
	{
		startHeight = lava.transform.position.y;
		Debug.Log("Initialized lava height: " + startHeight);
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		CustomCommands<CustomCommandType>.UnregisterListener(debugCommandHandle);
	}

	private void Update()
	{
		if (RunSettings.GetValue(RunSettings.SETTINGTYPE.Hazard_TheLavaRises) == 0)
		{
			base.enabled = false;
			return;
		}
		if ((PhotonNetwork.IsMasterClient && Singleton<MapHandler>.Instance.GetCurrentSegment() == Segment.TheKiln) || Singleton<MapHandler>.Instance.GetCurrentSegment() == Segment.Peak)
		{
			bool flag = false;
			syncTime += Time.deltaTime;
			if (!started && secondsWaitedToStart <= 0f && Ascents.fogEnabled)
			{
				StartWaiting();
			}
			if (!started && secondsWaitedToStart > 0f)
			{
				secondsWaitedToStart += Time.deltaTime;
				if (secondsWaitedToStart > initialWaitTime && Ascents.fogEnabled)
				{
					flag = true;
					started = true;
				}
			}
			if (syncTime > 15f)
			{
				syncTime = 0f;
				flag = true;
			}
			if (flag)
			{
				Debug.Log("Syncing Lava Rising to others...");
				GameUtils.instance.SyncLava(started, ended, timeTraveled, secondsWaitedToStart);
			}
		}
		if (started && !ended)
		{
			if (!shownLavaRisingMessage)
			{
				GUIManager.instance.TheLavaRises();
				GamefeelHandler.instance.AddPerlinShake(5f, 3f);
				shownLavaRisingMessage = true;
				Debug.Log("Lava rising started.");
			}
			timeTraveled += Time.deltaTime;
			float y = Mathf.Lerp(startHeight, topTransform.position.y, timeTraveled / travelTime);
			lava.MovePosition(new Vector3(lava.transform.position.x, y, lava.transform.position.z));
			if (timeTraveled > travelTime)
			{
				EndRising();
			}
		}
	}

	public void RecieveLavaData(bool started, bool ended, float time, float timeWaited)
	{
		this.started = started;
		this.ended = ended;
		timeTraveled = time;
		secondsWaitedToStart = timeWaited;
		Debug.Log($"Handle Lava Rising package: started: {started}, ended: {ended}, seconds waited: {secondsWaitedToStart}, time traveled: {timeTraveled} starting position: {startHeight} total time: {travelTime}");
	}

	public void StartWaiting()
	{
		if (secondsWaitedToStart > 0f)
		{
			Debug.LogError("Tried to start waiting for lava rising but already rising!");
			return;
		}
		Debug.Log("Starting wait for lava rising");
		secondsWaitedToStart = Time.deltaTime;
	}

	private void EndRising()
	{
		Debug.Log("Ending lava rising.");
		ended = true;
	}
}
