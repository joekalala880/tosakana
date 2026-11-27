using System.Collections.Generic;
using UnityEngine;

// The ring at Place du 30 Juin: fight for money. Walk into the ring (on foot, no police stars) and a fighter comes.
// Win and you get paid; every win brings a stronger fighter and more money. Leave the ring and the fight is over.
public class FightClub : MonoBehaviour
{
	public static FightClub Instance { get; private set; }

	public Vector3 ringCenter;
	public float ringHalfSize = 3.3f;

	public static readonly string[] Names = { "Bokilo", "Ngolo", "Makasi", "Mbuta Nkoy", "Nkoy ya Kin" };
	public static readonly int[] Prizes = { 400, 600, 800, 1100, 1500 };

	public enum State { Waiting, Countdown, Fighting, Over }
	public State CurrentState { get; private set; }
	public int Level { get; private set; }   // 0 .. 4
	public int Wins { get; private set; }
	public int Losses { get; private set; }
	public Pedestrian Opponent { get; private set; }
	public int Prize { get { return Prizes[Level]; } }
	public readonly List<Pedestrian> Crowd = new List<Pedestrian>();

	float timer;
	int lastCount = -1;
	bool mustStepOut;   // after a fight, step out of the ring and back in for the next one

	void Awake() { Instance = this; }

	public bool InRing(Vector3 p)
	{
		return Mathf.Abs(p.x - ringCenter.x) < ringHalfSize && Mathf.Abs(p.z - ringCenter.z) < ringHalfSize;
	}

	void Update()
	{
		TosakanaGame game = TosakanaGame.Instance;
		if (game == null || !game.IsPlaying)
			return;
		Vector3 you = game.player.transform.position;
		timer -= Time.deltaTime;
		switch (CurrentState)
		{
			case State.Waiting:
				bool wanted = PoliceManager.Instance != null && PoliceManager.Instance.Stars > 0;
				if (!InRing(you)) mustStepOut = false;
				if (!game.InVehicle && InRing(you) && !wanted && timer <= 0 && !mustStepOut)
					StartFight(game);
				break;
			case State.Countdown:
				int count = Mathf.CeilToInt(timer);
				if (count != lastCount && count > 0)
				{
					lastCount = count;
					game.ShowBig(count.ToString(), Opponent != null ? Opponent.displayName + "  ·  $" + Prize : "", Color.white, 0.9f);
				}
				if (timer <= 0)
				{
					CurrentState = State.Fighting;
					game.ShowBig("BETA!", "Fight!  ·  block (right click) when he pulls his fist back", new Color(1f, 0.85f, 0.2f), 1.5f);
					if (Opponent != null) { Opponent.hangOut = false; Opponent.AttackYou(); }
				}
				break;
			case State.Fighting:
				if (Opponent == null)
					End(2f);
				else if (Opponent.Defeated)
					Win(game);
				else if (Flat(you - ringCenter) > ringHalfSize + 7f || game.InVehicle)
				{
					game.ShowBig("FIGHT OVER", "You left the ring", new Color(1f, 0.5f, 0.3f), 2f);
					Opponent.CalmDown();
					End(1f);
				}
				break;
			case State.Over:
				if (timer <= 0)
					CleanUp();
				break;
		}
	}

	public void StartFight(TosakanaGame game)
	{
		PedestrianManager pm = PedestrianManager.Instance;
		if (pm == null)
			return;
		Vector3 you = game.player.transform.position;
		Vector3 side = you.x < ringCenter.x ? Vector3.right : Vector3.left;
		Vector3 pos = ringCenter + side * 2.2f;
		Opponent = pm.Spawn(pos, 0f);
		if (Opponent == null)
			return;
		pm.DressFighter(Opponent, false);
		Opponent.SetRole(Pedestrian.Role.Fighter, Names[Level] + "  (level " + (Level + 1) + ")", 70 + 25 * Level, 7 + 2 * Level, 0.15f + 0.07f * Level, 1f + 0.15f * Level);
		Opponent.transform.position = new Vector3(pos.x, ringCenter.y, pos.z);
		Opponent.hangOut = true;
		Opponent.home = Opponent.transform.position;
		Opponent.hangLookAt = you;
		Opponent.name = "Ring Fighter";
		// people come to watch
		for (int k = 0; k < 6; k++)
		{
			float a = k * Mathf.PI / 3f + 0.3f;
			Vector3 at = ringCenter + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (ringHalfSize + 2.2f);
			Pedestrian fan = pm.Spawn(at, 0f);
			if (fan == null) continue;
			fan.transform.position += Vector3.up * 0.14f;   // standing on the grass
			fan.cheering = true;
			fan.cheerAt = ringCenter;
			fan.name = "Ring Fan";
			Crowd.Add(fan);
		}
		CurrentState = State.Countdown;
		timer = 3f;
		lastCount = -1;
	}

	void Win(TosakanaGame game)
	{
		int prize = Prize;
		Wins++;
		game.AddMoney(prize);
		game.sound.Passed();
		game.ShowBig("YOU WIN!", "Olongi!  ·  +$" + prize + (Level < Prizes.Length - 1 ? "  ·  next: " + Names[Level + 1] + " ($" + Prizes[Level + 1] + ")" : "  ·  you are the champion of Kin!"), new Color(1f, 0.85f, 0.2f), 4f);
		Level = Mathf.Min(Level + 1, Prizes.Length - 1);
		End(7f);
	}

	// You got knocked out (PlayerCombat calls this)
	public void YouLost()
	{
		if (CurrentState != State.Fighting && CurrentState != State.Countdown)
			return;
		Losses++;
		if (Opponent != null) Opponent.CalmDown();
		End(2f);
	}

	void End(float seconds)
	{
		CurrentState = State.Over;
		timer = seconds;
	}

	void CleanUp()
	{
		if (Opponent != null) Destroy(Opponent.gameObject);
		Opponent = null;
		foreach (Pedestrian p in Crowd)
			if (p != null) Destroy(p.gameObject);
		Crowd.Clear();
		CurrentState = State.Waiting;
		timer = 1f;
		mustStepOut = true;
	}

	static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }
}
