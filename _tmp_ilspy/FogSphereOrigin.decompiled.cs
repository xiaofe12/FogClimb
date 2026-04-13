using UnityEngine;

public class FogSphereOrigin : MonoBehaviour
{
	public float size = 650f;

	public float moveOnHeight;

	public float moveOnForward;

	public bool disableFog;

	public void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.blue;
		Gizmos.DrawCube(new Vector3(0f, moveOnHeight, moveOnForward), new Vector3(200f, 200f, 2f));
		Gizmos.color = Color.green;
		Gizmos.DrawCube(new Vector3(0f, moveOnHeight, moveOnForward), new Vector3(200f, 2f, 1000f));
	}
}
