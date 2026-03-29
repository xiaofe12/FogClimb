using System;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;

// Token: 0x02000321 RID: 801
public class ScreenVFX : MonoBehaviour
{
	// Token: 0x060014AD RID: 5293 RVA: 0x000696AC File Offset: 0x000678AC
	private void Start()
	{
		if (GUIManager.instance.photosensitivity)
		{
			this.duration *= 2f;
		}
	}

	// Token: 0x060014AE RID: 5294 RVA: 0x000696CC File Offset: 0x000678CC
	public void Test()
	{
		this.Play(1f);
	}

	// Token: 0x060014AF RID: 5295 RVA: 0x000696DC File Offset: 0x000678DC
	public void Play(float amount)
	{
		base.gameObject.SetActive(true);
		amount = 1f;
		if (GUIManager.instance.photosensitivity)
		{
			amount *= 0.3f;
		}
		if (this.tween != null)
		{
			this.tween.Kill(false);
		}
		if (GUIManager.instance.photosensitivity)
		{
			if (this.sequence != null)
			{
				this.sequence.Kill(false);
			}
			this.sequence = DOTween.Sequence();
			this.sequence.Append(this.renderer.material.DOFloat(amount, ScreenVFX.INTENSITY, this.sequenceInitialDuration)).Append(this.renderer.material.DOFloat(0f, ScreenVFX.INTENSITY, this.duration)).SetDelay(this.delay).OnComplete(new TweenCallback(this.Disable));
			return;
		}
		this.renderer.material.SetFloat(ScreenVFX.INTENSITY, amount);
		this.tween = this.renderer.material.DOFloat(0f, ScreenVFX.INTENSITY, this.duration).SetDelay(this.delay).OnComplete(new TweenCallback(this.Disable));
	}

	// Token: 0x060014B0 RID: 5296 RVA: 0x00069818 File Offset: 0x00067A18
	public void StartFX(float photosensitive = 0.5f)
	{
		base.gameObject.SetActive(true);
		float num = 1f;
		float num2 = this.duration;
		if (GUIManager.instance.photosensitivity)
		{
			num *= photosensitive;
		}
		this.renderer.material.SetFloat(ScreenVFX.INTENSITY, 0f);
		this.renderer.material.DOFloat(num, ScreenVFX.INTENSITY, this.duration);
	}

	// Token: 0x060014B1 RID: 5297 RVA: 0x00069885 File Offset: 0x00067A85
	public void EndFX()
	{
		this.renderer.material.DOFloat(0f, ScreenVFX.INTENSITY, this.duration).OnComplete(new TweenCallback(this.Disable));
	}

	// Token: 0x060014B2 RID: 5298 RVA: 0x000698B9 File Offset: 0x00067AB9
	private void Disable()
	{
		base.gameObject.SetActive(false);
	}

	// Token: 0x0400132A RID: 4906
	private static readonly int INTENSITY = Shader.PropertyToID("_Intensity");

	// Token: 0x0400132B RID: 4907
	public Renderer renderer;

	// Token: 0x0400132C RID: 4908
	public float sequenceInitialDuration = 0.25f;

	// Token: 0x0400132D RID: 4909
	public float duration = 0.5f;

	// Token: 0x0400132E RID: 4910
	public float delay = 0.25f;

	// Token: 0x0400132F RID: 4911
	private Tweener tween;

	// Token: 0x04001330 RID: 4912
	private Sequence sequence;
}
