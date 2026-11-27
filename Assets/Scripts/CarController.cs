using System.Collections.Generic;
using UnityEngine;

// A simple arcade car: W/S = gas and reverse, A/D = steer, Space = brake.
// Only drives when the player is inside. Parked cars just sit there (you can push them).
[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
	public static readonly List<CarController> All = new List<CarController>();

	public float maxSpeed = 32f;        // m/s (about 115 km/h)
	public float acceleration = 14f;
	public float brakePower = 30f;
	public float reverseSpeed = 9f;
	public float turnRate = 95f;        // degrees per second
	public float rollDrag = 4f;         // slows down when you let go of the gas

	[Header("Wheels")]
	public Transform[] wheelSpins;      // all 4 wheels (they spin)
	public Transform[] frontWheels;     // front wheel pivots (they turn)

	// Test hooks for the play-mode checks
	[HideInInspector] public float testThrottle;
	[HideInInspector] public float testSteer;
	[HideInInspector] public bool testBrake;

	// Set by TrafficDriver when the city drives this car (ignored when you are driving it)
	[HideInInspector] public float aiThrottle;
	[HideInInspector] public float aiSteer;
	[HideInInspector] public bool aiBrake;

	public bool HasDriver { get; private set; }
	public float Speed { get; private set; }   // forward speed in m/s (minus = going backward)

	Rigidbody rb;
	float steerVisual;
	float spin;

	void Awake()
	{
		rb = GetComponent<Rigidbody>();
	}

	void OnEnable() { All.Add(this); }
	void OnDisable() { All.Remove(this); }

	public void SetDriver(bool on)
	{
		HasDriver = on;
	}

	public void Teleport(Vector3 position, float yaw)
	{
		Quaternion r = Quaternion.Euler(0, yaw, 0);
		rb.velocity = Vector3.zero;
		rb.angularVelocity = Vector3.zero;
		rb.position = position;
		rb.rotation = r;
		transform.SetPositionAndRotation(position, r);
		Speed = 0;
	}

	void FixedUpdate()
	{
		float throttle = testThrottle;
		float steer = testSteer;
		bool brake = testBrake;
		TosakanaGame game = TosakanaGame.Instance;
		if (!HasDriver)
		{
			throttle += aiThrottle;
			steer += aiSteer;
			brake |= aiBrake;
		}
		else if (game != null && game.IsPlaying)
		{
			throttle += Input.GetAxisRaw("Vertical");
			steer += Input.GetAxisRaw("Horizontal");
			brake |= Input.GetKey(KeyCode.Space);
		}
		throttle = Mathf.Clamp(throttle, -1f, 1f);
		steer = Mathf.Clamp(steer, -1f, 1f);

		float dt = Time.fixedDeltaTime;
		Vector3 v = rb.velocity;
		float forward = Vector3.Dot(v, transform.forward);

		if (brake)
			forward = Mathf.MoveTowards(forward, 0, brakePower * dt);
		else if (throttle > 0.05f)
			forward = forward < -0.5f ? Mathf.MoveTowards(forward, 0, brakePower * dt) : Mathf.MoveTowards(forward, maxSpeed * throttle, acceleration * dt);
		else if (throttle < -0.05f)
			forward = forward > 0.5f ? Mathf.MoveTowards(forward, 0, brakePower * dt) : Mathf.MoveTowards(forward, -reverseSpeed, acceleration * 0.6f * dt);
		else
			forward = Mathf.MoveTowards(forward, 0, rollDrag * dt);

		// Steering works better when moving, and a little less at top speed
		float grip = Mathf.Clamp(forward / 6f, -1f, 1f) * (1f - 0.35f * Mathf.Abs(forward) / maxSpeed);
		float yaw = rb.rotation.eulerAngles.y + steer * turnRate * grip * dt;
		Quaternion rotation = Quaternion.Euler(0, yaw, 0);
		rb.MoveRotation(rotation);
		rb.angularVelocity = Vector3.zero;
		rb.velocity = rotation * Vector3.forward * forward + Vector3.up * v.y;

		Speed = forward;
		steerVisual = Mathf.Lerp(steerVisual, steer * 30f, dt * 10f);
	}

	void Update()
	{
		spin += Speed * Time.deltaTime / 0.38f * Mathf.Rad2Deg;
		if (wheelSpins != null)
			foreach (Transform w in wheelSpins)
				if (w != null) w.localRotation = Quaternion.Euler(spin, 0, 0);
		if (frontWheels != null)
			foreach (Transform w in frontWheels)
				if (w != null) w.localRotation = Quaternion.Euler(0, steerVisual, 0);
	}

	void OnCollisionEnter(Collision c)
	{
		TosakanaGame game = TosakanaGame.Instance;
		if (HasDriver && game != null && c.relativeVelocity.magnitude > 6f)
			game.sound.Crash();

		// Ramming a police car when you are not wanted yet
		PoliceManager police = PoliceManager.Instance;
		if (HasDriver && police != null && police.Stars == 0 && c.relativeVelocity.magnitude > 5f)
		{
			TrafficDriver other = c.collider.GetComponentInParent<TrafficDriver>();
			if (other != null && other.police)
				police.ReportCrime(1, "You hit a police car!");
		}
	}
}
