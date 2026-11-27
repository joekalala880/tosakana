using System.Collections.Generic;
using UnityEngine;

// Version 12: people with real bodies (knees, elbows, shoulders) and real faces
// (eyes, eyebrows, nose, lips, ears, hair, beard). Used for you, the people in the street,
// the kuluna, the fighters and the police.
public static partial class CityBuilder
{
	static Material eyeWhite, lipsMat, shoeMat, beardMat;

	static void PeopleMaterials()
	{
		eyeWhite = Mat("Eye White", new Color(0.95f, 0.94f, 0.9f), 0.6f);
		lipsMat = Mat("Lips", new Color(0.24f, 0.12f, 0.1f), 0.4f);
		shoeMat = Mat("Street Shoes", new Color(0.12f, 0.1f, 0.09f), 0.5f);
		beardMat = Mat("Beard", new Color(0.05f, 0.04f, 0.035f), 0.1f);
	}

	class Human
	{
		public Body body;
		public List<Renderer> top = new List<Renderer>();       // shirt / jacket (body and upper arms)
		public List<Renderer> forearms = new List<Renderer>();  // long sleeves or bare arms
		public List<Renderer> legs = new List<Renderer>();
		public List<Renderer> feet = new List<Renderer>();
		public List<GameObject> beard = new List<GameObject>();
		public GameObject hair;
	}

	// names: what the dressing code looks for ("Shirt", "Pants", "Shoes", "Skin")
	static Human MakeHuman(GameObject root, Material top, Material bottom, Material feet, Material skinMat, Material hairMat,
		bool beard, bool longSleeves, string topName = "Shirt", string legName = "Pants", string feetName = "Shoes")
	{
		Human h = new Human();
		Body b = root.AddComponent<Body>();
		h.body = b;

		// Hips and belt
		h.legs.Add(R(Part(root, PrimitiveType.Cylinder, bottom, new Vector3(0, 0.98f, 0), new Vector3(0.36f, 0.08f, 0.24f)), legName));

		// Spine: chest, shoulders, neck, head and arms move with it
		b.spine = Pivot(root.transform, "Spine", new Vector3(0, 1.0f, 0));
		GameObject spine = b.spine.gameObject;
		h.top.Add(R(Part(spine, PrimitiveType.Capsule, top, new Vector3(0, 0.27f, 0), new Vector3(0.42f, 0.29f, 0.25f)), topName));
		GameObject shoulders = Part(spine, PrimitiveType.Capsule, top, new Vector3(0, 0.44f, -0.01f), new Vector3(0.18f, 0.26f, 0.19f));
		shoulders.transform.localRotation = Quaternion.Euler(0, 0, 90f);
		h.top.Add(R(shoulders, topName));
		Named(Part(spine, PrimitiveType.Cylinder, skinMat, new Vector3(0, 0.55f, 0.01f), new Vector3(0.1f, 0.06f, 0.1f)), "Skin");

		// The head and the face
		b.head = Pivot(b.spine, "Head", new Vector3(0, 0.58f, 0));
		GameObject head = b.head.gameObject;
		Named(Part(head, PrimitiveType.Sphere, skinMat, new Vector3(0, 0.15f, 0), new Vector3(0.23f, 0.27f, 0.25f)), "Skin");
		Named(Part(head, PrimitiveType.Sphere, skinMat, new Vector3(0, 0.065f, 0.025f), new Vector3(0.19f, 0.14f, 0.2f)), "Skin");   // jaw
		foreach (float side in new[] { -1f, 1f })
		{
			Named(Part(head, PrimitiveType.Sphere, skinMat, new Vector3(side * 0.117f, 0.14f, 0), new Vector3(0.045f, 0.08f, 0.06f)), "Skin");   // ear
			Named(Part(head, PrimitiveType.Sphere, eyeWhite, new Vector3(side * 0.05f, 0.172f, 0.104f), new Vector3(0.056f, 0.032f, 0.03f)), "Eye White");
			Named(Part(head, PrimitiveType.Sphere, black, new Vector3(side * 0.05f, 0.172f, 0.117f), new Vector3(0.027f, 0.027f, 0.012f)), "Eye");
			GameObject brow = Part(head, PrimitiveType.Cube, hairMat, new Vector3(side * 0.052f, 0.205f, 0.112f), new Vector3(0.068f, 0.015f, 0.02f));
			brow.transform.localRotation = Quaternion.Euler(0, 0, -side * 9f);
			Named(brow, "Brow");
		}
		Named(Part(head, PrimitiveType.Sphere, skinMat, new Vector3(0, 0.12f, 0.124f), new Vector3(0.068f, 0.06f, 0.07f)), "Skin");     // nose
		Named(Part(head, PrimitiveType.Cube, skinMat, new Vector3(0, 0.15f, 0.118f), new Vector3(0.03f, 0.06f, 0.03f)), "Skin");        // nose bridge
		Named(Part(head, PrimitiveType.Sphere, lipsMat, new Vector3(0, 0.072f, 0.123f), new Vector3(0.085f, 0.026f, 0.03f)), "Lips");
		h.hair = Named(Part(head, PrimitiveType.Sphere, hairMat, new Vector3(0, 0.205f, -0.012f), new Vector3(0.245f, 0.17f, 0.265f)), "Hair");
		if (beard)
		{
			h.beard.Add(Named(Part(head, PrimitiveType.Sphere, beardMat, new Vector3(0, 0.05f, 0.03f), new Vector3(0.198f, 0.12f, 0.2f)), "Beard"));
			h.beard.Add(Named(Part(head, PrimitiveType.Cube, beardMat, new Vector3(0, 0.094f, 0.124f), new Vector3(0.08f, 0.016f, 0.02f)), "Beard"));
		}

		// Arms: shoulder -> upper arm -> elbow -> forearm -> hand
		foreach (float side in new[] { -1f, 1f })
		{
			Transform shoulder = Pivot(b.spine, side < 0 ? "Left Arm" : "Right Arm", new Vector3(side * 0.25f, 0.44f, 0));
			h.top.Add(R(Part(shoulder.gameObject, PrimitiveType.Capsule, top, new Vector3(0, -0.15f, 0), new Vector3(0.11f, 0.155f, 0.11f)), topName));
			Transform elbow = Pivot(shoulder, "Elbow", new Vector3(0, -0.3f, 0));
			Renderer fore = R(Part(elbow.gameObject, PrimitiveType.Capsule, longSleeves ? top : skinMat, new Vector3(0, -0.13f, 0), new Vector3(0.095f, 0.14f, 0.095f)), longSleeves ? topName : "Skin");
			h.forearms.Add(fore);
			Named(Part(elbow.gameObject, PrimitiveType.Sphere, skinMat, new Vector3(0, -0.3f, 0.01f), new Vector3(0.085f, 0.11f, 0.065f)), "Skin");   // hand
			if (side < 0) { b.leftShoulder = shoulder; b.leftElbow = elbow; }
			else { b.rightShoulder = shoulder; b.rightElbow = elbow; }
		}

		// Legs: hip -> thigh -> knee -> shin -> foot
		foreach (float side in new[] { -1f, 1f })
		{
			Transform hip = Pivot(root.transform, side < 0 ? "Left Leg" : "Right Leg", new Vector3(side * 0.1f, 0.95f, 0));
			h.legs.Add(R(Part(hip.gameObject, PrimitiveType.Capsule, bottom, new Vector3(0, -0.23f, 0), new Vector3(0.155f, 0.245f, 0.155f)), legName));
			Transform knee = Pivot(hip, "Knee", new Vector3(0, -0.46f, 0));
			h.legs.Add(R(Part(knee.gameObject, PrimitiveType.Capsule, bottom, new Vector3(0, -0.22f, 0), new Vector3(0.125f, 0.235f, 0.125f)), legName));
			h.feet.Add(R(Part(knee.gameObject, PrimitiveType.Cube, feet, new Vector3(0, -0.45f, 0.05f), new Vector3(0.11f, 0.08f, 0.25f)), feetName));
			if (side < 0) { b.leftHip = hip; b.leftKnee = knee; }
			else { b.rightHip = hip; b.rightKnee = knee; }
		}
		return h;
	}

	static Transform Pivot(Transform parent, string name, Vector3 localPos)
	{
		Transform t = new GameObject(name).transform;
		t.SetParent(parent, false);
		t.localPosition = localPos;
		t.gameObject.layer = parent.gameObject.layer;
		return t;
	}

	static Renderer R(GameObject go, string name)
	{
		go.name = name;
		return go.GetComponent<Renderer>();
	}

	// A ring for street fights with money: Place du 30 Juin (the plaza), in the grass next to the flag
	static void FightRing(GameObject gameGo)
	{
		Vector3 c = new Vector3(8.5f, 0, 8.5f);
		Transform ring = new GameObject("Fight Ring").transform;
		ring.SetParent(city);
		ring.position = c;
		Material canvas = Mat("Ring Canvas", new Color(0.12f, 0.35f, 0.75f), 0.15f);
		Material rope = Mat("Ring Rope", new Color(0.9f, 0.12f, 0.1f), 0.4f);
		Shape(ring, PrimitiveType.Cube, "Ring Floor", canvas, c + Vector3.up * 0.15f, new Vector3(7.4f, 0.3f, 7.4f), Vector3.zero, true);
		for (int k = 0; k < 4; k++)
		{
			float sx = (k == 1 || k == 2) ? 1f : -1f, sz = k >= 2 ? 1f : -1f;
			Shape(ring, PrimitiveType.Cylinder, "Ring Post", k % 2 == 0 ? red : blue, c + new Vector3(sx * 3.5f, 1.0f, sz * 3.5f), new Vector3(0.18f, 0.7f, 0.18f), Vector3.zero, false);
		}
		foreach (float h in new[] { 0.75f, 1.15f, 1.55f })
		{
			Shape(ring, PrimitiveType.Cube, "Rope", rope, c + new Vector3(0, h, -3.5f), new Vector3(7f, 0.05f, 0.05f), Vector3.zero, false);
			Shape(ring, PrimitiveType.Cube, "Rope", rope, c + new Vector3(0, h, 3.5f), new Vector3(7f, 0.05f, 0.05f), Vector3.zero, false);
			Shape(ring, PrimitiveType.Cube, "Rope", rope, c + new Vector3(3.5f, h, 0), new Vector3(0.05f, 0.05f, 7f), Vector3.zero, false);
			// the side by the path stays open at the bottom rope so you can walk in
			if (h > 1f)
				Shape(ring, PrimitiveType.Cube, "Rope", rope, c + new Vector3(-3.5f, h, 0), new Vector3(0.05f, 0.05f, 7f), Vector3.zero, false);
		}
		Sign(ring, "RING YA KIN  ·  $", c + new Vector3(0, 0, -5.2f), 180f, signBlue, "Bitumba mpo na mbongo  ·  Fight for money");
		GameObject icon = Shape(ring, PrimitiveType.Sphere, "Map Icon", rope, c + Vector3.up * 149f, Vector3.one * 10f, Vector3.zero, false);
		icon.layer = MapLayer;

		FightClub club = gameGo.AddComponent<FightClub>();
		club.ringCenter = c + Vector3.up * 0.3f;
		club.ringHalfSize = 3.3f;
	}

	// Kuluna (street gangs) hang around in three communes. Red dots on the map.
	static void KulunaSpots(GameObject gameGo, List<Place> places)
	{
		Kuluna gangs = gameGo.AddComponent<Kuluna>();
		List<Vector3> spots = new List<Vector3>();
		foreach (Place p in places)
			if (p.placeName == "Matete" || p.placeName == "Kimbanseke" || p.placeName == "Selembao")
			{
				// on the sidewalk at the corner of their block, next to the street
				bool north = p.markerPosition.z > p.transform.position.z;
				Vector3 s = p.transform.position + new Vector3(-14f, 0, north ? 21.4f : -21.4f);
				spots.Add(s);
				GameObject icon = Shape(city, PrimitiveType.Sphere, "Map Icon", sirenRed, s + Vector3.up * 149f, Vector3.one * 8f, Vector3.zero, false);
				icon.layer = MapLayer;
			}
		gangs.spots = spots.ToArray();
	}
}
