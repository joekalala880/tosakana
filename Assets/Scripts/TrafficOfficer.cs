using System.Collections.Generic;
using UnityEngine;

// A roulage officer (traffic police) standing in the middle of a crossroads.
// He lets one way go (north-south or east-west) while the other way waits, then whistles and switches.
// He faces the cars that must stop, with one hand up, and waves the other cars on.
public class TrafficOfficer : MonoBehaviour
{
	public static readonly List<TrafficOfficer> All = new List<TrafficOfficer>();

	public int nodeA, nodeB;            // which crossroads (same numbers as TrafficManager)
	public float phaseSeconds = 9f;
	public Transform leftArm;
	public Transform rightArm;

	// 0 = north/south may go, 1 = east/west may go
	public int Phase { get; private set; }
	public int Switches { get; private set; }
	public bool IsDown { get { return downTimer > 0; } }

	[HideInInspector] public bool testFreeze;   // used by the checks

	float timer, downTimer, shoutCooldown, wave;
	float yaw;

	void OnEnable() { All.Add(this); }
	void OnDisable() { All.Remove(this); }

	void Start()
	{
		Phase = Random.Range(0, 2);
		timer = Random.Range(2f, phaseSeconds);
		yaw = Phase == 0 ? 90f : 0f;
	}

	public static TrafficOfficer At(int a, int b)
	{
		foreach (TrafficOfficer o in All)
			if (o.nodeA == a && o.nodeB == b && !o.IsDown)
				return o;
		return null;
	}

	// Can a car going this way (0 N, 1 E, 2 S, 3 W) pass now?
	public bool CanGo(int heading) { return (heading % 2 == 0) == (Phase == 0); }

	public void SetPhase(int phase)
	{
		Phase = phase;
		timer = phaseSeconds;
		Switches++;
		if (TosakanaGame.Instance != null)
			TosakanaGame.Instance.sound.WhistleAt(transform.position);
	}

	void Update()
	{
		float dt = Time.deltaTime;
		if (downTimer > 0)
		{
			downTimer -= dt;
			transform.rotation = Quaternion.Euler(downTimer > 0 ? -82f : 0f, yaw, 0);
			return;
		}

		if (!testFreeze)
		{
			timer -= dt;
			if (timer <= 0)
				SetPhase(1 - Phase);
		}

		// Face the cars that must wait (turn smoothly)
		float want = Phase == 0 ? 90f : 0f;
		yaw = Mathf.MoveTowardsAngle(yaw, want, 180f * dt);
		transform.rotation = Quaternion.Euler(0, yaw, 0);

		// Right hand up (stop), left arm waves the others on
		wave += dt * 5f;
		if (rightArm != null) rightArm.localRotation = Quaternion.Euler(-95f, 0, 0);
		if (leftArm != null) leftArm.localRotation = Quaternion.Euler(0, 0, -70f - 25f * Mathf.Sin(wave));

		// Shout at you when you come close
		shoutCooldown -= dt;
		TosakanaGame game = TosakanaGame.Instance;
		if (game != null && game.IsPlaying && shoutCooldown <= 0)
		{
			Vector3 d = game.Focus.position - transform.position;
			d.y = 0;
			if (d.magnitude < 14f)
			{
				shoutCooldown = 8f;
				Voice.Say(gameObject, Lingala.Pick(Lingala.Roulage));
			}
		}
	}

	public void Shot()
	{
		if (IsDown)
			return;
		downTimer = 25f;
	}

	public void TakePunch(PlayerCombat by)
	{
		if (IsDown)
			return;
		downTimer = 4f;
		Voice.Say(gameObject, Lingala.Pick(Lingala.Hit));
		if (PoliceManager.Instance != null)
			PoliceManager.Instance.ReportCrime(2, "You hit a roulage officer!");
	}

	void OnTriggerEnter(Collider other)
	{
		// Only a car that someone (you) is driving can knock him down; city traffic drives around him
		CarController car = other.GetComponentInParent<CarController>();
		if (car == null || !car.HasDriver || Mathf.Abs(car.Speed) < 3f || IsDown)
			return;
		downTimer = 4f;
		Voice.Say(gameObject, Lingala.Pick(Lingala.Hit));
		TosakanaGame game = TosakanaGame.Instance;
		if (game != null)
		{
			game.sound.BumpAt(transform.position);
			if (car == game.CurrentCar && PoliceManager.Instance != null)
				PoliceManager.Instance.ReportCrime(2, "You hit a roulage officer!");
		}
	}
}
