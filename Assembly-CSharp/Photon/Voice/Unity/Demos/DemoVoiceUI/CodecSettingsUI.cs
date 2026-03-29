using System;
using System.Collections.Generic;
using POpusCodec.Enums;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Photon.Voice.Unity.Demos.DemoVoiceUI
{
	// Token: 0x02000390 RID: 912
	public class CodecSettingsUI : MonoBehaviour
	{
		// Token: 0x0600176C RID: 5996 RVA: 0x00077230 File Offset: 0x00075430
		private void Awake()
		{
			this.frameDurationDropdown.ClearOptions();
			this.frameDurationDropdown.AddOptions(CodecSettingsUI.frameDurationOptions);
			this.InitFrameDuration();
			this.frameDurationDropdown.SetSingleOnValueChangedCallback(new UnityAction<int>(this.OnFrameDurationChanged));
			this.samplingRateDropdown.ClearOptions();
			this.samplingRateDropdown.AddOptions(CodecSettingsUI.samplingRateOptions);
			this.InitSamplingRate();
			this.samplingRateDropdown.SetSingleOnValueChangedCallback(new UnityAction<int>(this.OnSamplingRateChanged));
			this.bitrateInputField.SetSingleOnValueChangedCallback(new UnityAction<string>(this.OnBitrateChanged));
			this.InitBitrate();
		}

		// Token: 0x0600176D RID: 5997 RVA: 0x000772CA File Offset: 0x000754CA
		private void Update()
		{
			this.InitFrameDuration();
			this.InitSamplingRate();
			this.InitBitrate();
		}

		// Token: 0x0600176E RID: 5998 RVA: 0x000772E0 File Offset: 0x000754E0
		private void OnBitrateChanged(string newBitrateString)
		{
			int bitrate;
			if (int.TryParse(newBitrateString, out bitrate))
			{
				this.recorder.Bitrate = bitrate;
			}
		}

		// Token: 0x0600176F RID: 5999 RVA: 0x00077304 File Offset: 0x00075504
		private void OnFrameDurationChanged(int index)
		{
			OpusCodec.FrameDuration frameDuration = this.recorder.FrameDuration;
			switch (index)
			{
			case 0:
				frameDuration = OpusCodec.FrameDuration.Frame2dot5ms;
				break;
			case 1:
				frameDuration = OpusCodec.FrameDuration.Frame5ms;
				break;
			case 2:
				frameDuration = OpusCodec.FrameDuration.Frame10ms;
				break;
			case 3:
				frameDuration = OpusCodec.FrameDuration.Frame20ms;
				break;
			case 4:
				frameDuration = OpusCodec.FrameDuration.Frame40ms;
				break;
			case 5:
				frameDuration = OpusCodec.FrameDuration.Frame60ms;
				break;
			}
			this.recorder.FrameDuration = frameDuration;
		}

		// Token: 0x06001770 RID: 6000 RVA: 0x00077378 File Offset: 0x00075578
		private void OnSamplingRateChanged(int index)
		{
			SamplingRate samplingRate = this.recorder.SamplingRate;
			switch (index)
			{
			case 0:
				samplingRate = SamplingRate.Sampling08000;
				break;
			case 1:
				samplingRate = SamplingRate.Sampling12000;
				break;
			case 2:
				samplingRate = SamplingRate.Sampling16000;
				break;
			case 3:
				samplingRate = SamplingRate.Sampling24000;
				break;
			case 4:
				samplingRate = SamplingRate.Sampling48000;
				break;
			}
			this.recorder.SamplingRate = samplingRate;
		}

		// Token: 0x06001771 RID: 6001 RVA: 0x000773E0 File Offset: 0x000755E0
		private void InitFrameDuration()
		{
			int value = 0;
			OpusCodec.FrameDuration frameDuration = this.recorder.FrameDuration;
			if (frameDuration <= OpusCodec.FrameDuration.Frame10ms)
			{
				if (frameDuration != OpusCodec.FrameDuration.Frame5ms)
				{
					if (frameDuration == OpusCodec.FrameDuration.Frame10ms)
					{
						value = 2;
					}
				}
				else
				{
					value = 1;
				}
			}
			else if (frameDuration != OpusCodec.FrameDuration.Frame20ms)
			{
				if (frameDuration != OpusCodec.FrameDuration.Frame40ms)
				{
					if (frameDuration == OpusCodec.FrameDuration.Frame60ms)
					{
						value = 5;
					}
				}
				else
				{
					value = 4;
				}
			}
			else
			{
				value = 3;
			}
			this.frameDurationDropdown.value = value;
		}

		// Token: 0x06001772 RID: 6002 RVA: 0x00077450 File Offset: 0x00075650
		private void InitSamplingRate()
		{
			int value = 0;
			SamplingRate samplingRate = this.recorder.SamplingRate;
			if (samplingRate <= SamplingRate.Sampling16000)
			{
				if (samplingRate != SamplingRate.Sampling12000)
				{
					if (samplingRate == SamplingRate.Sampling16000)
					{
						value = 2;
					}
				}
				else
				{
					value = 1;
				}
			}
			else if (samplingRate != SamplingRate.Sampling24000)
			{
				if (samplingRate == SamplingRate.Sampling48000)
				{
					value = 4;
				}
			}
			else
			{
				value = 3;
			}
			this.samplingRateDropdown.value = value;
		}

		// Token: 0x06001773 RID: 6003 RVA: 0x000774B4 File Offset: 0x000756B4
		private void InitBitrate()
		{
			this.bitrateInputField.text = this.recorder.Bitrate.ToString();
		}

		// Token: 0x040015D5 RID: 5589
		[SerializeField]
		private Dropdown frameDurationDropdown;

		// Token: 0x040015D6 RID: 5590
		[SerializeField]
		private Dropdown samplingRateDropdown;

		// Token: 0x040015D7 RID: 5591
		[SerializeField]
		private InputField bitrateInputField;

		// Token: 0x040015D8 RID: 5592
		[SerializeField]
		private Recorder recorder;

		// Token: 0x040015D9 RID: 5593
		private static readonly List<string> frameDurationOptions = new List<string>
		{
			"2.5ms",
			"5ms",
			"10ms",
			"20ms",
			"40ms",
			"60ms"
		};

		// Token: 0x040015DA RID: 5594
		private static readonly List<string> samplingRateOptions = new List<string>
		{
			"8kHz",
			"12kHz",
			"16kHz",
			"24kHz",
			"48kHz"
		};
	}
}
