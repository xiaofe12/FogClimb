using System;
using UnityEngine;

// Token: 0x0200022F RID: 559
public class CinemaCamera : MonoBehaviour
{
	// Token: 0x0600109B RID: 4251 RVA: 0x0005389A File Offset: 0x00051A9A
	private void Start()
	{
		this.fog = Object.FindFirstObjectByType<Fog>();
		this.gM = Object.FindAnyObjectByType<GUIManager>();
		this.oldCam = Camera.main;
		this.rootObj = base.transform.root.gameObject;
	}

	// Token: 0x0600109C RID: 4252 RVA: 0x000538D4 File Offset: 0x00051AD4
	private void Update()
	{
		if (this.t)
		{
			this.rootObj.SetActive(false);
		}
		if ((Application.isEditor || Debug.isDebugBuild) && Input.GetKey(KeyCode.C) && Input.GetKeyDown(KeyCode.M))
		{
			this.on = true;
		}
		if (this.on)
		{
			this.ambience.parent = base.transform;
			if (this.fog)
			{
				this.fog.gameObject.SetActive(false);
			}
			if (this.oldCam)
			{
				this.oldCam.gameObject.SetActive(false);
			}
			base.transform.parent = null;
			this.cam.parent = null;
			this.cam.gameObject.SetActive(true);
			this.vel = Vector3.Lerp(this.vel, Vector3.zero, 1f * Time.deltaTime);
			this.rot = Vector3.Lerp(this.rot, Vector3.zero, 2.5f * Time.deltaTime);
			float num = 0.05f;
			this.rot.y = this.rot.y + Input.GetAxis("Mouse X") * num * 0.05f;
			this.rot.x = this.rot.x + Input.GetAxis("Mouse Y") * num * 0.05f;
			if (Input.GetKey(KeyCode.D))
			{
				this.vel.x = this.vel.x + num * Time.deltaTime;
			}
			if (Input.GetKey(KeyCode.A))
			{
				this.vel.x = this.vel.x - num * Time.deltaTime;
			}
			if (Input.GetKey(KeyCode.W))
			{
				this.vel.z = this.vel.z + num * Time.deltaTime;
			}
			if (Input.GetKey(KeyCode.S))
			{
				this.vel.z = this.vel.z - num * Time.deltaTime;
			}
			if (Input.GetKey(KeyCode.Space))
			{
				this.vel.y = this.vel.y + num * Time.deltaTime;
			}
			if (Input.GetKey(KeyCode.LeftControl))
			{
				this.vel.y = this.vel.y - num * Time.deltaTime;
			}
			if (Input.GetKey(KeyCode.CapsLock))
			{
				this.vel = Vector3.Lerp(this.vel, Vector3.zero, 5f * Time.deltaTime);
			}
			this.cam.transform.Rotate(Vector3.up * this.rot.y, Space.World);
			this.cam.transform.Rotate(base.transform.right * -this.rot.x);
			this.cam.transform.Translate(Vector3.right * this.vel.x, Space.Self);
			this.cam.transform.Translate(Vector3.forward * this.vel.z, Space.Self);
			this.cam.transform.Translate(Vector3.up * this.vel.y, Space.World);
			this.t = true;
		}
	}

	// Token: 0x04000ECD RID: 3789
	public bool on;

	// Token: 0x04000ECE RID: 3790
	public Transform cam;

	// Token: 0x04000ECF RID: 3791
	private GameObject rootObj;

	// Token: 0x04000ED0 RID: 3792
	private Fog fog;

	// Token: 0x04000ED1 RID: 3793
	private GUIManager gM;

	// Token: 0x04000ED2 RID: 3794
	private Camera oldCam;

	// Token: 0x04000ED3 RID: 3795
	private Vector3 vel;

	// Token: 0x04000ED4 RID: 3796
	private Vector3 rot;

	// Token: 0x04000ED5 RID: 3797
	private bool t;

	// Token: 0x04000ED6 RID: 3798
	public Transform ambience;
}
