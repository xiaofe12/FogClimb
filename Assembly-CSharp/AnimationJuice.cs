using System;
using UnityEngine;
using Zorro.Core;

// Token: 0x020001FE RID: 510
public class AnimationJuice : MonoBehaviour
{
	// Token: 0x06000F47 RID: 3911 RVA: 0x0004B720 File Offset: 0x00049920
	public void Screenshake(float amount)
	{
		Vector3 position = base.transform.position;
		if (this.overrideGameFeelTransform)
		{
			position = this.overrideGameFeelTransform.position;
		}
		if (GamefeelHandler.instance)
		{
			GamefeelHandler.instance.AddPerlinShakeProximity(position, amount, 0.3f, 15f, 5f);
		}
	}

	// Token: 0x06000F48 RID: 3912 RVA: 0x0004B77C File Offset: 0x0004997C
	public void PlayParticle(int index)
	{
		if (!this.particles.WithinRange(index))
		{
			Debug.LogError("PlayParticle index out of range");
			return;
		}
		ParticleSystem particleSystem = this.particles[index];
		if (particleSystem != null)
		{
			particleSystem.Play();
			return;
		}
		Debug.LogError("Particle could not be played, is null");
	}

	// Token: 0x04000D8C RID: 3468
	public Transform overrideGameFeelTransform;

	// Token: 0x04000D8D RID: 3469
	public ParticleSystem[] particles;
}
