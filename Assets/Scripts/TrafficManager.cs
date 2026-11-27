using System.Collections.Generic;
using UnityEngine;

// Puts cars on the streets and keeps the city busy.
// The streets are a 6 x 6 grid of crossroads. Cars drive on the right side of the road.
public class TrafficManager : MonoBehaviour
{
	public static TrafficManager Instance { get; private set; }

	public GameObject carPrefab;
	public Material[] carColors;
	public Material taxiColor;
	[Range(0, 1)] public float taxiChance = 0.35f;

	public int carCount = 18;
	public float cruiseSpeed = 13f;     // m/s (about 47 km/h)
	public float laneOffset = 2.3f;     // distance from the middle of the street
	public int gridSizeX = 8;           // crossroads west to east
	public int gridSizeZ = 7;           // crossroads south to north
	public float gridStart = -203f;
	public float gridStep = 58f;

	public readonly List<TrafficDriver> Drivers = new List<TrafficDriver>();

	static readonly Vector2Int[] Dirs = { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0) };   // N, E, S, W

	void Awake()
	{
		Instance = this;
	}

	void Start()
	{
		Vector3 start = TosakanaGame.Instance != null ? TosakanaGame.Instance.player.transform.position : Vector3.zero;
		for (int i = 0; i < carCount; i++)
			Spawn(start, 30f);
	}

	void Update()
	{
		// When you take a car, a new one comes somewhere you can't see
		Drivers.RemoveAll(d => d == null || !d.enabled);
		if (Drivers.Count < carCount && carPrefab != null)
		{
			Vector3 focus = TosakanaGame.Instance != null ? TosakanaGame.Instance.Focus.position : Vector3.zero;
			Spawn(focus, 70f);
		}
	}

	public int ActiveCount { get { return Drivers.FindAll(d => d != null && d.enabled).Count; } }

	public Vector3 Node(int a, int b) { return new Vector3(gridStart + a * gridStep, 0, gridStart + b * gridStep); }
	public static Vector2Int Dir(int heading) { return Dirs[(heading % 4 + 4) % 4]; }
	public static Vector3 Dir3(int heading) { Vector2Int d = Dir(heading); return new Vector3(d.x, 0, d.y); }
	public static Vector3 Right(int heading) { Vector3 d = Dir3(heading); return new Vector3(d.z, 0, -d.x); }
	public Vector3 ClampToGrid(Vector3 p)
	{
		float maxX = gridStart + (gridSizeX - 1) * gridStep, maxZ = gridStart + (gridSizeZ - 1) * gridStep;
		return new Vector3(Mathf.Clamp(p.x, gridStart, maxX), 0, Mathf.Clamp(p.z, gridStart, maxZ));
	}

	public bool InsideGrid(Vector3 p)
	{
		float maxX = gridStart + (gridSizeX - 1) * gridStep, maxZ = gridStart + (gridSizeZ - 1) * gridStep;
		return p.x > gridStart - 10f && p.x < maxX + 10f && p.z > gridStart - 10f && p.z < maxZ + 10f;
	}

	public bool Valid(int a, int b) { return a >= 0 && b >= 0 && a < gridSizeX && b < gridSizeZ; }

	// A point in the right lane, "t" meters after crossroad (a, b), going "heading"
	public Vector3 LanePoint(int a, int b, int heading, float t)
	{
		return Node(a, b) + Dir3(heading) * t + Right(heading) * laneOffset;
	}

	public bool FindSpot(Vector3 awayFrom, float minDistance, out int a, out int b, out int h, out float t)
	{
		return FindSpot(awayFrom, minDistance, float.MaxValue, out a, out b, out h, out t);
	}

	// A free spot in a lane, between minDistance and maxDistance from "awayFrom"
	public bool FindSpot(Vector3 awayFrom, float minDistance, float maxDistance, out int a, out int b, out int h, out float t)
	{
		for (int tries = 0; tries < 80; tries++)
		{
			a = Random.Range(0, gridSizeX);
			b = Random.Range(0, gridSizeZ);
			h = Random.Range(0, 4);
			t = Random.Range(10f, gridStep - 12f);
			Vector2Int d = Dir(h);
			if (!Valid(a + d.x, b + d.y))
				continue;
			Vector3 p = LanePoint(a, b, h, t);
			float away = Vector3.Distance(p, awayFrom);
			if (away < minDistance || away > maxDistance)
				continue;
			bool free = true;
			foreach (CarController c in CarController.All)
				if (Vector3.Distance(c.transform.position, p) < 10f)
					free = false;
			if (free)
				return true;
		}
		a = b = h = 0;
		t = 0;
		return false;
	}

	public TrafficDriver Spawn(Vector3 awayFrom, float minDistance)
	{
		int a, b, h;
		float t;
		if (carPrefab == null || !FindSpot(awayFrom, minDistance, out a, out b, out h, out t))
			return null;
		GameObject go = Instantiate(carPrefab, LanePoint(a, b, h, t), Quaternion.Euler(0, h * 90f, 0));
		Paint(go);
		TrafficDriver driver = go.AddComponent<TrafficDriver>();
		driver.Init(this, a, b, h);
		Drivers.Add(driver);
		return driver;
	}

	// Moves a lost or stuck car to a new street far from you
	public void Respawn(TrafficDriver driver)
	{
		int a, b, h;
		float t;
		Vector3 focus = TosakanaGame.Instance != null ? TosakanaGame.Instance.Focus.position : Vector3.zero;
		if (FindSpot(focus, 70f, out a, out b, out h, out t))
			driver.PlaceAt(a, b, h, t);
	}

	void Paint(GameObject car)
	{
		bool taxi = Random.value < taxiChance;
		Material body = taxi || carColors == null || carColors.Length == 0 ? taxiColor : carColors[Random.Range(0, carColors.Length)];
		foreach (Renderer r in car.GetComponentsInChildren<Renderer>(true))
		{
			if (r.name == "Body" || r.name == "Cabin")
				r.sharedMaterial = body;
			else if (r.name == "Stripe" || r.name == "Taxi Sign")
				r.gameObject.SetActive(taxi);
		}
		car.name = taxi ? "Taxi (traffic)" : "Car (traffic)";
	}
}
