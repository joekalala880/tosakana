using UnityEngine;
using UnityEngine.UI;

// Delivery missions: go to the yellow marker, pick up a package, bring it to another place before time runs out.
public class MissionManager : MonoBehaviour
{
	public enum Step { None, GoToPickup, Deliver, Passed, Failed }

	public Place[] places;
	public string firstPickup = "Grand Marché";
	public string firstDropoff = "Gombe";
	public float markerRadius = 4.5f;
	public float waitSeconds = 4f;
	public string boulevardName = "Boulevard du 30 Juin";
	public float boulevardZ = 29f;

	[Header("Communes (one row per block row, south to north, names split by commas)")]
	public string[] communeRows;
	public float communeStart = -174f;
	public float communeStep = 58f;

	[Header("Marker")]
	public Transform marker;
	public Transform markerRing;
	public TextMesh markerLabel;

	[Header("UI")]
	public Text titleText;
	public Text missionText;
	public Text timerText;

	public Step CurrentStep { get; private set; }
	public Place Pickup { get; private set; }
	public Place Dropoff { get; private set; }
	public int Reward { get; private set; }
	public int Completed { get; private set; }

	// Public so the play-mode checks can set them
	[HideInInspector] public float timeLeft;
	[HideInInspector] public float waitTimer;

	public bool HasTarget { get { return Target != null; } }
	public Place Target { get { return CurrentStep == Step.GoToPickup ? Pickup : CurrentStep == Step.Deliver ? Dropoff : null; } }

	void Start()
	{
		ShowMarker(null);
		UpdateTexts();
	}

	public void Begin()
	{
		Completed = 0;
		StartMission(Find(firstPickup), Find(firstDropoff));
	}

	public Place Find(string name)
	{
		foreach (Place p in places)
			if (p.placeName == name)
				return p;
		return places[0];
	}

	public void StartMission(Place from, Place to)
	{
		Pickup = from;
		Dropoff = to;
		Reward = Mathf.Max(200, Mathf.RoundToInt((100f + RouteLength(from, to) * 1.5f) / 50f) * 50);
		CurrentStep = Step.GoToPickup;
		ShowMarker(from);
		UpdateTexts();
	}

	// City streets are a grid, so the trip is about |dx| + |dz|
	public static float RouteLength(Place a, Place b)
	{
		Vector3 d = a.markerPosition - b.markerPosition;
		return Mathf.Abs(d.x) + Mathf.Abs(d.z);
	}

	public float TimeFor(Place a, Place b)
	{
		return 15f + RouteLength(a, b) / 10f;
	}

	void Update()
	{
		TosakanaGame game = TosakanaGame.Instance;
		if (game == null || !game.IsPlaying)
			return;
		Vector3 p = game.Focus.position;

		switch (CurrentStep)
		{
			case Step.GoToPickup:
				if (Inside(p, Pickup))
				{
					CurrentStep = Step.Deliver;
					timeLeft = TimeFor(Pickup, Dropoff);
					ShowMarker(Dropoff);
					game.sound.Pickup();
					game.ShowBig("PACKAGE PICKED UP", "Take it to " + Dropoff.placeName + "!  ·  " + Lingala.Carry(Dropoff.placeName), new Color(1f, 0.85f, 0.2f), 2.5f);
				}
				break;

			case Step.Deliver:
				timeLeft -= Time.deltaTime;
				if (Inside(p, Dropoff))
				{
					Completed++;
					game.AddMoney(Reward);
					CurrentStep = Step.Passed;
					waitTimer = waitSeconds;
					ShowMarker(null);
					game.sound.Passed();
					game.ShowBig("MISSION PASSED", "+$" + Reward.ToString("N0") + "  ·  " + Lingala.Passed.lingala, new Color(0.45f, 1f, 0.45f), waitSeconds);
				}
				else if (timeLeft <= 0)
				{
					timeLeft = 0;
					CurrentStep = Step.Failed;
					waitTimer = waitSeconds;
					ShowMarker(null);
					game.sound.Failed();
					game.ShowBig("MISSION FAILED", "You were too slow.  ·  " + Lingala.TooSlow.lingala, new Color(1f, 0.35f, 0.3f), waitSeconds);
				}
				break;

			case Step.Passed:
			case Step.Failed:
				waitTimer -= Time.deltaTime;
				if (waitTimer <= 0)
				{
					if (CurrentStep == Step.Passed)
						NextMission();
					else
						StartMission(Pickup, Dropoff);   // try the same job again
				}
				break;
		}

		AnimateMarker(game);
		UpdateTexts();
	}

	void NextMission()
	{
		Place from = Dropoff;
		while (from == Dropoff)
			from = places[Random.Range(0, places.Length)];
		Place to = from;
		while (to == from)
			to = places[Random.Range(0, places.Length)];
		StartMission(from, to);
	}

	bool Inside(Vector3 p, Place place)
	{
		Vector3 d = p - place.markerPosition;
		d.y = 0;
		return d.magnitude < markerRadius;
	}

	void ShowMarker(Place place)
	{
		if (marker == null)
			return;
		marker.gameObject.SetActive(place != null);
		if (place == null)
			return;
		marker.position = place.markerPosition;
		if (markerLabel != null)
			markerLabel.text = place.placeName;
	}

	void AnimateMarker(TosakanaGame game)
	{
		if (marker == null || !marker.gameObject.activeSelf)
			return;
		if (markerRing != null)
		{
			float s = markerRadius * 2f * (1f + 0.06f * Mathf.Sin(Time.time * 4f));
			markerRing.localScale = new Vector3(s, 0.05f, s);
		}
		if (markerLabel != null)
		{
			Transform cam = game.cameraRig.transform;
			markerLabel.transform.position = marker.position + Vector3.up * (6f + 0.4f * Mathf.Sin(Time.time * 2f));
			markerLabel.transform.rotation = Quaternion.LookRotation(markerLabel.transform.position - cam.position);
		}
	}

	// Which commune (neighborhood) of Kinshasa a point is in
	public string CommuneAt(Vector3 p)
	{
		if (communeRows == null || communeRows.Length == 0)
			return "Kinshasa";
		int j = Mathf.Clamp(Mathf.RoundToInt((p.z - communeStart) / communeStep), 0, communeRows.Length - 1);
		string[] row = communeRows[j].Split(',');
		int i = Mathf.Clamp(Mathf.RoundToInt((p.x - communeStart) / communeStep), 0, row.Length - 1);
		return row[i].Trim();
	}

	// The area name you see at the bottom right: the boulevard, a famous place, or the commune
	public string ZoneName(Vector3 p)
	{
		// On the big boulevard you see its name, like a street sign
		if (Mathf.Abs(p.z - boulevardZ) < 7f)
			return boulevardName;
		Place best = null;
		float bestDistance = float.MaxValue;
		foreach (Place place in places)
		{
			Vector3 d = p - place.transform.position;
			d.y = 0;
			if (place.radius > 0 && d.magnitude < place.radius && d.magnitude < bestDistance)
			{
				bestDistance = d.magnitude;
				best = place;
			}
		}
		return best != null ? best.placeName : CommuneAt(p);
	}

	void UpdateTexts()
	{
		if (titleText == null)
			return;
		TosakanaGame game = TosakanaGame.Instance;
		Place target = Target;
		string distance = "";
		if (game != null && target != null)
		{
			Vector3 d = target.markerPosition - game.Focus.position;
			d.y = 0;
			distance = "   <color=#ffd84d>" + Mathf.RoundToInt(d.magnitude) + " m</color>";
		}
		string tip = game != null && !game.InCar ? "\n<color=#9fd3ff>Tip: press E near a car to drive.</color>" : "";

		switch (CurrentStep)
		{
			case Step.GoToPickup:
				titleText.text = "MISSION " + (Completed + 1) + " · DELIVERY  ($" + Reward.ToString("N0") + ")";
				missionText.text = "Go to the yellow marker at <b>" + Pickup.placeName + "</b> and pick up a package." + distance +
					"\n<i><color=#ffd84d>" + Lingala.GoGet(Pickup.placeName) + "</color></i>" + tip;
				timerText.text = "";
				break;
			case Step.Deliver:
				titleText.text = "MISSION " + (Completed + 1) + " · DELIVERY  ($" + Reward.ToString("N0") + ")";
				missionText.text = "Take the package to <b>" + Dropoff.placeName + "</b> before time runs out!" + distance +
					"\n<i><color=#ffd84d>" + Lingala.Carry(Dropoff.placeName) + "</color></i>" + tip;
				int s = Mathf.CeilToInt(timeLeft);
				timerText.text = s / 60 + ":" + (s % 60).ToString("00");
				timerText.color = timeLeft < 10f ? new Color(1f, 0.35f, 0.3f) : Color.white;
				break;
			case Step.Passed:
				titleText.text = "MISSION PASSED";
				missionText.text = "Good job! A new job is coming...";
				timerText.text = "";
				break;
			case Step.Failed:
				titleText.text = "MISSION FAILED";
				missionText.text = "Too slow. Let's try that job again...";
				timerText.text = "";
				break;
			default:
				titleText.text = "";
				missionText.text = "";
				timerText.text = "";
				break;
		}
	}
}
