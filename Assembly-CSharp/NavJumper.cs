using System;
using System.Collections.Generic;
using System.Linq;
using pworld.Scripts.Extensions;
using UnityEngine;

// Token: 0x020002AC RID: 684
public class NavJumper : MonoBehaviour
{
	// Token: 0x060012AC RID: 4780 RVA: 0x0005F1C9 File Offset: 0x0005D3C9
	private void Start()
	{
	}

	// Token: 0x060012AD RID: 4781 RVA: 0x0005F1CC File Offset: 0x0005D3CC
	private void Jump()
	{
		List<RaycastHit> list = new List<RaycastHit>();
		for (int i = 0; i < this.castsPerJump; i++)
		{
			RaycastHit item;
			if (Physics.Raycast(base.transform.position + (ExtMath.RandInsideUnitCircle() * this.castRadius).xny(this.castHeight), Vector3.down * this.castHeight, out item))
			{
				list.Add(item);
			}
		}
		Debug.Log(string.Format("Total: {0}", list.Count));
		list = (from hit in list
		where Vector3.Angle(hit.normal, Vector3.up) < 50f
		select hit).ToList<RaycastHit>();
		Debug.Log(string.Format("After angle: {0}", list.Count));
		list = (from hit in list
		where Vector3.Distance(hit.point, base.transform.position) < this.maxDistance
		select hit).ToList<RaycastHit>();
		Debug.Log(string.Format("After distance: {0}", list.Count));
		list = (from hit in list
		where hit.point.z > base.transform.position.z && hit.point.y > base.transform.position.y
		select hit).ToList<RaycastHit>();
		list = (from hit in list
		where hit.point.y > base.transform.position.y
		select hit).ToList<RaycastHit>();
		Debug.Log(string.Format("After Z: {0}", list.Count));
		if (list.Count == 0)
		{
			return;
		}
		RaycastHit raycastHit = (from hit in list
		orderby hit.point.z descending
		select hit).First<RaycastHit>();
		Debug.DrawLine(base.transform.position + Vector3.up, raycastHit.point + Vector3.up, Color.green, 10f);
		base.transform.position = raycastHit.point;
	}

	// Token: 0x060012AE RID: 4782 RVA: 0x0005F391 File Offset: 0x0005D591
	private void Update()
	{
	}

	// Token: 0x04001166 RID: 4454
	public int castsPerJump = 100;

	// Token: 0x04001167 RID: 4455
	public float maxDistance = 3f;

	// Token: 0x04001168 RID: 4456
	public float castRadius = 1f;

	// Token: 0x04001169 RID: 4457
	public float castHeight = 100f;

	// Token: 0x0400116A RID: 4458
	private int fails;
}
