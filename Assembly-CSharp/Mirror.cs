using System;
using Unity.Mathematics;
using UnityEngine;

// Token: 0x0200013E RID: 318
[DefaultExecutionOrder(99999)]
public class Mirror : MonoBehaviour
{
	// Token: 0x06000A26 RID: 2598 RVA: 0x00035A0C File Offset: 0x00033C0C
	private void Start()
	{
		Vector2 quadSize = this.getQuadSize();
		this.mirrorWidth = quadSize.x;
		this.mirrorHeight = quadSize.y;
	}

	// Token: 0x06000A27 RID: 2599 RVA: 0x00035A38 File Offset: 0x00033C38
	private Vector2 getQuadSize()
	{
		Vector2 result = default(Vector2);
		Renderer component = this.mirrorTransform.GetComponent<Renderer>();
		result.x = Mathf.Abs(component.bounds.size.z);
		result.y = component.bounds.size.y;
		return result;
	}

	// Token: 0x06000A28 RID: 2600 RVA: 0x00035A94 File Offset: 0x00033C94
	private void LateUpdate()
	{
		if (this.player == null && Character.localCharacter != null)
		{
			this.player = Character.localCharacter;
		}
		if (this.player == null)
		{
			return;
		}
		if (Camera.main != null && !this.isInitialized)
		{
			this.mainCam = Camera.main;
			this.mirrorCamera.depth -= 1f;
			this.mirrorCamera.targetTexture = this.renderTexture;
			this.isInitialized = true;
		}
		this.mainCam.transform.position - this.mirrorTransform.position;
		if (this.mirrorCamera == null)
		{
			return;
		}
		Vector3 up = this.mirrorTransform.up;
		Vector3 position = this.mirrorTransform.position;
		Vector3 vector = this.mainCam.transform.position - position;
		Vector3 b = Vector3.Reflect(vector, up);
		this.depth = vector.x;
		if (this.useCameraTransform)
		{
			this.mirrorCamera.transform.position = position + b + this.mirrorTransform.forward * this.verticalOffset;
		}
		Vector3 upwards = Vector3.Reflect(this.mainCam.transform.up, up);
		Quaternion rotation = Quaternion.LookRotation(Vector3.Reflect(this.mainCam.transform.forward, up), upwards);
		if (this.useCameraRot)
		{
			this.mirrorCamera.transform.rotation = rotation;
		}
		float num = Mathf.Abs(this.mirrorCamera.transform.position.x - this.mirrorTransform.position.x);
		this.mirrorCamera.projectionMatrix = Mirror.MirrorProjectionMatrix(this.mirrorCamera, this.mirrorCamera.farClipPlane, this.nearplaneOffset + num, this.mirrorTransform.position, this.mirrorTransform.up, this.mirrorWidth, this.mirrorHeight);
	}

	// Token: 0x06000A29 RID: 2601 RVA: 0x00035C9C File Offset: 0x00033E9C
	private void OnPreRender()
	{
		GL.invertCulling = true;
	}

	// Token: 0x06000A2A RID: 2602 RVA: 0x00035CA4 File Offset: 0x00033EA4
	private void OnPostRender()
	{
		GL.invertCulling = false;
	}

	// Token: 0x06000A2B RID: 2603 RVA: 0x00035CAC File Offset: 0x00033EAC
	public void OnPreCull()
	{
	}

	// Token: 0x06000A2C RID: 2604 RVA: 0x00035CB0 File Offset: 0x00033EB0
	public static Matrix4x4 MirrorProjectionMatrix(Camera cam, float far, float near, Vector3 mirrorCenter, Vector3 mirrorForward, float mirrorWidth, float mirrorHeight)
	{
		Transform transform = cam.transform;
		Vector3 a = -Vector3.Cross(mirrorForward, Vector3.up).normalized;
		Vector3 vector = transform.InverseTransformPoint(mirrorCenter + -a * (mirrorWidth / 2f));
		Vector3 vector2 = transform.InverseTransformPoint(mirrorCenter + a * (mirrorWidth / 2f));
		Vector3 vector3 = transform.InverseTransformPoint(mirrorCenter + Vector3.up * (mirrorHeight / 2f));
		Vector3 vector4 = transform.InverseTransformPoint(mirrorCenter + Vector3.down * (mirrorHeight / 2f));
		Vector3 normalized = vector.normalized;
		Vector3 normalized2 = vector2.normalized;
		Vector3 normalized3 = vector3.normalized;
		Vector3 normalized4 = vector4.normalized;
		Plane plane = new Plane(Vector3.forward, Vector3.forward * near);
		float d;
		float d2;
		float d3;
		float d4;
		if (plane.Raycast(new Ray(Vector3.zero, normalized), out d) && plane.Raycast(new Ray(Vector3.zero, normalized2), out d2) && plane.Raycast(new Ray(Vector3.zero, normalized3), out d3) && plane.Raycast(new Ray(Vector3.zero, normalized4), out d4))
		{
			float x = (normalized * d).x;
			float x2 = (normalized2 * d2).x;
			float y = (normalized3 * d3).y;
			float y2 = (normalized4 * d4).y;
			return float4x4.PerspectiveOffCenter(x, x2, y2, y, near, far);
		}
		return Matrix4x4.identity;
	}

	// Token: 0x06000A2D RID: 2605 RVA: 0x00035E47 File Offset: 0x00034047
	public void Update()
	{
	}

	// Token: 0x06000A2E RID: 2606 RVA: 0x00035E4C File Offset: 0x0003404C
	private static Matrix4x4 PerspectiveOffCenter(float left, float right, float bottom, float top, float near, float far)
	{
		float value = 2f * near / (right - left);
		float value2 = 2f * near / (top - bottom);
		float value3 = (right + left) / (right - left);
		float value4 = (top + bottom) / (top - bottom);
		float value5 = -(far + near) / (far - near);
		float value6 = -(2f * far * near) / (far - near);
		float value7 = -1f;
		Matrix4x4 result = default(Matrix4x4);
		result[0, 0] = value;
		result[0, 1] = 0f;
		result[0, 2] = value3;
		result[0, 3] = 0f;
		result[1, 0] = 0f;
		result[1, 1] = value2;
		result[1, 2] = value4;
		result[1, 3] = 0f;
		result[2, 0] = 0f;
		result[2, 1] = 0f;
		result[2, 2] = value5;
		result[2, 3] = value6;
		result[3, 0] = 0f;
		result[3, 1] = 0f;
		result[3, 2] = value7;
		result[3, 3] = 0f;
		return result;
	}

	// Token: 0x04000980 RID: 2432
	public Camera mirrorCamera;

	// Token: 0x04000981 RID: 2433
	public Transform mirrorTransform;

	// Token: 0x04000982 RID: 2434
	private Character player;

	// Token: 0x04000983 RID: 2435
	private Camera mainCam;

	// Token: 0x04000984 RID: 2436
	public RenderTexture renderTexture;

	// Token: 0x04000985 RID: 2437
	private BoxCollider box;

	// Token: 0x04000986 RID: 2438
	public bool useCameraTransform;

	// Token: 0x04000987 RID: 2439
	public bool useCameraRot;

	// Token: 0x04000988 RID: 2440
	public float offsetScale;

	// Token: 0x04000989 RID: 2441
	public float mirrorCamDistance;

	// Token: 0x0400098A RID: 2442
	public float verticalOffset;

	// Token: 0x0400098B RID: 2443
	public bool isInitialized;

	// Token: 0x0400098C RID: 2444
	public float mirrorWidth;

	// Token: 0x0400098D RID: 2445
	public float mirrorHeight;

	// Token: 0x0400098E RID: 2446
	public float nearplaneOffset;

	// Token: 0x0400098F RID: 2447
	private float depth;
}
