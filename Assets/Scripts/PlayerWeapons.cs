using UnityEngine;
using UnityEngine.UI;

// Q = switch between your fists and a pistol.
// With the pistol out: left click (or R) shoots where the + in the middle of the screen points.
// People you shoot fall and don't get up. Shots scare everyone near and bring the police.
[RequireComponent(typeof(PlayerController))]
public class PlayerWeapons : MonoBehaviour
{
	public float range = 150f;
	public float fireDelay = 0.28f;

	[Header("Look")]
	public GameObject pistolModel;     // in the right hand
	public GameObject muzzleFlash;

	[Header("UI")]
	public Text crosshair;
	public Text weaponText;

	// Test hook for the play-mode checks
	[HideInInspector] public bool testShoot;

	public bool PistolOut { get; private set; }
	public int Shots { get; private set; }
	public int Kills { get; private set; }

	PlayerController pc;
	float cooldown, flashTimer, aimTimer;

	void Awake()
	{
		pc = GetComponent<PlayerController>();
		ShowPistol(false);
	}

	public void SetPistol(bool on)
	{
		PistolOut = on;
		ShowPistol(on);
	}

	void ShowPistol(bool on)
	{
		if (pistolModel != null) pistolModel.SetActive(on);
		if (muzzleFlash != null) muzzleFlash.SetActive(false);
	}

	void Update()
	{
		TosakanaGame game = TosakanaGame.Instance;
		if (game == null || !game.IsPlaying)
		{
			UpdateUI(false);
			return;
		}
		float dt = Time.deltaTime;
		cooldown -= dt;
		aimTimer -= dt;

		if (!game.InVehicle && Input.GetKeyDown(KeyCode.Q))
			SetPistol(!PistolOut);

		bool wants = testShoot || Input.GetKeyDown(KeyCode.R) || (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.Locked);
		testShoot = false;
		if (PistolOut && wants && cooldown <= 0 && !game.InVehicle)
			Shoot();

		flashTimer -= dt;
		if (muzzleFlash != null)
			muzzleFlash.SetActive(flashTimer > 0);
		UpdateUI(!game.InVehicle);
	}

	void LateUpdate()
	{
		// Holding the pistol: arm straight out in front
		if (PistolOut && pc.rightArm != null)
		{
			pc.rightArm.localRotation = Quaternion.Euler(-88f, 0, 0);
			Body body = GetComponent<Body>();
			if (body != null && body.rightElbow != null) body.rightElbow.localRotation = Quaternion.identity;
		}
	}

	// Shoots where the camera looks. Returns what it hit (a person, an officer, a wall...) or null.
	public Component Shoot()
	{
		TosakanaGame game = TosakanaGame.Instance;
		cooldown = fireDelay;
		flashTimer = 0.06f;
		Shots++;
		game.sound.Bang(transform.position);

		Camera cam = game.cameraRig.GetComponent<Camera>();
		Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
		// You turn to face where you shoot
		Vector3 flat = new Vector3(ray.direction.x, 0, ray.direction.z);
		if (flat.sqrMagnitude > 0.001f)
			transform.rotation = Quaternion.LookRotation(flat);

		// Shots are loud: people run, the police come
		foreach (Pedestrian p in Pedestrian.All)
			if (Vector3.Distance(p.transform.position, transform.position) < 30f)
				p.Scare();
		if (PoliceManager.Instance != null)
			PoliceManager.Instance.ReportCrime(1, "Gunshots!");

		RaycastHit[] hits = Physics.RaycastAll(ray, range, Physics.DefaultRaycastLayers | (1 << 2), QueryTriggerInteraction.Collide);
		System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
		foreach (RaycastHit hit in hits)
		{
			if (hit.collider.transform.IsChildOf(transform))
				continue;   // yourself
			Pedestrian person = hit.collider.GetComponentInParent<Pedestrian>();
			if (person != null && !person.IsDead)
			{
				person.Shot();
				Kills++;
				if (PoliceManager.Instance != null)
					PoliceManager.Instance.ReportCrime(1, "You shot someone!");
				return person;
			}
			PoliceFoot cop = hit.collider.GetComponentInParent<PoliceFoot>();
			if (cop != null)
			{
				cop.TakeShot();
				if (PoliceManager.Instance != null)
					PoliceManager.Instance.ReportCrime(2, "You shot a police officer!");
				return cop;
			}
			TrafficOfficer roulage = hit.collider.GetComponentInParent<TrafficOfficer>();
			if (roulage != null)
			{
				roulage.Shot();
				if (PoliceManager.Instance != null)
					PoliceManager.Instance.ReportCrime(2, "You shot a roulage officer!");
				return roulage;
			}
			if (hit.collider.isTrigger)
				continue;   // something you can see through
			return hit.collider;   // a wall, a car, the ground: the bullet stops
		}
		return null;
	}

	void UpdateUI(bool onFoot)
	{
		if (crosshair != null)
			crosshair.enabled = PistolOut && onFoot;
		if (weaponText != null)
			weaponText.text = onFoot ? (PistolOut ? "PISTOL  ·  Q = fists" : "FISTS  ·  Q = pistol") : "";
	}
}
