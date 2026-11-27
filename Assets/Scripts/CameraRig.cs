using UnityEngine;

// Third-person camera. The mouse turns it. In a car it goes back behind the car by itself.
// It moves closer when a wall is in the way.
public class CameraRig : MonoBehaviour
{
	public Transform target;
	public float footDistance = 5.5f;
	public float footHeight = 1.8f;
	public float carDistance = 8.5f;
	public float carHeight = 2.6f;
	public float mouseSensitivity = 3f;
	public bool titleMode;

	public float Yaw { get { return yaw; } }

	float yaw;
	float pitch = 12f;
	bool followCar;
	float lastMouse = -10f;
	const int Mask = ~((1 << 2) | (1 << 31));   // ignore the player, cars and map icons

	float customDistance, customHeight;

	public void SetTarget(Transform t, bool car)
	{
		SetTarget(t, car, 0f, 0f);
	}

	// A plane needs the camera further back: give a distance and height
	public void SetTarget(Transform t, bool car, float distance, float height)
	{
		target = t;
		followCar = car;
		customDistance = distance;
		customHeight = height;
		if (car)
			yaw = t.eulerAngles.y;
	}

	public void SetYaw(float value) { yaw = value; }
	public void SetPitch(float value) { pitch = value; }

	void LateUpdate()
	{
		if (target == null)
			return;
		float dt = Time.deltaTime;

		// Title screen: slowly fly around the city
		if (titleMode)
		{
			yaw += dt * 5f;
			Vector3 center = new Vector3(230, 10, -20);
			transform.position = center + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 190, -420);
			transform.LookAt(center);
			return;
		}

		float mx = 0, my = 0;
		if (Cursor.lockState == CursorLockMode.Locked)
		{
			mx = Input.GetAxis("Mouse X");
			my = Input.GetAxis("Mouse Y");
		}
		if (Mathf.Abs(mx) + Mathf.Abs(my) > 0.01f)
			lastMouse = Time.time;
		yaw += mx * mouseSensitivity;
		pitch = Mathf.Clamp(pitch - my * mouseSensitivity, -5f, 55f);

		if (followCar && Time.time - lastMouse > 1.2f)
		{
			yaw = Mathf.LerpAngle(yaw, target.eulerAngles.y, dt * 4f);
			pitch = Mathf.Lerp(pitch, 10f, dt * 2f);
		}

		float distance = customDistance > 0 ? customDistance : followCar ? carDistance : footDistance;
		float height = customHeight > 0 ? customHeight : followCar ? carHeight : footHeight;
		Vector3 pivot = target.position + Vector3.up * height;
		Vector3 back = Quaternion.Euler(pitch, yaw, 0) * Vector3.back;
		Vector3 want = pivot + back * distance;

		RaycastHit hit;
		if (Physics.SphereCast(pivot, 0.3f, back, out hit, distance, Mask, QueryTriggerInteraction.Ignore))
			want = pivot + back * Mathf.Max(0.5f, hit.distance - 0.1f);

		transform.position = want;
		transform.rotation = Quaternion.LookRotation(pivot - want);
	}
}
