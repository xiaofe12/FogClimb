using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Token: 0x02000259 RID: 601
public class FakeCursor : MonoBehaviour
{
	// Token: 0x06001133 RID: 4403 RVA: 0x00056854 File Offset: 0x00054A54
	private void Update()
	{
		Vector2 screenPoint = Mouse.current.position.ReadValue();
		Vector2 v;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(this.target.parent as RectTransform, screenPoint, null, out v);
		this.target.localPosition = v;
	}

	// Token: 0x04000FA9 RID: 4009
	public Transform target;
}
