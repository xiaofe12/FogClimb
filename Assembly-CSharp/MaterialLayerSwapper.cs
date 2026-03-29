using System;
using UnityEngine;

// Token: 0x020002A4 RID: 676
public class MaterialLayerSwapper : MonoBehaviour
{
	// Token: 0x06001287 RID: 4743 RVA: 0x0005E398 File Offset: 0x0005C598
	private void Swap()
	{
		string name = "_Color" + this.layer.x.ToString("F0");
		string name2 = "_Smooth" + this.layer.x.ToString("F0");
		string name3 = "_Height" + this.layer.x.ToString("F0");
		string name4 = "_Texture" + this.layer.x.ToString("F0");
		string name5 = "_Triplanar" + this.layer.x.ToString("F0");
		string name6 = "_UV" + this.layer.x.ToString("F0");
		string name7 = "_Flip" + this.layer.x.ToString("F0");
		string name8 = "_Remap" + this.layer.x.ToString("F0");
		Material material = base.GetComponentInChildren<Renderer>().sharedMaterials[this.targetMaterial];
		this.color = material.GetColor(name);
		this.smooth = material.GetFloat(name2);
		this.height = material.GetFloat(name3);
		this.texture = material.GetTexture(name4);
		this.triplanar = material.GetFloat(name5);
		this.uv = material.GetFloat(name6);
		this.flip = material.GetFloat(name7);
		this.remap = material.GetVector(name8);
		string name9 = "_Color" + this.layer.y.ToString("F0");
		string name10 = "_Smooth" + this.layer.y.ToString("F0");
		string name11 = "_Height" + this.layer.y.ToString("F0");
		string name12 = "_Texture" + this.layer.y.ToString("F0");
		string name13 = "_Triplanar" + this.layer.y.ToString("F0");
		string name14 = "_UV" + this.layer.y.ToString("F0");
		string name15 = "_Flip" + this.layer.y.ToString("F0");
		string name16 = "_Remap" + this.layer.y.ToString("F0");
		this.color2 = material.GetColor(name9);
		this.smooth2 = material.GetFloat(name10);
		this.height2 = material.GetFloat(name11);
		this.texture2 = material.GetTexture(name12);
		this.triplanar2 = material.GetFloat(name13);
		this.uv2 = material.GetFloat(name14);
		this.flip2 = material.GetFloat(name15);
		this.remap2 = material.GetVector(name16);
		material.SetColor(name9, this.color);
		material.SetFloat(name10, this.smooth);
		material.SetFloat(name11, this.height);
		material.SetTexture(name12, this.texture);
		material.SetFloat(name13, this.triplanar);
		material.SetFloat(name14, this.uv);
		material.SetFloat(name15, this.flip);
		material.SetVector(name16, this.remap);
		material.SetColor(name, this.color2);
		material.SetFloat(name2, this.smooth2);
		material.SetFloat(name3, this.height2);
		material.SetTexture(name4, this.texture2);
		material.SetFloat(name5, this.triplanar2);
		material.SetFloat(name6, this.uv2);
		material.SetFloat(name7, this.flip2);
		material.SetVector(name8, this.remap2);
	}

	// Token: 0x0400113F RID: 4415
	public int targetMaterial;

	// Token: 0x04001140 RID: 4416
	public Vector2Int layer;

	// Token: 0x04001141 RID: 4417
	[ColorUsage(true, true)]
	public Color color;

	// Token: 0x04001142 RID: 4418
	public float smooth;

	// Token: 0x04001143 RID: 4419
	public float height;

	// Token: 0x04001144 RID: 4420
	public Texture texture;

	// Token: 0x04001145 RID: 4421
	public float triplanar;

	// Token: 0x04001146 RID: 4422
	public float uv;

	// Token: 0x04001147 RID: 4423
	public float flip;

	// Token: 0x04001148 RID: 4424
	public Vector2 remap;

	// Token: 0x04001149 RID: 4425
	[ColorUsage(true, true)]
	public Color color2;

	// Token: 0x0400114A RID: 4426
	public float smooth2;

	// Token: 0x0400114B RID: 4427
	public float height2;

	// Token: 0x0400114C RID: 4428
	public Texture texture2;

	// Token: 0x0400114D RID: 4429
	public float triplanar2;

	// Token: 0x0400114E RID: 4430
	public float uv2;

	// Token: 0x0400114F RID: 4431
	public float flip2;

	// Token: 0x04001150 RID: 4432
	public Vector2 remap2;
}
