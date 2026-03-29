using System;
using ExitGames.Client.Photon;
using Photon.Realtime;

namespace Photon.Voice.Unity.Demos.DemoVoiceUI
{
	// Token: 0x02000395 RID: 917
	public static class PhotonDemoExtensions
	{
		// Token: 0x060017B7 RID: 6071 RVA: 0x00078AEE File Offset: 0x00076CEE
		public static bool Mute(this Photon.Realtime.Player player)
		{
			return player.SetCustomProperties(new Hashtable(1)
			{
				{
					"mu",
					true
				}
			}, null, null);
		}

		// Token: 0x060017B8 RID: 6072 RVA: 0x00078B0F File Offset: 0x00076D0F
		public static bool Unmute(this Photon.Realtime.Player player)
		{
			return player.SetCustomProperties(new Hashtable(1)
			{
				{
					"mu",
					false
				}
			}, null, null);
		}

		// Token: 0x060017B9 RID: 6073 RVA: 0x00078B30 File Offset: 0x00076D30
		public static bool IsMuted(this Photon.Realtime.Player player)
		{
			return player.HasBoolProperty("mu");
		}

		// Token: 0x060017BA RID: 6074 RVA: 0x00078B3D File Offset: 0x00076D3D
		public static bool SetPhotonVAD(this Photon.Realtime.Player player, bool value)
		{
			return player.SetCustomProperties(new Hashtable(1)
			{
				{
					"pv",
					value
				}
			}, null, null);
		}

		// Token: 0x060017BB RID: 6075 RVA: 0x00078B5E File Offset: 0x00076D5E
		public static bool SetWebRTCVAD(this Photon.Realtime.Player player, bool value)
		{
			return player.SetCustomProperties(new Hashtable(1)
			{
				{
					"wv",
					value
				}
			}, null, null);
		}

		// Token: 0x060017BC RID: 6076 RVA: 0x00078B7F File Offset: 0x00076D7F
		public static bool SetAEC(this Photon.Realtime.Player player, bool value)
		{
			return player.SetCustomProperties(new Hashtable(1)
			{
				{
					"ec",
					value
				}
			}, null, null);
		}

		// Token: 0x060017BD RID: 6077 RVA: 0x00078BA0 File Offset: 0x00076DA0
		public static bool SetAGC(this Photon.Realtime.Player player, bool agcEnabled, int gain, int level)
		{
			return player.SetCustomProperties(new Hashtable(1)
			{
				{
					"gc",
					new object[]
					{
						agcEnabled,
						gain,
						level
					}
				}
			}, null, null);
		}

		// Token: 0x060017BE RID: 6078 RVA: 0x00078BE9 File Offset: 0x00076DE9
		public static bool SetMic(this Photon.Realtime.Player player, Recorder.MicType type)
		{
			return player.SetCustomProperties(new Hashtable(1)
			{
				{
					"m",
					type
				}
			}, null, null);
		}

		// Token: 0x060017BF RID: 6079 RVA: 0x00078C0A File Offset: 0x00076E0A
		public static bool HasPhotonVAD(this Photon.Realtime.Player player)
		{
			return player.HasBoolProperty("pv");
		}

		// Token: 0x060017C0 RID: 6080 RVA: 0x00078C17 File Offset: 0x00076E17
		public static bool HasWebRTCVAD(this Photon.Realtime.Player player)
		{
			return player.HasBoolProperty("wv");
		}

		// Token: 0x060017C1 RID: 6081 RVA: 0x00078C24 File Offset: 0x00076E24
		public static bool HasAEC(this Photon.Realtime.Player player)
		{
			return player.HasBoolProperty("ec");
		}

		// Token: 0x060017C2 RID: 6082 RVA: 0x00078C34 File Offset: 0x00076E34
		public static bool HasAGC(this Photon.Realtime.Player player)
		{
			object[] array = player.GetObjectProperty("gc") as object[];
			return array != null && array.Length != 0 && (bool)array[0];
		}

		// Token: 0x060017C3 RID: 6083 RVA: 0x00078C64 File Offset: 0x00076E64
		public static int GetAGCGain(this Photon.Realtime.Player player)
		{
			object[] array = player.GetObjectProperty("gc") as object[];
			if (array == null || array.Length <= 1)
			{
				return 0;
			}
			return (int)array[1];
		}

		// Token: 0x060017C4 RID: 6084 RVA: 0x00078C98 File Offset: 0x00076E98
		public static int GetAGCLevel(this Photon.Realtime.Player player)
		{
			object[] array = player.GetObjectProperty("gc") as object[];
			if (array == null || array.Length <= 2)
			{
				return 0;
			}
			return (int)array[2];
		}

		// Token: 0x060017C5 RID: 6085 RVA: 0x00078CCC File Offset: 0x00076ECC
		public static Recorder.MicType? GetMic(this Photon.Realtime.Player player)
		{
			Recorder.MicType? result = null;
			try
			{
				result = new Recorder.MicType?((Recorder.MicType)player.GetObjectProperty("m"));
			}
			catch
			{
				result = null;
			}
			return result;
		}

		// Token: 0x060017C6 RID: 6086 RVA: 0x00078D18 File Offset: 0x00076F18
		private static bool HasBoolProperty(this Photon.Realtime.Player player, string prop)
		{
			object obj;
			return player.CustomProperties.TryGetValue(prop, out obj) && (bool)obj;
		}

		// Token: 0x060017C7 RID: 6087 RVA: 0x00078D40 File Offset: 0x00076F40
		private static int? GetIntProperty(this Photon.Realtime.Player player, string prop)
		{
			object obj;
			if (player.CustomProperties.TryGetValue(prop, out obj))
			{
				return new int?((int)obj);
			}
			return null;
		}

		// Token: 0x060017C8 RID: 6088 RVA: 0x00078D74 File Offset: 0x00076F74
		private static object GetObjectProperty(this Photon.Realtime.Player player, string prop)
		{
			object result;
			if (player.CustomProperties.TryGetValue(prop, out result))
			{
				return result;
			}
			return null;
		}

		// Token: 0x04001620 RID: 5664
		internal const string MUTED_KEY = "mu";

		// Token: 0x04001621 RID: 5665
		internal const string PHOTON_VAD_KEY = "pv";

		// Token: 0x04001622 RID: 5666
		internal const string WEBRTC_AEC_KEY = "ec";

		// Token: 0x04001623 RID: 5667
		internal const string WEBRTC_VAD_KEY = "wv";

		// Token: 0x04001624 RID: 5668
		internal const string WEBRTC_AGC_KEY = "gc";

		// Token: 0x04001625 RID: 5669
		internal const string MIC_KEY = "m";
	}
}
