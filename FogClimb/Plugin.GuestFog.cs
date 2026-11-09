using Photon.Pun;
using UnityEngine;

namespace FogColdControl;

public sealed partial class Plugin
{
	private readonly FogSpeedSamples _guestFogSpeedSamples = new FogSpeedSamples();
	private OrbFogHandler _guestFogSampleHandler;
	private int _guestFogSampleHostId = -1;

	internal static void NotifyGuestFogSnapshot(OrbFogHandler handler, float size, bool moving)
	{
		Plugin instance = Instance;
		if (instance == null || handler == null || !IsReadOnlyFogUiViewer())
		{
			return;
		}
		int hostId = PhotonNetwork.MasterClient?.ActorNumber ?? -1;
		if (instance._guestFogSampleHandler != handler || instance._guestFogSampleHostId != hostId)
		{
			instance._guestFogSpeedSamples.Reset();
		}
		instance._guestFogSampleHandler = handler;
		instance._guestFogSampleHostId = hostId;
		instance._guestFogSpeedSamples.Observe(handler.currentID, size, moving, Time.time);
	}

	private void ResetGuestFogSpeedSamples()
	{
		_guestFogSpeedSamples.Reset();
		_guestFogSampleHandler = null;
		_guestFogSampleHostId = -1;
	}
}
