using UnityEngine;

// Puts people on the sidewalks of every block and gives each one their own clothes.
public class PedestrianManager : MonoBehaviour
{
	public static PedestrianManager Instance { get; private set; }

	public GameObject pedestrianPrefab;
	public int count = 70;
	public int blocksX = 7;
	public int blocksZ = 6;
	public float firstBlock = -174f;
	public float blockStep = 58f;

	static readonly Color[] Clothes =
	{
		new Color(1f, 0.8f, 0.1f), new Color(0.85f, 0.12f, 0.12f), new Color(0.1f, 0.4f, 0.9f), new Color(0.15f, 0.6f, 0.3f),
		new Color(0.6f, 0.25f, 0.75f), new Color(1f, 0.5f, 0.1f), new Color(0.95f, 0.95f, 0.95f), new Color(0.95f, 0.45f, 0.65f),
		new Color(0.1f, 0.7f, 0.75f), new Color(0.2f, 0.2f, 0.25f),
	};
	static readonly Color[] Pants = { new Color(0.12f, 0.12f, 0.18f), new Color(0.25f, 0.3f, 0.5f), new Color(0.4f, 0.3f, 0.2f), new Color(0.15f, 0.15f, 0.15f), new Color(0.85f, 0.82f, 0.7f) };
	static readonly Color[] Skin = { new Color(0.36f, 0.22f, 0.14f), new Color(0.27f, 0.16f, 0.1f), new Color(0.45f, 0.29f, 0.18f) };
	static readonly Color[] Basins = { new Color(0.85f, 0.85f, 0.88f), new Color(0.2f, 0.45f, 0.9f), new Color(0.9f, 0.2f, 0.2f) };
	static readonly Color[] Shoes = { new Color(0.1f, 0.08f, 0.07f), new Color(0.9f, 0.9f, 0.9f), new Color(0.35f, 0.2f, 0.1f), new Color(0.15f, 0.2f, 0.45f) };
	static readonly Color[] HairColors = { new Color(0.04f, 0.03f, 0.03f), new Color(0.08f, 0.05f, 0.04f), new Color(0.2f, 0.12f, 0.06f) };

	void Awake()
	{
		Instance = this;
	}

	void Start()
	{
		Vector3 start = TosakanaGame.Instance != null ? TosakanaGame.Instance.player.transform.position : Vector3.zero;
		int made = 0, tries = 0;
		while (made < count && tries++ < count * 5)
		{
			Vector3 c = BlockCenter(Random.Range(0, blocksX), Random.Range(0, blocksZ));
			Pedestrian p = Make();
			if (p == null)
				return;
			p.Init(this, c, Random.Range(0, 4), Random.value < 0.5f ? 1 : -1, Random.Range(0f, 42f));
			if (Vector3.Distance(p.transform.position, start) < 6f)
			{
				Destroy(p.gameObject);
				continue;
			}
			made++;
		}
	}

	// People who were shot are taken away; new people come somewhere you can't see, so the city stays busy
	float refillTimer;
	void Update()
	{
		refillTimer -= Time.deltaTime;
		if (refillTimer > 0 || Pedestrian.All.Count >= count || pedestrianPrefab == null)
			return;
		refillTimer = 1f;
		Vector3 you = TosakanaGame.Instance != null ? TosakanaGame.Instance.Focus.position : Vector3.zero;
		for (int tries = 0; tries < 10; tries++)
		{
			Vector3 c = BlockCenter(Random.Range(0, blocksX), Random.Range(0, blocksZ));
			if (Vector3.Distance(c, you) < 80f)
				continue;
			Pedestrian p = Make();
			p.Init(this, c, Random.Range(0, 4), Random.value < 0.5f ? 1 : -1, Random.Range(0f, 42f));
			return;
		}
	}

	public Vector3 BlockCenter(int i, int j) { return new Vector3(firstBlock + i * blockStep, 0, firstBlock + j * blockStep); }
	public bool ValidBlock(int i, int j) { return i >= 0 && j >= 0 && i < blocksX && j < blocksZ; }

	public bool BlockIndex(Vector3 center, out int i, out int j)
	{
		i = Mathf.RoundToInt((center.x - firstBlock) / blockStep);
		j = Mathf.RoundToInt((center.z - firstBlock) / blockStep);
		return ValidBlock(i, j);
	}

	public Vector3 NearestBlockCenter(Vector3 p)
	{
		int i = Mathf.Clamp(Mathf.RoundToInt((p.x - firstBlock) / blockStep), 0, blocksX - 1);
		int j = Mathf.Clamp(Mathf.RoundToInt((p.z - firstBlock) / blockStep), 0, blocksZ - 1);
		return BlockCenter(i, j);
	}

	// The driver of a car you took
	public Pedestrian SpawnDriver(Vector3 position, float yaw)
	{
		Pedestrian p = Make();
		if (p != null)
			p.Leave(this, position);
		return p;
	}

	Pedestrian Make()
	{
		if (pedestrianPrefab == null)
			return null;
		GameObject go = Instantiate(pedestrianPrefab);
		Pedestrian p = go.GetComponent<Pedestrian>();
		Dress(p);
		p.walkSpeed = Random.Range(1.1f, 1.7f);
		return p;
	}

	// A person for a fight (kuluna, ring fighter, someone watching): stands where you say
	public Pedestrian Spawn(Vector3 position, float yaw)
	{
		Pedestrian p = Make();
		if (p == null)
			return null;
		p.Init(this, NearestBlockCenter(position), 0, 1, 0f);
		p.transform.SetPositionAndRotation(new Vector3(position.x, 0.06f, position.z), Quaternion.Euler(0, yaw, 0));
		return p;
	}

	// Kuluna (street gang): black or red tank top, dark trousers, red bandana. Ring fighters: bare chest and shorts.
	public void DressFighter(Pedestrian p, bool kuluna)
	{
		p.carriesBasin = false;
		Color skin = Pick(Skin);
		Color top = kuluna ? (Random.value < 0.5f ? new Color(0.08f, 0.08f, 0.08f) : new Color(0.6f, 0.05f, 0.05f)) : skin;
		Color legs = kuluna ? new Color(0.15f, 0.17f, 0.12f) : (Random.value < 0.5f ? new Color(0.8f, 0.1f, 0.1f) : new Color(0.1f, 0.3f, 0.8f));
		Paint(p, skin, top, legs, false, false, false, kuluna, true, Pick(HairColors), Shoes[0], Basins[0], top);
	}

	// Different clothes for everyone: shirts and trousers, or a pagne (wrap skirt) and head wrap.
	// Some carry a basin on their head, like sellers in Kinshasa. Some men have a beard.
	void Dress(Pedestrian p)
	{
		bool pagne = Random.value < 0.45f;
		bool scarf = pagne && Random.value < 0.75f;
		bool basin = Random.value < (pagne ? 0.35f : 0.1f);
		bool beard = !pagne && Random.value < 0.35f;
		p.carriesBasin = basin;
		Paint(p, Pick(Skin), Pick(Clothes), Pick(Pants), pagne, scarf, basin, false, beard, Pick(HairColors), Pick(Shoes), Pick(Basins), Pick(Clothes), Pick(Clothes));
	}

	void Paint(Pedestrian p, Color skin, Color shirt, Color legs, bool pagne, bool scarf, bool basin, bool bandana, bool beard,
		Color hairColor, Color shoes, Color bowl, Color wrap, Color? headColor = null)
	{
		Color head = headColor ?? wrap;
		Color lips = new Color(skin.r * 0.62f, skin.g * 0.5f, skin.b * 0.5f);

		MaterialPropertyBlock mpb = new MaterialPropertyBlock();
		foreach (Renderer r in p.GetComponentsInChildren<Renderer>(true))
		{
			Color? c = null;
			switch (r.name)
			{
				case "Shirt": c = shirt; break;
				case "Skin": c = skin; break;
				case "Pants": c = pagne ? skin : legs; break;
				case "Wrap": r.gameObject.SetActive(pagne); c = wrap; break;
				case "Scarf": r.gameObject.SetActive(scarf); c = head; break;
				case "Hair": r.gameObject.SetActive(!scarf && !bandana); c = hairColor; break;
				case "Bandana": r.gameObject.SetActive(bandana); break;
				case "Beard": r.gameObject.SetActive(beard); c = hairColor; break;
				case "Brow": c = hairColor; break;
				case "Lips": c = lips; break;
				case "Shoes": c = shoes; break;
				case "Basin": r.gameObject.SetActive(basin); c = bowl; break;
				case "Goods": r.gameObject.SetActive(basin); break;
			}
			if (c.HasValue)
			{
				r.GetPropertyBlock(mpb);
				mpb.SetColor("_Color", c.Value);
				r.SetPropertyBlock(mpb);
			}
		}
	}

	static Color Pick(Color[] list) { return list[Random.Range(0, list.Length)]; }
}
