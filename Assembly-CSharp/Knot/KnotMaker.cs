using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using pworld.Scripts.Extensions;
using TMPro;
using UnityEngine;

namespace Knot
{
	// Token: 0x020003A4 RID: 932
	public class KnotMaker : MonoBehaviour
	{
		// Token: 0x0600183C RID: 6204 RVA: 0x0007A944 File Offset: 0x00078B44
		private void Update()
		{
			this.scoreText.text = this.score.ToString();
			if (Input.GetKey(KeyCode.Escape))
			{
				this.Clear();
			}
			if (Input.GetKeyDown(KeyCode.Mouse0) && this.TryGrab())
			{
				this.grabbedRope = true;
				this.TieKnotFillToPoint(Input.mousePosition);
			}
			if (this.grabbedRope)
			{
				this.TieKnotFillToPoint(Input.mousePosition);
				Vector3 me;
				if (this.MouseToPlaneRaycast(out me, Input.mousePosition))
				{
					this.visualizer.knot.Add(new TiedKnotVisualizer.KnotPart(false, me.xyo(), -1));
					this.visualizer.Refresh();
					this.visualizer.knot.RemoveLast<TiedKnotVisualizer.KnotPart>();
				}
			}
			else
			{
				this.visualizer.Refresh();
			}
			if (Input.GetKeyUp(KeyCode.Mouse0))
			{
				this.grabbedRope = false;
			}
		}

		// Token: 0x0600183D RID: 6205 RVA: 0x0007AA18 File Offset: 0x00078C18
		private bool TryGrab()
		{
			RaycastHit[] source = Physics.SphereCastAll(Camera.main.ScreenPointToRay(Input.mousePosition), this.width);
			if (this.visualizer.knot.Count == 0)
			{
				return source.Any((RaycastHit hit) => hit.transform.GetSiblingIndex() < this.maxPartJumpAllowed);
			}
			Vector3 me;
			if (this.visualizer.knot.Count > 0 && this.MouseToPlaneRaycast(out me, Input.mousePosition))
			{
				Vector3 a = me.xyo();
				List<TiedKnotVisualizer.KnotPart> knot = this.visualizer.knot;
				float num = Vector3.Distance(a, knot[knot.Count - 1].position.xyo());
				Debug.Log(string.Format("distance: {0}", num));
				if (num < this.grabDistance)
				{
					return true;
				}
			}
			return false;
		}

		// Token: 0x0600183E RID: 6206 RVA: 0x0007AAD8 File Offset: 0x00078CD8
		public bool MouseToPlaneRaycast(out Vector3 position, Vector3 mousePosition)
		{
			Ray ray = Camera.main.ScreenPointToRay(mousePosition);
			Plane plane = new Plane(Camera.main.transform.forward, (KnotTemplateBoss.me != null) ? KnotTemplateBoss.me.displayRoot.position : Vector3.zero);
			float d;
			if (plane.Raycast(ray, out d))
			{
				position = ray.direction * d + ray.origin;
				return true;
			}
			position = Vector3.zero;
			return false;
		}

		// Token: 0x0600183F RID: 6207 RVA: 0x0007AB63 File Offset: 0x00078D63
		public void Clear()
		{
			this.score = 0f;
			this.grabbedRope = false;
			this.visualizer.knot.Clear();
		}

		// Token: 0x06001840 RID: 6208 RVA: 0x0007AB88 File Offset: 0x00078D88
		private void TieKnotFillToPoint(Vector3 mousePosition)
		{
			if (this.visualizer.knot.Count == 0)
			{
				this.TieKnot(mousePosition);
				return;
			}
			Camera main = Camera.main;
			List<TiedKnotVisualizer.KnotPart> knot = this.visualizer.knot;
			Vector3 a = main.WorldToScreenPoint(knot[knot.Count - 1].position);
			int num = Mathf.FloorToInt(Vector3.Distance(a, mousePosition) / this.minKnotSpacing);
			num = Mathf.Min(num, 100);
			Vector3 normalized = (a - mousePosition).normalized;
			for (int i = 0; i < num; i++)
			{
				this.TieKnot(a + -normalized * (this.minKnotSpacing * (float)(i + 1)));
			}
		}

		// Token: 0x06001841 RID: 6209 RVA: 0x0007AC38 File Offset: 0x00078E38
		private void TieKnot(Vector3 mousePosition)
		{
			KnotMaker.<>c__DisplayClass13_0 CS$<>8__locals1;
			CS$<>8__locals1.<>4__this = this;
			CS$<>8__locals1.mousePosition = mousePosition;
			RaycastHit[] array = Physics.SphereCastAll(Camera.main.ScreenPointToRay(CS$<>8__locals1.mousePosition), this.width);
			if (array.Length != 0)
			{
				int templateProgress = 0;
				if (this.visualizer.count > 0)
				{
					if (this.visualizer.knot.Any((TiedKnotVisualizer.KnotPart knot) => knot.part != -1))
					{
						templateProgress = this.visualizer.knot.Last((TiedKnotVisualizer.KnotPart knot) => knot.part != -1).part;
					}
				}
				int num = (from hit in array.ToList<RaycastHit>()
				orderby Mathf.Abs(hit.transform.GetSiblingIndex() - (templateProgress + 1))
				select hit).First<RaycastHit>().collider.transform.GetSiblingIndex();
				int num2 = templateProgress - 1;
				int num3 = templateProgress + this.maxPartJumpAllowed;
				bool quality = true;
				if (num > templateProgress && num < num3)
				{
					templateProgress = num;
				}
				if (num <= num2)
				{
					quality = false;
					num = -1;
					this.score -= 1f;
				}
				if (num >= num3)
				{
					quality = false;
					num = -1;
					this.score -= 1f;
				}
				this.<TieKnot>g__AddKnotPositionAtMousePosition|13_0(quality, num, ref CS$<>8__locals1);
				return;
			}
			this.score -= 1f;
			this.<TieKnot>g__AddKnotPositionAtMousePosition|13_0(false, -1, ref CS$<>8__locals1);
		}

		// Token: 0x06001844 RID: 6212 RVA: 0x0007AE04 File Offset: 0x00079004
		[CompilerGenerated]
		private void <TieKnot>g__AddKnotPositionAtMousePosition|13_0(bool quality, int hitPart, ref KnotMaker.<>c__DisplayClass13_0 A_3)
		{
			Vector3 me;
			if (this.MouseToPlaneRaycast(out me, A_3.mousePosition))
			{
				this.visualizer.knot.Add(new TiedKnotVisualizer.KnotPart(quality, me.xyo(), hitPart));
				Debug.Log(string.Format("Quality: {0}, Position: {1}", quality, me.xyo()));
			}
		}

		// Token: 0x0400166F RID: 5743
		public TiedKnotVisualizer visualizer;

		// Token: 0x04001670 RID: 5744
		public TextMeshProUGUI scoreText;

		// Token: 0x04001671 RID: 5745
		public float score;

		// Token: 0x04001672 RID: 5746
		public float minKnotSpacing = 0.01f;

		// Token: 0x04001673 RID: 5747
		public int maxPartJumpAllowed = 10;

		// Token: 0x04001674 RID: 5748
		public float width = 0.07f;

		// Token: 0x04001675 RID: 5749
		[SerializeField]
		private float grabDistance;

		// Token: 0x04001676 RID: 5750
		public bool grabbedRope;
	}
}
