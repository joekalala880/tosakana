using UnityEngine;

// The round map at the bottom left. A camera looks down from the sky and follows you.
// The yellow dot shows where to go. When it is far away, it stays on the edge of the map.
public class MiniMap : MonoBehaviour
{
	public Camera mapCam;
	public Transform playerIcon;
	public Transform targetIcon;
	public float camHeight = 220f;
	public float iconHeight = 150f;

	void LateUpdate()
	{
		TosakanaGame game = TosakanaGame.Instance;
		if (game == null || mapCam == null)
			return;
		Transform focus = game.Focus;
		Vector3 p = focus.position;

		// The map turns with the camera, so "up" on the map is where you are looking
		mapCam.transform.SetPositionAndRotation(new Vector3(p.x, camHeight, p.z), Quaternion.Euler(90f, game.cameraRig.Yaw, 0));
		playerIcon.SetPositionAndRotation(new Vector3(p.x, iconHeight, p.z), Quaternion.Euler(0, focus.eulerAngles.y, 0));

		Place target = game.missions.Target;
		targetIcon.gameObject.SetActive(target != null && game.IsPlaying);
		if (target != null)
		{
			Vector3 offset = target.markerPosition - p;
			offset.y = 0;
			float maxDistance = mapCam.orthographicSize * 0.82f;
			if (offset.magnitude > maxDistance)
				offset = offset.normalized * maxDistance;
			targetIcon.position = new Vector3(p.x + offset.x, iconHeight + 1f, p.z + offset.z);
		}
	}
}
