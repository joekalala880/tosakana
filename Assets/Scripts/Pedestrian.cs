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

	[Header("Version 12: fighting")]
	public Role role = Role.Normal;
	public string displayName = "Moto ya Kin";
	public int maxHealth = 60;
	public int attackDamage = 8;
	[Range(0, 1)] public float blockChance = 0f;
	public float attackRate = 1f;   // bigger = attacks more often
	public enum Role { Normal, Kuluna, Fighter }
	public int Health { get; private set; }
	public bool Defeated { get; private set; }          // health ran out (kuluna and ring fighters stay down)
	public Pedestrian FoePerson { get; private set; }  // who they fight (null = you)
	public bool FightingYou { get { return mood == Mood.Fight && FoePerson == null; } }
	public int HitsTaken { get; private set; }
	public int Blocks { get; private set; }
	public System.Action<Pedestrian> onDefeated;
	[HideInInspector] public bool cheering;            // watching a fight: arms up
	[HideInInspector] public Vector3 cheerAt;
	[HideInInspector] public bool hangOut;              // kuluna standing at their corner
	[HideInInspector] public Vector3 home;
	[HideInInspector] public Vector3 hangLookAt;

	// Test hooks for the play-mode checks
	[HideInInspector] public bool testHold;     // stand still (still dodges)
	[HideInInspector] public bool testNoDodge;  // don't jump away

	public bool IsDown { get { return downTimer > 0 || IsDead; } }
	public bool IsDead { get; private set; }
	public enum Mood { Walk, Flee, Fight }
	public Mood CurrentMood { get { return mood; } }
	public int Punched { get; private set; }
	[Range(0, 1)] public float fightBackChance = 0.3f;
	[HideInInspector] public bool testForceFight;
	[HideInInspector] public bool testForceFlee;
	Mood mood, moodAfterGettingUp;
	float moodTimer, attackCooldown, punchAnim, deadTimer;
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
	Body body;
	Body.Move move;
	float moveT, moveLength, windup, staggerTimer, fall;
	Vector3 staggerPush;

	void Awake()
	{
		body = GetComponent<Body>();
		Health = maxHealth;
	}

	// Kuluna, ring fighters: more health, hit harder, block
	public void SetRole(Role r, string name, int health, int damage, float block, float rate)
	{
		role = r;
		displayName = name;
		maxHealth = health;
		Health = health;
		attackDamage = damage;
		blockChance = block;
		attackRate = rate;
		Defeated = false;
	}

	public void HealFully() { Health = maxHealth; Defeated = false; }

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

		// Shot: lies there, then is taken away
		if (IsDead)
		{
			deadTimer -= dt;
			FallTo(1f, 5f);
			if (deadTimer <= 0)
				Destroy(gameObject);
			return;
		}

		// Lying on the ground after a car hit them (or a knockout punch): they fall, then get back up
		if (downTimer > 0)
		{
			downTimer -= dt;
			FallTo(1f, 5f);
			if (downTimer <= 0)
			{
				if (Defeated)
				{
					// beaten kuluna / fighters get up slowly and walk away
					HealFully();
					hangOut = false;
					cheering = false;
					mood = Mood.Walk;
					moodAfterGettingUp = Mood.Walk;
					StartMood(Mood.Flee);
				}
				else if (moodAfterGettingUp != Mood.Walk)
					StartMood(moodAfterGettingUp);
			}
			return;
		}
		if (fall > 0f)
		{
			FallTo(0f, 2.5f);   // getting up
			return;
		}

		// Got hit: stumble back
		if (staggerTimer > 0)
		{
			staggerTimer -= dt;
			transform.position += staggerPush * dt;
			if (body != null) body.Do(Body.Move.Hit, 1f - staggerTimer / 0.35f);
			return;
		}

		// Watching a fight
		if (cheering && mood == Mood.Walk)
		{
			Face(cheerAt - transform.position);
			if (body != null) body.Do(Body.Move.Cheer, 0f);
			return;
		}

		// Kuluna at their corner
		if (hangOut && mood == Mood.Walk)
		{
			Vector3 back = home - transform.position;
			back.y = 0;
			if (back.magnitude > 0.6f)
			{
				transform.position += back.normalized * Mathf.Min(back.magnitude, walkSpeed * 1.5f * dt);
				Face(back);
				Swing(walkSpeed * 1.5f);
			}
			else
			{
				Face(hangLookAt - transform.position);
				Swing(0);
			}
			return;
		}

		// After a fight: run away, or fight you
		if (mood != Mood.Walk)
		{
			UpdateMood(dt);
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

	// You punched them (old way: one normal punch)
	public void TakePunch(PlayerCombat by) { TakeHit(12, by.transform, true, false); }

	// Someone hit them (you, or another person in a street fight).
	// A normal person you hit by surprise falls down. In a fight they stumble back, and fall when their health
	// runs out or a big hit (hook, kick) lands when they are already hurt. Returns true if they fell.
	public bool TakeHit(int damage, Transform from, bool byYou, bool heavy)
	{
		if (IsDown || fall > 0f)
			return false;
		if (byYou) Punched++;
		HitsTaken++;
		bool wasFightingYou = FightingYou;
		Pedestrian attacker = from != null ? from.GetComponent<Pedestrian>() : null;
		Vector3 away = transform.position - (from != null ? from.position : transform.position - transform.forward);
		away.y = 0;
		if (away.sqrMagnitude < 0.001f) away = -transform.forward;
		away.Normalize();
		yaw = Quaternion.LookRotation(-away).eulerAngles.y;
		dodgeTimer = 0;
		talkTimer = 0;
		cheering = false;
		TosakanaGame game = TosakanaGame.Instance;

		// Fighters sometimes block (not a hook or a kick, and not while they are getting ready to punch)
		if (mood == Mood.Fight && !heavy && windup <= 0 && blockChance > 0 && Random.value < blockChance)
		{
			Blocks++;
			Health -= Mathf.Max(1, damage / 4);
			PlayMove(Body.Move.Block, 0.3f);
			if (game != null) game.sound.BumpAt(transform.position);
			return false;
		}

		Health -= damage;
		bool surprise = mood == Mood.Walk && role == Role.Normal && byYou;
		bool down = Health <= 0 || surprise || (heavy && Health < maxHealth * 0.5f);
		windup = 0;
		if (down)
		{
			transform.position += away * 1f;
			if (Health <= 0)
			{
				Health = 0;
				Defeated = role != Role.Normal;
			}
			downTimer = Defeated ? 8f : 2.5f;
			Voice.Say(gameObject, Lingala.Pick(Lingala.Hit));
			if (Defeated)
			{
				moodAfterGettingUp = Mood.Walk;
				mood = Mood.Walk;
				if (onDefeated != null) onDefeated(this);
			}
			else if (role != Role.Normal)
				moodAfterGettingUp = Mood.Fight;   // kuluna and fighters get up and keep fighting
			else if (Health <= 0)
			{
				moodAfterGettingUp = Mood.Flee;    // beaten: runs away
				Health = maxHealth / 2;
			}
			else if (mood == Mood.Fight)
				moodAfterGettingUp = Mood.Fight;
			else
			{
				bool fight = testForceFight || (!testForceFlee && Random.value < fightBackChance);
				moodAfterGettingUp = fight ? Mood.Fight : Mood.Flee;
			}
		}
		else
		{
			staggerTimer = 0.35f;
			staggerPush = away * (heavy ? 2.2f : 1.2f);
			if (Random.value < 0.35f)
				Voice.Say(gameObject, Lingala.Pick(Lingala.Hit), false);
		}

		// Who do they fight now?
		if (byYou) FoePerson = null;
		else if (attacker != null) FoePerson = attacker;
		if (!down && mood != Mood.Fight && role != Role.Normal)
			StartMood(Mood.Fight);

		// You hit a normal person who was not attacking you: that's a crime, and people around run away
		if (byYou && role == Role.Normal && !wasFightingYou && (Punched == 1 || down))
		{
			if (PoliceManager.Instance != null)
				PoliceManager.Instance.ReportCrime(1, "You hit a person!");
			foreach (Pedestrian p in All)
				if (p != this && !p.IsDown && p.mood == Mood.Walk && p.role == Role.Normal && !p.cheering && Vector3.Distance(p.transform.position, transform.position) < 9f)
					p.StartMood(Mood.Flee);
		}
		return down;
	}

	// Start a street fight with another person
	public void FightPerson(Pedestrian other)
	{
		FoePerson = other;
		hangOut = false;
		cheering = false;
		talkTimer = 0;
		StartMood(Mood.Fight);
		moodTimer = 40f;
	}

	// Lost a street fight: runs away when they get up
	public void GiveUp()
	{
		FoePerson = null;
		moodAfterGettingUp = Mood.Flee;
		if (!IsDown) StartMood(Mood.Flee);
	}

	// Kuluna / ring fighter: go for you
	public void AttackYou()
	{
		FoePerson = null;
		cheering = false;
		if (mood != Mood.Fight && !IsDown)
			StartMood(Mood.Fight);
		moodTimer = 999f;
	}

	// You shot them: they fall and don't get up
	public void Shot()
	{
		if (IsDead)
			return;
		IsDead = true;
		deadTimer = 15f;
		dodgeTimer = 0;
		talkTimer = 0;
		mood = Mood.Walk;
		if (TosakanaGame.Instance != null)
			TosakanaGame.Instance.sound.BumpAt(transform.position);
	}

	// Gunshots nearby: run away
	public void Scare()
	{
		if (!IsDown && mood == Mood.Walk)
			StartMood(Mood.Flee);
	}

	public void CalmDown()
	{
		mood = Mood.Walk;
		moodAfterGettingUp = Mood.Walk;
		FoePerson = null;
		windup = 0;
	}

	void StartMood(Mood m)
	{
		mood = m;
		moodTimer = m == Mood.Fight ? (role == Role.Normal ? 12f : 999f) : 5f;
		attackCooldown = 0.8f;
		moodAfterGettingUp = Mood.Walk;
		Voice.Say(gameObject, Lingala.Pick(m == Mood.Fight ? Lingala.FightBack : Lingala.Help));
	}

	void PlayMove(Body.Move m, float length)
	{
		move = m;
		moveT = 0;
		moveLength = length;
	}

	// Fall down (target 1) or get up (target 0), smoothly
	void FallTo(float target, float speed)
	{
		fall = Mathf.MoveTowards(fall, target, Time.deltaTime * speed);
		float eased = fall * fall * (3f - 2f * fall);
		transform.rotation = Quaternion.Euler(-82f * eased, yaw, 0);
		if (body != null)
		{
			if (fall > 0.4f) body.Lie();
			else body.Stand();
		}
		else
			Swing(0);
	}

	void UpdateMood(float dt)
	{
		TosakanaGame game = TosakanaGame.Instance;
		moodTimer -= dt;
		if (game == null || moodTimer <= 0)
		{
			mood = Mood.Walk;
			return;
		}
		if (FoePerson != null && (FoePerson.IsDown || FoePerson.IsDead || !FoePerson.isActiveAndEnabled))
		{
			// won the street fight
			FoePerson = null;
			mood = Mood.Walk;
			return;
		}
		Transform foe = FoePerson != null ? FoePerson.transform : game.player.transform;
		Vector3 to = foe.position - transform.position;
		to.y = 0;
		float distance = to.magnitude;
		if (mood == Mood.Flee)
		{
			transform.position += -to.normalized * 5f * dt;
			Face(-to);
			Swing(5f);
			return;
		}
		// Fight: walk up and punch
		if (FoePerson == null && (game.InVehicle || distance > 25f))
		{
			mood = Mood.Walk;
			windup = 0;
			return;
		}
		Face(to);
		// Getting ready to punch (the right fist goes back: block or dodge now!)
		if (windup > 0)
		{
			windup -= dt;
			if (body != null) body.Do(Body.Move.Windup, 1f - windup / 0.32f);
			if (windup <= 0)
				Strike(game, foe, distance);
			return;
		}
		if (distance > 1.25f)
		{
			transform.position += to.normalized * 3.8f * dt;
			Swing(3.8f);
			return;
		}
		// Close: guard, move around a little, attack
		transform.position += transform.right * Mathf.Sin(Time.time * 1.7f + yaw) * 0.6f * dt;
		Swing(0);
		attackCooldown -= dt * attackRate;
		if (attackCooldown <= 0)
		{
			attackCooldown = Random.Range(0.9f, 1.5f);
			windup = 0.32f;
		}
	}

	void Strike(TosakanaGame game, Transform foe, float distance)
	{
		PlayMove(Random.value < 0.5f ? Body.Move.Cross : Body.Move.Hook, 0.28f);
		punchAnim = 0.25f;
		if (distance > 1.8f)
			return;   // you stepped away: missed
		if (FoePerson != null)
		{
			FoePerson.TakeHit(attackDamage, transform, false, Random.value < 0.2f);
			if (game != null) game.sound.PunchAt(foe.position);
			return;
		}
		PlayerCombat combat = game.player.GetComponent<PlayerCombat>();
		if (combat != null)
			combat.TakeDamage(attackDamage);
	}

	// Sometimes people say hello when you walk past
	void GreetYou(float dt)
	{
		greetCooldown -= dt;
		TosakanaGame game = TosakanaGame.Instance;
		if (game == null || !game.IsPlaying || game.InVehicle || greetCooldown > 0 || Time.time < nextGreetTime)
			return;
		Vector3 d = game.player.transform.position - transform.position;
		d.y = 0;
		if (d.magnitude < 6f)
		{
			greetCooldown = 25f;
			nextGreetTime = Time.time + 4f;
			if (Random.value < 0.6f)
				Voice.Say(gameObject, Lingala.Pick(carriesBasin ? Lingala.SellerSays : Lingala.Greetings), false);   // only if nobody is talking
		}
	}

	// You pressed T: stop, look at you and answer
	public void Answer(int lineIndex)
	{
		talkTimer = 3.5f;
		Lingala.Line[] answers = carriesBasin && Random.value < 0.5f ? Lingala.SellerSays : Lingala.Answers[lineIndex % Lingala.Answers.Length];
		Voice.Say(gameObject, Lingala.Pick(answers));   // answering you: waits for you to finish
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
			Voice.Say(gameObject, Lingala.Pick(Lingala.WatchOut));
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
		Voice.Say(gameObject, Lingala.Pick(Lingala.Hit));
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
		if (body != null)
		{
			cycle += Time.deltaTime * speed * 1.7f;
			punchAnim -= Time.deltaTime;
			if (moveT < moveLength)
			{
				moveT += Time.deltaTime;
				body.Do(move, moveT / moveLength);
				return;
			}
			if (mood == Mood.Fight && speed < 0.1f)
				body.Guard(Mathf.Sin(Time.time * 6f) * 3f);
			else
				body.Walk(cycle, Mathf.Clamp01(speed / 6f) + (speed > 0.2f ? 0.25f : 0f));
			if (carriesBasin && rightArm != null)
			{
				rightArm.localRotation = Quaternion.Euler(180f, 0, -12f);
				if (body.rightElbow != null) body.rightElbow.localRotation = Quaternion.Euler(-8f, 0, 0);
			}
			return;
		}
		cycle += Time.deltaTime * speed * 4f;
		float swing = Mathf.Sin(cycle) * Mathf.Clamp01(speed) * 28f;
		if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(swing, 0, 0);
		if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
		if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-swing * 0.8f, 0, 0);
		// Someone carrying a basin holds it on their head with the right arm; a punch shoots it forward
		punchAnim -= Time.deltaTime;
		if (rightArm != null)
			rightArm.localRotation = punchAnim > 0 ? Quaternion.Euler(-90f, 0, 0) : carriesBasin ? Quaternion.Euler(180f, 0, -12f) : Quaternion.Euler(swing * 0.8f, 0, 0);
	}
}
