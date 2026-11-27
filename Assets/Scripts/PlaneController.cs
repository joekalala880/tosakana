using System.Collections.Generic;
using UnityEngine;

// A plane you can fly. Press E next to it to get in.
// W / S = faster / slower, A / D = turn, Up arrow or Space = climb, Down arrow or C = dive.
// You need speed to take off. Hit a building, or the ground too hard, and the plane crashes.
public class PlaneController : MonoBehaviour
{
	public static readonly List<PlaneController> All = new List<PlaneController>();

	public float maxSpeed = 90f;          // m/s (about 320 km/h)
	public float takeoffSpeed = 32f;
	public float acceleration = 10f;
	public float turnRate = 38f;          // degrees per second
	public float worldRadius = 1500f;     // fly too far and the plane turns back by itself

	// Test hooks for the play-mode checks (-1 .. 1)
	[HideInInspector] public float testThrottle, testTurn, testPitch;

	public bool HasPilot { get; private set; }
	public float Speed { get; private set; }
	public int Crashes { get; private set; }
	public float Altitude { get { return transform.position.y - groundY; } }
	public bool OnGround { get { return Altitude < 0.3f; } }
	public Vector3 Home { get { return homePos; } }

	Vector3 homePos;
	Quaternion homeRot;
	float groundY, pitch, yaw, roll;
	AudioSource engine;
	static readonly Vector3 WorldCenter = new Vector3(400f, 0, -20f);

	void Awake()
	{
		homePos = transform.position;
		homeRot = transform.rotation;
		groundY = homePos.y;
		yaw = homeRot.eulerAngles.y;
		engine = gameObject.AddComponent<AudioSource>();
		engine.loop = true;
		engine.spatialBlend = 1f;
		engine.rolloffMode = AudioRolloffMode.Linear;
		engine.minDistance = 20f;
		engine.maxDistance = 300f;
		engine.volume = 0.6f;
	}

	void OnEnable() { All.Add(this); }
	void OnDisable() { All.Remove(this); }

	public void SetPilot(bool on)
	{
		HasPilot = on;
		if (engine.clip == null && TosakanaGame.Instance != null)
			engine.clip = TosakanaGame.Instance.sound.JetClip;
		if (on && engine.clip != null && !engine.isPlaying)
			engine.Play();
		if (!on)
			engine.Stop();
	}

	// Put the plane somewhere (used after a crash, and by the checks)
	public void Place(Vector3 position, float heading, float speed = 0f)
	{
		yaw = heading;
		pitch = 0;
		roll = 0;
		Speed = speed;
		transform.SetPositionAndRotation(position, Quaternion.Euler(0, heading, 0));
	}

	public void GoHome() { Place(homePos, homeRot.eulerAngles.y); }

	void Update()
	{
		TosakanaGame game = TosakanaGame.Instance;
		float dt = Time.deltaTime;

		float throttle = testThrottle, turn = testTurn, climb = testPitch;
		if (HasPilot && game != null && game.IsPlaying)
		{
			if (Input.GetKey(KeyCode.W)) throttle += 1;
			if (Input.GetKey(KeyCode.S)) throttle -= 1;
			if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) turn -= 1;
			if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) turn += 1;
			if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.Space)) climb += 1;
			if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl)) climb -= 1;
		}
		throttle = Mathf.Clamp(throttle, -1f, 1f);
		turn = Mathf.Clamp(turn, -1f, 1f);
		climb = Mathf.Clamp(climb, -1f, 1f);

		if (!HasPilot && throttle == 0 && turn == 0 && climb == 0 && OnGround && Speed == 0)
			return;   // parked

		// Speed
		if (throttle > 0)
			Speed = Mathf.MoveTowards(Speed, maxSpeed, acceleration * throttle * dt);
		else if (throttle < 0)
			Speed = Mathf.MoveTowards(Speed, 0, 18f * -throttle * dt);
		else if (OnGround)
			Speed = Mathf.MoveTowards(Speed, 0, 4f * dt);

		// Turning (slowly on the ground, like taxiing)
		float turnFactor = OnGround ? Mathf.Clamp01(Speed / 8f) * 0.8f : 1f;
		yaw += turn * turnRate * turnFactor * dt;

		// Fly too far: the plane turns back toward the city
		Vector3 flat = new Vector3(transform.position.x, 0, transform.position.z);
		if (!OnGround && Vector3.Distance(flat, WorldCenter) > worldRadius)
		{
			float back = Quaternion.LookRotation(WorldCenter - flat).eulerAngles.y;
			yaw = Mathf.MoveTowardsAngle(yaw, back, 45f * dt);
		}

		// Nose up / down. Too slow in the air = the nose drops.
		float want = 0;
		if (climb > 0 && Speed >= takeoffSpeed) want = 14f;
		else if (climb < 0 && !OnGround) want = -14f;
		if (!OnGround && Speed < 25f) want = -10f;
		// Close to the ground: the plane levels out by itself so you can land softly
		bool landing = !OnGround && Altitude < 14f && climb <= 0;
		if (landing) want = Mathf.Max(want, -4f);
		pitch = Mathf.MoveTowards(pitch, want, 25f * dt);
		if (landing) pitch = Mathf.Max(pitch, -4f);
		roll = Mathf.Lerp(roll, OnGround ? 0 : -turn * 30f, dt * 3f);

		Quaternion rotation = Quaternion.Euler(-pitch, yaw, roll);
		Vector3 move = Quaternion.Euler(-pitch, yaw, 0) * Vector3.forward * Speed * dt;
		if (!OnGround && Speed < 25f)
			move += Vector3.down * Mathf.Min(6f, (25f - Speed) * 0.4f) * dt;

		// Something in front? (buildings, walls, the control tower)
		if (Speed > 1f && move.sqrMagnitude > 0.0001f)
		{
			Vector3 nose = transform.position + rotation * Vector3.forward * 19f + Vector3.up * 3f;
			RaycastHit hit;
			if (Physics.SphereCast(nose, 2f, move.normalized, out hit, move.magnitude + 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
			{
				if (Speed > 15f)
				{
					Crash();
					return;
				}
				Speed = 0;
				move = Vector3.zero;
			}
		}

		// The ground: land gently or crash
		Vector3 next = transform.position + move;
		if (next.y < groundY)
		{
			float sink = (transform.position.y - next.y) / Mathf.Max(dt, 0.0001f);
			if (!OnGround && (sink > 12f || pitch < -12f))
			{
				Crash();
				return;
			}
			next.y = groundY;
			if (pitch < 0) pitch = 0;
			rotation = Quaternion.Euler(-pitch, yaw, 0);
		}

		transform.SetPositionAndRotation(next, rotation);
		if (engine.isPlaying)
			engine.pitch = 0.6f + Speed / maxSpeed;
	}

	void Crash()
	{
		Crashes++;
		TosakanaGame game = TosakanaGame.Instance;
		if (game != null)
		{
			game.sound.Crash();
			if (HasPilot)
				game.PlaneCrashed(this);
		}
		SetPilot(false);
		GoHome();
	}
}
