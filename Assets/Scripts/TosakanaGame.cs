using UnityEngine;
using UnityEngine.UI;

// Runs the game: title screen, money, getting in and out of cars, and the HUD.
public class TosakanaGame : MonoBehaviour
{
	public enum State { Title, Playing }
	public static TosakanaGame Instance { get; private set; }

	public string author = "Joel Kalala";
	public float enterCarDistance = 4.5f;

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
	public Text areaBanner;
	public Text areaBannerSub;

	public State CurrentState { get; private set; }
	public bool IsPlaying { get { return CurrentState == State.Playing; } }
	public int Money { get; private set; }
	public CarController CurrentCar { get; private set; }
	public bool InCar { get { return CurrentCar != null; } }
	public Transform Focus { get { return InCar ? CurrentCar.transform : player.transform; } }
	public Vector3 RespawnPoint { get; private set; }   // where you start again after BUSTED

	float bigTimer, bigLength, bannerTimer;
	int talkIndex;
	string lastCommune = "";
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

		if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F))
			ToggleCar();
		if (InCar && Input.GetKeyDown(KeyCode.H))
			sound.Horn();
		if (!InCar && Input.GetKeyDown(KeyCode.T))
			TalkToNearest();

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

	// E: get in the nearest car, or get out of the car you are driving
	public bool ToggleCar()
	{
		if (!IsPlaying)
			return false;

		if (InCar)
		{
			CarController car = CurrentCar;
			car.SetDriver(false);
			CurrentCar = null;
			player.gameObject.SetActive(true);
			player.Teleport(ExitSpot(car), car.transform.eulerAngles.y);
			cameraRig.SetTarget(player.transform, false);
			sound.Door();
			return true;
		}

		CarController nearest = NearestCar(player.transform.position, enterCarDistance);
		if (nearest == null)
			return false;
		// Taking a car from the traffic: the driver gets out, shouts and walks away
		TrafficDriver driver = nearest.GetComponent<TrafficDriver>();
		if (driver != null && driver.enabled)
		{
			if (PedestrianManager.Instance != null)
			{
				Pedestrian angry = PedestrianManager.Instance.SpawnDriver(ExitSpot(nearest), nearest.transform.eulerAngles.y - 90f);
				if (angry != null)
					Voice.Say(angry.gameObject, Lingala.Pick(Lingala.Thief));
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
		CurrentCar = nearest;
		nearest.SetDriver(true);
		player.gameObject.SetActive(false);
		cameraRig.SetTarget(nearest.transform, true);
		sound.Door();
		return true;
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

	public void AddMoney(int amount)
	{
		Money += amount;
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
		zoneText.text = missions.ZoneName(Focus.position);

		if (!InCar && NearestCar(player.transform.position, enterCarDistance) != null)
			hintText.text = "Press E to get in the car  ·  <i>" + Lingala.GetInCar.lingala + "</i>";
		else if (!InCar && NearestPerson(4.5f) != null)
			hintText.text = "Press T to talk  ·  <i>" + Lingala.Talk.lingala + "</i>";
		else if (InCar && Mathf.Abs(CurrentCar.Speed) < 1f)
			hintText.text = "Press E to get out   ·   H = horn";
		else
			hintText.text = "";

		speedText.text = InCar ? Mathf.RoundToInt(Mathf.Abs(CurrentCar.Speed) * 3.6f) + " km/h" : "";

		// Entering a new commune: "Boyei malamu na LEMBA" (Welcome to Lemba)
		string commune = missions.CommuneAt(Focus.position);
		if (commune != lastCommune)
		{
			lastCommune = commune;
			if (IsPlaying)
				Voice.SayToYou(Lingala.WelcomeTo(commune), false);   // only if you recorded it
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
