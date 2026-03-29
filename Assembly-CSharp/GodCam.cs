using System;
using UnityEngine;

// Token: 0x0200001D RID: 29
[Serializable]
public class GodCam
{
	// Token: 0x0600023A RID: 570 RVA: 0x00010AD2 File Offset: 0x0000ECD2
	public void Update(Transform transform, MainCamera cam)
	{
		this.DoOrbiting(transform, cam);
		this.DoRotation(transform, cam);
		this.DoMovement(transform, cam);
		this.DoFOV(transform, cam);
		this.DoGamefeel(transform, cam);
	}

	// Token: 0x0600023B RID: 571 RVA: 0x00010AFC File Offset: 0x0000ECFC
	private void DoOrbiting(Transform transform, MainCamera cam)
	{
		if (!this.isOrbiting)
		{
			if (Input.GetKey(KeyCode.Mouse0) && this.canOrbit)
			{
				Character orbitCharacter = this.GetOrbitCharacter(transform, cam);
				if (orbitCharacter)
				{
					this.isOrbiting = true;
					this.orbitingCharacter = orbitCharacter;
					this.orbitingPoint = orbitCharacter.Center;
				}
				else
				{
					RaycastHit raycastHit = HelperFunctions.LineCheck(transform.position, transform.TransformPoint(Vector3.forward * 5000f), HelperFunctions.LayerType.AllPhysical, 0f, QueryTriggerInteraction.Ignore);
					if (raycastHit.transform)
					{
						this.isOrbiting = true;
						this.orbitingCharacter = null;
						this.orbitingPoint = raycastHit.point;
					}
				}
			}
		}
		else if (!Input.GetKey(KeyCode.Mouse0))
		{
			this.isOrbiting = false;
		}
		if (this.isOrbiting)
		{
			this.orbitinAmount = Mathf.MoveTowards(this.orbitinAmount, 1f, Time.unscaledDeltaTime * Mathf.Lerp(this.orbitinAmount, 1f, 0.3f));
		}
		else
		{
			this.orbitinAmount = Mathf.Lerp(this.orbitinAmount, 0f, Time.unscaledDeltaTime * 2f);
		}
		if (this.orbitinAmount > 0.001f)
		{
			if (this.orbitingCharacter)
			{
				this.orbitingPoint = this.orbitingCharacter.Center;
			}
			Vector3 normalized = (this.orbitingPoint - transform.position).normalized;
			Vector3 dir = FRILerp.Lerp(transform.forward, normalized, 2f * this.orbitinAmount, false);
			this.lookVel = FRILerp.Lerp(this.lookVel, Vector2.zero, 2f * this.orbitinAmount, false);
			this.lookData = HelperFunctions.DirectionToLook(dir);
		}
	}

	// Token: 0x0600023C RID: 572 RVA: 0x00010CC4 File Offset: 0x0000EEC4
	private Character GetOrbitCharacter(Transform transform, MainCamera cam)
	{
		float num = 15f;
		Character result = null;
		foreach (Character character in Character.AllCharacters)
		{
			float num2 = Vector3.Angle(character.Center - transform.position, transform.forward);
			if (num2 < num && HelperFunctions.LineCheck(transform.position, character.Center, HelperFunctions.LayerType.TerrainMap, 0f, QueryTriggerInteraction.Ignore).transform == null)
			{
				num = num2;
				result = character;
			}
		}
		return result;
	}

	// Token: 0x0600023D RID: 573 RVA: 0x00010D6C File Offset: 0x0000EF6C
	private void DoGamefeel(Transform transform, MainCamera cam)
	{
		transform.Rotate(GamefeelHandler.instance.GetRotation(), Space.World);
	}

	// Token: 0x0600023E RID: 574 RVA: 0x00010D80 File Offset: 0x0000EF80
	private void DoFOV(Transform transform, MainCamera cam)
	{
		float a = cam.cam.fieldOfView / 70f;
		this.targetFov += -Input.mouseScrollDelta.y * 2f * Mathf.Lerp(a, 1f, 0.25f);
		this.targetFov = Mathf.Clamp(this.targetFov, 1f, 120f);
		cam.cam.fieldOfView = Mathf.Lerp(cam.cam.fieldOfView, this.targetFov, Time.unscaledDeltaTime * 5f);
	}

	// Token: 0x0600023F RID: 575 RVA: 0x00010E18 File Offset: 0x0000F018
	private void DoMovement(Transform transform, MainCamera cam)
	{
		this.currentKeyMult = Mathf.Lerp(this.currentKeyMult, this.currentKeyMultTarget, Time.unscaledDeltaTime * 2f);
		if (Input.GetKey(KeyCode.LeftShift))
		{
			this.sprintMult = Mathf.Lerp(this.sprintMult, 10f, Time.unscaledDeltaTime * 2f);
		}
		else
		{
			this.sprintMult = Mathf.Lerp(this.sprintMult, 1f, Time.unscaledDeltaTime * 2f);
		}
		Vector3 zero = Vector3.zero;
		if (Input.GetKey(KeyCode.W))
		{
			zero.z += 1f;
		}
		if (Input.GetKey(KeyCode.S))
		{
			zero.z -= 1f;
		}
		if (Input.GetKey(KeyCode.A))
		{
			zero.x -= 1f;
		}
		if (Input.GetKey(KeyCode.D))
		{
			zero.x += 1f;
		}
		if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.E))
		{
			zero.y += 1f;
		}
		if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.Q))
		{
			zero.y -= 1f;
		}
		this.vel += transform.TransformDirection(new Vector3(zero.x, zero.y, zero.z)) * this.force * this.sprintMult * this.currentKeyMult * Time.unscaledDeltaTime;
		this.vel = FRILerp.Lerp(this.vel, Vector3.zero, this.drag, false);
		transform.position += this.vel * Time.unscaledDeltaTime;
	}

	// Token: 0x06000240 RID: 576 RVA: 0x00010FE0 File Offset: 0x0000F1E0
	private void DoRotation(Transform transform, MainCamera cam)
	{
		float d = cam.cam.fieldOfView / 70f;
		Vector2 a = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
		this.lookVel += a * 0.1f * this.lookSens * d;
		this.lookVel = FRILerp.Lerp(this.lookVel, Vector2.zero, this.lookDrag, false);
		this.lookData += this.lookVel * Time.unscaledDeltaTime;
		transform.rotation = Quaternion.LookRotation(HelperFunctions.LookToDirection(new Vector3(this.lookData.x, this.lookData.y, 0f), Vector3.forward));
	}

	// Token: 0x04000215 RID: 533
	public float lookSens = 5f;

	// Token: 0x04000216 RID: 534
	public float lookDrag = 3f;

	// Token: 0x04000217 RID: 535
	public float force = 5f;

	// Token: 0x04000218 RID: 536
	public float drag = 3f;

	// Token: 0x04000219 RID: 537
	private Vector3 vel = Vector3.zero;

	// Token: 0x0400021A RID: 538
	private Vector2 lookData = Vector2.zero;

	// Token: 0x0400021B RID: 539
	private Vector2 lookVel = Vector2.zero;

	// Token: 0x0400021C RID: 540
	private bool isOrbiting;

	// Token: 0x0400021D RID: 541
	private Vector3 orbitingPoint;

	// Token: 0x0400021E RID: 542
	private Character orbitingCharacter;

	// Token: 0x0400021F RID: 543
	private float currentKeyMult = 1f;

	// Token: 0x04000220 RID: 544
	private float currentKeyMultTarget = 1f;

	// Token: 0x04000221 RID: 545
	private float sprintMult = 1f;

	// Token: 0x04000222 RID: 546
	private float targetFov = 70f;

	// Token: 0x04000223 RID: 547
	private float orbitinAmount;

	// Token: 0x04000224 RID: 548
	internal bool canOrbit = true;
}
