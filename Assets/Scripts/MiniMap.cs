using UnityEngine;

// The round map at the bottom left. A camera looks down from the sky and follows you.
// The yellow dot shows where to go. When it is far away, it stays on the edge of the map.
public class MiniMap : MonoBehaviour
{
	public Camera mapCam;
	public Transform playerIcon;
	public Transform targetIcon;
	public Transform waypointIcon;   // purple: the waypoint you put on the big map (Tab)
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
			PutOnEdge(targetIcon, target.markerPosition, p, 1f);
		if (waypointIcon != null)
		{
			waypointIcon.gameObject.SetActive(BigMap.HasWaypoint && game.IsPlaying);
			if (BigMap.HasWaypoint)
				PutOnEdge(waypointIcon, BigMap.Waypoint, p, 1.5f);
		}
	}

	// When it is far away, the icon stays on the edge of the round map
	void PutOnEdge(Transform icon, Vector3 world, Vector3 p, float up)
	{
		Vector3 offset = world - p;
		offset.y = 0;
		float maxDistance = mapCam.orthographicSize * 0.82f;
		if (offset.magnitude > maxDistance)
			offset = offset.normalized * maxDistance;
		icon.position = new Vector3(p.x + offset.x, iconHeight + up, p.z + offset.z);
	}
}
