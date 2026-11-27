using System.Collections.Generic;
using UnityEngine;

// The GPS: a line on the roads (seen on the mini-map and the big map) from you to your mission (yellow)
// or, when there is no mission to go to, to your waypoint (purple).
// Kinshasa's streets are a grid, so the way is: along this street, turn, along the next street, then to the spot.
public class Gps : MonoBehaviour
{
	public LineRenderer line;
	public float gridStart = -203f, gridStep = 58f;
	public int nodesX = 14, nodesZ = 7;
	public float boulevardZ = 29f;       // Boulevard du 30 Juin / Lumumba (goes on to the airport)
	public float height = 146f;          // just under the map icons
	public Color missionColor = new Color(1f, 0.82f, 0.1f);
	public Color waypointColor = new Color(0.75f, 0.3f, 1f);

	public bool Showing { get { return line != null && line.enabled; } }
	public bool ToMission { get; private set; }
	public readonly List<Vector3> Points = new List<Vector3>();

	float timer;

	void Update()
	{
		timer -= Time.unscaledDeltaTime;
		if (timer > 0)
			return;
		timer = 0.25f;
		Refresh();
	}

	public void Refresh()
	{
		TosakanaGame game = TosakanaGame.Instance;
		if (game == null || line == null)
			return;
		Place target = game.IsPlaying ? game.missions.Target : null;
		bool any = target != null || BigMap.HasWaypoint;
		line.enabled = any && game.IsPlaying;
		if (!line.enabled)
			return;
		ToMission = target != null;
		Vector3 to = ToMission ? target.markerPosition : BigMap.Waypoint;
		Route(game.Focus.position, to);
		Color c = ToMission ? missionColor : waypointColor;
		line.startColor = c;
		line.endColor = c;
		if (line.material != null) line.material.color = c;
		line.positionCount = Points.Count;
		for (int i = 0; i < Points.Count; i++)
			line.SetPosition(i, new Vector3(Points[i].x, height, Points[i].z));
	}

	float MaxX { get { return gridStart + (nodesX - 1) * gridStep; } }

	// The way on the roads from one place to another
	public void Route(Vector3 from, Vector3 to)
	{
		Points.Clear();
		Points.Add(from);
		Vector3 a = Snap(from), b = Snap(to);
		// out at the airport: come back along the airport road first
		if (from.x > MaxX + 10f)
		{
			Points.Add(new Vector3(from.x, 0, boulevardZ));
			a = new Vector3(MaxX, 0, boulevardZ);
		}
		Points.Add(a);
		bool airport = to.x > MaxX + 10f;
		if (airport)
			b = new Vector3(MaxX, 0, boulevardZ);
		// along the street of a, then along the street of b (always roads: the streets make a grid)
		if (Mathf.Abs(a.x - b.x) > 0.5f && Mathf.Abs(a.z - b.z) > 0.5f)
			Points.Add(new Vector3(b.x, 0, a.z));
		Points.Add(b);
		if (airport)
			Points.Add(new Vector3(to.x, 0, boulevardZ));
		Points.Add(to);
		// remove points that are the same
		for (int i = Points.Count - 1; i > 0; i--)
			if ((Points[i] - Points[i - 1]).sqrMagnitude < 1f)
				Points.RemoveAt(i);
	}

	// The nearest crossroads
	Vector3 Snap(Vector3 p)
	{
		int i = Mathf.Clamp(Mathf.RoundToInt((p.x - gridStart) / gridStep), 0, nodesX - 1);
		int j = Mathf.Clamp(Mathf.RoundToInt((p.z - gridStart) / gridStep), 0, nodesZ - 1);
		return new Vector3(gridStart + i * gridStep, 0, gridStart + j * gridStep);
	}

	// How long the way is (meters)
	public float Length()
	{
		float total = 0;
		for (int i = 1; i < Points.Count; i++)
			total += Vector3.Distance(Points[i - 1], Points[i]);
		return total;
	}
}
