using System;
using System.Collections.Generic;
using System.Linq;
using pworld.Scripts.Extensions;
using UnityEngine;
using Zorro.Core;

// Token: 0x02000346 RID: 838
public class StupidRockPlacer : MonoBehaviour
{
	// Token: 0x1700014A RID: 330
	// (get) Token: 0x0600157F RID: 5503 RVA: 0x0006E8BF File Offset: 0x0006CABF
	public Vector3 size
	{
		get
		{
			return base.transform.localScale.xyz();
		}
	}

	// Token: 0x06001580 RID: 5504 RVA: 0x0006E8D1 File Offset: 0x0006CAD1
	private void OnDrawGizmosSelected()
	{
		Gizmos.DrawWireCube(base.transform.position + this.size / 2f, this.size);
	}

	// Token: 0x06001581 RID: 5505 RVA: 0x0006E8FE File Offset: 0x0006CAFE
	public void Clear()
	{
		if (this.rockParent)
		{
			this.rockParent.KillAllChildren(true, false, true);
		}
	}

	// Token: 0x06001582 RID: 5506 RVA: 0x0006E91B File Offset: 0x0006CB1B
	private void Start()
	{
	}

	// Token: 0x06001583 RID: 5507 RVA: 0x0006E920 File Offset: 0x0006CB20
	private void ValidatePool()
	{
		foreach (Transform transform in (from t in this.pieceRoot.GetComponentsInChildren<Transform>()
		where t != this.pieceRoot
		select t).ToList<Transform>())
		{
			transform.gameObject.GetOrAddComponent<PutMeInWall>();
			transform.gameObject.layer = LayerMask.NameToLayer("Terrain");
			PExt.DirtyObj(transform.gameObject);
		}
	}

	// Token: 0x06001584 RID: 5508 RVA: 0x0006E9B4 File Offset: 0x0006CBB4
	public void Go()
	{
		this.rockParent = null;
		this.rockParent = base.transform.parent.Find("Rocks: " + base.gameObject.name);
		if (!this.rockParent)
		{
			this.rockParent = new GameObject("Rocks: " + base.gameObject.name).transform;
			this.rockParent.SetParent(base.transform.parent);
		}
		this.rockParent.SetSiblingIndex(base.transform.GetSiblingIndex() + 1);
		this.rocks = (from x in this.pieceRoot.GetComponentsInChildren<PutMeInWall>()
		select x.gameObject).ToList<GameObject>();
		this.lastPlaced = new List<GameObject>();
		int num = 0;
		int num2 = 0;
		while (num2 < this.amount || num > this.amount * 10)
		{
			num++;
			Vector3 startCast = base.transform.position + new Vector3(this.size.x.Rand(), this.size.y.Rand(), 0f);
			GameObject random = this.rocks.GetRandom<GameObject>();
			Vector3? wallPosition = random.GetComponent<PutMeInWall>().GetWallPosition(startCast, base.transform.localScale.z);
			if (wallPosition == null)
			{
				num2--;
			}
			else
			{
				GameObject gameObject = Object.Instantiate<GameObject>(random, wallPosition.Value, ExtQuaternion.RandomRotation());
				gameObject.transform.SetParent(this.rockParent);
				PutMeInWall putMeInWall;
				if (!gameObject.TryGetComponent<PutMeInWall>(out putMeInWall))
				{
					putMeInWall = gameObject.AddComponent<PutMeInWall>();
				}
				putMeInWall.gameObject.SetActive(true);
				this.lastPlaced.Add(gameObject);
				putMeInWall.RandomScale();
				Physics.SyncTransforms();
				PExt.DirtyObj(gameObject);
			}
			num2++;
		}
	}

	// Token: 0x06001585 RID: 5509 RVA: 0x0006EBA0 File Offset: 0x0006CDA0
	public void RemoveLastPlaced()
	{
		foreach (GameObject x in this.lastPlaced)
		{
			x == null;
		}
		this.lastPlaced = new List<GameObject>();
	}

	// Token: 0x06001586 RID: 5510 RVA: 0x0006EC00 File Offset: 0x0006CE00
	private void Update()
	{
	}

	// Token: 0x04001454 RID: 5204
	public List<GameObject> rocks;

	// Token: 0x04001455 RID: 5205
	public Transform pieceRoot;

	// Token: 0x04001456 RID: 5206
	public int amount = 10;

	// Token: 0x04001457 RID: 5207
	public Transform rockParent;

	// Token: 0x04001458 RID: 5208
	public List<GameObject> lastPlaced = new List<GameObject>();
}
