using UnityEngine;
using UnityEngine.UI;

// Your fists (Version 12: real fighting moves).
//   Left click / R  = punch. Click again quickly for a combo: JAB, CROSS, HOOK (the hook is a big hit)
//   G               = kick (strong and slow: knocks people down)
//   Right click / B = block (hold it: you take much less damage)
//   X               = dodge (jump to the side: their punch misses)
// Watch your enemy: when they pull their fist back, block or dodge!
// Your health is the red bar; it comes back slowly when nobody hits you.
// If it runs out you are KNOCKED OUT: you lose some money and start again at Place du 30 Juin.
[RequireComponent(typeof(PlayerController))]
public class PlayerCombat : MonoBehaviour
{
	public int maxHealth = 100;
	public float punchRange = 2.2f;
	public float punchCooldown = 0.45f;
	public int knockOutFine = 100;

	[Header("UI")]
	public RectTransform healthFill;
	public Image damageFlash;
	public GameObject foeBar;          // the health bar of who you are fighting
	public RectTransform foeFill;
	public Text foeName;
	public Text moveText;              // "JAB", "CROSS", "HOOK!", "KICK!", "BLOCK"

	// Test hooks for the play-mode checks
	[HideInInspector] public bool testPunch;
	[HideInInspector] public bool testKick;
	[HideInInspector] public bool testBlock;
	[HideInInspector] public bool testDodge;

	public int Health { get; private set; }
	public int Punches { get; private set; }
	public int KnockOuts { get; private set; }
	public int Kicks { get; private set; }
	public int Dodges { get; private set; }
	public int BlockedHits { get; private set; }
	public Body.Move LastMove { get; private set; }
	public bool Blocking { get; private set; }
	public bool Dodging { get { return dodgeTimer > 0; } }
	public Pedestrian Foe { get; private set; }

	static readonly Body.Move[] Combo = { Body.Move.Jab, Body.Move.Cross, Body.Move.Hook };
	static readonly int[] ComboDamage = { 12, 16, 26 };

	PlayerController pc;
	Body body;
	float cooldown, sinceHurt = 99f, regen, flash, comboWindow, moveTime = 9f, moveLength, dodgeTimer, foeTimer, moveTextTimer;
	int comboStep;
	Body.Move animMove;
	Vector3 dodgeDir;

	void Awake()
	{
		pc = GetComponent<PlayerController>();
		body = GetComponent<Body>();
		Health = maxHealth;
	}

	void Update()
	{
		TosakanaGame game = TosakanaGame.Instance;
		if (game == null || !game.IsPlaying)
			return;
		float dt = Time.deltaTime;
		cooldown -= dt;
		comboWindow -= dt;
		sinceHurt += dt;
		moveTime += dt;
		foeTimer -= dt;

		bool mouse = Cursor.lockState == CursorLockMode.Locked;
		PlayerWeapons weapons = GetComponent<PlayerWeapons>();
		bool fists = !game.InVehicle && (weapons == null || !weapons.PistolOut);
		bool wantsPunch = testPunch || Input.GetKeyDown(KeyCode.R) || (Input.GetMouseButtonDown(0) && mouse);
		bool wantsKick = testKick || Input.GetKeyDown(KeyCode.G);
		bool wantsDodge = testDodge || Input.GetKeyDown(KeyCode.X);
		Blocking = fists && (testBlock || Input.GetKey(KeyCode.B) || (Input.GetMouseButton(1) && mouse)) && dodgeTimer <= 0;
		testPunch = testKick = testDodge = false;

		if (fists && !Blocking && cooldown <= 0)
		{
			if (wantsKick) Kick();
			else if (wantsPunch) Punch();
		}
		if (fists && wantsDodge && dodgeTimer <= 0)
			Dodge();

		// Dodging: a quick jump to the side
		if (dodgeTimer > 0)
		{
			dodgeTimer -= dt;
			CharacterController cc = GetComponent<CharacterController>();
			if (cc != null && cc.enabled)
				cc.Move(dodgeDir * 7f * dt);
		}

		// Health comes back slowly after 4 seconds without being hit
		if (sinceHurt > 4f && Health < maxHealth)
		{
			regen += dt * 6f;
			if (regen >= 1f)
			{
				int add = (int)regen;
				regen -= add;
				Health = Mathf.Min(maxHealth, Health + add);
			}
		}
		UpdateUI(dt);
	}

	void LateUpdate()
	{
		TosakanaGame game = TosakanaGame.Instance;
		if (body == null || game == null || game.InVehicle)
		{
			// old bodies: the right arm shoots forward
			if (body == null && moveTime < moveLength && pc.rightArm != null)
				pc.rightArm.localRotation = Quaternion.Euler(-90f, 0, 0);
			return;
		}
		PlayerWeapons weapons = GetComponent<PlayerWeapons>();
		if (weapons != null && weapons.PistolOut)
			return;
		// Fighting moves win over walking
		if (moveTime < moveLength)
			body.Do(animMove, moveTime / moveLength);
		else if (Blocking)
			body.Do(Body.Move.Block, 1f);
		else if (dodgeTimer > 0)
			body.Do(Body.Move.Hit, 0.5f);
		else if (InAFight() && pc.CurrentSpeed < 0.5f)
			body.Guard(Mathf.Sin(Time.time * 6f) * 3f);
	}

	// Someone is fighting you right now (close by)
	public bool InAFight()
	{
		foreach (Pedestrian p in Pedestrian.All)
			if (p.FightingYou && !p.IsDown && Flat(p.transform.position - transform.position) < 6f)
				return true;
		return Foe != null && foeTimer > 0 && !Foe.IsDown;
	}

	// Left click: the next punch of the combo (jab, cross, hook). Returns who you hit, or null.
	public Component Punch()
	{
		if (comboWindow <= 0)
			comboStep = 0;
		Body.Move m = Combo[comboStep];
		int damage = ComboDamage[comboStep];
		bool heavy = m == Body.Move.Hook;
		comboStep = (comboStep + 1) % Combo.Length;
		comboWindow = 0.9f;
		cooldown = heavy ? 0.45f : 0.28f;
		Punches++;
		return Attack(m, damage, heavy, heavy ? 0.36f : 0.24f, punchRange);
	}

	// G: a strong kick
	public Component Kick()
	{
		Kicks++;
		comboStep = 0;
		cooldown = 0.75f;
		return Attack(Body.Move.Kick, 30, true, 0.5f, punchRange + 0.4f);
	}

	Component Attack(Body.Move m, int damage, bool heavy, float length, float range)
	{
		TosakanaGame game = TosakanaGame.Instance;
		LastMove = m;
		animMove = m;
		moveTime = 0;
		moveLength = length;
		ShowMove(m == Body.Move.Jab ? "JAB" : m == Body.Move.Cross ? "CROSS" : m == Body.Move.Hook ? "HOOK!" : "KICK!");
		game.sound.Whoosh();

		Component best = null;
		float bestDistance = range;
		foreach (PoliceFoot cop in PoliceFoot.All)
			if (!cop.IsDown) Consider(cop, ref best, ref bestDistance, range);
		foreach (Pedestrian p in Pedestrian.All)
			if (!p.IsDown && !p.IsDead) Consider(p, ref best, ref bestDistance, range);
		foreach (TrafficOfficer o in TrafficOfficer.All)
			if (!o.IsDown) Consider(o, ref best, ref bestDistance, range);
		if (best == null)
			return null;

		Vector3 to = best.transform.position - transform.position;
		to.y = 0;
		if (to.sqrMagnitude > 0.001f)
			transform.rotation = Quaternion.LookRotation(to);
		game.sound.PunchAt(best.transform.position);

		if (best is Pedestrian)
		{
			Pedestrian p = (Pedestrian)best;
			p.TakeHit(damage, transform, true, heavy);
			Foe = p;
			foeTimer = 4f;
		}
		else if (best is PoliceFoot) ((PoliceFoot)best).TakePunch(this);
		else if (best is TrafficOfficer) ((TrafficOfficer)best).TakePunch(this);
		return best;
	}

	// X: jump to the side (left or right of where you look, or the way you are walking)
	public void Dodge()
	{
		Dodges++;
		dodgeTimer = 0.3f;
		float h = Input.GetAxisRaw("Horizontal");
		dodgeDir = transform.right * (h < -0.1f ? -1f : h > 0.1f ? 1f : (Dodges % 2 == 0 ? -1f : 1f));
		ShowMove("DODGE");
		TosakanaGame.Instance.sound.Whoosh();
	}

	void Consider(Component c, ref Component best, ref float bestScore, float range)
	{
		Vector3 d = c.transform.position - transform.position;
		d.y = 0;
		if (d.magnitude > range)
			return;
		// a little bonus for people in front of you, so you hit who you are looking at
		float score = d.magnitude - (d.sqrMagnitude > 0.001f && Vector3.Dot(d.normalized, transform.forward) > 0.5f ? 0.4f : 0f);
		if (best == null || score < bestScore)
		{
			bestScore = score;
			best = c;
		}
	}

	public void TakeDamage(int amount)
	{
		TosakanaGame game = TosakanaGame.Instance;
		if (game == null || !game.IsPlaying || game.InVehicle || !gameObject.activeInHierarchy)
			return;
		if (Dodging)
			return;   // the punch missed you
		if (Blocking)
		{
			BlockedHits++;
			amount = Mathf.Max(1, amount / 4);
			ShowMove("BLOCK");
		}
		Health -= amount;
		sinceHurt = 0;
		flash = Blocking ? 0.1f : 0.35f;
		game.sound.PunchAt(transform.position);
		if (Health <= 0)
			KnockedOut(game);
	}

	public void Heal() { Health = maxHealth; }
	public void SetHealth(int value) { Health = Mathf.Clamp(value, 1, maxHealth); }

	void KnockedOut(TosakanaGame game)
	{
		KnockOuts++;
		int fine = Mathf.Min(game.Money, knockOutFine);
		game.AddMoney(-fine);
		Health = maxHealth;
		pc.Teleport(game.RespawnPoint, 90f);
		game.cameraRig.SetYaw(90f);
		game.ShowBig("KNOCKED OUT", Lingala.KnockedOut.lingala + (fine > 0 ? "  ·  -$" + fine : ""), new Color(1f, 0.3f, 0.3f), 3.5f);
		Voice.SayToYou(Lingala.KnockedOut);
		if (PoliceManager.Instance != null)
			PoliceManager.Instance.StopEverything();
		foreach (Pedestrian p in Pedestrian.All)
			p.CalmDown();
		Foe = null;
		if (FightClub.Instance != null)
			FightClub.Instance.YouLost();
	}

	void ShowMove(string text)
	{
		if (moveText == null)
			return;
		moveText.text = text;
		moveTextTimer = 0.6f;
	}

	void UpdateUI(float dt)
	{
		if (healthFill != null)
		{
			healthFill.anchorMax = new Vector2(Mathf.Clamp01((float)Health / maxHealth), 1f);
			Image img = healthFill.GetComponent<Image>();
			if (img != null)
				img.color = Health > 30 ? new Color(0.9f, 0.2f, 0.2f) : new Color(1f, 0.2f, 0.2f, 0.6f + 0.4f * Mathf.PingPong(Time.time * 3f, 1f));
		}
		if (damageFlash != null)
		{
			flash = Mathf.Max(0, flash - dt);
			damageFlash.color = new Color(0.8f, 0f, 0f, flash);
		}
		// The enemy's health bar: whoever you hit last, or whoever is fighting you
		if (Foe == null || !Foe.isActiveAndEnabled || foeTimer <= 0)
		{
			Pedestrian attacker = null;
			foreach (Pedestrian p in Pedestrian.All)
				if (p.FightingYou && Flat(p.transform.position - transform.position) < 8f) { attacker = p; break; }
			if (attacker != null) { Foe = attacker; foeTimer = 1f; }
		}
		bool showFoe = Foe != null && Foe.isActiveAndEnabled && (foeTimer > 0 || Foe.FightingYou);
		if (foeBar != null)
		{
			foeBar.SetActive(showFoe);
			if (showFoe)
			{
				foeFill.anchorMax = new Vector2(Mathf.Clamp01((float)Foe.Health / Foe.maxHealth), 1f);
				foeName.text = Foe.displayName + (Foe.Defeated ? "  ·  KO!" : "");
			}
		}
		if (moveText != null)
		{
			moveTextTimer -= dt;
			moveText.color = new Color(1f, 0.9f, 0.3f, Mathf.Clamp01(moveTextTimer * 3f));
		}
	}

	static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }
}
