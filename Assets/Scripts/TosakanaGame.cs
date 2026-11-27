using UnityEngine;
using UnityEngine.UI;

// Runs the game: title screen, money, getting in and out of cars, and the HUD.
public class TosakanaGame : MonoBehaviour
{
	public enum State { Title, Playing }
	public static TosakanaGame Instance { get; private set; }

	public string author = "Joel Kalala";
	public float enterCarDistance = 4.5f;
	[Range(0, 1)] public float carjackFightChance = 0.5f;   // the driver you pull out comes after you
	public Pedestrian LastJackedDriver { get; private set; }
	public int TimesPulledOut { get; private set; }
	public GameObject seatedYou;        // you, sitting at the wheel (shown inside the car you drive)

	[Header("The goal of the game")]
	public int startMoney = 1000;       // you are a rich man already...
	public int goalMoney = 25000;       // ...earn this much to become Mokonzi ya Kinshasa (king of Kinshasa)
	public bool GoalReached { get; private set; }

	[Header("World")]
	public PlayerController player;
	public CameraRig cameraRig;
	public MissionManager missions;
	public GameSound sound;

	[Header("UI")]
	public GameObject titlePanel;
	public Text pressEnterText;
	public GameObject hud;
	public Text moneyText;
	public Text zoneText;
	public Text hintText;
	public Text speedText;
	public Text bigText;
	public Text bigSubText;
	public Text goalText;
	public Text areaBanner;
	public Text areaBannerSub;

	public State CurrentState { get; private set; }
	public bool IsPlaying { get { return CurrentState == State.Playing; } }
	public int Money { get; private set; }
	public CarController CurrentCar { get; private set; }
	public bool InCar { get { return CurrentCar != null; } }
	public PlaneController CurrentPlane { get; private set; }
	public bool InPlane { get { return CurrentPlane != null; } }
	public bool InVehicle { get { return InCar || InPlane; } }   // in a car or a plane
	public float enterPlaneDistance = 24f;   // planes are big: from the nose, the wings or the tail
	public Transform Focus { get { return InPlane ? CurrentPlane.transform : InCar ? CurrentCar.transform : player.transform; } }
	public Vector3 RespawnPoint { get; private set; }   // where you start again after BUSTED

	float bigTimer, bigLength, bannerTimer;
	int talkIndex;
	string lastCommune = "";
	readonly System.Collections.Generic.Dictionary<string, float> welcomedAt = new System.Collections.Generic.Dictionary<string, float>();
	public string CurrentCommune { get { return lastCommune; } }
	Color bigColor;

	void Awake()
	{
		Instance = this;
		if (sound == null)
			sound = GetComponent<GameSound>();
		if (sound == null)
			sound = gameObject.AddComponent<GameSound>();
	}

	void Start()
	{
		Time.timeScale = 1f;
		RespawnPoint = player.transform.position;
		CurrentState = State.Title;
		titlePanel.SetActive(true);
		hud.SetActive(false);
		cameraRig.titleMode = true;
		cameraRig.SetTarget(player.transform, false);
		bigText.text = "";
		bigSubText.text = "";
	}

	void Update()
	{
		if (CurrentState == State.Title)
		{
			pressEnterText.color = new Color(1f, 1f, 1f, 0.45f + 0.55f * Mathf.PingPong(Time.time * 1.2f, 1f));
			if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
				StartGame();
			return;
		}
		if (BigMap.IsOpenNow)
			return;   // the big map uses the mouse and keys

		if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F))
			ToggleCar();
		if (InCar && Input.GetKeyDown(KeyCode.H))
			sound.Horn();
		if (InCar && Input.GetKeyDown(KeyCode.V))
			ToggleInsideView();
		if (!InVehicle && Input.GetKeyDown(KeyCode.T))
			TalkToNearest();
		if (!InVehicle && Input.GetKeyDown(KeyCode.O))
			ChangeClothes();

		// Esc frees the mouse, a click takes it back
		if (Input.GetKeyDown(KeyCode.Escape))
		{
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
		}
		else if (Input.GetMouseButtonDown(0))
			LockMouse();

		UpdateHud();
	}

	public void StartGame()
	{
		if (CurrentState != State.Title)
			return;
		CurrentState = State.Playing;
		Money = startMoney;
		titlePanel.SetActive(false);
		hud.SetActive(true);
		cameraRig.titleMode = false;
		cameraRig.SetTarget(player.transform, false);
		cameraRig.SetYaw(player.transform.eulerAngles.y);
		missions.Begin();
		LockMouse();
		UpdateHud();
	}

	static void LockMouse()
	{
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}

	public CarController NearestCar(Vector3 from, float maxDistance)
	{
		CarController best = null;
		float bestDistance = maxDistance;
		foreach (CarController car in CarController.All)
		{
			Vector3 d = car.transform.position - from;
			d.y = 0;
			if (d.magnitude < bestDistance)
			{
				bestDistance = d.magnitude;
				best = car;
			}
		}
		return best;
	}

	public PlaneController NearestPlane(Vector3 from, float maxDistance)
	{
		PlaneController best = null;
		float bestDistance = maxDistance;
		foreach (PlaneController plane in PlaneController.All)
		{
			Vector3 d = plane.transform.position - from;
			d.y = 0;
			if (plane.OnGround && d.magnitude < bestDistance)
			{
				bestDistance = d.magnitude;
				best = plane;
			}
		}
		return best;
	}

	// E: get in the nearest car or plane, or get out
	public bool ToggleCar()
	{
		if (!IsPlaying)
			return false;

		if (InPlane)
		{
			// You can only get out on the ground, stopped
			if (!CurrentPlane.OnGround || Mathf.Abs(CurrentPlane.Speed) > 3f)
				return false;
			LeavePlane(CurrentPlane.transform.position + CurrentPlane.transform.forward * 22f);
			return true;
		}

		if (InCar)
		{
			CarController car = CurrentCar;
			car.SetDriver(false);
			CurrentCar = null;
			SitInCar(null);
			player.gameObject.SetActive(true);
			player.Teleport(ExitSpot(car), car.transform.eulerAngles.y);
			cameraRig.SetTarget(player.transform, false);
			sound.Door();
			return true;
		}

		CarController nearest = NearestCar(player.transform.position, enterCarDistance);
		if (nearest == null)
		{
			PlaneController plane = NearestPlane(player.transform.position, enterPlaneDistance);
			if (plane == null)
				return false;
			CurrentPlane = plane;
			plane.SetPilot(true);
			player.gameObject.SetActive(false);
			cameraRig.SetTarget(plane.transform, true, 48f, 13f);
			sound.Door();
			return true;
		}
		// Taking a car from the traffic: the driver gets out, shouts and walks away
		TrafficDriver driver = nearest.GetComponent<TrafficDriver>();
		if (driver != null && driver.enabled)
		{
			if (PedestrianManager.Instance != null)
			{
				// You pull the driver out: they fall on the road, then run away or come after you
				Pedestrian angry = PedestrianManager.Instance.SpawnDriver(ExitSpot(nearest), nearest.transform.eulerAngles.y - 90f);
				if (angry != null)
				{
					angry.YankedOut(nearest.transform.position, Random.value < carjackFightChance);
					LastJackedDriver = angry;
				}
			}
			PoliceManager police = PoliceManager.Instance;
			if (police != null)
			{
				if (driver.police)
					police.ReportCrime(2, "You stole a police car!");
				else if (police.PoliceNear(nearest.transform.position, 50f))
					police.ReportCrime(1, "The police saw you take a car!");
			}
		}
		// The city driver is out: hide them inside the car
		Transform cityDriver = nearest.transform.Find("Driver");
		if (cityDriver != null)
			cityDriver.gameObject.SetActive(false);
		CurrentCar = nearest;
		nearest.SetDriver(true);
		SitInCar(nearest);
		player.gameObject.SetActive(false);
		cameraRig.SetTarget(nearest.transform, true);
		sound.Door();
		return true;
	}

	void LeavePlane(Vector3 spot)
	{
		PlaneController plane = CurrentPlane;
		plane.SetPilot(false);
		CurrentPlane = null;
		player.gameObject.SetActive(true);
		player.Teleport(new Vector3(spot.x, 0.05f, spot.z), plane.transform.eulerAngles.y);
		cameraRig.SetTarget(player.transform, false);
		sound.Door();
	}

	// The owner of the car you took caught you: pulled out of the car, and they fight you
	public void PulledOut(Pedestrian by)
	{
		if (!InCar)
			return;
		CarController car = CurrentCar;
		LeaveVehicle();
		player.Teleport(car.transform.position - car.transform.right * 2.6f, car.transform.eulerAngles.y);
		ShowBig("PULLED OUT!", "Zongisa motuka na ngai!  ·  Give me back my car!", new Color(1f, 0.35f, 0.25f), 2.5f);
		if (by != null)
			by.JoinFight();
		TimesPulledOut++;
	}

	// Get out of whatever you are in (BUSTED, crash)
	public void LeaveVehicle()
	{
		if (InCar)
			ToggleCar();
		else if (InPlane)
			LeavePlane(CurrentPlane.transform.position + CurrentPlane.transform.forward * 22f);
	}

	// The plane you were flying crashed: you wait at the airport and pay $100
	public void PlaneCrashed(PlaneController plane)
	{
		if (CurrentPlane != plane)
			return;
		LeavePlane(new Vector3(plane.Home.x, 0, plane.Home.z - 25f));   // in front of the plane's parking spot
		int fine = Mathf.Min(Money, 100);
		AddMoney(-fine);
		ShowBig("CRASHED", Lingala.PlaneCrash.lingala + (fine > 0 ? "  ·  -$" + fine : ""), new Color(1f, 0.35f, 0.3f), 3.5f);
		Voice.SayToYou(Lingala.PlaneCrash);
	}

	// Find free space next to the car: left side, right side, behind, or on the roof
	static Vector3 ExitSpot(CarController car)
	{
		Transform t = car.transform;
		Vector3[] tries = { t.position - t.right * 2.3f, t.position + t.right * 2.3f, t.position - t.forward * 3.6f, t.position + t.forward * 3.6f };
		foreach (Vector3 spot in tries)
		{
			Vector3 p = new Vector3(spot.x, 0.05f, spot.z);
			if (!Physics.CheckCapsule(p + Vector3.up * 0.4f, p + Vector3.up * 1.5f, 0.35f, Physics.DefaultRaycastLayers | (1 << 2), QueryTriggerInteraction.Ignore))
				return p;
		}
		return t.position + Vector3.up * 2f;
	}

	// T: say something in Lingala to the nearest person, and they answer
	public Pedestrian TalkToNearest()
	{
		if (!IsPlaying || InCar)
			return null;
		Pedestrian best = null;
		float bestDistance = 4.5f;
		foreach (Pedestrian p in Pedestrian.All)
		{
			if (p.IsDown)
				continue;
			float d = Vector3.Distance(p.transform.position, player.transform.position);
			if (d < bestDistance)
			{
				bestDistance = d;
				best = p;
			}
		}
		if (best == null)
			return null;
		int line = talkIndex % Lingala.PlayerSays.Length;
		talkIndex++;
		Voice.Say(player.gameObject, Lingala.PlayerSays[line]);
		StartCoroutine(AnswerSoon(best, line));
		return best;
	}

	System.Collections.IEnumerator AnswerSoon(Pedestrian p, int line)
	{
		yield return new WaitForSeconds(0.8f);
		if (p != null && !p.IsDown)
			p.Answer(line);
	}

	Pedestrian NearestPerson(float maxDistance)
	{
		foreach (Pedestrian p in Pedestrian.All)
			if (!p.IsDown && Vector3.Distance(p.transform.position, player.transform.position) < maxDistance)
				return p;
		return null;
	}

	// O: the sapeur suit or the street look (Leopards jersey). The you in the car wears the same.
	public void ChangeClothes()
	{
		PlayerLooks looks = player.GetComponent<PlayerLooks>();
		if (looks == null)
			return;
		looks.SetStreet(!looks.street);
		ShowBig(looks.street ? "STREET LOOK" : "SAPEUR", looks.LookName, looks.street ? new Color(0.4f, 0.8f, 1f) : new Color(1f, 0.85f, 0.3f), 1.8f);
	}

	// You sit at the wheel of the car you drive (hidden when you look from inside)
	void SitInCar(CarController car)
	{
		if (seatedYou == null)
			return;
		if (car == null)
		{
			seatedYou.transform.SetParent(null, false);
			seatedYou.SetActive(false);
			return;
		}
		seatedYou.transform.SetParent(car.transform, false);
		seatedYou.transform.localPosition = Vector3.zero;
		seatedYou.transform.localRotation = Quaternion.identity;
		PlayerLooks mine = player.GetComponent<PlayerLooks>(), seated = seatedYou.GetComponent<PlayerLooks>();
		if (mine != null && seated != null)
			seated.SetStreet(mine.street);
		seatedYou.SetActive(!cameraRig.insideView);
	}

	// V: look from inside the car / from behind the car
	public void ToggleInsideView()
	{
		cameraRig.insideView = !cameraRig.insideView;
		if (seatedYou != null && InCar)
			seatedYou.SetActive(!cameraRig.insideView);
	}

	public void AddMoney(int amount)
	{
		Money += amount;
		if (!GoalReached && Money >= goalMoney)
		{
			GoalReached = true;
			ShowBig("MOKONZI YA KINSHASA!", "You are the King of Kinshasa!  ·  " + Lingala.King.lingala, new Color(1f, 0.84f, 0.1f), 7f);
			Voice.SayToYou(Lingala.King);
			sound.Passed();
		}
	}

	// Big message in the middle of the screen (MISSION PASSED, ...)
	public void ShowBig(string title, string sub, Color color, float seconds)
	{
		bigText.text = title;
		bigSubText.text = sub;
		bigColor = color;
		bigTimer = seconds;
		bigLength = seconds;
	}

	void UpdateHud()
	{
		moneyText.text = "$" + Money.ToString("N0");
		if (goalText != null)
			goalText.text = GoalReached ? "Mokonzi ya Kinshasa ♛" : "Goal: $" + Money.ToString("N0") + " / $" + goalMoney.ToString("N0") + "  (become the King of Kinshasa)";
		zoneText.text = missions.ZoneName(Focus.position);

		if (InPlane)
			hintText.text = CurrentPlane.OnGround && Mathf.Abs(CurrentPlane.Speed) < 3f
				? "W speed up · A/D turn · ↑ or Space climb (when fast)  ·  E get out"
				: "W/S speed · A/D turn · ↑ or Space climb · ↓ or C dive";
		else if (!InCar && NearestCar(player.transform.position, enterCarDistance) != null)
			hintText.text = "Press E to get in the car  ·  <i>" + Lingala.GetInCar.lingala + "</i>";
		else if (!InCar && NearestPlane(player.transform.position, enterPlaneDistance) != null)
			hintText.text = "Press E to get in the plane  ·  <i>" + Lingala.GetInPlane.lingala + "</i>";
		else if (!InCar && NearestPerson(4.5f) != null)
			hintText.text = "Press T to talk  ·  <i>" + Lingala.Talk.lingala + "</i>";
		else if (InCar && Mathf.Abs(CurrentCar.Speed) < 1f)
			hintText.text = "Press E to get out   ·   H = horn   ·   V = " + (cameraRig.insideView ? "view from behind" : "view from inside the car");
		else
			hintText.text = "";

		speedText.text = InPlane ? Mathf.RoundToInt(CurrentPlane.Speed * 3.6f) + " km/h  ·  " + Mathf.RoundToInt(CurrentPlane.Altitude) + " m"
			: InCar ? Mathf.RoundToInt(Mathf.Abs(CurrentCar.Speed) * 3.6f) + " km/h" : "";

		// Entering a new commune: "Bokomi malamu na LEMBA" (Welcome to Lemba)
		// The voice says it once when you really arrive: not on the border road, not from the sky in a plane,
		// and not again if you were welcomed to the same place in the last 2 minutes.
		string commune = missions.CommuneAt(Focus.position);
		if (commune != lastCommune && missions.ClearlyIn(Focus.position))
		{
			lastCommune = commune;
			float last;
			bool welcomedLately = welcomedAt.TryGetValue(commune, out last) && Time.time - last < 120f;
			if (IsPlaying && !InPlane && !welcomedLately)
			{
				welcomedAt[commune] = Time.time;
				Voice.SayToYou(Lingala.WelcomeTo(commune), false);   // only if you recorded it
			}
			if (areaBanner != null)
			{
				areaBanner.text = Lingala.Welcome.lingala + " " + commune.ToUpper();
				areaBannerSub.text = Lingala.Welcome.english + " " + commune;
				bannerTimer = 3.5f;
			}
		}
		if (areaBanner != null)
		{
			bannerTimer -= Time.deltaTime;
			float a = Mathf.Clamp01(Mathf.Min((3.5f - bannerTimer) / 0.4f, bannerTimer / 0.8f));
			areaBanner.color = new Color(1f, 0.86f, 0.25f, a);
			areaBannerSub.color = new Color(1f, 1f, 1f, a * 0.9f);
		}

		if (bigTimer > 0)
		{
			bigTimer -= Time.deltaTime;
			float shown = bigLength - bigTimer;
			float a = Mathf.Clamp01(Mathf.Min(shown / 0.25f, bigTimer / 0.6f));
			bigText.color = new Color(bigColor.r, bigColor.g, bigColor.b, a);
			bigSubText.color = new Color(1f, 1f, 1f, a);
		}
		else
		{
			bigText.text = "";
			bigSubText.text = "";
		}
	}
}
