using UnityEngine;
using UnityEngine.UI;

// Your fists. Left click (or R) = punch the nearest person or police officer.
// People and police can hit you back. Your health is the red bar; it comes back slowly when nobody hits you.
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

	// Test hook for the play-mode checks
	[HideInInspector] public bool testPunch;

	public int Health { get; private set; }
	public int Punches { get; private set; }
	public int KnockOuts { get; private set; }

	PlayerController pc;
	float cooldown, punchAnim, sinceHurt = 99f, regen, flash;

	void Awake()
	{
		pc = GetComponent<PlayerController>();
		Health = maxHealth;
	}

	void Update()
	{
		TosakanaGame game = TosakanaGame.Instance;
		if (game == null || !game.IsPlaying)
			return;
		float dt = Time.deltaTime;
		cooldown -= dt;
		punchAnim -= dt;
		sinceHurt += dt;

		bool wants = testPunch || Input.GetKeyDown(KeyCode.R) || (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.Locked);
		testPunch = false;
		PlayerWeapons weapons = GetComponent<PlayerWeapons>();
		if (wants && cooldown <= 0 && !game.InVehicle && (weapons == null || !weapons.PistolOut))
			Punch();

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
		// Punch: the right arm shoots forward
		if (punchAnim > 0 && pc.rightArm != null)
			pc.rightArm.localRotation = Quaternion.Euler(-90f, 0, 0);
	}

	// Hit the nearest person or officer (you turn to face them). Returns who you hit, or null.
	public Component Punch()
	{
		TosakanaGame game = TosakanaGame.Instance;
		cooldown = punchCooldown;
		punchAnim = 0.22f;
		Punches++;
		game.sound.Whoosh();

		Component best = null;
		float bestDistance = punchRange;
		foreach (PoliceFoot cop in PoliceFoot.All)
			if (!cop.IsDown) Consider(cop, ref best, ref bestDistance);
		foreach (Pedestrian p in Pedestrian.All)
			if (!p.IsDown) Consider(p, ref best, ref bestDistance);
		foreach (TrafficOfficer o in TrafficOfficer.All)
			if (!o.IsDown) Consider(o, ref best, ref bestDistance);
		if (best == null)
			return null;

		Vector3 to = best.transform.position - transform.position;
		to.y = 0;
		if (to.sqrMagnitude > 0.001f)
			transform.rotation = Quaternion.LookRotation(to);
		game.sound.PunchAt(best.transform.position);

		if (best is Pedestrian) ((Pedestrian)best).TakePunch(this);
		else if (best is PoliceFoot) ((PoliceFoot)best).TakePunch(this);
		else if (best is TrafficOfficer) ((TrafficOfficer)best).TakePunch(this);
		return best;
	}

	void Consider(Component c, ref Component best, ref float bestDistance)
	{
		Vector3 d = c.transform.position - transform.position;
		d.y = 0;
		if (d.magnitude < bestDistance)
		{
			bestDistance = d.magnitude;
			best = c;
		}
	}

	public void TakeDamage(int amount)
	{
		TosakanaGame game = TosakanaGame.Instance;
		if (game == null || !game.IsPlaying || game.InVehicle || !gameObject.activeInHierarchy)
			return;
		Health -= amount;
		sinceHurt = 0;
		flash = 0.35f;
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
	}
}
