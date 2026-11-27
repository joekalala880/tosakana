using System.Collections.Generic;
using UnityEngine;

// Kuluna: street gangs that hang around at a corner in some communes (red dots on the map).
// Walk close and they attack you all together. Beat all of them: you get money. They come back later.
// Fighting kuluna is not a crime (no police stars).
public class Kuluna : MonoBehaviour
{
	public static Kuluna Instance { get; private set; }

	public Vector3[] spots;
	public int perGang = 3;
	public int reward = 300;
	public float angryDistance = 13f;
	public float respawnSeconds = 180f;

	static readonly string[] Names = { "Kuluna · Ngolo", "Kuluna · Makasi", "Kuluna · Mbuta (chef)" };

	public class Gang
	{
		public Vector3 spot;
		public List<Pedestrian> members = new List<Pedestrian>();
		public bool angry, beaten;
		public float respawnAt, cleanupAt;
	}

	public readonly List<Gang> Gangs = new List<Gang>();
	public int Beaten { get; private set; }

	void Awake() { Instance = this; }

	void Start()
	{
		if (spots == null)
			return;
		foreach (Vector3 s in spots)
		{
			Gang g = new Gang { spot = s };
			Gangs.Add(g);
			SpawnMembers(g);
		}
	}

	void SpawnMembers(Gang g)
	{
		PedestrianManager pm = PedestrianManager.Instance;
		if (pm == null)
			return;
		g.members.Clear();
		g.angry = false;
		g.beaten = false;
		for (int k = 0; k < perGang; k++)
		{
			float a = k * Mathf.PI * 2f / perGang;
			Vector3 pos = g.spot + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 1.5f;
			Pedestrian p = pm.Spawn(pos, 0f);
			if (p == null)
				continue;
			pm.DressFighter(p, true);
			bool chef = k == perGang - 1;
			p.SetRole(Pedestrian.Role.Kuluna, Names[Mathf.Min(k, Names.Length - 1)], chef ? 110 : 80, chef ? 12 : 9, 0.25f, 1.1f);
			p.hangOut = true;
			p.home = pos;
			p.hangLookAt = g.spot;
			p.name = "Kuluna";
			g.members.Add(p);
		}
	}

	void Update()
	{
		TosakanaGame game = TosakanaGame.Instance;
		if (game == null || !game.IsPlaying)
			return;
		Vector3 you = game.player.transform.position;
		bool onFoot = !game.InVehicle;
		foreach (Gang g in Gangs)
		{
			if (g.beaten)
			{
				if (g.cleanupAt > 0 && Time.time > g.cleanupAt)
				{
					foreach (Pedestrian p in g.members)
						if (p != null) Destroy(p.gameObject);
					g.members.Clear();
					g.cleanupAt = 0;
				}
				if (Time.time > g.respawnAt && Flat(game.Focus.position - g.spot) > 80f)
					SpawnMembers(g);
				continue;
			}
			g.members.RemoveAll(p => p == null);
			float d = Flat(you - g.spot);
			if (!g.angry && onFoot && d < angryDistance)
				Anger(g);
			else if (g.angry && (d > 45f || !onFoot))
			{
				// you got away: they go back to their corner
				g.angry = false;
				foreach (Pedestrian p in g.members)
					if (!p.IsDown && !p.Defeated) { p.CalmDown(); p.hangOut = true; }
			}
			if (g.members.Count > 0 && g.members.TrueForAll(p => p.Defeated))
				Win(g, game);
		}
	}

	public void Anger(Gang g)
	{
		g.angry = true;
		foreach (Pedestrian p in g.members)
			if (!p.IsDown) { p.hangOut = false; p.AttackYou(); }
		TosakanaGame game = TosakanaGame.Instance;
		if (game != null)
			game.ShowBig("KULUNA!", "Bunda to kima!  ·  Fight or run!", new Color(1f, 0.2f, 0.2f), 2.5f);
	}

	void Win(Gang g, TosakanaGame game)
	{
		g.beaten = true;
		g.angry = false;
		g.respawnAt = Time.time + respawnSeconds;
		g.cleanupAt = Time.time + 25f;
		Beaten++;
		game.AddMoney(reward);
		game.ShowBig("KULUNA BEATEN", "Obeti ba kuluna!  ·  +$" + reward, new Color(1f, 0.85f, 0.2f), 3.5f);
		game.sound.Passed();
	}

	static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }
}
