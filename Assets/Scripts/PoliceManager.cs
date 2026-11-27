using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Wanted stars and the police.
// Crimes give you stars. More stars = more police cars chasing you.
// Get far from all police for a while and they lose you. If they catch you while you are stopped: BUSTED.
public class PoliceManager : MonoBehaviour
{
	public static PoliceManager Instance { get; private set; }

	public GameObject policePrefab;
	public int patrolCars = 2;
	public int maxStars = 5;
	public float escapeDistance = 70f;
	public float escapeBaseSeconds = 5f;    // seconds out of sight to lose the police...
	public float escapePerStar = 2f;        // ...plus this much per star
	public float bustSeconds = 1.5f;
	public int bustFine = 200;

	[Header("UI")]
	public RawImage[] starIcons;
	public Text wantedText;

	public int Stars { get; private set; }
	public int Busts { get; private set; }
	public int Escapes { get; private set; }
	public bool Searching { get { return Stars > 0 && escapeTimer > 0.5f; } }   // the police can't see you: stars blink
	public bool Seen { get; private set; }   // a chasing police car got close to you since you became wanted
	public float EscapeTime { get { return escapeBaseSeconds + escapePerStar * Stars; } }

	public readonly List<TrafficDriver> Police = new List<TrafficDriver>();

	float escapeTimer, bustTimer, spawnTimer, wantedTextTimer, wantedSince;
	readonly Dictionary<string, float> lastCrime = new Dictionary<string, float>();

	void Awake()
	{
		Instance = this;
	}

	void Start()
	{
		for (int i = 0; i < patrolCars; i++)
			SpawnPolice(40f, float.MaxValue);
		UpdateStars();
	}

	public int ChasingCount { get { return Police.FindAll(d => d != null && d.enabled && d.Chasing).Count; } }

	// Something bad happened: add stars (the same crime only counts once every 2 seconds)
	public void ReportCrime(int stars, string what)
	{
		TosakanaGame game = TosakanaGame.Instance;
		if (game == null || !game.IsPlaying)
			return;
		float last;
		if (lastCrime.TryGetValue(what, out last) && Time.time - last < 2f)
			return;
		lastCrime[what] = Time.time;
		if (Stars == 0)
		{
			Seen = false;
			wantedSince = Time.time;
		}
		Stars = Mathf.Min(maxStars, Stars + stars);
		escapeTimer = 0;
		if (wantedText != null)
		{
			wantedText.text = what;
			wantedTextTimer = 3f;
		}
		UpdateStars();
	}

	public bool PoliceNear(Vector3 p, float distance)
	{
		foreach (TrafficDriver d in Police)
			if (d != null && d.enabled && Vector3.Distance(d.transform.position, p) < distance)
				return true;
		return false;
	}

	// Used by the play-mode checks
	public void ResetWanted()
	{
		Stars = 0;
		escapeTimer = 0;
		bustTimer = 0;
		lastCrime.Clear();
		foreach (TrafficDriver d in Police)
			if (d != null) d.SetChasing(false);
		UpdateStars();
	}

	void Update()
	{
		TosakanaGame game = TosakanaGame.Instance;
		TrafficManager traffic = TrafficManager.Instance;
		if (game == null || traffic == null)
			return;
		float dt = Time.deltaTime;

		// A police car you took is yours now
		Police.RemoveAll(d => d == null || !d.enabled);

		if (wantedTextTimer > 0)
		{
			wantedTextTimer -= dt;
			if (wantedTextTimer <= 0 && wantedText != null)
				wantedText.text = "";
		}

		spawnTimer -= dt;
		if (Stars == 0)
		{
			foreach (TrafficDriver d in Police)
				d.SetChasing(false);
			if (Police.Count < patrolCars && spawnTimer <= 0)
			{
				SpawnPolice(70f, float.MaxValue);
				spawnTimer = 2f;
			}
			UpdateStars();
			return;
		}

		// One police car per star
		if (ChasingCount < Stars && spawnTimer <= 0)
		{
			TrafficDriver d = NearestPatrol(game.Focus.position);
			if (d == null)
				d = SpawnPolice(60f, 140f);
			if (d != null)
				d.SetChasing(true);
			spawnTimer = 2f;
		}

		// Out of sight: the police search, then lose you
		bool near = false;
		foreach (TrafficDriver d in Police)
			if (d.Chasing && Flat(d.transform.position - game.Focus.position) < escapeDistance)
				near = true;
		// You can only lose the police after they found you (or after 20 s if they never come)
		if (near)
			Seen = true;
		bool canEscape = Seen || Time.time - wantedSince > 20f;
		escapeTimer = near || !canEscape ? 0 : escapeTimer + dt;
		if (escapeTimer > EscapeTime)
		{
			LoseThePolice(game);
			return;
		}

		// Caught: a police car right next to you while you are stopped
		bool caught = false;
		foreach (TrafficDriver d in Police)
		{
			if (!d.Chasing)
				continue;
			float distance = Flat(d.transform.position - game.Focus.position);
			if (game.InCar)
				caught |= Mathf.Abs(game.CurrentCar.Speed) < 1.5f && distance < 7f;
			else
				caught |= distance < 5f && Mathf.Abs(d.Car.Speed) < 4f;
		}
		bustTimer = caught ? bustTimer + dt : Mathf.Max(0, bustTimer - dt);
		if (bustTimer > bustSeconds)
			Busted(game);

		UpdateStars();
	}

	TrafficDriver SpawnPolice(float minDistance, float maxDistance)
	{
		TrafficManager traffic = TrafficManager.Instance;
		TosakanaGame game = TosakanaGame.Instance;
		if (policePrefab == null || traffic == null)
			return null;
		Vector3 focus = game != null ? game.Focus.position : Vector3.zero;
		int a, b, h;
		float t;
		if (!traffic.FindSpot(focus, minDistance, maxDistance, out a, out b, out h, out t))
			return null;
		GameObject go = Instantiate(policePrefab, traffic.LanePoint(a, b, h, t), Quaternion.Euler(0, h * 90f, 0));
		go.name = "Police Car";
		TrafficDriver d = go.AddComponent<TrafficDriver>();
		d.police = true;
		d.Init(traffic, a, b, h);
		Police.Add(d);
		return d;
	}

	TrafficDriver NearestPatrol(Vector3 p)
	{
		TrafficDriver best = null;
		float bestDistance = float.MaxValue;
		foreach (TrafficDriver d in Police)
		{
			if (d.Chasing)
				continue;
			float distance = Vector3.Distance(d.transform.position, p);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				best = d;
			}
		}
		return best;
	}

	void LoseThePolice(TosakanaGame game)
	{
		Escapes++;
		Stars = 0;
		escapeTimer = 0;
		bustTimer = 0;
		game.ShowBig("YOU LOST THE POLICE", "", new Color(0.45f, 0.75f, 1f), 2.5f);
		SendExtraPoliceAway();
		UpdateStars();
	}

	void Busted(TosakanaGame game)
	{
		Busts++;
		int fine = Mathf.Min(game.Money, bustFine);
		game.AddMoney(-fine);
		Stars = 0;
		escapeTimer = 0;
		bustTimer = 0;
		if (game.InCar)
			game.ToggleCar();
		game.player.Teleport(game.RespawnPoint, 90f);
		game.cameraRig.SetYaw(90f);
		game.ShowBig("BUSTED", fine > 0 ? "Fine: -$" + fine : "", new Color(1f, 0.3f, 0.3f), 3.5f);
		game.sound.Failed();
		SendExtraPoliceAway();
		// The police that caught you go back to patrol somewhere else
		foreach (TrafficDriver d in Police)
			TrafficManager.Instance.Respawn(d);
		UpdateStars();
	}

	// Stop all chases and keep only the patrol cars
	void SendExtraPoliceAway()
	{
		foreach (TrafficDriver d in Police)
			d.SetChasing(false);
		while (Police.Count > patrolCars)
		{
			TrafficDriver last = Police[Police.Count - 1];
			Police.RemoveAt(Police.Count - 1);
			Destroy(last.gameObject);
		}
	}

	void UpdateStars()
	{
		if (starIcons == null)
			return;
		bool blinkOff = Searching && Mathf.PingPong(Time.time * 3f, 1f) < 0.5f;
		for (int i = 0; i < starIcons.Length; i++)
		{
			if (starIcons[i] == null)
				continue;
			bool lit = i < Stars;
			starIcons[i].color = lit ? (blinkOff ? new Color(1f, 0.85f, 0.2f, 0.35f) : new Color(1f, 0.85f, 0.2f, 1f)) : new Color(1f, 1f, 1f, 0.18f);
		}
	}

	static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }
}
