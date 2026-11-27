using UnityEngine;

// Puts people on the sidewalks of every block and gives each one their own clothes.
public class PedestrianManager : MonoBehaviour
{
	public static PedestrianManager Instance { get; private set; }

	public GameObject pedestrianPrefab;
	public int count = 45;
	public int blocks = 5;
	public float firstBlock = -116f;
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
			Vector3 c = BlockCenter(Random.Range(0, blocks), Random.Range(0, blocks));
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

	public Vector3 BlockCenter(int i, int j) { return new Vector3(firstBlock + i * blockStep, 0, firstBlock + j * blockStep); }
	public bool ValidBlock(int i, int j) { return i >= 0 && j >= 0 && i < blocks && j < blocks; }

	public bool BlockIndex(Vector3 center, out int i, out int j)
	{
		i = Mathf.RoundToInt((center.x - firstBlock) / blockStep);
		j = Mathf.RoundToInt((center.z - firstBlock) / blockStep);
		return ValidBlock(i, j);
	}

	public Vector3 NearestBlockCenter(Vector3 p)
	{
		int i = Mathf.Clamp(Mathf.RoundToInt((p.x - firstBlock) / blockStep), 0, blocks - 1);
		int j = Mathf.Clamp(Mathf.RoundToInt((p.z - firstBlock) / blockStep), 0, blocks - 1);
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

	// Different clothes for everyone: shirts and trousers, or a pagne (wrap skirt) and head wrap.
	// Some carry a basin on their head, like sellers in Kinshasa.
	void Dress(Pedestrian p)
	{
		bool pagne = Random.value < 0.45f;
		bool scarf = pagne && Random.value < 0.75f;
		bool basin = Random.value < (pagne ? 0.35f : 0.1f);
		p.carriesBasin = basin;
		Color shirt = Pick(Clothes), wrap = Pick(Clothes), legs = Pick(Pants), skin = Pick(Skin), head = Pick(Clothes), bowl = Pick(Basins);

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
				case "Hair": r.gameObject.SetActive(!scarf); break;
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
