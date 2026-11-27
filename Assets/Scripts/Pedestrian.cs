using System.Collections.Generic;
using UnityEngine;

// A person walking around a block on the sidewalk. At a corner they sometimes cross the street.
// They jump out of the way of fast cars. If a car hits them, they fall down and get back up.
public class Pedestrian : MonoBehaviour
{
	public static readonly List<Pedestrian> All = new List<Pedestrian>();

	public float walkSpeed = 1.4f;
	public float sidewalkOffset = 21.4f;   // the sidewalk ring, measured from the block center
	[Range(0, 1)] public float crossChance = 0.3f;
	public bool carriesBasin;

	[Header("Body parts that swing when walking")]
	public Transform leftLeg;
	public Transform rightLeg;
	public Transform leftArm;
	public Transform rightArm;

	// Test hooks for the play-mode checks
	[HideInInspector] public bool testHold;     // stand still (still dodges)
	[HideInInspector] public bool testNoDodge;  // don't jump away

	public bool IsDown { get { return downTimer > 0; } }
	public bool Talking { get { return talkTimer > 0; } }
	public bool Crossing { get; private set; }
	public int Dodges { get; private set; }
	public int Crossings { get; private set; }
	public int Knockdowns { get; private set; }

	PedestrianManager manager;
	Vector3 block;
	int corner;
	int turnDir = 1;
	Vector3 target;
	float downTimer, dodgeTimer, cycle, talkTimer, greetCooldown;
	static float nextGreetTime;
	Vector3 dodgeVelocity;
	float yaw;

	void OnEnable() { All.Add(this); }
	void OnDisable() { All.Remove(this); }

	// Start walking along a block edge: "along" meters after corner "startCorner"
	public void Init(PedestrianManager m, Vector3 blockCenter, int startCorner, int direction, float along)
	{
		manager = m;
		block = blockCenter;
		turnDir = direction;
		corner = (startCorner + turnDir + 4) % 4;
		Vector3 from = CornerPos(block, startCorner);
		target = CornerPos(block, corner);
		Vector3 p = Vector3.MoveTowards(from, target, along);
		transform.position = new Vector3(p.x, 0.06f, p.z);
		Face(target - p);
	}

	// A driver who got out of a car: walk to the nearest sidewalk corner
	public void Leave(PedestrianManager m, Vector3 from)
	{
		manager = m;
		block = m.NearestBlockCenter(from);
		turnDir = Random.value < 0.5f ? 1 : -1;
		float best = float.MaxValue;
		for (int k = 0; k < 4; k++)
		{
			float d = Vector3.Distance(CornerPos(block, k), from);
			if (d < best)
			{
				best = d;
				corner = k;
			}
		}
		target = CornerPos(block, corner);
		Crossing = true;
		transform.position = new Vector3(from.x, 0.06f, from.z);
		Face(target - from);
	}

	Vector3 CornerPos(Vector3 c, int k)
	{
		float sx = (k == 1 || k == 2) ? 1f : -1f;
		float sz = k >= 2 ? 1f : -1f;
		return c + new Vector3(sx * sidewalkOffset, 0, sz * sidewalkOffset);
	}

	void Update()
	{
		if (manager == null)
			return;
		float dt = Time.deltaTime;

		// Lying on the ground after a car hit them
		if (downTimer > 0)
		{
			downTimer -= dt;
			transform.rotation = Quaternion.Euler(downTimer > 0 ? -82f : 0f, yaw, 0);
			Swing(0);
			return;
		}

		// Jumping out of the way
		if (dodgeTimer > 0)
		{
			dodgeTimer -= dt;
			transform.position += dodgeVelocity * dt;
			Swing(6f);
			return;
		}

		if (!testNoDodge && WatchForCars())
			return;

		// Talking with you: stand and look at you
		if (talkTimer > 0)
		{
			talkTimer -= dt;
			TosakanaGame g = TosakanaGame.Instance;
			if (g != null)
				Face(g.player.transform.position - transform.position);
			Swing(0);
			return;
		}

		GreetYou(dt);

		if (testHold)
		{
			Swing(0);
			return;
		}

		Vector3 to = target - transform.position;
		to.y = 0;
		if (to.magnitude < 0.3f)
			ArriveAtCorner();
		else
		{
			Vector3 step = Vector3.ClampMagnitude(to, walkSpeed * dt);
			transform.position += step;
			Face(to);
		}
		Swing(walkSpeed);
	}

	// Sometimes people say hello when you walk past
	void GreetYou(float dt)
	{
		greetCooldown -= dt;
		TosakanaGame game = TosakanaGame.Instance;
		if (game == null || !game.IsPlaying || game.InCar || greetCooldown > 0 || Time.time < nextGreetTime)
			return;
		Vector3 d = game.player.transform.position - transform.position;
		d.y = 0;
		if (d.magnitude < 6f)
		{
			greetCooldown = 25f;
			nextGreetTime = Time.time + 4f;
			if (Random.value < 0.6f)
				SpeechBubble.Say(gameObject, Lingala.Pick(carriesBasin ? Lingala.SellerSays : Lingala.Greetings));
		}
	}

	// You pressed T: stop, look at you and answer
	public void Answer(int lineIndex)
	{
		talkTimer = 3.5f;
		Lingala.Line[] answers = carriesBasin && Random.value < 0.5f ? Lingala.SellerSays : Lingala.Answers[lineIndex % Lingala.Answers.Length];
		SpeechBubble.Say(gameObject, Lingala.Pick(answers), 3.2f);
	}

	void ArriveAtCorner()
	{
		if (Crossing)
			Crossing = false;
		else if (Random.value < crossChance && TryCross())
			return;
		corner = (corner + turnDir + 4) % 4;
		target = CornerPos(block, corner);
	}

	// Cross the street to the block next door (if there is one)
	bool TryCross()
	{
		int i, j;
		if (!manager.BlockIndex(block, out i, out j))
			return false;
		int sx = (corner == 1 || corner == 2) ? 1 : -1;
		int sz = corner >= 2 ? 1 : -1;
		bool canX = manager.ValidBlock(i + sx, j);
		bool canZ = manager.ValidBlock(i, j + sz);
		if (!canX && !canZ)
			return false;
		bool goX = canX && (!canZ || Random.value < 0.5f);
		Vector3 newBlock = goX ? manager.BlockCenter(i + sx, j) : manager.BlockCenter(i, j + sz);
		int nx = goX ? -sx : sx;
		int nz = goX ? sz : -sz;
		corner = nz < 0 ? (nx < 0 ? 0 : 1) : (nx > 0 ? 2 : 3);
		block = newBlock;
		target = CornerPos(block, corner);
		Crossing = true;
		Crossings++;
		return true;
	}

	// A fast car coming straight at me? Jump to the side.
	bool WatchForCars()
	{
		Vector3 pos = transform.position;
		foreach (CarController car in CarController.All)
		{
			float s = car.Speed;
			if (Mathf.Abs(s) < 4f)
				continue;
			Transform t = car.transform;
			Vector3 rel = pos - t.position;
			rel.y = 0;
			float ahead = Vector3.Dot(rel, t.forward * Mathf.Sign(s));
			if (ahead < 0 || ahead > 4f + Mathf.Abs(s) * 0.5f)
				continue;
			float side = Vector3.Dot(rel, t.right);
			if (Mathf.Abs(side) > 2.2f)
				continue;
			float away = side >= 0 ? 1f : -1f;
			dodgeVelocity = t.right * away * 6.5f;
			dodgeTimer = 0.5f;
			Dodges++;
			SpeechBubble.Say(gameObject, Lingala.Pick(Lingala.WatchOut), 2f);
			Face(dodgeVelocity);
			return true;
		}
		return false;
	}

	void OnTriggerEnter(Collider other)
	{
		CarController car = other.GetComponentInParent<CarController>();
		if (car != null && Mathf.Abs(car.Speed) > 3f && !IsDown)
			KnockDown(car);
	}

	public void KnockDown(CarController car)
	{
		downTimer = 3f;
		dodgeTimer = 0;
		talkTimer = 0;
		Knockdowns++;
		SpeechBubble.Say(gameObject, Lingala.Pick(Lingala.Hit), 3f, 1.4f);
		transform.position += car.transform.forward * Mathf.Sign(car.Speed) * 1.5f;
		yaw = car.transform.eulerAngles.y + (car.Speed > 0 ? 180f : 0f);
		TosakanaGame game = TosakanaGame.Instance;
		if (game != null)
		{
			game.sound.BumpAt(transform.position);
			if (car == game.CurrentCar && PoliceManager.Instance != null)
				PoliceManager.Instance.ReportCrime(1, "You hit a person!");
		}
	}

	void Face(Vector3 direction)
	{
		direction.y = 0;
		if (direction.sqrMagnitude < 0.0001f)
			return;
		yaw = Quaternion.LookRotation(direction).eulerAngles.y;
		transform.rotation = Quaternion.Euler(0, yaw, 0);
	}

	void Swing(float speed)
	{
		cycle += Time.deltaTime * speed * 4f;
		float swing = Mathf.Sin(cycle) * Mathf.Clamp01(speed) * 28f;
		if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(swing, 0, 0);
		if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
		if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-swing * 0.8f, 0, 0);
		// Someone carrying a basin holds it on their head with the right arm
		if (rightArm != null) rightArm.localRotation = carriesBasin ? Quaternion.Euler(180f, 0, -12f) : Quaternion.Euler(swing * 0.8f, 0, 0);
	}
}
