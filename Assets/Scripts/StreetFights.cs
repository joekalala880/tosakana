using System.Collections.Generic;
using UnityEngine;

// Sometimes two people near you start arguing and fight in the street, and people stop to watch.
// You can watch, or join in: hit one of them and they fight you instead.
public class StreetFights : MonoBehaviour
{
	public static StreetFights Instance { get; private set; }

	public float minSeconds = 40f;   // time between street fights
	public float maxSeconds = 75f;

	public Pedestrian A { get; private set; }
	public Pedestrian B { get; private set; }
	public bool Active { get { return A != null && B != null; } }
	public int Started { get; private set; }
	public Pedestrian Winner { get; private set; }
	public readonly List<Pedestrian> Watchers = new List<Pedestrian>();

	float timer, fightTime;

	void Awake()
	{
		Instance = this;
		timer = Random.Range(20f, 35f);
	}

	void Update()
	{
		TosakanaGame game = TosakanaGame.Instance;
		if (game == null || !game.IsPlaying)
			return;
		if (Active)
		{
			Track();
			return;
		}
		timer -= Time.deltaTime;
		if (timer > 0)
			return;
		timer = Random.Range(minSeconds, maxSeconds);
		TryStart(game.Focus.position);
	}

	// Find two people walking close to each other, not too close and not too far from you
	public bool TryStart(Vector3 near)
	{
		List<Pedestrian> ok = new List<Pedestrian>();
		foreach (Pedestrian p in Pedestrian.All)
		{
			float d = Flat(p.transform.position - near);
			if (Usable(p) && d > 10f && d < 60f)
				ok.Add(p);
		}
		for (int i = 0; i < ok.Count; i++)
			for (int j = i + 1; j < ok.Count; j++)
				if (Flat(ok[i].transform.position - ok[j].transform.position) < 10f)
				{
					Begin(ok[i], ok[j]);
					return true;
				}
		return false;
	}

	static bool Usable(Pedestrian p)
	{
		return p.role == Pedestrian.Role.Normal && p.CurrentMood == Pedestrian.Mood.Walk && !p.IsDown && !p.carriesBasin &&
			!p.cheering && !p.hangOut && !p.Crossing && !p.Talking;
	}

	public void Begin(Pedestrian a, Pedestrian b)
	{
		A = a;
		B = b;
		Winner = null;
		fightTime = 0;
		Started++;
		a.FightPerson(b);
		b.FightPerson(a);
		Vector3 middle = (a.transform.position + b.transform.position) / 2f;
		Watchers.Clear();
		foreach (Pedestrian p in Pedestrian.All)
			if (p != a && p != b && Usable(p) && Flat(p.transform.position - middle) < 16f && Watchers.Count < 5)
			{
				p.cheering = true;
				p.cheerAt = middle;
				Watchers.Add(p);
			}
		TosakanaGame game = TosakanaGame.Instance;
		if (game != null && Flat(game.Focus.position - middle) < 80f)
			game.ShowBig("STREET FIGHT!", "Bitumba na balabala  ·  watch, or join in", new Color(1f, 0.55f, 0.15f), 2.5f);
	}

	void Track()
	{
		fightTime += Time.deltaTime;
		bool aOut = A == null || A.IsDown || A.IsDead || !A.isActiveAndEnabled;
		bool bOut = B == null || B.IsDown || B.IsDead || !B.isActiveAndEnabled;
		bool stopped = !aOut && !bOut && (A.FoePerson != B || B.FoePerson != A) && !A.FightingYou && !B.FightingYou;
		if (aOut || bOut || stopped || fightTime > 45f)
		{
			if (aOut && !bOut) Winner = B;
			else if (bOut && !aOut) Winner = A;
			// the loser runs away when they get up, the winner walks off
			if (Winner != null)
			{
				Winner.CalmDown();
				Pedestrian loser = Winner == A ? B : A;
				if (loser != null) loser.GiveUp();
			}
			Stop();
		}
	}

	public void Stop()
	{
		foreach (Pedestrian p in Watchers)
			if (p != null) p.cheering = false;
		Watchers.Clear();
		A = null;
		B = null;
	}

	static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }
}
