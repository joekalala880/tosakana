using System;
using System.Collections;
using System.IO;
using UnityEngine;

// Saves preview pictures of the running game to Downloads/Tosakana_Files (used by the TosakanaSnapshot test).
public class TosakanaSnapshotTaker : MonoBehaviour
{
	const int Width = 1600, Height = 900;
	public bool Finished { get; private set; }

	IEnumerator Start()
	{
		Camera cam = Camera.main;
		RenderTexture rt = new RenderTexture(Width, Height, 24);
		Canvas canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceCamera;
		canvas.worldCamera = cam;
		canvas.planeDistance = 1;

		TosakanaGame game = FindObjectOfType<TosakanaGame>();
		MissionManager missions = game.missions;
		CameraRig rig = game.cameraRig;
		string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads/Tosakana_Files");
		Directory.CreateDirectory(folder);

		yield return new WaitForSeconds(1f);
		Save(cam, rt, folder, "1_title");

		game.StartGame();
		yield return new WaitForSeconds(0.3f);
		rig.SetYaw(60f);
		rig.SetPitch(14f);
		yield return new WaitForSeconds(0.3f);
		Save(cam, rt, folder, "2_on_foot_next_to_taxi");

		CarController taxi = GameObject.Find("Taxi (yours)").GetComponent<CarController>();
		game.player.Teleport(taxi.transform.position - taxi.transform.right * 2.4f, 90f);
		yield return null;
		game.ToggleCar();
		taxi.Teleport(new Vector3(-135f, 0f, 29f), 90f);
		rig.SetYaw(90f);
		taxi.testThrottle = 1;
		yield return new WaitForSeconds(1.6f);
		Save(cam, rt, folder, "3_driving_boulevard_du_30_juin");
		taxi.testThrottle = 0;

		taxi.Teleport(new Vector3(-30f, 0f, -32.5f), 270f);
		rig.SetYaw(250f);
		rig.SetPitch(16f);
		yield return new WaitForSeconds(0.4f);
		Save(cam, rt, folder, "4_grand_marche_marker");

		taxi.Teleport(missions.Pickup.markerPosition, 270f);
		yield return new WaitForSeconds(0.5f);
		Save(cam, rt, folder, "5_package_picked_up");

		taxi.Teleport(new Vector3(32.5f, 0f, -15f), 0f);
		rig.SetYaw(15f);
		rig.SetPitch(4f);
		taxi.testThrottle = 0.6f;
		yield return new WaitForSeconds(0.8f);
		Save(cam, rt, folder, "6_gombe_towers");
		taxi.testThrottle = 0;

		taxi.Teleport(missions.Dropoff.markerPosition + new Vector3(0, 0, 0), 90f);
		rig.SetYaw(70f);
		yield return new WaitForSeconds(0.5f);
		Save(cam, rt, folder, "7_mission_passed");

		taxi.Teleport(new Vector3(-10f, 0f, 145f), 270f);
		rig.SetYaw(300f);
		rig.SetPitch(14f);
		yield return new WaitForSeconds(0.4f);
		Save(cam, rt, folder, "8_fleuve_congo_port");

		taxi.Teleport(new Vector3(84.5f, 0f, -70f), 180f);
		rig.SetYaw(165f);
		rig.SetPitch(10f);
		yield return new WaitForSeconds(0.4f);
		Save(cam, rt, folder, "9_stade_des_martyrs");

		taxi.Teleport(new Vector3(-12f, 0f, -26f), 50f);
		rig.SetYaw(35f);
		rig.SetPitch(16f);
		yield return new WaitForSeconds(0.4f);
		Save(cam, rt, folder, "10_place_du_30_juin_flag");

		// Version 2: traffic and people
		taxi.Teleport(new Vector3(-58f, 0f, 22.6f), 90f);   // parked at the curb of the boulevard
		game.ToggleCar();
		game.player.Teleport(new Vector3(-58f, 0.05f, 21.2f), 90f);
		rig.SetYaw(70f);
		rig.SetPitch(12f);
		yield return new WaitForSeconds(4f);
		Save(cam, rt, folder, "11_people_on_the_sidewalk");

		game.player.Teleport(new Vector3(-21f, 0.05f, -21f), 225f);
		rig.SetYaw(225f);
		rig.SetPitch(20f);
		yield return new WaitForSeconds(2f);
		Save(cam, rt, folder, "12_traffic_at_crossroads");

		// Close-up of someone carrying a basin
		Pedestrian seller = null;
		foreach (Pedestrian p in Pedestrian.All)
			if (p.carriesBasin && !p.Crossing) { seller = p; break; }
		if (seller != null)
		{
			seller.testHold = true;
			yield return null;
			Vector3 front = seller.transform.position + seller.transform.forward * 3.2f;
			game.player.Teleport(new Vector3(front.x, 0.05f, front.z), seller.transform.eulerAngles.y + 180f);
			rig.SetYaw(seller.transform.eulerAngles.y + 160f);
			rig.SetPitch(8f);
			yield return new WaitForSeconds(0.4f);
			Save(cam, rt, folder, "13_basin_seller");
			seller.testHold = false;
		}

		// Version 3: a police chase
		PoliceManager pm = PoliceManager.Instance;
		game.player.Teleport(taxi.transform.position - taxi.transform.right * 2.4f, 90f);
		yield return null;
		game.ToggleCar();
		taxi.Teleport(new Vector3(-110f, 0f, 26.7f), 90f);   // the eastbound lane (the roulage stands in the middle)
		rig.SetYaw(90f);
		rig.SetPitch(10f);
		pm.ReportCrime(3, "You hit a person!");
		taxi.testThrottle = 0.5f;
		yield return new WaitForSeconds(5f);
		taxi.testThrottle = 0;
		taxi.testBrake = true;
		TrafficDriver near = null;
		float wait = Time.time + 15f;
		while (Time.time < wait)
		{
			near = null;
			float best = 18f;
			foreach (TrafficDriver d in pm.Police)
			{
				float dist = Vector3.Distance(d.transform.position, taxi.transform.position);
				if (d.Chasing && dist < best) { best = dist; near = d; }
			}
			if (near != null) break;
			yield return null;
		}
		if (near != null)
		{
			Vector3 look = near.transform.position - taxi.transform.position;
			rig.SetYaw(Quaternion.LookRotation(new Vector3(look.x, 0, look.z)).eulerAngles.y);
			rig.SetPitch(14f);
		}
		yield return new WaitForSeconds(0.25f);
		Save(cam, rt, folder, "14_police_chase");
		wait = Time.time + 20f;
		while (pm.Busts == 0 && Time.time < wait)
			yield return null;
		yield return new WaitForSeconds(0.4f);
		Save(cam, rt, folder, "15_busted");
		taxi.testBrake = false;

		// Version 4: communes and Lingala
		yield return new WaitForSeconds(3.6f);   // let the BUSTED message go away
		pm.enabled = false;
		pm.ResetWanted();
		game.player.Teleport(new Vector3(58f, 0.05f, -94.6f), 270f);   // Lemba sidewalk
		Pedestrian friend = null;
		foreach (Pedestrian p in Pedestrian.All)
			if (!p.IsDown && !p.Crossing) { friend = p; break; }
		friend.testHold = true;
		friend.testNoDodge = true;
		friend.transform.position = new Vector3(55.6f, 0.06f, -94.6f);
		rig.SetYaw(250f);
		rig.SetPitch(10f);
		yield return new WaitForSeconds(0.3f);
		game.TalkToNearest();
		yield return new WaitForSeconds(1.2f);
		Save(cam, rt, folder, "16_talking_in_lingala");
		friend.testHold = false;
		friend.testNoDodge = false;

		game.player.Teleport(new Vector3(52f, 0.05f, -80f), 210f);   // in front of the Lemba sign
		rig.SetYaw(170f);
		rig.SetPitch(6f);
		yield return new WaitForSeconds(0.4f);
		Save(cam, rt, folder, "17_lemba_bokomi_malamu");

		game.player.Teleport(new Vector3(-30f, 0.05f, -145f), 270f);   // between Bumbu and Mont-Ngafula
		rig.SetYaw(230f);
		rig.SetPitch(18f);
		yield return new WaitForSeconds(0.6f);
		Save(cam, rt, folder, "18_bumbu_and_mont_ngafula");

		// Version 5: a roulage officer on Boulevard du 30 Juin
		TrafficOfficer officer = TrafficOfficer.At(3, 4);
		Vector3 o = officer.transform.position;
		game.player.Teleport(new Vector3(o.x - 7f, 0.05f, o.z - 8.5f), 45f);
		rig.SetYaw(40f);
		rig.SetPitch(8f);
		yield return new WaitForSeconds(1f);
		Save(cam, rt, folder, "19_roulage_officer");

		// Version 6: a fight
		pm.enabled = false;
		pm.ResetWanted();
		game.player.Teleport(new Vector3(10f, 0.05f, -36.6f), 90f);   // a Kalamu sidewalk
		Pedestrian boxer = null;
		foreach (Pedestrian p in Pedestrian.All)
			if (!p.IsDown && !p.Crossing) { boxer = p; break; }
		boxer.testForceFight = true;
		boxer.testNoDodge = true;
		boxer.transform.position = new Vector3(11.3f, 0.06f, -36.6f);
		rig.SetYaw(20f);
		rig.SetPitch(10f);
		yield return null;
		game.player.GetComponent<PlayerCombat>().Punch();
		yield return new WaitForSeconds(0.15f);
		Save(cam, rt, folder, "20_punch");
		yield return new WaitForSeconds(3.6f);
		Save(cam, rt, folder, "21_they_fight_back");
		boxer.testForceFight = false;
		boxer.testNoDodge = false;
		boxer.CalmDown();

		// The airport
		game.player.Teleport(new Vector3(700f, 0.05f, 40f), 120f);
		rig.SetYaw(125f);
		rig.SetPitch(4f);
		PlaneTakeoff flyer = FindObjectOfType<PlaneTakeoff>();
		float planeWait = Time.time + 60f;
		while (Time.time < planeWait && !(flyer.Flying && flyer.transform.position.y > 15f))
			yield return null;
		Save(cam, rt, folder, "22_ndjili_airport_takeoff");

		game.player.Teleport(new Vector3(850f, 0.05f, 20f), 0f);
		rig.SetYaw(0f);
		rig.SetPitch(8f);
		yield return new WaitForSeconds(0.4f);
		Save(cam, rt, folder, "23_ndjili_terminal");

		// Version 7: flying over Kinshasa
		PlaneController jet = PlaneController.All[0];
		Vector3 nose = jet.transform.position + jet.transform.forward * 21f;
		game.player.Teleport(new Vector3(nose.x, 0.05f, nose.z), 0f);
		yield return null;
		game.ToggleCar();
		jet.Place(new Vector3(420f, 110f, -60f), 285f, 70f);
		jet.testThrottle = 0.4f;
		rig.SetYaw(285f);
		yield return new WaitForSeconds(2.5f);
		Save(cam, rt, folder, "24_flying_over_kinshasa");
		jet.testThrottle = 0;

		// Version 8: the rich sapeur, with the arrow to the mission
		jet.Place(jet.Home + Vector3.zero, 180f);
		game.LeaveVehicle();
		game.player.Teleport(new Vector3(-58f, 0.05f, 21.4f), 90f);   // a boulevard sidewalk
		rig.SetYaw(270f);   // look at your face
		rig.SetPitch(6f);
		yield return new WaitForSeconds(0.5f);
		Save(cam, rt, folder, "25_rich_sapeur");
		rig.SetYaw(40f);
		rig.SetPitch(14f);
		yield return new WaitForSeconds(0.3f);
		Save(cam, rt, folder, "26_arrow_to_the_mission");

		// Version 9: the pistol
		game.player.GetComponent<PlayerWeapons>().SetPistol(true);
		rig.SetYaw(90f);
		rig.SetPitch(6f);
		yield return new WaitForSeconds(0.3f);
		Save(cam, rt, folder, "27_pistol");
		game.player.GetComponent<PlayerWeapons>().SetPistol(false);

		// Version 10: sitting in the car, the view from inside, the mission card
		CarController myTaxi = GameObject.Find("Taxi (yours)").GetComponent<CarController>();
		game.player.Teleport(myTaxi.transform.position - myTaxi.transform.right * 2.3f, 0f);
		yield return null;
		game.ToggleCar();
		myTaxi.Teleport(new Vector3(-150f, 0f, 26.7f), 90f);
		rig.SetYaw(45f);
		rig.SetPitch(12f);
		yield return new WaitForSeconds(0.4f);
		Save(cam, rt, folder, "28_you_driving");
		game.ToggleInsideView();
		myTaxi.testThrottle = 0.5f;
		yield return new WaitForSeconds(1.5f);
		Save(cam, rt, folder, "29_view_from_inside");
		myTaxi.testThrottle = 0;
		game.ToggleInsideView();
		missions.StartMission(missions.Find("Matonge"), missions.Find("Lemba"));
		yield return new WaitForSeconds(0.3f);
		Save(cam, rt, folder, "30_mission_card");

		// Version 11: the street look, the radio, sunset and night
		DayNight dn = game.GetComponent<DayNight>();
		missions.briefingPanel.SetActive(false);
		dn.SetHour(17.6f);
		myTaxi.Teleport(new Vector3(-20f, 0f, 26.7f), 90f);
		game.GetComponent<CarRadio>().NextStation();
		rig.SetYaw(60f);
		rig.SetPitch(10f);
		yield return new WaitForSeconds(0.5f);
		Save(cam, rt, folder, "31_sunset_radio");
		dn.SetHour(22f);
		myTaxi.Teleport(new Vector3(230f, 0f, 26.7f), 270f);
		rig.SetYaw(240f);
		rig.SetPitch(8f);
		yield return new WaitForSeconds(0.5f);
		Save(cam, rt, folder, "32_kinshasa_at_night");
		game.ToggleCar();
		game.player.Teleport(new Vector3(-58f, 0.05f, 21.4f), 90f);
		game.ChangeClothes();
		dn.SetHour(10f);
		rig.SetYaw(270f);
		rig.SetPitch(6f);
		yield return new WaitForSeconds(2.2f);
		Save(cam, rt, folder, "33_street_look_leopards");

		// Version 12: your face, a street fight, kuluna, the ring
		game.ChangeClothes();   // back in the sapeur suit
		game.player.Teleport(new Vector3(-58f, 0.05f, 21.4f), 90f);
		rig.SetTarget(game.player.transform, false, 1.7f, 1.62f);
		rig.SetYaw(250f);
		rig.SetPitch(2f);
		yield return new WaitForSeconds(0.6f);
		Save(cam, rt, folder, "34_your_face");
		rig.SetTarget(game.player.transform, false);

		PlayerCombat fists = game.player.GetComponent<PlayerCombat>();
		StreetFights sf = game.GetComponent<StreetFights>();
		Vector3 me = game.player.transform.position;
		var walkers = Pedestrian.All.FindAll(p => p.role == Pedestrian.Role.Normal && p.CurrentMood == Pedestrian.Mood.Walk && !p.IsDown && !p.carriesBasin && !p.cheering);
		if (walkers.Count >= 5)
		{
			Vector3 at = me + new Vector3(7f, 0.01f, 0);
			walkers[0].transform.position = at + new Vector3(-0.7f, 0, 0);
			walkers[1].transform.position = at + new Vector3(0.7f, 0, 0);
			for (int k = 2; k < 5; k++) walkers[k].transform.position = at + new Vector3(-2f + (k - 2) * 2f, 0, -2.6f);
			sf.Begin(walkers[0], walkers[1]);
			rig.SetYaw(70f);
			rig.SetPitch(10f);
			yield return new WaitForSeconds(2.2f);
			Save(cam, rt, folder, "35_street_fight");
			sf.Stop();
			walkers[0].CalmDown();
			walkers[1].CalmDown();
		}

		Kuluna gangs = game.GetComponent<Kuluna>();
		if (gangs.Gangs.Count > 0)
		{
			Kuluna.Gang g = gangs.Gangs[0];
			game.player.Teleport(g.spot + new Vector3(16f, 0.05f, 0), 270f);
			rig.SetYaw(270f);
			rig.SetPitch(8f);
			yield return new WaitForSeconds(0.6f);
			Save(cam, rt, folder, "36_kuluna_corner");
		}

		FightClub club = game.GetComponent<FightClub>();
		if (PoliceManager.Instance != null) PoliceManager.Instance.ResetWanted();
		game.player.Teleport(club.ringCenter + new Vector3(-1.5f, 0.1f, 0), 90f);
		float until = Time.time + 6f;
		while (club.CurrentState != FightClub.State.Fighting && Time.time < until)
			yield return null;
		rig.SetYaw(45f);
		rig.SetPitch(14f);
		yield return new WaitForSeconds(1.2f);
		if (club.Opponent != null)
		{
			club.Opponent.transform.position = game.player.transform.position + game.player.transform.forward * 1.3f + Vector3.up * 0.0f;
			fists.Kick();
			yield return new WaitForSeconds(0.2f);
		}
		Save(cam, rt, folder, "37_ring_fight_kick");
		Finished = true;
	}

	static void Save(Camera cam, RenderTexture rt, string folder, string name)
	{
		cam.targetTexture = rt;
		cam.Render();
		RenderTexture.active = rt;
		Texture2D tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
		tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
		tex.Apply();
		RenderTexture.active = null;
		cam.targetTexture = null;
		string path = Path.Combine(folder, name + ".png");
		File.WriteAllBytes(path, tex.EncodeToPNG());
		Debug.Log("TOSAKANA-SNAPSHOT " + path);
	}
}
