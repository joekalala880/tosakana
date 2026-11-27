using UnityEngine;

// The big yellow arrow over your head (or your car, or your plane) that points to where your mission is.
public class TargetArrow : MonoBehaviour
{
	public Transform arrow;
	public float heightOnFoot = 3.2f;
	public float heightInCar = 4.5f;
	public float heightInPlane = 14f;

	[Header("On the screen")]
	public RectTransform hudArrow;   // turns to show which way to go
	public UnityEngine.UI.Text hudText;  // "Gombe · 320 m"

	public Vector3 PointsTo { get; private set; }
	public float ScreenAngle { get; private set; }

	void LateUpdate()
	{
		TosakanaGame game = TosakanaGame.Instance;
		if (game == null || arrow == null)
			return;
		Place target = game.IsPlaying ? game.missions.Target : null;
		arrow.gameObject.SetActive(target != null && !game.cameraRig.insideView);
		if (hudArrow != null)
			hudArrow.gameObject.SetActive(target != null);
		if (hudText != null)
			hudText.text = target != null ? target.placeName : "";
		if (target == null)
			return;

		Transform focus = game.Focus;
		float h = game.InPlane ? heightInPlane : game.InCar ? heightInCar : heightOnFoot;
		arrow.position = focus.position + Vector3.up * (h + 0.25f * Mathf.Sin(Time.time * 3f));
		Vector3 to = target.markerPosition - focus.position;
		to.y = 0;
		if (to.sqrMagnitude > 0.01f)
		{
			arrow.rotation = Quaternion.LookRotation(to);
			PointsTo = to.normalized;
		}
		// On the screen: 0° = straight ahead of the camera
		Transform cam = game.cameraRig.transform;
		Vector3 camFlat = new Vector3(cam.forward.x, 0, cam.forward.z);
		if (camFlat.sqrMagnitude > 0.001f && to.sqrMagnitude > 0.01f)
		{
			ScreenAngle = Vector3.SignedAngle(camFlat, to, Vector3.up);
			if (hudArrow != null)
				hudArrow.localEulerAngles = new Vector3(0, 0, -ScreenAngle);
		}
		if (hudText != null)
			hudText.text = target.placeName + "  ·  " + Mathf.RoundToInt(to.magnitude) + " m";

		// Grow a little when you are far away, so you always see it
		float s = Mathf.Lerp(1f, 1.6f, Mathf.InverseLerp(20f, 300f, to.magnitude)) * (game.InPlane ? 3f : 1f);
		arrow.localScale = Vector3.one * s;
	}
}
