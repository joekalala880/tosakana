using UnityEngine;

// The "brain" of a traffic car. It follows its lane, picks a new street at each crossroad,
// slows down for turns, stops for cars and people in front, and honks when you block it.
// Police cars use the same brain. When chasing you they pick the streets that get closer to you,
// drive fast, push through traffic, and drive straight at you when they are near.
// When you take the car (E), the brain turns off and the car is yours.
[RequireComponent(typeof(CarController))]
public class TrafficDriver : MonoBehaviour
{
	public bool police;
	public bool Chasing { get; private set; }

	public int TurnsMade { get; private set; }
	public float LaneError { get; private set; }   // meters away from its lane
	public bool Blocked { get; private set; }      // something is in front
	public bool WaitingForRoulage { get; private set; }   // a roulage officer told it to stop

	TrafficManager manager;
	CarController car;
	PoliceLights lights;
	int fromA, fromB, toA, toB, heading;
	int nextHeading = -1;
	bool direct;
	float blockedTime, ignoreCarsTimer, stuckTime, honkCooldown, shoutCooldown;
	const float TurnSpeed = 6.5f;
	const float ChaseSpeed = 24f;
	const float ChaseTurnSpeed = 9f;
	const float DirectChaseDistance = 30f;

	public CarController Car { get { return car; } }

	public void Init(TrafficManager m, int a, int b, int h)
	{
		manager = m;
		car = GetComponent<CarController>();
		lights = GetComponent<PoliceLights>();
		fromA = a;
		fromB = b;
		heading = h;
		Vector2Int d = TrafficManager.Dir(h);
		toA = a + d.x;
		toB = b + d.y;
		nextHeading = -1;
		stuckTime = 0;
		blockedTime = 0;
	}

	public void PlaceAt(int a, int b, int h, float t)
	{
		Init(manager, a, b, h);
		direct = false;
		car.Teleport(manager.LanePoint(a, b, h, t), h * 90f);
	}

	// Police: lights and siren on, start the chase (or stop it)
	public void SetChasing(bool on)
	{
		if (Chasing == on)
			return;
		Chasing = on;
		if (lights != null)
			lights.Set(on);
		if (!on && direct)
			Relocate();
		direct = false;
		nextHeading = -1;
	}

	void Update()
	{
		if (manager == null)
			return;

		// You took this car: stop driving it
		if (car.HasDriver)
		{
			car.aiThrottle = 0;
			car.aiSteer = 0;
			car.aiBrake = false;
			if (lights != null)
				lights.Set(false);
			Chasing = false;
			enabled = false;
			return;
		}

		float dt = Time.deltaTime;
		Vector3 pos = transform.position;
		float speed = Mathf.Abs(car.Speed);

		// Police close to you: drive straight at you
		TosakanaGame game = TosakanaGame.Instance;
		if (Chasing && game != null)
		{
			Vector3 target = game.Focus.position;
			Vector3 gap = target - pos;
			gap.y = 0;
			if (gap.magnitude < DirectChaseDistance)
			{
				shoutCooldown -= Time.deltaTime;
				if (shoutCooldown <= 0)
				{
					shoutCooldown = 5f;
					Voice.Say(gameObject, Lingala.Pick(Lingala.Police));
				}
				direct = true;
				DriveStraightAt(target, gap.magnitude, speed);
				return;
			}
			if (direct)
			{
				direct = false;
				Relocate();
			}
		}

		Vector3 dir, right, start;
		float along;
		Measure(pos, out dir, out right, out start, out along);
		float remaining = manager.gridStep - along;

		// Pick the next street before the crossroad
		if (nextHeading < 0 && remaining < 30f)
			nextHeading = ChooseNext();
		int turn = nextHeading < 0 ? 0 : (nextHeading - heading + 4) % 4;   // 0 straight, 1 right, 3 left
		float switchAt = turn == 1 ? 7.5f : turn == 3 ? 2.5f : 0f;
		if (nextHeading >= 0 && remaining < switchAt)
		{
			if (turn != 0)
				TurnsMade++;
			fromA = toA;
			fromB = toB;
			heading = nextHeading;
			Vector2Int d = TrafficManager.Dir(heading);
			toA = fromA + d.x;
			toB = fromB + d.y;
			nextHeading = -1;
			turn = 0;
			Measure(pos, out dir, out right, out start, out along);
			remaining = manager.gridStep - along;
		}
		LaneError = Mathf.Abs(Vector3.Dot(pos - start, right));

		// Steer toward a point a little ahead in the lane
		Vector3 aim = start + dir * (along + 5f + speed * 0.4f);
		Vector3 local = transform.InverseTransformPoint(aim);
		car.aiSteer = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg / 25f, -1f, 1f);

		// How fast to go
		float want = Chasing ? ChaseSpeed : manager.cruiseSpeed;
		if (turn != 0 && remaining < (Chasing ? 30f : 22f))
			want = Chasing ? ChaseTurnSpeed : TurnSpeed;
		if (Mathf.Abs(car.aiSteer) > 0.6f)
			want = Mathf.Min(want, Chasing ? 10f : 7f);

		// A roulage officer at the next crossroads says stop: wait before the crossroads (the police in a chase don't wait)
		WaitingForRoulage = false;
		if (!Chasing && remaining > 6f)
		{
			TrafficOfficer officer = TrafficOfficer.At(toA, toB);
			if (officer != null && !officer.CanGo(heading))
			{
				WaitingForRoulage = true;
				want = Mathf.Min(want, Mathf.Max(0f, (remaining - 11f) * 1.2f));
			}
		}

		// Look ahead for cars and people (the police in a chase push through)
		Blocked = false;
		if (!Chasing)
		{
			RaycastHit hit;
			Vector3 origin = pos + Vector3.up + transform.forward * 2.4f;
			float range = 6f + speed * 0.9f;
			if (Physics.SphereCast(origin, 0.8f, transform.forward, out hit, range, 1 << 2, QueryTriggerInteraction.Collide))
			{
				bool isCar = hit.collider.GetComponentInParent<CarController>() != null;
				bool isPerson = hit.collider.GetComponentInParent<PlayerController>() != null || hit.collider.GetComponentInParent<Pedestrian>() != null;
				if (isPerson || (isCar && ignoreCarsTimer <= 0))
				{
					Blocked = true;
					want = Mathf.Min(want, Mathf.Max(0f, (hit.distance - 1.5f) * 1.5f));
				}
			}
		}

		// Waiting: honk after 2 s, and after 5 s squeeze past other cars (no traffic jams forever)
		honkCooldown -= dt;
		ignoreCarsTimer -= dt;
		if (Blocked && speed < 0.5f)
		{
			blockedTime += dt;
			if (blockedTime > 2f && honkCooldown <= 0)
			{
				honkCooldown = 6f;
				if (game != null)
					game.sound.HornAt(pos);
				Voice.Say(gameObject, Lingala.Pick(Lingala.Honk));
			}
			if (blockedTime > 5f)
			{
				ignoreCarsTimer = 2f;
				blockedTime = 0;
			}
		}
		else
			blockedTime = Mathf.Max(0, blockedTime - dt);

		car.aiThrottle = want / car.maxSpeed;
		car.aiBrake = want < 0.5f && speed > 0.3f;

		// Lost (pushed far off the street) or stuck: move it somewhere you can't see
		if (LaneError > 6f || (speed < 0.3f && !Blocked && !WaitingForRoulage))
			stuckTime += dt;
		else
			stuckTime = 0;
		if (stuckTime > 6f && FarFromYou(60f))
			manager.Respawn(this);
	}

	void DriveStraightAt(Vector3 target, float distance, float speed)
	{
		Vector3 local = transform.InverseTransformPoint(target);
		float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
		car.aiSteer = Mathf.Clamp(angle / 20f, -1f, 1f);
		float want = distance < 5f ? 0f : Mathf.Clamp(distance, 6f, ChaseSpeed);
		if (Mathf.Abs(angle) > 100f)
			want = Mathf.Min(want, 7f);   // you are behind: turn around slowly
		car.aiThrottle = want / car.maxSpeed;
		car.aiBrake = distance < 5f && speed > 1f;
		LaneError = 0;
		Blocked = false;
	}

	// After driving straight at you, find the street we are on again
	void Relocate()
	{
		int h = ((Mathf.RoundToInt(transform.eulerAngles.y / 90f) % 4) + 4) % 4;
		Vector3 p = transform.position;
		float gx = (p.x - manager.gridStart) / manager.gridStep;
		float gz = (p.z - manager.gridStart) / manager.gridStep;
		int a, b;
		if (h == 0 || h == 2)
		{
			a = Mathf.Clamp(Mathf.RoundToInt(gx), 0, manager.gridSizeX - 1);
			b = h == 0 ? Mathf.Clamp(Mathf.FloorToInt(gz), 0, manager.gridSizeZ - 2) : Mathf.Clamp(Mathf.CeilToInt(gz), 1, manager.gridSizeZ - 1);
		}
		else
		{
			b = Mathf.Clamp(Mathf.RoundToInt(gz), 0, manager.gridSizeZ - 1);
			a = h == 1 ? Mathf.Clamp(Mathf.FloorToInt(gx), 0, manager.gridSizeX - 2) : Mathf.Clamp(Mathf.CeilToInt(gx), 1, manager.gridSizeX - 1);
		}
		Init(manager, a, b, h);
	}

	void Measure(Vector3 pos, out Vector3 dir, out Vector3 right, out Vector3 start, out float along)
	{
		dir = TrafficManager.Dir3(heading);
		right = TrafficManager.Right(heading);
		start = manager.Node(fromA, fromB) + right * manager.laneOffset;
		along = Vector3.Dot(pos - start, dir);
	}

	int ChooseNext()
	{
		// Police in a chase: take the street that gets closest to you
		TosakanaGame game = TosakanaGame.Instance;
		if (Chasing && game != null)
		{
			int best = -1;
			float bestDistance = float.MaxValue;
			foreach (int h in new[] { heading, (heading + 1) % 4, (heading + 3) % 4 })
			{
				Vector2Int d = TrafficManager.Dir(h);
				if (!manager.Valid(toA + d.x, toB + d.y))
					continue;
				float distance = Vector3.Distance(manager.Node(toA + d.x, toB + d.y), game.Focus.position);
				if (distance < bestDistance)
				{
					bestDistance = distance;
					best = h;
				}
			}
			if (best >= 0)
				return best;
		}

		// Normal traffic: straight is more likely than turning. No U-turns unless there is no other way.
		int[] options = { heading, heading, (heading + 1) % 4, (heading + 3) % 4 };
		for (int tries = 0; tries < 12; tries++)
		{
			int h = options[Random.Range(0, options.Length)];
			Vector2Int d = TrafficManager.Dir(h);
			if (manager.Valid(toA + d.x, toB + d.y))
				return h;
		}
		foreach (int h in new[] { heading, (heading + 1) % 4, (heading + 3) % 4, (heading + 2) % 4 })
		{
			Vector2Int d = TrafficManager.Dir(h);
			if (manager.Valid(toA + d.x, toB + d.y))
				return h;
		}
		return (heading + 2) % 4;
	}

	bool FarFromYou(float distance)
	{
		TosakanaGame game = TosakanaGame.Instance;
		return game == null || Vector3.Distance(game.Focus.position, transform.position) > distance;
	}
}
