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
		taxi.Teleport(new Vector3(-110f, 0f, 29f), 90f);
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
