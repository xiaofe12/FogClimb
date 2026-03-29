using System;
using System.Linq;
using UnityEngine;

// Token: 0x020001F0 RID: 496
public class VariationSwapper : MonoBehaviour, IGenConfigStep
{
	// Token: 0x06000F0C RID: 3852 RVA: 0x00049A98 File Offset: 0x00047C98
	public void EnableRandom()
	{
		float maxInclusive = this.Variations.Sum((VariationSwapper.Variation variation) => variation.chance);
		float num = Random.Range(0f, maxInclusive);
		GameObject parent = this.Variations.First<VariationSwapper.Variation>().parent;
		float num2 = 0f;
		foreach (VariationSwapper.Variation variation2 in this.Variations)
		{
			num2 += variation2.chance;
			if (num < num2)
			{
				Debug.Log(string.Format("Found new: {0}", variation2.parent));
				parent = variation2.parent;
				break;
			}
		}
		if (parent != null)
		{
			VariationSwapper.Variation[] variations = this.Variations;
			for (int i = 0; i < variations.Length; i++)
			{
				variations[i].parent.SetActive(false);
			}
			parent.SetActive(true);
		}
	}

	// Token: 0x06000F0D RID: 3853 RVA: 0x00049B7E File Offset: 0x00047D7E
	public void RunStep()
	{
		this.EnableRandom();
	}

	// Token: 0x04000D13 RID: 3347
	public VariationSwapper.Variation[] Variations;

	// Token: 0x020004C1 RID: 1217
	[Serializable]
	public class Variation
	{
		// Token: 0x04001A60 RID: 6752
		public GameObject parent;

		// Token: 0x04001A61 RID: 6753
		public float chance = 1f;
	}
}
