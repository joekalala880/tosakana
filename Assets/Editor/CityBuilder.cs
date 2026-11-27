using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Tools > Tosakana > Build City: makes the materials, the Kinshasa city, the cars, the player and the UI.
// The city is a 13 x 6 grid of blocks with streets between them, the Fleuve Congo in the north,
// and N'djili airport in the east at the end of Boulevard Lumumba.
// Every block belongs to a commune of Kinshasa (see Communes below).
public static class CityBuilder
{
	const int BlocksX = 13;                   // west to east (Kintambo ... Kingasani)
	const int BlocksZ = 6;                    // south to north (the river)
	const float Pitch = 58f;                  // block + street
	const float RoadWidth = 14f;
	const float BlockSize = 44f;
	const float FirstBlock = -174f;           // middle of the first block (west and south)
	const float FirstRoad = FirstBlock - Pitch / 2f;   // -203 = middle of the outside streets (west and south)
	const float NorthRoad = FirstRoad + BlocksZ * Pitch;   // 145 = the street along the river
	const float Inner = -FirstRoad + RoadWidth / 2f;   // 210 = edge of the city (west and south)
	const float CityEast = FirstRoad + BlocksX * Pitch + RoadWidth / 2f;   // 558 = east edge of the streets
	const float AirportStart = 560f;          // N'djili airport, east of the city
	const float AirportEnd = 1150f;
	const float WorldMidX = (AirportEnd - Inner) / 2f;   // 470
	const float Boulevard = 29f;              // z of Boulevard du 30 Juin
	const float RiverZ = 160f;
	const int MapLayer = 31;
	static readonly Vector3 PlayerStart = new Vector3(8f, 0.05f, -22.6f);
	static readonly Vector3 TaxiStart = new Vector3(17f, 0f, -24.3f);   // parked at the curb, out of the traffic lane

	// The communes of Kinshasa, one row per block row from south (Mont-Ngafula) to north (the river), west to east.
	// Roughly where they really are: Gombe by the river, Kintambo in the west, Limete, Masina and N'djili going east
	// toward the airport, Mont-Ngafula in the hills, Kimbanseke and Kingasani in the south-east.
	public static readonly string[] Communes =
	{
		"Mont-Ngafula, Mont-Ngafula, Mont-Ngafula, Mont-Ngafula, Mont-Ngafula, Mont-Ngafula, Mont-Ngafula, Mont-Ngafula, Kimbanseke, Kimbanseke, Kimbanseke, Kingasani, Kingasani",
		"Selembao, Selembao, Bumbu, Kalamu, Lemba, Lemba, Lemba, Lemba, Kimbanseke, Kimbanseke, Kingasani, Kingasani, Kingasani",
		"Bandal, Bumbu, Kalamu, Kalamu, Matonge, Lemba, Limete, Matete, Matete, Kimbanseke, Kimbanseke, Kingasani, Kingasani",
		"Bandal, Ngiri-Ngiri, Kalamu, Kalamu, Matonge, Matonge, Limete, Matete, Matete, N'djili, N'djili, N'djili, N'djili",
		"Kintambo, Ngiri-Ngiri, Lingwala, Lingwala, Gombe, Gombe, Limete, Limete, Matete, Masina, Masina, N'djili, N'djili",
		"Kintambo, Gombe, Gombe, Gombe, Gombe, Gombe, Limete, Limete, Masina, Masina, Masina, Masina, N'djili",
	};

	static string CommuneOf(int i, int j) { return Communes[j].Split(',')[i].Trim(); }

	static Font font;
	static System.Random rng;
	static Transform city;
	static List<Vector3> markerSpots = new List<Vector3>();
	static int buildingCount;

	static Material asphalt, sidewalk, dash, grass, sand, water, concrete, roofMat, metal, darkMetal, trunk, leaf, white, black,
		glass, wood, signBlue, signGreen, tire, hub, red, yellow, blue, skin, shirt, pants, hair, markerMat, beamMat,
		mapPlayer, mapTarget, flagMat, lightMat, tailMat, fieldMat, farBank;
	static Material[] houseMats, brightMats, towerMats, carMats, stallMats, fruitMats;
	static PhysicMaterial slippery;
	static Material sirenRed, sirenBlue, policeWhite, rust;
	static Texture2D starTexture;

	[MenuItem("Tools/Tosakana/Build City")]
	public static void Build()
	{
		rng = new System.Random(243);   // 243 = Congo's phone code. Same city every time.
		markerSpots.Clear();
		buildingCount = 0;
		font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		Folder("Assets", "Materials");
		Folder("Assets", "Textures");
		Folder("Assets", "Scenes");
		Folder("Assets", "Prefabs");
		MakeMaterials();

		var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
		SetupLight();
		city = new GameObject("City").transform;

		BuildGround();
		BuildRoads();
		List<Place> places = new List<Place>();
		for (int i = 0; i < BlocksX; i++)
			for (int j = 0; j < BlocksZ; j++)
				BuildBlock(i, j, places);
		BuildBoulevard();
		BuildRiver(places);
		BuildAirport(places);
		BuildOutside();

		// Cars
		Transform cars = new GameObject("Cars").transform;
		MakeCar(cars, "Taxi (yours)", TaxiStart, 90f, true, yellow);
		ParkCars(cars, 45);

		// Roulage officers at busy crossroads (4 on Boulevard du 30 Juin, 1 near the Grand Marché)
		Transform roulage = new GameObject("Roulage").transform;
		foreach (Vector2Int n in new[] { new Vector2Int(2, 4), new Vector2Int(3, 4), new Vector2Int(4, 4), new Vector2Int(5, 4), new Vector2Int(3, 2), new Vector2Int(9, 4) })
			MakeOfficer(roulage, n.x, n.y);

		PlayerController player = MakePlayer();

		// Game manager
		GameObject gameGo = new GameObject("Game");
		TosakanaGame game = gameGo.AddComponent<TosakanaGame>();
		MissionManager missions = gameGo.AddComponent<MissionManager>();
		game.sound = gameGo.AddComponent<GameSound>();

		// Traffic and people (Version 2)
		TrafficManager traffic = gameGo.AddComponent<TrafficManager>();
		GameObject carTemplate = MakeCar(null, "Traffic Car", Vector3.zero, 0f, true, yellow).gameObject;
		traffic.carPrefab = SavePrefab(carTemplate, "Assets/Prefabs/TrafficCar.prefab");
		traffic.carColors = carMats;
		traffic.taxiColor = yellow;
		traffic.carCount = 36;
		traffic.gridSizeX = BlocksX + 1;
		traffic.gridSizeZ = BlocksZ + 1;
		traffic.gridStart = FirstRoad;
		PedestrianManager people = gameGo.AddComponent<PedestrianManager>();
		people.pedestrianPrefab = SavePrefab(MakePedestrian(), "Assets/Prefabs/Pedestrian.prefab");
		people.count = 110;
		people.blocksX = BlocksX;
		people.blocksZ = BlocksZ;
		people.firstBlock = FirstBlock;

		// Police and wanted stars (Version 3)
		PoliceManager police = gameGo.AddComponent<PoliceManager>();
		police.policePrefab = SavePrefab(MakePoliceCar(), "Assets/Prefabs/PoliceCar.prefab");
		police.patrolCars = 4;
		police.officerPrefab = SavePrefab(MakePoliceFoot(), "Assets/Prefabs/PoliceOfficer.prefab");
		MiniMap map = gameGo.AddComponent<MiniMap>();
		game.player = player;
		game.missions = missions;
		missions.places = places.ToArray();
		missions.boulevardZ = Boulevard;
		missions.communeRows = Communes;
		missions.communeStart = FirstBlock;
		missions.communeStep = Pitch;

		// Camera
		Camera cam = Camera.main;
		cam.farClipPlane = 1500f;
		cam.fieldOfView = 60f;
		cam.cullingMask = ~(1 << MapLayer);
		cam.transform.position = new Vector3(0, 80, -165);
		game.cameraRig = cam.gameObject.AddComponent<CameraRig>();
		game.cameraRig.target = player.transform;

		MakeMarker(missions);
		RenderTexture mapTexture = MakeMiniMap(map);
		BuildUI(game, missions, mapTexture);

		EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");
		EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true) };
		PlayerSettings.productName = "Tosakana";
		PlayerSettings.companyName = "Joel Kalala";
		AssetDatabase.SaveAssets();
		Debug.Log("TOSAKANA-BUILD done: " + buildingCount + " buildings, " + places.Count + " places");
	}

	static Vector3 BlockCenter(int i, int j)
	{
		return new Vector3(FirstBlock + i * Pitch, 0, FirstBlock + j * Pitch);
	}

	static float Range(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
	static T Pick<T>(T[] list) { return list[rng.Next(list.Length)]; }

	// ---------- Materials ----------
	static void MakeMaterials()
	{
		Texture2D windows = SaveTexture(WindowTexture(), "Assets/Textures/Windows.asset");
		Texture2D dashTex = SaveTexture(DashTexture(), "Assets/Textures/RoadDash.asset");
		Texture2D noise = SaveTexture(NoiseTexture(), "Assets/Textures/Asphalt.asset");
		Texture2D waterTex = SaveTexture(WaterTexture(), "Assets/Textures/Water.asset");
		Texture2D flag = SaveTexture(FlagTexture(), "Assets/Textures/DRCFlag.asset");
		starTexture = SaveTexture(StarTexture(), "Assets/Textures/Star.asset");

		asphalt = Mat("Asphalt", new Color(0.36f, 0.36f, 0.38f), 0.1f);
		asphalt.mainTexture = noise;
		asphalt.mainTextureScale = new Vector2(280, 280);
		sidewalk = Mat("Sidewalk", new Color(0.72f, 0.7f, 0.66f), 0.1f);
		grass = Mat("Grass", new Color(0.33f, 0.55f, 0.22f), 0.05f);
		sand = Mat("Sand", new Color(0.78f, 0.68f, 0.5f), 0.05f);
		water = Mat("River", new Color(0.75f, 0.8f, 0.72f), 0.35f);
		water.mainTexture = waterTex;
		water.mainTextureScale = new Vector2(30, 20);
		concrete = Mat("Concrete", new Color(0.8f, 0.78f, 0.74f), 0.1f);
		roofMat = Mat("Roof", new Color(0.32f, 0.3f, 0.3f), 0.1f);
		rust = Mat("Tin Roof Rust", new Color(0.55f, 0.32f, 0.2f), 0.35f);
		metal = Mat("Metal", new Color(0.55f, 0.62f, 0.68f), 0.4f);
		darkMetal = Mat("DarkMetal", new Color(0.28f, 0.33f, 0.4f), 0.3f);
		trunk = Mat("PalmTrunk", new Color(0.5f, 0.38f, 0.25f), 0.1f);
		leaf = Mat("PalmLeaf", new Color(0.2f, 0.5f, 0.15f), 0.15f);
		white = Mat("White", Color.white, 0.3f);
		black = Mat("Black", new Color(0.06f, 0.06f, 0.06f), 0.3f);
		glass = Mat("CarGlass", new Color(0.12f, 0.17f, 0.22f), 0.9f);
		wood = Mat("Wood", new Color(0.5f, 0.33f, 0.18f), 0.15f);
		signBlue = Mat("SignBlue", new Color(0.05f, 0.3f, 0.65f), 0.3f);
		signGreen = Mat("SignGreen", new Color(0.05f, 0.45f, 0.25f), 0.3f);
		tire = Mat("Tire", new Color(0.08f, 0.08f, 0.08f), 0.2f);
		hub = Mat("Hubcap", new Color(0.7f, 0.7f, 0.72f), 0.7f);
		red = Mat("Red", new Color(0.8f, 0.12f, 0.1f), 0.4f);
		yellow = Mat("TaxiYellow", new Color(1f, 0.8f, 0.05f), 0.5f);
		blue = Mat("Blue", new Color(0.0f, 0.42f, 0.85f), 0.4f);
		skin = Mat("Skin", new Color(0.36f, 0.22f, 0.14f), 0.3f);
		shirt = Mat("Shirt", new Color(0.0f, 0.45f, 0.9f), 0.2f);
		pants = Mat("Pants", new Color(0.15f, 0.15f, 0.2f), 0.2f);
		hair = Mat("Hair", new Color(0.04f, 0.03f, 0.03f), 0.2f);
		fieldMat = Mat("Field", new Color(0.25f, 0.6f, 0.25f), 0.05f);
		farBank = Mat("FarBank", new Color(0.25f, 0.42f, 0.22f), 0.05f);
		lightMat = Unlit("Headlight", new Color(1f, 1f, 0.85f));
		tailMat = Unlit("Taillight", new Color(0.9f, 0.05f, 0.05f));
		mapPlayer = Unlit("MapPlayer", Color.white);
		mapTarget = Unlit("MapTarget", new Color(1f, 0.85f, 0.1f));
		sirenRed = Unlit("Siren Red", new Color(1f, 0.1f, 0.1f));
		sirenBlue = Unlit("Siren Blue", new Color(0.1f, 0.35f, 1f));
		policeWhite = Mat("Police White", new Color(0.95f, 0.95f, 0.97f), 0.6f);

		dash = new Material(Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse"));
		dash.color = new Color(1f, 0.95f, 0.7f);
		dash.mainTexture = dashTex;
		dash.mainTextureScale = new Vector2(1, 2f * Inner / 6f);
		dash = SaveMat(dash, "RoadDash");

		markerMat = Transparent("Marker", new Color(1f, 0.85f, 0.1f, 0.5f));
		beamMat = Transparent("MarkerBeam", new Color(1f, 0.85f, 0.1f, 0.22f));

		flagMat = Mat("DRCFlag", Color.white, 0.2f);
		flagMat.mainTexture = flag;

		houseMats = new[]
		{
			Building("House Cream", new Color(0.93f, 0.88f, 0.75f), windows),
			Building("House Sand", new Color(0.85f, 0.72f, 0.52f), windows),
			Building("House White", new Color(0.95f, 0.95f, 0.93f), windows),
			Building("House Terracotta", new Color(0.78f, 0.45f, 0.32f), windows),
			Building("House Pale Blue", new Color(0.65f, 0.8f, 0.9f), windows),
			Building("House Gray", new Color(0.7f, 0.7f, 0.7f), windows),
		};
		brightMats = new[]
		{
			Building("Bright Yellow", new Color(1f, 0.82f, 0.25f), windows),
			Building("Bright Pink", new Color(0.95f, 0.5f, 0.6f), windows),
			Building("Bright Mint", new Color(0.5f, 0.85f, 0.65f), windows),
			Building("Bright Blue", new Color(0.3f, 0.6f, 0.95f), windows),
			Building("Bright Orange", new Color(1f, 0.6f, 0.25f), windows),
			Building("Bright Purple", new Color(0.65f, 0.45f, 0.85f), windows),
		};
		towerMats = new[]
		{
			Building("Tower Glass Blue", new Color(0.45f, 0.6f, 0.75f), windows),
			Building("Tower Glass Green", new Color(0.45f, 0.65f, 0.6f), windows),
			Building("Tower White", new Color(0.9f, 0.9f, 0.88f), windows),
			Building("Tower Bronze", new Color(0.6f, 0.5f, 0.4f), windows),
		};
		carMats = new[]
		{
			Mat("Car White", new Color(0.92f, 0.92f, 0.92f), 0.6f),
			Mat("Car Red", new Color(0.75f, 0.08f, 0.08f), 0.6f),
			Mat("Car Black", new Color(0.08f, 0.08f, 0.1f), 0.7f),
			Mat("Car Silver", new Color(0.6f, 0.62f, 0.65f), 0.7f),
			Mat("Car Blue", new Color(0.1f, 0.25f, 0.6f), 0.6f),
			Mat("Car Green", new Color(0.15f, 0.4f, 0.2f), 0.6f),
		};
		stallMats = new[]
		{
			Mat("Stall Red", new Color(0.85f, 0.15f, 0.12f), 0.1f),
			Mat("Stall Yellow", new Color(1f, 0.8f, 0.1f), 0.1f),
			Mat("Stall Blue", new Color(0.1f, 0.4f, 0.85f), 0.1f),
			Mat("Stall Green", new Color(0.2f, 0.6f, 0.25f), 0.1f),
			Mat("Stall Orange", new Color(1f, 0.5f, 0.1f), 0.1f),
		};
		fruitMats = new[]
		{
			Mat("Fruit Orange", new Color(1f, 0.55f, 0.05f), 0.4f),
			Mat("Fruit Banana", new Color(1f, 0.88f, 0.2f), 0.4f),
			Mat("Fruit Green", new Color(0.35f, 0.7f, 0.15f), 0.4f),
			Mat("Fruit Red", new Color(0.8f, 0.1f, 0.1f), 0.4f),
			Mat("Cassava", new Color(0.55f, 0.38f, 0.22f), 0.2f),
		};

		slippery = SaveAsset(new PhysicMaterial("Slippery") { dynamicFriction = 0, staticFriction = 0, frictionCombine = PhysicMaterialCombine.Minimum, bounciness = 0 },
			"Assets/Materials/Slippery.physicMaterial");
	}

	static Material Building(string name, Color color, Texture2D windows)
	{
		Shader s = Shader.Find("Tosakana/Building");
		if (s == null)
			return Mat(name, color, 0.1f);
		Material m = new Material(s);
		m.color = color;
		m.mainTexture = windows;
		m.SetVector("_Tile", new Vector4(1f / 3.5f, 1f / 3.3f, 0, 0));
		return SaveMat(m, name);
	}

	static void SetupLight()
	{
		Light sun = Object.FindObjectOfType<Light>();
		sun.name = "Sun";
		sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
		sun.color = new Color(1f, 0.95f, 0.84f);
		sun.intensity = 1.15f;
		sun.shadows = LightShadows.Soft;
		sun.shadowStrength = 0.75f;
		// Soft fill light from the sky and the ground, so the shady side of buildings is not black
		RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
		RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.8f);
		RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.52f);
		RenderSettings.ambientGroundColor = new Color(0.36f, 0.33f, 0.28f);
		RenderSettings.reflectionIntensity = 0.5f;
		RenderSettings.fog = true;
		RenderSettings.fogMode = FogMode.Linear;
		RenderSettings.fogStartDistance = 200f;
		RenderSettings.fogEndDistance = 1100f;
		RenderSettings.fogColor = new Color(0.78f, 0.82f, 0.86f);
		QualitySettings.shadowDistance = 160f;
	}

	// ---------- Ground, streets, outside ----------
	static void BuildGround()
	{
		GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
		ground.name = "Ground (streets)";
		ground.transform.SetParent(city);
		ground.transform.localScale = new Vector3(280, 1, 280);   // 2800 m
		ground.transform.position = new Vector3(WorldMidX, 0, -100f);
		ground.GetComponent<Renderer>().sharedMaterial = asphalt;
		Static(ground);
	}

	static void BuildRoads()
	{
		Transform roads = new GameObject("Road Lines").transform;
		roads.SetParent(city);
		// (the parts past the edge hide under the grass, sand and water)
		for (int k = 0; k <= BlocksX; k++)
			Line(roads, new Vector3(FirstRoad + k * Pitch, 0.02f, 0), 0f, 2f * Inner);
		float westToEast = CityEast + Inner;
		for (int k = 0; k <= BlocksZ; k++)
			Line(roads, new Vector3((CityEast - Inner) / 2f, 0.025f, FirstRoad + k * Pitch), 90f, westToEast);
	}

	static void Line(Transform parent, Vector3 pos, float yaw, float length)
	{
		GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
		q.name = "Lane Line";
		Object.DestroyImmediate(q.GetComponent<Collider>());
		q.transform.SetParent(parent);
		q.transform.position = pos;
		q.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
		q.transform.localScale = new Vector3(0.3f, length, 1f);
		Renderer r = q.GetComponent<Renderer>();
		r.sharedMaterial = DashFor(length);
		r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		Static(q);
	}

	// The dashes must be 6 m apart, so each line length gets its own material
	static readonly Dictionary<int, Material> dashes = new Dictionary<int, Material>();
	static Material DashFor(float length)
	{
		int key = Mathf.RoundToInt(length);
		if (key == Mathf.RoundToInt(2f * Inner))
			return dash;
		Material m;
		if (!dashes.TryGetValue(key, out m) || m == null)
		{
			m = new Material(dash);
			m.mainTextureScale = new Vector2(1, length / 6f);
			m = SaveMat(m, "RoadDash " + key);
			dashes[key] = m;
		}
		return m;
	}

	static void BuildOutside()
	{
		Transform outside = new GameObject("Outside").transform;
		outside.SetParent(city);
		float west = -Inner, south = -Inner, east = AirportEnd, north = RiverZ;
		float sideLength = north - south + 300f;
		float sideCenter = (north + south - 300f) / 2f;
		// Grass around the city (not on the river side)
		Cube(outside, "Grass West", grass, new Vector3(west - 150, 0.05f, sideCenter), new Vector3(300, 0.1f, sideLength), false);
		Cube(outside, "Grass East", grass, new Vector3(east + 150, 0.05f, sideCenter), new Vector3(300, 0.1f, sideLength), false);
		Cube(outside, "Grass South", grass, new Vector3(WorldMidX, 0.05f, south - 150), new Vector3(east - west + 600, 0.1f, 300), false);

		// Walls you can't see, so you stay in the city
		float wallLength = north - south + 20f;
		float wallCenter = (north + south) / 2f;
		Wall(outside, new Vector3(west - 3, 3, wallCenter), new Vector3(2, 6, wallLength));
		Wall(outside, new Vector3(east + 3, 3, wallCenter), new Vector3(2, 6, wallLength));
		Wall(outside, new Vector3(WorldMidX, 3, south - 3), new Vector3(east - west + 8, 6, 2));

		// Palm trees around the city
		for (float z = south + 10; z <= NorthRoad; z += 20)
		{
			Palm(outside, new Vector3(west - 8 - Range(0, 6), 0, z + Range(-4, 4)));
			Palm(outside, new Vector3(east + 8 + Range(0, 6), 0, z + Range(-4, 4)));
		}
		for (float x = west + 10; x <= east - 10; x += 22)
			Palm(outside, new Vector3(x + Range(-4, 4), 0, south - 8 - Range(0, 6)));
	}

	static void Wall(Transform parent, Vector3 pos, Vector3 size)
	{
		GameObject w = Cube(parent, "Invisible Wall", concrete, pos, size, true);
		Object.DestroyImmediate(w.GetComponent<MeshRenderer>());
		Object.DestroyImmediate(w.GetComponent<MeshFilter>());
	}

	// ---------- Blocks ----------
	static void BuildBlock(int i, int j, List<Place> places)
	{
		Vector3 c = BlockCenter(i, j);
		string commune = CommuneOf(i, j);
		Transform block = new GameObject("Block " + i + "-" + j + " (" + commune + ")").transform;
		block.SetParent(city);
		block.position = c;
		Cube(block, "Sidewalk", sidewalk, c + Vector3.up * 0.06f, new Vector3(BlockSize, 0.12f, BlockSize), false);

		Vector3 north = c + new Vector3(0, 0, Pitch / 2f);
		Vector3 south = c - new Vector3(0, 0, Pitch / 2f);
		Vector3 c0 = new Vector3(c.x, 0, c.z);

		// ---- Famous places ----
		if (c0 == new Vector3(0, 0, 0))
		{
			Plaza(block, c);
			places.Add(MakePlace("Place du 30 Juin", c, 30f, south));
			return;
		}
		if (c0 == new Vector3(-58, 0, -58))
		{
			Market(block, c);
			places.Add(MakePlace("Grand Marché", c, 32f, north));
			return;
		}
		if (c0 == new Vector3(116, 0, -116))
		{
			Stadium(block, c);
			places.Add(MakePlace("Stade des Martyrs", c, 35f, north));
			return;
		}
		if (j == BlocksZ - 1 && (c.x == -116 || c.x == -58))
		{
			Warehouses(block, c);
			if (c.x == -58)
			{
				Sign(block, "PORT DE NGOBILA", c + new Vector3(0, 0, 21f), 0f, signBlue);
				places.Add(MakePlace("Port de Ngobila", new Vector3(-87, 0, 128), 45f, north));
			}
			return;
		}

		// ---- Each commune has its own look ----
		switch (commune)
		{
			case "Gombe":
				if (c.x >= 58)
					Lots(block, c, 2, 28f, 70f, towerMats, true);   // the towers
				else
					Lots(block, c, 2, 14f, 32f, towerMats, true);
				break;
			case "Matonge":
				Lots(block, c, 4, 4f, 9f, brightMats, false);
				break;
			case "Bumbu":
			case "Selembao":
				Lots(block, c, 5, 2.8f, 5f, houseMats, false, rust);   // small houses, tin roofs
				break;
			case "Ngiri-Ngiri":
				Lots(block, c, 4, 3.5f, 8f, rng.NextDouble() < 0.5 ? brightMats : houseMats, false, rust);
				break;
			case "Limete":
				if (rng.NextDouble() < 0.5)
					Warehouses(block, c);
				else
					Lots(block, c, 2, 5f, 14f, houseMats, false);
				break;
			case "Mont-Ngafula":
				Hills(block, c);
				break;
			case "Masina":
				if (rng.NextDouble() < 0.25)
					Warehouses(block, c);
				else
					Lots(block, c, 5, 2.8f, 5f, houseMats, false, rust);
				break;
			case "Matete":
				Lots(block, c, 4, 3.5f, 8f, rng.NextDouble() < 0.5 ? brightMats : houseMats, false);
				break;
			case "N'djili":
				Lots(block, c, 3, 4f, 10f, houseMats, false);
				break;
			case "Kimbanseke":
			case "Kingasani":
				Lots(block, c, 5, 2.6f, 4.8f, rng.NextDouble() < 0.3 ? brightMats : houseMats, false, rust);
				break;
			default:   // Kintambo, Lingwala, Bandal, Kalamu, Lemba
				Lots(block, c, rng.Next(2, 4), 4.5f, 8f + (i + j) * 1.6f, rng.NextDouble() < 0.4 ? brightMats : houseMats, false);
				break;
		}

		// ---- Mission places and "Bokomi malamu" signs, one per commune ----
		switch (c.x + "," + c.z)
		{
			case "58,58": CommunePlace(block, places, "Gombe", c, south); break;
			case "116,0": CommunePlace(block, places, "Matonge", c, south); break;
			case "58,-116": CommunePlace(block, places, "Lemba", c, north); break;
			case "-116,58": CommunePlace(block, places, "Ngiri-Ngiri", c, north); break;
			case "-174,0": CommunePlace(block, places, "Bandal", c, north); break;
			case "-58,-116": CommunePlace(block, places, "Bumbu", c, south); break;
			case "-174,-116": CommunePlace(block, places, "Selembao", c, north); break;
			case "0,-174": CommunePlace(block, places, "Mont-Ngafula", c, north); break;
			case "-174,116": CommunePlace(block, places, "Kintambo", c, south); break;
			case "174,58": CommunePlace(block, places, "Limete", c, south); break;
			case "-58,0": CommunePlace(block, places, "Kalamu", c, north); break;
			case "0,58": CommunePlace(block, places, "Lingwala", c, north); break;
			case "348,116": CommunePlace(block, places, "Masina", c, south); break;
			case "290,0": CommunePlace(block, places, "Matete", c, north); break;
			case "406,0": CommunePlace(block, places, "N'djili", c, north); break;
			case "348,-116": CommunePlace(block, places, "Kimbanseke", c, north); break;
			case "522,-116": CommunePlace(block, places, "Kingasani", c, north); break;
		}
	}

	// A mission place for a commune, with a big sign on the street side: "LEMBA — Bokomi malamu"
	static void CommunePlace(Transform block, List<Place> places, string name, Vector3 c, Vector3 marker)
	{
		bool northSide = marker.z > c.z;
		Sign(block, name.ToUpper(), c + new Vector3(0, 0, northSide ? 21f : -21f), northSide ? 0f : 180f, signGreen, "Bokomi malamu!  ·  Welcome!");
		places.Add(MakePlace(name, c, 0f, marker));
	}

	// Mont-Ngafula: green land, a few houses and mango trees
	static void Hills(Transform block, Vector3 c)
	{
		Cube(block, "Grass", grass, c + Vector3.up * 0.13f, new Vector3(BlockSize - 4, 0.14f, BlockSize - 4), false);
		for (int k = 0; k < 4; k++)
		{
			Vector3 p = c + new Vector3(Range(-13, 13), 0, Range(-13, 13));
			MakeBuilding(block, p, Range(5, 8), Range(5, 8), Range(3, 4.5f), Pick(houseMats), false, rust);
		}
		for (int k = 0; k < 7; k++)
			RoundTree(block, c + new Vector3(Range(-18, 18), 0, Range(-18, 18)));
	}

	// A round tree (like a mango tree)
	static void RoundTree(Transform parent, Vector3 pos)
	{
		Transform tree = new GameObject("Mango Tree").transform;
		tree.SetParent(parent);
		tree.position = pos;
		Shape(tree, PrimitiveType.Cylinder, "Trunk", trunk, pos + Vector3.up * 1.4f, new Vector3(0.45f, 1.4f, 0.45f), Vector3.zero, true);
		float size = Range(4f, 6f);
		Shape(tree, PrimitiveType.Sphere, "Leaves", leaf, pos + Vector3.up * (2.6f + size * 0.35f), new Vector3(size, size * 0.8f, size), Vector3.zero, false);
	}

	static Place MakePlace(string name, Vector3 center, float radius, Vector3 marker)
	{
		GameObject go = new GameObject("Place - " + name);
		go.transform.SetParent(city);
		go.transform.position = center;
		Place p = go.AddComponent<Place>();
		p.placeName = name;
		p.radius = radius;
		p.markerPosition = new Vector3(marker.x, 0, marker.z);
		markerSpots.Add(p.markerPosition);
		return p;
	}

	// Splits the block into n x n lots and puts a building on each
	static void Lots(Transform block, Vector3 c, int n, float minH, float maxH, Material[] mats, bool tower, Material roof = null)
	{
		float area = BlockSize - 4f;
		float lot = area / n;
		for (int a = 0; a < n; a++)
			for (int b = 0; b < n; b++)
			{
				if (!tower && n > 2 && rng.NextDouble() < 0.08)
					continue;   // an empty lot
				Vector3 lc = c + new Vector3(-area / 2f + lot * (a + 0.5f), 0, -area / 2f + lot * (b + 0.5f));
				float w = lot * Range(0.72f, 0.94f);
				float d = lot * Range(0.72f, 0.94f);
				float h = Range(minH, maxH);
				MakeBuilding(block, lc, w, d, h, Pick(mats), tower, roof);
			}
	}

	static void MakeBuilding(Transform block, Vector3 lc, float w, float d, float h, Material mat, bool tower, Material roof = null)
	{
		buildingCount++;
		Cube(block, "Building", mat, lc + Vector3.up * (h / 2f), new Vector3(w, h, d), true);
		Cube(block, "Roof", roof != null ? roof : roofMat, lc + Vector3.up * (h + 0.15f), new Vector3(w + 0.4f, 0.3f, d + 0.4f), false);
		if (tower && rng.NextDouble() < 0.5)
		{
			float top = h * 0.12f;
			Cube(block, "Tower Top", mat, lc + Vector3.up * (h + 0.3f + top / 2f), new Vector3(w * 0.6f, top, d * 0.6f), false);
		}
		else if (!tower && rng.NextDouble() < 0.4)
		{
			GameObject tank = Shape(block, PrimitiveType.Cylinder, "Water Tank", rng.NextDouble() < 0.5 ? darkMetal : black,
				lc + new Vector3(w * 0.25f, h + 1.1f, d * 0.25f), new Vector3(1.6f, 0.8f, 1.6f), Vector3.zero, false);
			tank.transform.SetParent(block);
		}
	}

	static void Plaza(Transform block, Vector3 c)
	{
		Cube(block, "Grass", grass, c + Vector3.up * 0.1f, new Vector3(BlockSize - 4, 0.2f, BlockSize - 4), false);
		Cube(block, "Path", concrete, c + Vector3.up * 0.11f, new Vector3(BlockSize - 4, 0.22f, 5f), false);
		Cube(block, "Path", concrete, c + Vector3.up * 0.11f, new Vector3(5f, 0.22f, BlockSize - 4), false);
		Shape(block, PrimitiveType.Cylinder, "Flag Base", concrete, c + Vector3.up * 0.5f, new Vector3(3f, 0.5f, 3f), Vector3.zero, true);

		// The DRC flag on a tall pole
		Shape(block, PrimitiveType.Cylinder, "Flag Pole", white, c + Vector3.up * 8f, new Vector3(0.25f, 8f, 0.25f), Vector3.zero, true);
		GameObject front = GameObject.CreatePrimitive(PrimitiveType.Quad);
		Object.DestroyImmediate(front.GetComponent<Collider>());
		front.name = "DRC Flag";
		front.transform.SetParent(block);
		front.transform.position = c + new Vector3(3.15f, 13.8f, 0);
		front.transform.localScale = new Vector3(6f, 4f, 1f);
		front.GetComponent<Renderer>().sharedMaterial = flagMat;
		GameObject back = Object.Instantiate(front, block);
		back.name = "DRC Flag (back)";
		back.transform.rotation = Quaternion.Euler(0, 180, 0);

		for (int k = 0; k < 8; k++)
		{
			float a = k * 45f * Mathf.Deg2Rad + 0.39f;
			Palm(block, c + new Vector3(Mathf.Sin(a) * 15f, 0, Mathf.Cos(a) * 15f));
		}
		Sign(block, "PLACE DU 30 JUIN", c + new Vector3(0, 0, -21f), 180f, signBlue);
	}

	static void Market(Transform block, Vector3 c)
	{
		for (int a = 0; a < 4; a++)
			for (int b = 0; b < 4; b++)
			{
				Vector3 p = c + new Vector3(-15f + a * 10f, 0, -15f + b * 10f);
				Transform stall = new GameObject("Stall").transform;
				stall.SetParent(block);
				stall.position = p;
				Material cloth = Pick(stallMats);
				foreach (Vector2 corner in new[] { new Vector2(-2.8f, -2.2f), new Vector2(2.8f, -2.2f), new Vector2(-2.8f, 2.2f), new Vector2(2.8f, 2.2f) })
					Shape(stall, PrimitiveType.Cylinder, "Pole", wood, p + new Vector3(corner.x, 1.25f, corner.y), new Vector3(0.12f, 1.25f, 0.12f), Vector3.zero, false);
				Shape(stall, PrimitiveType.Cube, "Canopy", cloth, p + new Vector3(0, 2.6f, 0), new Vector3(6.4f, 0.12f, 5.2f), new Vector3(6f, 0, 0), false);
				Shape(stall, PrimitiveType.Cube, "Table", wood, p + new Vector3(0, 0.45f, 0), new Vector3(5f, 0.9f, 2f), Vector3.zero, true);
				for (int f = 0; f < 6; f++)
				{
					PrimitiveType t = rng.NextDouble() < 0.6 ? PrimitiveType.Sphere : PrimitiveType.Cube;
					Shape(stall, t, "Goods", Pick(fruitMats), p + new Vector3(-2f + f * 0.8f, 1.1f, Range(-0.4f, 0.4f)), Vector3.one * Range(0.35f, 0.55f), Vector3.zero, false);
				}
			}
		Sign(block, "GRAND MARCHÉ", c + new Vector3(0, 0, 21f), 0f, signBlue);
	}

	static void Stadium(Transform block, Vector3 c)
	{
		Cube(block, "Field", fieldMat, c + Vector3.up * 0.1f, new Vector3(26f, 0.2f, 16f), false);
		Cube(block, "Center Line", white, c + Vector3.up * 0.21f, new Vector3(0.3f, 0.02f, 16f), false);
		const int segments = 20;
		for (int k = 0; k < segments; k++)
		{
			float a = k * 360f / segments;
			Vector3 dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
			Shape(block, PrimitiveType.Cube, "Stand", concrete, c + dir * 18.5f + Vector3.up * 6f, new Vector3(5.9f, 12f, 2.5f), new Vector3(0, a, 0), true);
			Shape(block, PrimitiveType.Cube, "Stand Top", k % 2 == 0 ? blue : red, c + dir * 18.5f + Vector3.up * 12.5f, new Vector3(6f, 1f, 2.7f), new Vector3(0, a, 0), false);
		}
		buildingCount++;
		Sign(block, "STADE DES MARTYRS", c + new Vector3(0, 0, 21.2f), 0f, signBlue);
	}

	static void Warehouses(Transform block, Vector3 c)
	{
		foreach (float z in new[] { -10f, 10f })
		{
			buildingCount++;
			Cube(block, "Warehouse", rng.NextDouble() < 0.5 ? metal : darkMetal, c + new Vector3(0, 4f, z), new Vector3(36f, 8f, 16f), true);
			Cube(block, "Warehouse Roof", roofMat, c + new Vector3(0, 8.2f, z), new Vector3(37f, 0.4f, 17f), false);
			for (int k = -1; k <= 1; k++)
				Cube(block, "Door", black, c + new Vector3(k * 11f, 2.5f, z + 8.02f), new Vector3(5f, 5f, 0.1f), false);
		}
	}

	static void BuildBoulevard()
	{
		float[] boulevardSigns = { -116f, 0f, 116f };
		float[] lumumbaSigns = { 232f, 464f };
		float[] communeSigns = { -174f, -58f, 290f, 406f };   // commune signs stand on the boulevard here
		Transform b = new GameObject("Boulevard du 30 Juin").transform;
		b.SetParent(city);
		for (float x = -196; x <= CityEast - 12; x += 14)
		{
			bool crossing = false;
			for (int k = 0; k <= BlocksX; k++)
				if (Mathf.Abs(x - (FirstRoad + k * Pitch)) < 10f)
					crossing = true;
			if (crossing)
				continue;
			bool nearSign = false;
			foreach (float[] list in new[] { boulevardSigns, lumumbaSigns, communeSigns })
				foreach (float sx in list)
					if (Mathf.Abs(x - sx) < 9f)
						nearSign = true;
			if (!nearSign)
				Palm(b, new Vector3(x, 0, Boulevard - 8.3f));
			Palm(b, new Vector3(x + 7, 0, Boulevard + 8.3f));
		}
		foreach (float x in boulevardSigns)
			Sign(b, "Boulevard du 30 Juin", new Vector3(x, 0, Boulevard - 7.9f), 0f, signGreen);
		foreach (float x in lumumbaSigns)
			Sign(b, "Boulevard Lumumba", new Vector3(x, 0, Boulevard - 7.9f), 0f, signGreen);
	}

	static void BuildRiver(List<Place> places)
	{
		Transform river = new GameObject("Fleuve Congo").transform;
		river.SetParent(city);
		float bank = NorthRoad + RoadWidth / 2f;
		Cube(river, "River Bank", sand, new Vector3(WorldMidX, 0.04f, (bank + RiverZ) / 2f), new Vector3(2800, 0.08f, RiverZ - bank), false);
		Cube(river, "River Wall", concrete, new Vector3(WorldMidX, 0.55f, RiverZ + 0.3f), new Vector3(2800, 1.1f, 0.6f), true);
		Cube(river, "Water", water, new Vector3(WorldMidX, 0.02f, RiverZ + 500f), new Vector3(3400, 0.1f, 1000f), false);

		// Ngobila dock and pirogues
		Cube(river, "Dock", wood, new Vector3(-58, 0.35f, RiverZ + 13f), new Vector3(8f, 0.4f, 24f), false);
		for (int k = 0; k < 6; k++)
		{
			float x = -90 + k * 12 + Range(-3, 3);
			float z = RiverZ + 10 + Range(0, 25);
			Shape(river, PrimitiveType.Sphere, "Pirogue", wood, new Vector3(x, 0.15f, z), new Vector3(1.1f, 0.5f, 7.5f), new Vector3(0, Range(-25, 25), 0), false);
		}
		Cube(river, "Ferry", white, new Vector3(-35, 1.2f, RiverZ + 40f), new Vector3(9f, 2.4f, 28f), false);
		Cube(river, "Ferry Cabin", signBlue, new Vector3(-35, 3.4f, RiverZ + 34f), new Vector3(7f, 2f, 8f), false);

		// The other side of the river, far away
		Cube(river, "Far Bank", farBank, new Vector3(WorldMidX, 1.5f, RiverZ + 760f), new Vector3(3600, 3f, 200f), false);
		for (int k = 0; k < 60; k++)
		{
			float h = Range(4, 22);
			Cube(river, "Far Building", Pick(houseMats), new Vector3(Range(-900, 1800), h / 2f + 3f, RiverZ + Range(680, 760)), new Vector3(Range(8, 20), h, Range(8, 20)), false);
		}
		Sign(river, "FLEUVE CONGO", new Vector3(30, 0, RiverZ - 1.5f), 180f, signGreen);
	}

	// ---------- N'djili airport ----------
	static void BuildAirport(List<Place> places)
	{
		Transform ap = new GameObject("Aéroport de N'djili").transform;
		ap.SetParent(city);
		float midX = (AirportStart + AirportEnd) / 2f;
		Material runway = Mat("Runway", new Color(0.22f, 0.22f, 0.24f), 0.1f);
		Cube(ap, "Airport Grass", grass, new Vector3(midX, 0.05f, (RiverZ - Inner) / 2f - 4f), new Vector3(AirportEnd - CityEast, 0.1f, RiverZ + Inner - 8f), false);

		// The road from Boulevard Lumumba to the terminal
		Cube(ap, "Airport Road", runway, new Vector3((CityEast + 720f) / 2f, 0.11f, Boulevard), new Vector3(720f - CityEast, 0.04f, RoadWidth), false);
		Line(ap, new Vector3((CityEast + 720f) / 2f, 0.14f, Boulevard), 90f, 720f - CityEast);

		// Runway with a dashed middle line and white edges
		float rz = -95f, rx0 = 600f, rx1 = 1120f, rw = 46f;
		Cube(ap, "Runway", runway, new Vector3((rx0 + rx1) / 2f, 0.12f, rz), new Vector3(rx1 - rx0, 0.06f, rw), false);
		Line(ap, new Vector3((rx0 + rx1) / 2f, 0.16f, rz), 90f, rx1 - rx0 - 40f);
		foreach (float side in new[] { -1f, 1f })
			Cube(ap, "Runway Edge", white, new Vector3((rx0 + rx1) / 2f, 0.16f, rz + side * (rw / 2f - 1f)), new Vector3(rx1 - rx0, 0.02f, 0.6f), false);
		foreach (float endX in new[] { rx0 + 8f, rx1 - 8f })
			for (int k = 0; k < 8; k++)
				Cube(ap, "Runway Stripe", white, new Vector3(endX, 0.16f, rz - 17.5f + k * 5f), new Vector3(14f, 0.02f, 1.6f), false);

		// Taxiway and apron
		Cube(ap, "Taxiway", runway, new Vector3(760f, 0.11f, (rz + 20f) / 2f), new Vector3(24f, 0.05f, 20f - rz), false);
		Cube(ap, "Apron", Mat("Apron", new Color(0.5f, 0.5f, 0.49f), 0.1f), new Vector3(850f, 0.1f, 62f), new Vector3(320f, 0.06f, 96f), false);

		// Terminal with its big name
		Cube(ap, "Terminal", towerMats[0], new Vector3(850f, 7f, 126f), new Vector3(260f, 14f, 22f), true);
		Cube(ap, "Terminal Roof", roofMat, new Vector3(850f, 14.3f, 126f), new Vector3(264f, 0.6f, 26f), false);
		Cube(ap, "Terminal Canopy", white, new Vector3(850f, 5f, 112f), new Vector3(120f, 0.4f, 6f), false);
		Cube(ap, "Name Board", signBlue, new Vector3(850f, 10.5f, 114.75f), new Vector3(96f, 5f, 0.3f), false);
		WorldText(ap, "AÉROPORT INTERNATIONAL DE N'DJILI", new Vector3(850f, 10.5f, 114.55f), Quaternion.identity, 94f, 5f, 72, Color.white, 0.05f);

		// Control tower
		Shape(ap, PrimitiveType.Cylinder, "Control Tower", concrete, new Vector3(1040f, 18f, 122f), new Vector3(5f, 18f, 5f), Vector3.zero, true);
		Shape(ap, PrimitiveType.Cylinder, "Tower Cab", towerMats[0], new Vector3(1040f, 37.5f, 122f), new Vector3(10f, 2.5f, 10f), Vector3.zero, false);
		Shape(ap, PrimitiveType.Cylinder, "Tower Roof", roofMat, new Vector3(1040f, 40.3f, 122f), new Vector3(11f, 0.4f, 11f), Vector3.zero, false);

		// Hangars
		foreach (float hz in new[] { 0f, 50f })
		{
			Cube(ap, "Hangar", metal, new Vector3(1085f, 8f, hz), new Vector3(50f, 16f, 36f), true);
			Cube(ap, "Hangar Door", black, new Vector3(1059.9f, 6f, hz), new Vector3(0.2f, 12f, 30f), false);
		}

		// Planes waiting at the terminal, and one that takes off again and again
		// The 3 planes at the terminal: you can get in and fly them (E)
		foreach (float px in new[] { 760f, 850f, 940f })
			MakePlane(ap, new Vector3(px, 0.13f, 78f), 180f, true, true);
		GameObject flyer = MakePlane(ap, new Vector3(rx0 + 15f, 0.15f, rz), 90f, false);
		flyer.name = "Plane (takes off)";
		flyer.AddComponent<PlaneTakeoff>().runwayLength = rx1 - rx0 - 40f;

		Sign(ap, "AÉROPORT DE N'DJILI", new Vector3(CityEast + 6f, 0, Boulevard - 8.5f), 270f, signBlue, "Bokomi malamu!  ·  Welcome!");
		places.Add(MakePlace("Aéroport de N'djili", new Vector3(850f, 0, -20f), 320f, new Vector3(850f, 0, 38f)));
	}

	// A plane: white body with a blue and yellow stripe, wings, engines and a blue tail
	static GameObject MakePlane(Transform parent, Vector3 pos, float yaw, bool solid, bool flyable = false)
	{
		GameObject plane = new GameObject(flyable ? "Plane (you can fly)" : "Plane");
		plane.transform.SetParent(parent);
		if (flyable)
			plane.layer = 2;   // its own crash check and the camera look past it
		Material engineMat = metal;
		GameObject body = Part(plane, PrimitiveType.Cylinder, white, new Vector3(0, 3.2f, 0), new Vector3(4f, 17f, 4f));
		body.transform.localRotation = Quaternion.Euler(90, 0, 0);
		Part(plane, PrimitiveType.Sphere, white, new Vector3(0, 3.2f, 17f), new Vector3(4f, 4f, 6f));
		Part(plane, PrimitiveType.Sphere, white, new Vector3(0, 3.6f, -17f), new Vector3(3.6f, 3.6f, 8f));
		Part(plane, PrimitiveType.Cube, glass, new Vector3(0, 3.9f, 1f), new Vector3(4.06f, 0.5f, 26f));
		Part(plane, PrimitiveType.Cube, blue, new Vector3(0, 2.55f, 0), new Vector3(4.08f, 0.55f, 31f));
		Part(plane, PrimitiveType.Cube, yellow, new Vector3(0, 2.15f, 0), new Vector3(4.1f, 0.22f, 31f));
		Part(plane, PrimitiveType.Cube, white, new Vector3(0, 2.4f, 1f), new Vector3(34f, 0.5f, 6.5f));
		foreach (float x in new[] { -7f, 7f })
		{
			GameObject engine = Part(plane, PrimitiveType.Cylinder, engineMat, new Vector3(x, 1.4f, 3f), new Vector3(1.7f, 2.2f, 1.7f));
			engine.transform.localRotation = Quaternion.Euler(90, 0, 0);
		}
		Part(plane, PrimitiveType.Cube, blue, new Vector3(0, 7.5f, -15f), new Vector3(0.5f, 7f, 6f));
		Part(plane, PrimitiveType.Cube, white, new Vector3(0, 4f, -15.5f), new Vector3(12f, 0.4f, 3.5f));
		foreach (Vector3 w in new[] { new Vector3(0, 0.7f, 12f), new Vector3(-2.5f, 0.7f, 0), new Vector3(2.5f, 0.7f, 0) })
		{
			GameObject wheel = Part(plane, PrimitiveType.Cylinder, tire, w, new Vector3(1.2f, 0.25f, 1.2f));
			wheel.transform.localRotation = Quaternion.Euler(0, 0, 90);
		}
		if (solid)
		{
			BoxCollider b1 = plane.AddComponent<BoxCollider>();
			b1.center = new Vector3(0, 3.2f, 0);
			b1.size = new Vector3(4f, 6.4f, 40f);
			BoxCollider b2 = plane.AddComponent<BoxCollider>();
			b2.center = new Vector3(0, 2.4f, 1f);
			b2.size = new Vector3(34f, 1f, 6.5f);
		}
		if (flyable)
		{
			Rigidbody rb = plane.AddComponent<Rigidbody>();
			rb.isKinematic = true;
			rb.useGravity = false;
			plane.AddComponent<PlaneController>();
		}
		plane.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
		return plane;
	}

	// A police officer on foot: dark blue uniform and a blue beret
	static GameObject MakePoliceFoot()
	{
		Material uniform = Mat("Police Uniform", new Color(0.1f, 0.13f, 0.3f), 0.2f);
		GameObject o = new GameObject("Police Officer");
		o.layer = 2;
		CapsuleCollider col = o.AddComponent<CapsuleCollider>();
		col.isTrigger = true;
		col.center = new Vector3(0, 0.9f, 0);
		col.height = 1.8f;
		col.radius = 0.35f;
		Rigidbody rb = o.AddComponent<Rigidbody>();
		rb.isKinematic = true;
		rb.useGravity = false;
		PoliceFoot cop = o.AddComponent<PoliceFoot>();
		Part(o, PrimitiveType.Capsule, uniform, new Vector3(0, 1.2f, 0), new Vector3(0.56f, 0.42f, 0.4f));
		Part(o, PrimitiveType.Sphere, skin, new Vector3(0, 1.72f, 0), Vector3.one * 0.32f);
		Part(o, PrimitiveType.Sphere, sirenBlue, new Vector3(0.03f, 1.86f, -0.01f), new Vector3(0.36f, 0.12f, 0.36f));
		Part(o, PrimitiveType.Cube, white, new Vector3(0.16f, 1.3f, 0.2f), new Vector3(0.1f, 0.1f, 0.02f));
		foreach (float x in new[] { -0.07f, 0.07f })
			Part(o, PrimitiveType.Sphere, black, new Vector3(x, 1.75f, 0.15f), Vector3.one * 0.045f);
		cop.leftLeg = Limb(o, "Left Leg", new Vector3(-0.12f, 0.86f, 0), new Vector3(0.17f, 0.86f, 0.19f), uniform);
		cop.rightLeg = Limb(o, "Right Leg", new Vector3(0.12f, 0.86f, 0), new Vector3(0.17f, 0.86f, 0.19f), uniform);
		cop.leftArm = Limb(o, "Left Arm", new Vector3(-0.34f, 1.42f, 0), new Vector3(0.13f, 0.62f, 0.15f), uniform);
		cop.rightArm = Limb(o, "Right Arm", new Vector3(0.34f, 1.42f, 0), new Vector3(0.13f, 0.62f, 0.15f), uniform);
		return o;
	}

	// ---------- Palm trees and signs ----------
	static void Palm(Transform parent, Vector3 pos)
	{
		Transform tree = new GameObject("Palm Tree").transform;
		tree.SetParent(parent);
		tree.position = pos;
		tree.rotation = Quaternion.Euler(0, Range(0, 360), 0);
		float lean = Range(0.1f, 0.35f);
		Vector3 top = Vector3.zero;
		for (int k = 0; k < 3; k++)
		{
			float r = 0.34f - k * 0.05f;
			Vector3 local = new Vector3(lean * k * k * 0.5f, 1.1f + k * 2.1f, 0);
			GameObject seg = Shape(tree, PrimitiveType.Cylinder, "Trunk", trunk, Vector3.zero, new Vector3(r, 1.1f, r), Vector3.zero, k == 0);
			seg.transform.localPosition = local;
			if (k == 0)
				seg.GetComponent<Collider>().isTrigger = false;
			top = local + Vector3.up * 1.1f;
		}
		for (int k = 0; k < 6; k++)
		{
			Transform pivot = new GameObject("Leaf").transform;
			pivot.SetParent(tree, false);
			pivot.localPosition = top;
			pivot.localRotation = Quaternion.Euler(22f + Range(-6, 6), k * 60f + Range(-10, 10), 0);
			GameObject l = Shape(pivot, PrimitiveType.Cube, "Leaf", leaf, Vector3.zero, new Vector3(0.9f, 0.07f, 3.2f), Vector3.zero, false);
			l.transform.localPosition = new Vector3(0, 0, 1.6f);
			l.transform.localRotation = Quaternion.identity;
		}
		Static(tree.gameObject);
	}

	// A sign on two poles. "yaw" turns it; 0 = you read it from the north (+z) side.
	static void Sign(Transform parent, string text, Vector3 pos, float yaw, Material board, string subtitle = null)
	{
		float width = Mathf.Max(1.2f + text.Length * 0.5f, subtitle != null ? 1.2f + subtitle.Length * 0.27f : 0f);
		float boardHeight = subtitle != null ? 2.1f : 1.4f;
		Transform sign = new GameObject("Sign " + text).transform;
		sign.SetParent(parent);
		sign.position = new Vector3(pos.x, 0, pos.z);
		sign.rotation = Quaternion.Euler(0, yaw, 0);
		foreach (float x in new[] { -width / 2f + 0.3f, width / 2f - 0.3f })
		{
			GameObject post = Shape(sign, PrimitiveType.Cylinder, "Post", darkMetal, Vector3.zero, new Vector3(0.15f, 1.4f, 0.15f), Vector3.zero, false);
			post.transform.localPosition = new Vector3(x, 1.4f, 0);
		}
		GameObject b = Shape(sign, PrimitiveType.Cube, "Board", board, Vector3.zero, new Vector3(width, boardHeight, 0.15f), Vector3.zero, false);
		b.transform.localPosition = new Vector3(0, 3.3f + (boardHeight - 1.4f) / 2f, 0);
		b.transform.localRotation = Quaternion.identity;
		float titleY = subtitle != null ? 3.95f : 3.3f;
		WorldText(sign, text, new Vector3(0, titleY, 0.09f), Quaternion.Euler(0, 180, 0), width, 1.4f, 80, Color.white);
		WorldText(sign, text, new Vector3(0, titleY, -0.09f), Quaternion.identity, width, 1.4f, 80, Color.white);
		if (subtitle != null)
		{
			Color gold = new Color(1f, 0.86f, 0.3f);
			WorldText(sign, subtitle, new Vector3(0, 3.15f, 0.09f), Quaternion.Euler(0, 180, 0), width, 0.7f, 42, gold);
			WorldText(sign, subtitle, new Vector3(0, 3.15f, -0.09f), Quaternion.identity, width, 0.7f, 42, gold);
		}
		Static(sign.gameObject);
	}

	// fontSize is in canvas units; "scale" is meters per unit (0.01 = 100 units per meter). Big signs use a bigger scale.
	static void WorldText(Transform parent, string value, Vector3 localPos, Quaternion localRot, float width, float height, int fontSize, Color color, float scale = 0.01f)
	{
		GameObject go = new GameObject("Text", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
		go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
		go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4;
		RectTransform rt = (RectTransform)go.transform;
		rt.SetParent(parent, false);
		rt.localPosition = localPos;
		rt.localRotation = localRot;
		rt.sizeDelta = new Vector2(width / scale, height / scale);
		rt.localScale = Vector3.one * scale;

		GameObject t = new GameObject("Label", typeof(RectTransform), typeof(Text));
		RectTransform trt = Stretch(t, rt);
		trt.localScale = Vector3.one;
		Text text = t.GetComponent<Text>();
		text.font = font;
		text.text = value;
		text.fontSize = fontSize;
		text.fontStyle = FontStyle.Bold;
		text.alignment = TextAnchor.MiddleCenter;
		text.color = color;
		text.horizontalOverflow = HorizontalWrapMode.Overflow;
		text.raycastTarget = false;
	}

	// ---------- Cars ----------
	static void ParkCars(Transform parent, int count)
	{
		List<Vector3> used = new List<Vector3> { TaxiStart };
		int tries = 0;
		while (count > 0 && tries++ < 500)
		{
			bool alongZ = rng.NextDouble() < 0.5;
			float line = alongZ ? FirstRoad + rng.Next(BlocksX + 1) * Pitch : FirstRoad + rng.Next(BlocksZ + 1) * Pitch;
			float along = alongZ ? FirstBlock + rng.Next(BlocksZ) * Pitch + Range(-14f, 14f) : FirstBlock + rng.Next(BlocksX) * Pitch + Range(-14f, 14f);
			float side = rng.NextDouble() < 0.5 ? -4.5f : 4.5f;
			Vector3 p = alongZ ? new Vector3(line + side, 0, along) : new Vector3(along, 0, line + side);
			float yaw = alongZ ? (side > 0 ? 0f : 180f) : (side > 0 ? 270f : 90f);

			bool ok = Vector3.Distance(p, TaxiStart) > 45f && p.z < NorthRoad - 1f;
			foreach (Vector3 u in used)
				if (Vector3.Distance(p, u) < 9f) ok = false;
			foreach (Vector3 m in markerSpots)
				if (Vector3.Distance(p, m) < 10f) ok = false;
			if (!ok)
				continue;
			used.Add(p);
			bool taxi = rng.NextDouble() < 0.3;
			MakeCar(parent, taxi ? "Taxi" : "Car", p, yaw, taxi, taxi ? yellow : Pick(carMats));
			count--;
		}
	}

	static CarController MakeCar(Transform parent, string name, Vector3 pos, float yaw, bool taxi, Material body)
	{
		GameObject car = new GameObject(name);
		if (parent != null)
			car.transform.SetParent(parent);
		car.layer = 2;   // cameras and the exit check ignore cars where needed
		Part(car, PrimitiveType.Cube, body, new Vector3(0, 0.75f, 0), new Vector3(2f, 0.75f, 4.4f)).name = "Body";
		Part(car, PrimitiveType.Cube, body, new Vector3(0, 1.45f, -0.25f), new Vector3(1.8f, 0.7f, 2.3f)).name = "Cabin";
		Part(car, PrimitiveType.Cube, glass, new Vector3(0, 1.42f, -0.25f), new Vector3(1.84f, 0.5f, 2.34f)).name = "Glass";
		if (taxi)
		{
			Part(car, PrimitiveType.Cube, blue, new Vector3(0, 0.82f, 0), new Vector3(2.02f, 0.16f, 4.42f)).name = "Stripe";
			Part(car, PrimitiveType.Cube, white, new Vector3(0, 1.88f, -0.25f), new Vector3(0.7f, 0.18f, 0.35f)).name = "Taxi Sign";
		}
		foreach (float x in new[] { -0.65f, 0.65f })
		{
			Part(car, PrimitiveType.Cube, lightMat, new Vector3(x, 0.85f, 2.2f), new Vector3(0.45f, 0.2f, 0.05f));
			Part(car, PrimitiveType.Cube, tailMat, new Vector3(x, 0.85f, -2.2f), new Vector3(0.45f, 0.18f, 0.05f));
		}

		List<Transform> spins = new List<Transform>();
		List<Transform> fronts = new List<Transform>();
		foreach (float x in new[] { -1f, 1f })
			foreach (float z in new[] { -1.4f, 1.4f })
			{
				Transform pivot = new GameObject("Wheel").transform;
				pivot.SetParent(car.transform, false);
				pivot.localPosition = new Vector3(x, 0.38f, z);
				Transform spinT = new GameObject("Spin").transform;
				spinT.SetParent(pivot, false);
				GameObject wheel = Part(spinT.gameObject, PrimitiveType.Cylinder, tire, Vector3.zero, new Vector3(0.76f, 0.14f, 0.76f));
				wheel.transform.localRotation = Quaternion.Euler(0, 0, 90);
				GameObject cap = Part(spinT.gameObject, PrimitiveType.Cylinder, hub, new Vector3(x * 0.03f, 0, 0), new Vector3(0.4f, 0.15f, 0.4f));
				cap.transform.localRotation = Quaternion.Euler(0, 0, 90);
				spins.Add(spinT);
				if (z > 0)
					fronts.Add(pivot);
			}

		BoxCollider box = car.AddComponent<BoxCollider>();
		box.center = new Vector3(0, 0.8f, 0);
		box.size = new Vector3(2f, 1.6f, 4.4f);
		box.sharedMaterial = slippery;
		Rigidbody rb = car.AddComponent<Rigidbody>();
		rb.mass = 1200f;
		rb.drag = 0f;
		rb.angularDrag = 0f;
		rb.interpolation = RigidbodyInterpolation.Interpolate;
		rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
		rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

		CarController cc = car.AddComponent<CarController>();
		cc.wheelSpins = spins.ToArray();
		cc.frontWheels = fronts.ToArray();
		car.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
		return cc;
	}

	// ---------- Player ----------
	static PlayerController MakePlayer()
	{
		GameObject p = new GameObject("Player (Joel)");
		p.layer = 2;
		CharacterController cc = p.AddComponent<CharacterController>();
		cc.center = new Vector3(0, 0.9f, 0);
		cc.height = 1.8f;
		cc.radius = 0.35f;
		cc.stepOffset = 0.35f;
		PlayerController pc = p.AddComponent<PlayerController>();
		p.AddComponent<PlayerCombat>();

		Part(p, PrimitiveType.Capsule, shirt, new Vector3(0, 1.2f, 0), new Vector3(0.55f, 0.42f, 0.38f));
		Part(p, PrimitiveType.Sphere, skin, new Vector3(0, 1.72f, 0), Vector3.one * 0.34f);
		Part(p, PrimitiveType.Sphere, hair, new Vector3(0, 1.82f, -0.02f), new Vector3(0.33f, 0.18f, 0.33f));
		foreach (float x in new[] { -0.07f, 0.07f })
			Part(p, PrimitiveType.Sphere, black, new Vector3(x, 1.75f, 0.155f), Vector3.one * 0.05f);

		pc.leftLeg = Limb(p, "Left Leg", new Vector3(-0.13f, 0.88f, 0), new Vector3(0.18f, 0.88f, 0.2f), pants);
		pc.rightLeg = Limb(p, "Right Leg", new Vector3(0.13f, 0.88f, 0), new Vector3(0.18f, 0.88f, 0.2f), pants);
		pc.leftArm = Limb(p, "Left Arm", new Vector3(-0.36f, 1.45f, 0), new Vector3(0.13f, 0.65f, 0.15f), skin);
		pc.rightArm = Limb(p, "Right Arm", new Vector3(0.36f, 1.45f, 0), new Vector3(0.13f, 0.65f, 0.15f), skin);

		p.transform.SetPositionAndRotation(PlayerStart, Quaternion.Euler(0, 90f, 0));
		return pc;
	}

	static Transform Limb(GameObject body, string name, Vector3 joint, Vector3 size, Material mat, string partName = "Cube")
	{
		Transform pivot = new GameObject(name).transform;
		pivot.SetParent(body.transform, false);
		pivot.localPosition = joint;
		Part(pivot.gameObject, PrimitiveType.Cube, mat, new Vector3(0, -size.y / 2f, 0), size).name = partName;
		return pivot;
	}

	// A person for the sidewalks. PedestrianManager picks their clothes when they appear.
	static GameObject MakePedestrian()
	{
		GameObject p = new GameObject("Pedestrian");
		p.layer = 2;
		CapsuleCollider col = p.AddComponent<CapsuleCollider>();
		col.isTrigger = true;
		col.center = new Vector3(0, 0.9f, 0);
		col.height = 1.8f;
		col.radius = 0.35f;
		Rigidbody rb = p.AddComponent<Rigidbody>();
		rb.isKinematic = true;
		rb.useGravity = false;
		Pedestrian ped = p.AddComponent<Pedestrian>();

		Part(p, PrimitiveType.Capsule, shirt, new Vector3(0, 1.2f, 0), new Vector3(0.5f, 0.4f, 0.36f)).name = "Shirt";
		Part(p, PrimitiveType.Sphere, skin, new Vector3(0, 1.72f, 0), Vector3.one * 0.32f).name = "Skin";
		Part(p, PrimitiveType.Sphere, hair, new Vector3(0, 1.81f, -0.02f), new Vector3(0.31f, 0.17f, 0.31f)).name = "Hair";
		Part(p, PrimitiveType.Sphere, shirt, new Vector3(0, 1.84f, -0.01f), new Vector3(0.38f, 0.24f, 0.38f)).name = "Scarf";
		Part(p, PrimitiveType.Cylinder, shirt, new Vector3(0, 0.62f, 0), new Vector3(0.46f, 0.36f, 0.4f)).name = "Wrap";
		Part(p, PrimitiveType.Cylinder, metal, new Vector3(0, 2.02f, 0), new Vector3(0.75f, 0.05f, 0.75f)).name = "Basin";
		for (int k = 0; k < 3; k++)
			Part(p, PrimitiveType.Sphere, fruitMats[k], new Vector3(-0.15f + k * 0.15f, 2.12f, (k % 2) * 0.1f - 0.05f), Vector3.one * 0.16f).name = "Goods";
		foreach (float x in new[] { -0.07f, 0.07f })
			Part(p, PrimitiveType.Sphere, black, new Vector3(x, 1.75f, 0.15f), Vector3.one * 0.045f).name = "Eye";

		ped.leftLeg = Limb(p, "Left Leg", new Vector3(-0.12f, 0.86f, 0), new Vector3(0.17f, 0.86f, 0.19f), pants, "Pants");
		ped.rightLeg = Limb(p, "Right Leg", new Vector3(0.12f, 0.86f, 0), new Vector3(0.17f, 0.86f, 0.19f), pants, "Pants");
		ped.leftArm = Limb(p, "Left Arm", new Vector3(-0.33f, 1.42f, 0), new Vector3(0.12f, 0.62f, 0.14f), skin, "Skin");
		ped.rightArm = Limb(p, "Right Arm", new Vector3(0.33f, 1.42f, 0), new Vector3(0.12f, 0.62f, 0.14f), skin, "Skin");
		return p;
	}

	// A white police car with a blue stripe, red and blue lights, a siren and a dot for the mini-map
	static GameObject MakePoliceCar()
	{
		GameObject car = MakeCar(null, "Police Car", Vector3.zero, 0f, true, policeWhite).gameObject;
		Object.DestroyImmediate(car.transform.Find("Taxi Sign").gameObject);
		Part(car, PrimitiveType.Cube, black, new Vector3(0, 1.85f, -0.1f), new Vector3(1.4f, 0.1f, 0.4f)).name = "Light Bar";
		GameObject red = Part(car, PrimitiveType.Cube, sirenRed, new Vector3(-0.36f, 1.98f, -0.1f), new Vector3(0.62f, 0.18f, 0.36f));
		red.name = "Red Light";
		GameObject blueLight = Part(car, PrimitiveType.Cube, sirenBlue, new Vector3(0.36f, 1.98f, -0.1f), new Vector3(0.62f, 0.18f, 0.36f));
		blueLight.name = "Blue Light";

		GameObject icon = Part(car, PrimitiveType.Sphere, sirenBlue, new Vector3(0, 149f, 0), Vector3.one * 9f);
		icon.name = "Map Icon";
		icon.layer = MapLayer;
		icon.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

		AudioSource siren = car.AddComponent<AudioSource>();
		siren.loop = true;
		siren.playOnAwake = false;
		siren.spatialBlend = 1f;
		siren.rolloffMode = AudioRolloffMode.Linear;
		siren.minDistance = 8f;
		siren.maxDistance = 160f;
		siren.volume = 0.55f;

		PoliceLights lights = car.AddComponent<PoliceLights>();
		lights.redLight = red.GetComponent<Renderer>();
		lights.blueLight = blueLight.GetComponent<Renderer>();
		lights.mapIcon = icon.GetComponent<Renderer>();
		lights.redMaterial = sirenRed;
		lights.blueMaterial = sirenBlue;
		lights.siren = siren;
		return car;
	}

	// A roulage officer: blue uniform, white cap, yellow vest, white gloves, on a white platform
	static void MakeOfficer(Transform parent, int a, int b)
	{
		Vector3 pos = new Vector3(FirstRoad + a * Pitch, 0, FirstRoad + b * Pitch);
		Material uniform = Mat("Roulage Blue", new Color(0.08f, 0.15f, 0.38f), 0.2f);
		Material vest = Mat("Roulage Vest", new Color(0.85f, 0.95f, 0.15f), 0.3f);
		GameObject platform = Shape(parent, PrimitiveType.Cylinder, "Roulage Platform", white, pos + Vector3.up * 0.06f, new Vector3(2.2f, 0.06f, 2.2f), Vector3.zero, false);
		platform.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

		GameObject o = new GameObject("Roulage Officer");
		o.layer = 2;
		o.transform.SetParent(parent);
		o.transform.position = pos + Vector3.up * 0.12f;
		CapsuleCollider col = o.AddComponent<CapsuleCollider>();
		col.isTrigger = true;
		col.center = new Vector3(0, 0.9f, 0);
		col.height = 1.8f;
		col.radius = 0.4f;
		Rigidbody rb = o.AddComponent<Rigidbody>();
		rb.isKinematic = true;
		rb.useGravity = false;
		TrafficOfficer officer = o.AddComponent<TrafficOfficer>();
		officer.nodeA = a;
		officer.nodeB = b;

		Part(o, PrimitiveType.Capsule, uniform, new Vector3(0, 1.2f, 0), new Vector3(0.55f, 0.42f, 0.38f));
		Part(o, PrimitiveType.Cube, vest, new Vector3(0, 1.22f, 0), new Vector3(0.58f, 0.5f, 0.42f));
		Part(o, PrimitiveType.Sphere, skin, new Vector3(0, 1.72f, 0), Vector3.one * 0.32f);
		Part(o, PrimitiveType.Cylinder, white, new Vector3(0, 1.88f, 0), new Vector3(0.36f, 0.06f, 0.36f));
		Part(o, PrimitiveType.Cube, black, new Vector3(0, 1.84f, 0.18f), new Vector3(0.3f, 0.03f, 0.14f));
		foreach (float x in new[] { -0.07f, 0.07f })
			Part(o, PrimitiveType.Sphere, black, new Vector3(x, 1.75f, 0.15f), Vector3.one * 0.045f);
		Limb(o, "Left Leg", new Vector3(-0.12f, 0.86f, 0), new Vector3(0.17f, 0.86f, 0.19f), uniform);
		Limb(o, "Right Leg", new Vector3(0.12f, 0.86f, 0), new Vector3(0.17f, 0.86f, 0.19f), uniform);
		officer.leftArm = Limb(o, "Left Arm", new Vector3(-0.34f, 1.42f, 0), new Vector3(0.13f, 0.62f, 0.15f), uniform);
		officer.rightArm = Limb(o, "Right Arm", new Vector3(0.34f, 1.42f, 0), new Vector3(0.13f, 0.62f, 0.15f), uniform);
		foreach (Transform arm in new[] { officer.leftArm, officer.rightArm })
			Part(arm.gameObject, PrimitiveType.Sphere, white, new Vector3(0, -0.66f, 0), Vector3.one * 0.15f).name = "Glove";
	}

	static GameObject SavePrefab(GameObject go, string path)
	{
		GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
		Object.DestroyImmediate(go);
		return prefab;
	}

	// ---------- Mission marker and mini-map ----------
	static void MakeMarker(MissionManager missions)
	{
		GameObject marker = new GameObject("Mission Marker");
		GameObject ring = Part(marker, PrimitiveType.Cylinder, markerMat, new Vector3(0, 0.06f, 0), new Vector3(9f, 0.05f, 9f));
		ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		GameObject beam = Part(marker, PrimitiveType.Cylinder, beamMat, new Vector3(0, 40f, 0), new Vector3(1.4f, 40f, 1.4f));
		beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

		GameObject label = new GameObject("Label");
		label.transform.SetParent(marker.transform, false);
		label.transform.localPosition = new Vector3(0, 6, 0);
		TextMesh tm = label.AddComponent<TextMesh>();
		tm.font = font;
		tm.fontSize = 80;
		tm.characterSize = 0.18f;
		tm.anchor = TextAnchor.MiddleCenter;
		tm.alignment = TextAlignment.Center;
		tm.fontStyle = FontStyle.Bold;
		tm.color = new Color(1f, 0.9f, 0.3f);
		tm.text = "Place";
		label.GetComponent<MeshRenderer>().sharedMaterial = font.material;

		missions.marker = marker.transform;
		missions.markerRing = ring.transform;
		missions.markerLabel = tm;
	}

	static RenderTexture MakeMiniMap(MiniMap map)
	{
		RenderTexture rt = new RenderTexture(512, 512, 16);
		rt.name = "MiniMap";
		rt = SaveAsset(rt, "Assets/Textures/MiniMap.renderTexture");

		GameObject camGo = new GameObject("Mini-Map Camera");
		Camera cam = camGo.AddComponent<Camera>();
		cam.orthographic = true;
		cam.orthographicSize = 85f;
		cam.clearFlags = CameraClearFlags.SolidColor;
		cam.backgroundColor = new Color(0.12f, 0.14f, 0.12f);
		cam.farClipPlane = 400f;
		cam.targetTexture = rt;
		cam.depth = -5;
		camGo.transform.SetPositionAndRotation(new Vector3(0, 220, 0), Quaternion.Euler(90, 0, 0));
		map.mapCam = cam;

		// Player arrow: two bars in a "V" that points forward
		GameObject arrow = new GameObject("Map Player Arrow");
		foreach (float side in new[] { -1f, 1f })
		{
			GameObject bar = Part(arrow, PrimitiveType.Cube, mapPlayer, new Vector3(side * 1.6f, 0, -1.2f), new Vector3(1.8f, 1f, 7f));
			bar.transform.localRotation = Quaternion.Euler(0, -side * 28f, 0);
		}
		SetLayer(arrow, MapLayer);
		map.playerIcon = arrow.transform;

		GameObject target = Part(new GameObject("Map Target"), PrimitiveType.Sphere, mapTarget, Vector3.zero, Vector3.one * 13f).transform.parent.gameObject;
		SetLayer(target, MapLayer);
		map.targetIcon = target.transform;
		return rt;
	}

	static void SetLayer(GameObject go, int layer)
	{
		go.layer = layer;
		foreach (Transform t in go.transform)
			SetLayer(t.gameObject, layer);
	}

	// ---------- UI ----------
	static void BuildUI(TosakanaGame game, MissionManager missions, RenderTexture mapTexture)
	{
		GameObject canvasGo = new GameObject("Canvas");
		Canvas canvas = canvasGo.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920, 1080);
		scaler.matchWidthOrHeight = 0.5f;
		canvasGo.AddComponent<GraphicRaycaster>();
		GameObject es = new GameObject("EventSystem");
		es.AddComponent<UnityEngine.EventSystems.EventSystem>();
		es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

		Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
		Color panel = new Color(0, 0, 0, 0.5f);
		Color gold = new Color(1f, 0.84f, 0.1f);

		// ----- HUD -----
		RectTransform hud = Stretch(new GameObject("HUD", typeof(RectTransform)), canvasGo.transform);
		game.hud = hud.gameObject;

		// Mission box (top left)
		RectTransform box = Rect(hud, "Mission Panel", new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(700, 190), panel, null);
		missions.titleText = Label(box, "Mission Title", "", 26, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -12), new Vector2(630, 34), gold, FontStyle.Bold);
		missions.missionText = Label(box, "Mission Text", "", 25, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -50), new Vector2(670, 140), Color.white, FontStyle.Normal);
		missions.missionText.horizontalOverflow = HorizontalWrapMode.Wrap;

		// Timer (top center), money (top right)
		missions.timerText = Label(hud, "Timer", "", 64, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(300, 80), Color.white, FontStyle.Bold);
		game.moneyText = Label(hud, "Money", "$0", 56, TextAnchor.MiddleRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -24), new Vector2(500, 70), new Color(0.45f, 0.95f, 0.45f), FontStyle.Bold);

		// Wanted stars (under the money) and why you are wanted
		PoliceManager police = game.GetComponent<PoliceManager>();
		police.starIcons = new RawImage[5];
		for (int i = 0; i < 5; i++)
		{
			GameObject star = new GameObject("Wanted Star " + (5 - i), typeof(RectTransform), typeof(RawImage), typeof(Shadow));
			RectTransform srt = (RectTransform)star.transform;
			srt.SetParent(hud, false);
			srt.anchorMin = srt.anchorMax = new Vector2(1, 1);
			srt.pivot = new Vector2(1, 1);
			srt.sizeDelta = new Vector2(48, 48);
			srt.anchoredPosition = new Vector2(-34 - (4 - i) * 54, -100);
			RawImage img = star.GetComponent<RawImage>();
			img.texture = starTexture;
			img.raycastTarget = false;
			img.color = new Color(1, 1, 1, 0.18f);
			star.GetComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
			police.starIcons[i] = img;
		}
		police.wantedText = Label(hud, "Wanted Reason", "", 26, TextAnchor.MiddleRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -156), new Vector2(700, 36), new Color(1f, 0.55f, 0.5f), FontStyle.Bold);

		// Big message (center)
		game.bigText = Label(hud, "Big Text", "", 84, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 190), new Vector2(1400, 110), gold, FontStyle.Bold);
		game.bigSubText = Label(hud, "Big Sub Text", "", 42, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(1400, 60), Color.white, FontStyle.Normal);

		// "Bokomi malamu na LEMBA" when you enter a commune (top center, under the timer)
		game.areaBanner = Label(hud, "Area Banner", "", 54, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(1400, 70), gold, FontStyle.Bold);
		game.areaBannerSub = Label(hud, "Area Banner Sub", "", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -182), new Vector2(1400, 40), Color.white, FontStyle.Italic);

		// Hint (bottom center)
		game.hintText = Label(hud, "Hint", "", 32, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(1000, 50), Color.white, FontStyle.Bold);

		// Area name and speed (bottom right)
		game.zoneText = Label(hud, "Area Name", "", 40, TextAnchor.MiddleRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-34, 120), new Vector2(700, 56), Color.white, FontStyle.BoldAndItalic);
		game.speedText = Label(hud, "Speed", "", 46, TextAnchor.MiddleRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-34, 60), new Vector2(400, 60), new Color(0.6f, 0.85f, 1f), FontStyle.Bold);

		// Health bar (above the mini-map) and a red flash when you get hit
		RectTransform healthBack = Rect(hud, "Health Bar", new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 362), new Vector2(300, 16), new Color(0, 0, 0, 0.6f), null);
		GameObject fillGo = new GameObject("Health Fill", typeof(RectTransform), typeof(Image));
		RectTransform fill = Stretch(fillGo, healthBack);
		fillGo.GetComponent<Image>().color = new Color(0.9f, 0.2f, 0.2f);
		fillGo.GetComponent<Image>().raycastTarget = false;
		RectTransform flash = Stretch(new GameObject("Hurt Flash", typeof(RectTransform), typeof(Image)), hud);
		flash.GetComponent<Image>().color = new Color(0.8f, 0, 0, 0);
		flash.GetComponent<Image>().raycastTarget = false;
		PlayerCombat combat = game.player.GetComponent<PlayerCombat>();
		combat.healthFill = fill;
		combat.damageFlash = flash.GetComponent<Image>();

		// Mini-map (bottom left), round
		RectTransform frame = Rect(hud, "Mini-Map Frame", new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 30), new Vector2(320, 320), new Color(0.05f, 0.05f, 0.05f, 0.85f), knob);
		RectTransform maskRt = Rect(frame, "Mini-Map Mask", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 300), Color.white, knob);
		maskRt.gameObject.AddComponent<Mask>().showMaskGraphic = false;
		GameObject mapImg = new GameObject("Mini-Map", typeof(RectTransform), typeof(RawImage));
		Stretch(mapImg, maskRt);
		mapImg.GetComponent<RawImage>().texture = mapTexture;
		mapImg.GetComponent<RawImage>().raycastTarget = false;

		// ----- Title screen -----
		RectTransform title = Stretch(new GameObject("Title Panel", typeof(RectTransform), typeof(Image)), canvasGo.transform);
		title.GetComponent<Image>().color = new Color(0, 0, 0, 0.55f);
		game.titlePanel = title.gameObject;

		Text logo = Label(title, "Logo", "TOSAKANA", 190, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(1600, 220), gold, FontStyle.Bold);
		Outline o = logo.GetComponent<Outline>();
		o.effectColor = new Color(0.8f, 0.05f, 0.05f, 1f);
		o.effectDistance = new Vector2(5, -5);
		// Flag colors under the logo
		Rect(title, "Stripe Blue", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-260, 128), new Vector2(170, 12), new Color(0f, 0.5f, 1f), null);
		Rect(title, "Stripe Yellow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-87, 128), new Vector2(170, 12), gold, null);
		Rect(title, "Stripe Red", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(87, 128), new Vector2(170, 12), new Color(0.85f, 0.1f, 0.1f), null);
		Rect(title, "Stripe Blue 2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(260, 128), new Vector2(170, 12), new Color(0f, 0.5f, 1f), null);

		Label(title, "Subtitle", "Kinshasa Streets", 56, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(1400, 70), Color.white, FontStyle.Bold);
		Label(title, "Lingala", "\"Tosakana\" means \"let's play\" in Lingala", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 15), new Vector2(1400, 40), new Color(1f, 0.95f, 0.8f), FontStyle.Italic);

		RectTransform controls = Rect(title, "Controls Box", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -130), new Vector2(980, 200), new Color(0, 0, 0, 0.55f), null);
		Label(controls, "Controls", "W A S D  –  walk / drive          Mouse  –  look around\n" +
			"Shift  –  run          Space  –  jump / brake\n" +
			"E  –  get in or out of a car          H  –  horn\n" +
			"Left click / R  –  punch          T  –  talk          Esc  –  free the mouse", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 190), Color.white, FontStyle.Normal);

		game.pressEnterText = Label(title, "Press Enter", "Press ENTER to start", 50, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -310), new Vector2(1000, 64), Color.white, FontStyle.Bold);
		Label(title, "Made By", "Made by " + game.author, 32, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -400), new Vector2(1000, 44), new Color(1f, 0.95f, 0.75f), FontStyle.Italic);
	}

	// pos = where the pivot sits, measured from the anchor
	static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color, Sprite sprite)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
		RectTransform rt = (RectTransform)go.transform;
		rt.SetParent(parent, false);
		rt.anchorMin = anchor;
		rt.anchorMax = anchor;
		rt.pivot = pivot;
		rt.sizeDelta = size;
		rt.anchoredPosition = pos;
		Image img = go.GetComponent<Image>();
		img.color = color;
		img.sprite = sprite;
		img.raycastTarget = false;
		return rt;
	}

	static Text Label(Transform parent, string name, string value, int size, TextAnchor align, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box, Color color, FontStyle style)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
		RectTransform rt = (RectTransform)go.transform;
		rt.SetParent(parent, false);
		rt.anchorMin = anchor;
		rt.anchorMax = anchor;
		rt.pivot = pivot;
		rt.sizeDelta = box;
		rt.anchoredPosition = pos;
		Text text = go.GetComponent<Text>();
		text.font = font;
		text.text = value;
		text.fontSize = size;
		text.fontStyle = style;
		text.alignment = align;
		text.color = color;
		text.supportRichText = true;
		text.horizontalOverflow = HorizontalWrapMode.Overflow;
		text.verticalOverflow = VerticalWrapMode.Overflow;
		text.raycastTarget = false;
		Outline outline = go.GetComponent<Outline>();
		outline.effectColor = new Color(0, 0, 0, 0.85f);
		outline.effectDistance = new Vector2(2, -2);
		return text;
	}

	static RectTransform Stretch(GameObject go, Transform parent)
	{
		RectTransform rt = (RectTransform)go.transform;
		rt.SetParent(parent, false);
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
		return rt;
	}

	// ---------- Shape helpers ----------

	// A box in world space. "solid" keeps its collider.
	static GameObject Cube(Transform parent, string name, Material mat, Vector3 pos, Vector3 size, bool solid)
	{
		GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
		go.name = name;
		go.transform.SetParent(parent);
		go.transform.position = pos;
		go.transform.localScale = size;
		go.GetComponent<Renderer>().sharedMaterial = mat;
		if (!solid)
			Object.DestroyImmediate(go.GetComponent<Collider>());
		Static(go);
		return go;
	}

	// Any shape in world space with a rotation
	static GameObject Shape(Transform parent, PrimitiveType type, string name, Material mat, Vector3 pos, Vector3 scale, Vector3 rot, bool solid)
	{
		GameObject go = GameObject.CreatePrimitive(type);
		go.name = name;
		go.transform.SetParent(parent);
		go.transform.position = pos;
		go.transform.rotation = Quaternion.Euler(rot);
		go.transform.localScale = scale;
		go.GetComponent<Renderer>().sharedMaterial = mat;
		if (!solid)
			Object.DestroyImmediate(go.GetComponent<Collider>());
		Static(go);
		return go;
	}

	// A shape inside a moving object (car, player). No collider.
	static GameObject Part(GameObject parent, PrimitiveType type, Material mat, Vector3 localPos, Vector3 scale)
	{
		GameObject go = GameObject.CreatePrimitive(type);
		go.name = type.ToString();
		go.transform.SetParent(parent.transform, false);
		go.transform.localPosition = localPos;
		go.transform.localScale = scale;
		go.GetComponent<Renderer>().sharedMaterial = mat;
		go.layer = parent.layer;
		Object.DestroyImmediate(go.GetComponent<Collider>());
		return go;
	}

	static void Static(GameObject go)
	{
		GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
	}

	static void Folder(string parent, string name)
	{
		if (!AssetDatabase.IsValidFolder(parent + "/" + name))
			AssetDatabase.CreateFolder(parent, name);
	}

	static Material Mat(string name, Color color, float smooth)
	{
		Material m = new Material(Shader.Find("Standard"));
		m.color = color;
		m.SetFloat("_Glossiness", smooth);
		return SaveMat(m, name);
	}

	static Material Unlit(string name, Color color)
	{
		Material m = new Material(Shader.Find("Unlit/Color"));
		m.color = color;
		return SaveMat(m, name);
	}

	static Material Transparent(string name, Color color)
	{
		Material m = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse"));
		m.color = color;
		return SaveMat(m, name);
	}

	// Updates the asset if it is already there (so its ID stays the same), or makes a new one
	static T SaveAsset<T>(T fresh, string path) where T : Object
	{
		T existing = AssetDatabase.LoadAssetAtPath<T>(path);
		if (existing == null)
		{
			AssetDatabase.CreateAsset(fresh, path);
			return fresh;
		}
		string name = existing.name;
		EditorUtility.CopySerialized(fresh, existing);
		existing.name = name;
		EditorUtility.SetDirty(existing);
		return existing;
	}

	static Material SaveMat(Material m, string name)
	{
		return SaveAsset(m, "Assets/Materials/" + name + ".mat");
	}

	static Texture2D SaveTexture(Texture2D tex, string path)
	{
		return SaveAsset(tex, path);
	}

	// ---------- Textures made in code ----------

	// One window per tile. Alpha 1 = window, alpha 0 = wall.
	static Texture2D WindowTexture()
	{
		const int s = 64;
		Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, true);
		t.wrapMode = TextureWrapMode.Repeat;
		for (int y = 0; y < s; y++)
			for (int x = 0; x < s; x++)
			{
				bool inside = x >= 12 && x < 52 && y >= 14 && y < 54;
				bool frame = inside && (x < 14 || x >= 50 || y < 16 || y >= 52 || x == 31 || x == 32);
				Color c = new Color(0, 0, 0, 0);
				if (frame)
					c = new Color(0.85f, 0.85f, 0.82f, 1f);
				else if (inside)
				{
					float g = (float)(y - 14) / 40f;
					c = Color.Lerp(new Color(0.16f, 0.22f, 0.3f), new Color(0.45f, 0.58f, 0.7f), g);
					c.a = 1f;
				}
				t.SetPixel(x, y, c);
			}
		t.Apply();
		return t;
	}

	static Texture2D DashTexture()
	{
		Texture2D t = new Texture2D(8, 64, TextureFormat.RGBA32, true);
		t.wrapMode = TextureWrapMode.Repeat;
		for (int y = 0; y < 64; y++)
			for (int x = 0; x < 8; x++)
				t.SetPixel(x, y, y < 30 ? Color.white : new Color(1, 1, 1, 0));
		t.Apply();
		return t;
	}

	static Texture2D NoiseTexture()
	{
		Texture2D t = new Texture2D(64, 64, TextureFormat.RGB24, true);
		t.wrapMode = TextureWrapMode.Repeat;
		System.Random r = new System.Random(1);
		for (int y = 0; y < 64; y++)
			for (int x = 0; x < 64; x++)
			{
				float v = 0.85f + (float)r.NextDouble() * 0.15f;
				t.SetPixel(x, y, new Color(v, v, v));
			}
		t.Apply();
		return t;
	}

	static Texture2D WaterTexture()
	{
		const int s = 128;
		Texture2D t = new Texture2D(s, s, TextureFormat.RGB24, true);
		t.wrapMode = TextureWrapMode.Repeat;
		for (int y = 0; y < s; y++)
			for (int x = 0; x < s; x++)
			{
				float wave = Mathf.Sin((x / (float)s) * Mathf.PI * 4f + Mathf.Sin(y / (float)s * Mathf.PI * 2f) * 2f);
				float v = 0.85f + wave * 0.08f;
				t.SetPixel(x, y, new Color(0.48f * v, 0.5f * v, 0.38f * v));
			}
		t.Apply();
		return t;
	}

	// DRC flag: sky blue, red band with yellow edges from bottom left to top right, yellow star top left
	static Texture2D FlagTexture()
	{
		const int w = 300, h = 200;
		Texture2D t = new Texture2D(w, h, TextureFormat.RGB24, true);
		Color sky = new Color(0f, 0.5f, 1f), yel = new Color(0.97f, 0.84f, 0.09f), redC = new Color(0.81f, 0.06f, 0.15f);
		float len = Mathf.Sqrt(w * w + h * h);
		Vector2 star = new Vector2(w * 0.17f, h * 0.76f);
		Vector2[] pts = new Vector2[10];
		for (int k = 0; k < 10; k++)
		{
			float a = Mathf.PI / 2f + k * Mathf.PI / 5f;
			float r = k % 2 == 0 ? h * 0.15f : h * 0.06f;
			pts[k] = star + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
		}
		for (int y = 0; y < h; y++)
			for (int x = 0; x < w; x++)
			{
				float d = Mathf.Abs(h * x - w * y) / len;
				Color c = d < h * 0.12f ? redC : d < h * 0.16f ? yel : sky;
				if (InPolygon(new Vector2(x, y), pts))
					c = yel;
				t.SetPixel(x, y, c);
			}
		t.Apply();
		return t;
	}

	// A white 5-point star for the wanted level
	static Texture2D StarTexture()
	{
		const int s = 64;
		Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, true);
		t.wrapMode = TextureWrapMode.Clamp;
		Vector2 c = new Vector2(s / 2f, s / 2f - 2f);
		Vector2[] pts = new Vector2[10];
		for (int k = 0; k < 10; k++)
		{
			float a = Mathf.PI / 2f + k * Mathf.PI / 5f;
			float r = k % 2 == 0 ? s * 0.48f : s * 0.2f;
			pts[k] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
		}
		for (int y = 0; y < s; y++)
			for (int x = 0; x < s; x++)
			{
				// 4 samples per pixel for soft edges
				int inside = 0;
				for (int k = 0; k < 4; k++)
					if (InPolygon(new Vector2(x + 0.25f + (k % 2) * 0.5f, y + 0.25f + (k / 2) * 0.5f), pts))
						inside++;
				t.SetPixel(x, y, new Color(1, 1, 1, inside / 4f));
			}
		t.Apply();
		return t;
	}

	static bool InPolygon(Vector2 p, Vector2[] poly)
	{
		bool inside = false;
		for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
			if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
				inside = !inside;
		return inside;
	}
}
