using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Play-mode checks for Tosakana. Results are logged with the prefix "TOSAKANA-CHECK".
public class TosakanaRequirementsRunner : MonoBehaviour
{
	int passed, failed;
	TosakanaGame game;
	MissionManager missions;
	PlayerController player;
	CameraRig rig;
	MiniMap map;
	CarController taxi;

	IEnumerator Start()
	{
		DontDestroyOnLoad(gameObject);
		game = FindObjectOfType<TosakanaGame>();
		missions = game.missions;
		player = game.player;
		rig = game.cameraRig;
		map = game.GetComponent<MiniMap>();
		yield return null;
		// The police wait until the Version 3 checks, so stars from earlier tests don't get in the way
		PoliceManager.Instance.enabled = false;

		// ---- Title ----
		Check("Starts on the title screen (HUD hidden)", game.CurrentState == TosakanaGame.State.Title && game.titlePanel.activeSelf && !game.hud.activeSelf);
		Check("Title shows TOSAKANA and 'Made by Joel Kalala'", HasText(game.titlePanel, "TOSAKANA") && HasText(game.titlePanel, "Made by Joel Kalala"));
		int buildings = 0;
		foreach (Transform t in GameObject.Find("City").GetComponentsInChildren<Transform>())
			if (t.name == "Building" || t.name == "Warehouse") buildings++;
		Check("City is built (" + buildings + " buildings, " + CarController.All.Count + " cars, " + missions.places.Length + " places)",
			buildings > 450 && CarController.All.Count >= 40 && missions.places.Length == 22);

		game.StartGame();
		yield return null;
		Check("Enter starts the game, first job: Grand Marché -> Gombe", game.IsPlaying && game.hud.activeSelf && !game.titlePanel.activeSelf &&
			missions.CurrentStep == MissionManager.Step.GoToPickup && missions.Pickup.placeName == "Grand Marché" && missions.Dropoff.placeName == "Gombe");
		yield return null;
		Check("Mission text says where to go", missions.missionText.text.Contains("Grand Marché") && missions.titleText.text.Contains("MISSION 1"));
		Check("Yellow marker is at Grand Marché", missions.marker.gameObject.activeSelf && Flat(missions.marker.position - missions.Pickup.markerPosition) < 0.1f);

		// ---- Walking ----
		yield return new WaitForSeconds(0.3f);
		Vector3 p0 = player.transform.position;
		player.testMove = new Vector2(0, 1);
		yield return new WaitForSeconds(0.5f);
		float walked = Flat(player.transform.position - p0);
		player.testRun = true;
		p0 = player.transform.position;
		yield return new WaitForSeconds(0.5f);
		float ran = Flat(player.transform.position - p0);
		player.testMove = Vector2.zero;
		player.testRun = false;
		Check("Walk with WASD (" + walked.ToString("0.0") + " m in 0.5 s)", walked > 1.2f && walked < 3.5f);
		Check("Shift = run faster (" + ran.ToString("0.0") + " m in 0.5 s)", ran > walked * 1.5f);

		// ---- Getting in the car ----
		taxi = GameObject.Find("Taxi (yours)").GetComponent<CarController>();
		Check("Your taxi is parked next to you", taxi != null && Flat(taxi.transform.position - player.transform.position) < 12f);
		ClearAround(taxi.transform.position, 12f);
		player.Teleport(taxi.transform.position - taxi.transform.right * 2.5f, taxi.transform.eulerAngles.y);
		yield return null;
		yield return null;
		Check("Hint: 'Press E to get in the car'", game.hintText.text.Contains("Press E"));
		bool entered = game.ToggleCar();
		yield return null;
		Check("E = get in the car (player hidden, camera follows the car)", entered && game.InCar && game.CurrentCar == taxi && !player.gameObject.activeSelf && rig.target == taxi.transform);

		// ---- Driving ----
		ClearAround(new Vector3(-90f, 0, 29f), 60f);
		taxi.Teleport(new Vector3(-130f, 0f, 29f), 90f);   // on Boulevard du 30 Juin, going east
		rig.SetYaw(90f);
		yield return new WaitForFixedUpdate();
		Vector3 c0 = taxi.transform.position;
		taxi.testThrottle = 1;
		yield return new WaitForSeconds(1.5f);
		float moved = Flat(taxi.transform.position - c0);
		Check("W = drive (" + Mathf.RoundToInt(taxi.Speed * 3.6f) + " km/h, " + moved.ToString("0") + " m in 1.5 s)", taxi.Speed > 12f && moved > 10f);
		Check("Speed shows on the screen (" + game.speedText.text + ")", game.speedText.text.EndsWith("km/h") && game.speedText.text != "0 km/h");
		Check("Engine sound is on and gets higher with speed (pitch " + game.sound.EnginePitch.ToString("0.00") + ")", game.sound.EngineVolume > 0.1f && game.sound.EnginePitch > 0.9f);
		Check("Area name on the boulevard (" + game.zoneText.text + ")", game.zoneText.text == "Boulevard du 30 Juin");

		float yaw0 = taxi.transform.eulerAngles.y;
		taxi.testSteer = -1;
		yield return new WaitForSeconds(0.4f);
		taxi.testSteer = 0;
		float turned = Mathf.Abs(Mathf.DeltaAngle(yaw0, taxi.transform.eulerAngles.y));
		Check("A/D = steer (turned " + turned.ToString("0") + "°)", turned > 15f);

		taxi.testThrottle = 0;
		taxi.testBrake = true;
		yield return new WaitForSeconds(1.2f);
		taxi.testBrake = false;
		Check("Space = brake (speed now " + taxi.Speed.ToString("0.0") + " m/s)", Mathf.Abs(taxi.Speed) < 0.5f);

		// ---- Buildings are solid ----
		ClearAround(new Vector3(-116f, 0, 90f), 20f);
		taxi.Teleport(new Vector3(-116f, 0f, 87f), 0f);   // facing a warehouse at the port
		yield return new WaitForFixedUpdate();
		taxi.testThrottle = 1;
		yield return new WaitForSeconds(2.5f);
		taxi.testThrottle = 0;
		Check("Car can't drive through buildings (stopped at z " + taxi.transform.position.z.ToString("0.0") + ", wall at 98)", taxi.transform.position.z < 97f && taxi.transform.position.z > 90f);

		// ---- Getting out ----
		yield return new WaitForSeconds(0.5f);
		game.ToggleCar();
		yield return null;
		Check("E again = get out (player next to the car)", !game.InCar && player.gameObject.activeSelf && Flat(player.transform.position - taxi.transform.position) < 5f);

		player.Teleport(new Vector3(-110f, 0.05f, 94f), 0f);
		rig.SetYaw(0f);
		yield return null;
		player.testMove = new Vector2(0, 1);
		yield return new WaitForSeconds(1.5f);
		player.testMove = Vector2.zero;
		Check("Player can't walk through buildings (z " + player.transform.position.z.ToString("0.0") + ", wall at 98)", player.transform.position.z < 97.9f);

		// ---- Mission: pick up and deliver ----
		player.Teleport(taxi.transform.position - taxi.transform.right * 2.3f, 0f);
		yield return null;
		game.ToggleCar();
		taxi.Teleport(missions.Pickup.markerPosition, 90f);
		yield return null;
		yield return null;
		Check("Drive into the marker = package picked up", missions.CurrentStep == MissionManager.Step.Deliver && game.bigText.text.Contains("PICKED UP"));
		Check("Timer starts (" + missions.timerText.text + "), marker moves to Gombe", missions.timeLeft > 15f && missions.timerText.text.Contains(":") &&
			Flat(missions.marker.position - missions.Dropoff.markerPosition) < 0.1f);

		int money0 = game.Money;
		int reward = missions.Reward;
		taxi.Teleport(missions.Dropoff.markerPosition, 90f);
		yield return null;
		yield return null;
		Check("Deliver in time = MISSION PASSED +$" + reward + " (money $" + game.Money + ")", missions.CurrentStep == MissionManager.Step.Passed && game.Money == money0 + reward && reward >= 200 && game.bigText.text == "MISSION PASSED");
		Check("Money shows on the screen (" + game.moneyText.text + ")", game.moneyText.text == "$" + game.Money.ToString("N0"));
		Check("End of the mission: the customer blesses you (\"" + Voice.Narrator.LastLingala + "\")", IsLine(Voice.Narrator.LastLingala, Lingala.Blessings));

		missions.waitTimer = 0.01f;
		yield return new WaitForSeconds(0.15f);
		Check("A new job starts (" + missions.Pickup.placeName + " -> " + missions.Dropoff.placeName + ")", missions.CurrentStep == MissionManager.Step.GoToPickup &&
			missions.Completed == 1 && missions.Pickup != missions.Dropoff && missions.titleText.text.Contains("MISSION 2"));

		// ---- Mission failed ----
		Place from = missions.Pickup, to = missions.Dropoff;
		taxi.Teleport(from.markerPosition, 0f);
		yield return null;
		yield return null;
		missions.timeLeft = 0.01f;
		yield return new WaitForSeconds(0.15f);
		Check("Too slow = MISSION FAILED (no money)", missions.CurrentStep == MissionManager.Step.Failed && game.Money == money0 + reward && game.bigText.text == "MISSION FAILED");
		taxi.Teleport(new Vector3(0, 0, -87f), 0f);
		missions.waitTimer = 0.01f;
		yield return new WaitForSeconds(0.15f);
		Check("After failing you can try the same job again", missions.CurrentStep == MissionManager.Step.GoToPickup && missions.Pickup == from && missions.Dropoff == to);

		// ---- Area names and mini-map ----
		taxi.Teleport(new Vector3(87f, 0f, 87f), 0f);
		yield return null;
		yield return null;
		Check("Area name in Gombe (" + game.zoneText.text + ")", game.zoneText.text == "Gombe");
		Check("Mini-map camera follows you", map.mapCam.targetTexture != null && Flat(map.mapCam.transform.position - taxi.transform.position) < 0.5f);
		Check("Mini-map shows where to go (yellow dot on)", map.targetIcon.gameObject.activeSelf);
		RawImage mapImage = System.Array.Find(game.hud.GetComponentsInChildren<RawImage>(), r => r.name == "Mini-Map");
		Check("Mini-map is on the HUD", mapImage != null && mapImage.texture == map.mapCam.targetTexture);

		// ================= Version 2: traffic and people =================
		TrafficManager tm = TrafficManager.Instance;
		Check("Traffic: " + tm.ActiveCount + " cars driving around the city", tm.ActiveCount >= tm.carCount - 1);
		float speedSum = 0;
		int onLane = 0, turns = 0, n = 0;
		// Watch for 1 second: a car counts as "in its lane" if it was in the lane (not in the middle of a turn) at some point
		var inLane = new System.Collections.Generic.HashSet<TrafficDriver>();
		for (int sample = 0; sample < 10; sample++)
		{
			foreach (TrafficDriver d in tm.Drivers)
				if (d != null && d.enabled && d.LaneError < 1.5f) inLane.Add(d);
			yield return new WaitForSeconds(0.1f);
		}
		foreach (TrafficDriver d in tm.Drivers)
		{
			if (d == null || !d.enabled) continue;
			n++;
			speedSum += Mathf.Abs(d.GetComponent<CarController>().Speed);
			if (inLane.Contains(d)) onLane++;
			turns += d.TurnsMade;
		}
		Check("Traffic is moving (average " + Mathf.RoundToInt(speedSum / n * 3.6f) + " km/h)", speedSum / n > 4f);
		Check("Traffic drives on the right side, in its lane (" + onLane + " of " + n + ")", onLane >= n * 0.75f);
		Check("Traffic turns at crossroads (" + turns + " turns so far)", turns >= 3);

		// Traffic stops for you
		game.ToggleCar();   // get out
		yield return null;
		TrafficDriver stopper = tm.Drivers.Find(d => d != null && d.enabled);
		CarController stopCar = stopper.GetComponent<CarController>();
		Vector3 laneAhead = tm.LanePoint(1, 0, 1, 26f);   // southern street, going east
		ClearAround(laneAhead, 40f, stopCar);
		stopper.PlaceAt(1, 0, 1, 4f);
		player.Teleport(new Vector3(laneAhead.x, 0.05f, laneAhead.z), 270f);
		float honkWait = Time.time + 7f;
		while (Time.time < honkWait && !IsLine(Said(stopper.gameObject), Lingala.Honk))
			yield return null;
		float gap = Flat(stopCar.transform.position - player.transform.position);
		Check("Traffic stops for people in the road (stopped " + gap.ToString("0.0") + " m away)", Mathf.Abs(stopCar.Speed) < 1f && gap > 2.2f && gap < 12f && stopper.Blocked);
		Check("The waiting driver shouts in Lingala (\"" + Said(stopper.gameObject) + "\")", IsLine(Said(stopper.gameObject), Lingala.Honk));

		// Taking a car from the traffic
		player.Teleport(stopCar.transform.position - stopCar.transform.right * 2.4f, 0f);
		yield return null;
		int peopleBefore = Pedestrian.All.Count;
		bool took = game.ToggleCar();
		yield return null;
		yield return null;
		Check("E on a traffic car = you take it and the driver gets out", took && game.CurrentCar == stopCar && !stopper.enabled && Pedestrian.All.Count == peopleBefore + 1);
		Pedestrian angryDriver = Pedestrian.All[Pedestrian.All.Count - 1];
		Check("The driver shouts \"" + Said(angryDriver.gameObject) + "\"", IsLine(Said(angryDriver.gameObject), Lingala.Thief));
		yield return new WaitForSeconds(0.3f);
		Check("A new car joins the traffic, so the city stays busy (" + tm.ActiveCount + ")", tm.ActiveCount >= tm.carCount);

		// ---- People ----
		Check("People: " + Pedestrian.All.Count + " people in the city", Pedestrian.All.Count >= 60);
		var before = new System.Collections.Generic.Dictionary<Pedestrian, Vector3>();
		foreach (Pedestrian p in Pedestrian.All) before[p] = p.transform.position;
		yield return new WaitForSeconds(1.5f);
		int walking = 0, onSidewalk = 0, crossings = 0;
		foreach (Pedestrian p in Pedestrian.All)
		{
			Vector3 v;
			if (before.TryGetValue(p, out v) && Flat(p.transform.position - v) > 1f) walking++;
			if (p.Crossing || OnSidewalk(p.transform.position)) onSidewalk++;
			crossings += p.Crossings;
		}
		Check("People walk around (" + walking + " of " + before.Count + " moved)", walking >= before.Count * 0.8f);
		Check("People stay on the sidewalks unless crossing (" + onSidewalk + " of " + Pedestrian.All.Count + ")", onSidewalk >= Pedestrian.All.Count * 0.9f);
		Check("People cross the streets (" + crossings + " crossings so far)", crossings >= 1);

		// People jump out of the way of a fast car
		Pedestrian jumper = Pedestrian.All.Find(p => !p.Crossing && !p.IsDown);
		jumper.testHold = true;
		ClearAround(new Vector3(-110f, 0, -87f), 30f, stopCar);
		stopCar.Teleport(new Vector3(-132f, 0f, -87f), 90f);
		jumper.transform.position = new Vector3(-114f, 0.06f, -87f);
		stopCar.testThrottle = 1;
		yield return new WaitForSeconds(1.7f);
		stopCar.testThrottle = 0;
		Check("People jump out of the way of a fast car (" + jumper.Dodges + " jump)", jumper.Dodges > 0 && !jumper.IsDown);
		Check("...and shout \"" + Said(jumper.gameObject) + "\"", IsLine(Said(jumper.gameObject), Lingala.WatchOut));
		jumper.testHold = false;

		// A car that hits someone knocks them down; they get back up
		Pedestrian unlucky = Pedestrian.All.Find(p => p != jumper && !p.Crossing && !p.IsDown);
		unlucky.testHold = true;
		unlucky.testNoDodge = true;
		ClearAround(new Vector3(-60f, 0, -87f), 25f, stopCar);
		stopCar.Teleport(new Vector3(-76f, 0f, -87f), 90f);
		unlucky.transform.position = new Vector3(-64f, 0.06f, -87f);
		stopCar.testThrottle = 1;
		yield return new WaitForSeconds(1.5f);
		stopCar.testThrottle = 0;
		stopCar.testBrake = true;
		Check("A car that hits someone knocks them down", unlucky.IsDown && unlucky.Knockdowns == 1);
		Check("...and they say \"" + Said(unlucky.gameObject) + "\"", IsLine(Said(unlucky.gameObject), Lingala.Hit));
		yield return new WaitForSeconds(3.2f);
		stopCar.testBrake = false;
		Check("...and they get back up", !unlucky.IsDown);
		unlucky.testHold = false;
		unlucky.testNoDodge = false;

		// ================= Version 3: police and wanted stars =================
		PoliceManager pm = PoliceManager.Instance;
		pm.ResetWanted();
		pm.bustSeconds = 999f;   // no BUSTED until the check that waits for it
		pm.enabled = true;
		yield return null;
		Check("Police cars patrol the city (" + pm.Police.Count + "), no stars yet", pm.Police.Count >= 2 && pm.Stars == 0 && pm.ChasingCount == 0 && StarsLit(pm) == 0);
		PoliceLights firstLights = pm.Police[0].GetComponent<PoliceLights>();
		Check("Police cars show on the mini-map", firstLights.mapIcon.gameObject.layer == 31 && firstLights.mapIcon.gameObject.activeInHierarchy);

		// Crime: hit a person with your car
		Pedestrian victim = Pedestrian.All.Find(p => p != unlucky && !p.Crossing && !p.IsDown);
		victim.testHold = true;
		victim.testNoDodge = true;
		ClearAround(new Vector3(-8f, 0, -87f), 25f, stopCar);
		stopCar.Teleport(new Vector3(-20f, 0f, -87f), 90f);
		victim.transform.position = new Vector3(-8f, 0.06f, -87f);
		yield return null;
		stopCar.testThrottle = 1;
		yield return new WaitForSeconds(0.6f);
		stopCar.testThrottle = 0;
		victim.KnockDown(stopCar);   // (the crash itself is checked in Version 2)
		stopCar.testBrake = true;
		yield return null;
		Check("Hitting a person with your car = 1 wanted star (\"" + pm.wantedText.text + "\")", victim.IsDown && pm.Stars == 1 && StarsLit(pm) == 1);
		victim.testHold = false;
		victim.testNoDodge = false;

		yield return new WaitForSeconds(2.5f);
		Check("A police car chases you with flashing lights and siren", pm.ChasingCount >= 1 && ChaserLightsOn(pm));
		pm.ReportCrime(1, "Another crime");
		yield return new WaitForSeconds(2.5f);
		Check("More stars = more police (" + pm.Stars + " stars, " + pm.ChasingCount + " chasing)", pm.Stars == 2 && pm.ChasingCount >= 2 && StarsLit(pm) == 2);

		// Stay stopped: the police come and catch you
		pm.bustSeconds = 1.5f;
		int moneyBefore = game.Money;
		float closest = float.MaxValue;
		float giveUp = Time.time + 50f;
		string shout = "";
		while (pm.Busts == 0 && Time.time < giveUp)
		{
			foreach (TrafficDriver d in pm.Police)
				if (d != null && d.Chasing)
				{
					closest = Mathf.Min(closest, Flat(d.transform.position - stopCar.transform.position));
					if (IsLine(Said(d.gameObject), Lingala.Police)) shout = Said(d.gameObject);
				}
			yield return null;
		}
		Check("Police shout in Lingala when they are close (\"" + shout + "\")", shout != "");
		stopCar.testBrake = false;
		Check("Police drive through the streets to you (closest " + closest.ToString("0.0") + " m)", closest < 8f);
		Check("Caught while stopped = BUSTED (fine $" + (moneyBefore - game.Money) + "), stars gone, back at the start", pm.Busts == 1 && pm.Stars == 0 &&
			game.Money == moneyBefore - Mathf.Min(moneyBefore, pm.bustFine) && !game.InCar && Flat(player.transform.position - game.RespawnPoint) < 1f && game.bigText.text == "BUSTED");
		Check("After BUSTED the police stop chasing", pm.ChasingCount == 0 && pm.Police.Count == pm.patrolCars);

		// Get away from the police: they lose you
		pm.escapeBaseSeconds = 0.5f;
		pm.escapePerStar = 0.5f;
		pm.ReportCrime(2, "Escape test");
		float ready = Time.time + 20f;
		while ((pm.ChasingCount < pm.Stars || !pm.Seen) && Time.time < ready)
			yield return null;
		int far = 0;
		foreach (TrafficDriver d in pm.Police)
			if (d.Chasing)
				d.Car.Teleport(new Vector3(300f + 12f * far++, 0f, 300f), 0f);
		bool sawSearching = false;
		float end = Time.time + 5f;
		while (pm.Stars > 0 && Time.time < end)
		{
			sawSearching |= pm.Searching;
			yield return null;
		}
		Check("Far from the police: the stars blink, then YOU LOST THE POLICE (" + far + " sent away, stars " + pm.Stars + ")", far >= 2 && sawSearching && pm.Stars == 0 && pm.Escapes == 1 && game.bigText.text.Contains("LOST"));
		pm.escapeBaseSeconds = 5f;
		pm.escapePerStar = 2f;

		// Steal a police car
		pm.ResetWanted();
		yield return new WaitForSeconds(0.2f);
		TrafficDriver cop = pm.Police.Find(d => d != null && d.enabled);
		player.Teleport(cop.transform.position - cop.transform.right * 2.4f, 0f);
		ClearAround(cop.transform.position, 10f, cop.Car);
		yield return null;
		bool tookCop = game.ToggleCar();
		yield return null;
		Check("Stealing a police car = 2 stars (\"" + pm.wantedText.text + "\")", tookCop && game.CurrentCar == cop.Car && pm.Stars == 2 && pm.wantedText.text.Contains("police car"));
		yield return new WaitForSeconds(0.3f);
		Check("The stolen police car is yours now (lights off, not in the police list)", !pm.Police.Contains(cop) && !cop.GetComponent<PoliceLights>().On);
		pm.ResetWanted();

		// ================= Version 4: Kinshasa communes and Lingala =================
		string[] communes = { "Gombe", "Matonge", "Lemba", "Ngiri-Ngiri", "Bandal", "Bumbu", "Selembao", "Mont-Ngafula", "Kintambo", "Limete", "Kalamu", "Lingwala",
			"Matete", "Masina", "N'djili", "Kimbanseke", "Kingasani" };
		int found = 0;
		string missing = "";
		foreach (string name in communes)
		{
			Place place = System.Array.Find(missions.places, pl => pl.placeName == name);
			if (place != null && missions.CommuneAt(place.transform.position) == name) found++;
			else missing += name + " ";
		}
		Check("All 17 communes are in the city, each with a mission place (" + found + "/17) " + missing, found == 17);

		if (game.InCar) game.ToggleCar();
		yield return null;
		player.Teleport(new Vector3(58f, 0.05f, -94.6f), 0f);   // a sidewalk in Lemba
		yield return null;
		yield return null;
		Check("Entering a commune: \"" + game.areaBanner.text + "\" / \"" + game.areaBannerSub.text + "\"", game.areaBanner.text == "Bokomi malamu na LEMBA" && game.areaBannerSub.text == "Welcome to Lemba");
		Check("Area name at the bottom right says Lemba (" + game.zoneText.text + ")", game.zoneText.text == "Lemba");

		// Talk to someone with T
		Pedestrian friend = Pedestrian.All.Find(p => !p.IsDown && !p.Crossing && !p.carriesBasin);
		friend.testHold = true;
		friend.testNoDodge = true;
		friend.transform.position = player.transform.position + new Vector3(1.8f, 0.01f, 0.5f);
		ClearAround(player.transform.position, 8f);
		ClearPeopleAround(player.transform.position, 6f, friend);
		yield return null;
		yield return null;
		Check("Hint: \"" + game.hintText.text + "\"", game.hintText.text.Contains("Press T to talk") && game.hintText.text.Contains("Loba"));
		Pedestrian talked = game.TalkToNearest();
		yield return null;
		Check("T = you say \"" + Said(player.gameObject) + "\"", talked == friend && Said(player.gameObject) == "Mbote!");
		yield return new WaitForSeconds(1.1f);
		Check("...and they answer \"" + Said(friend.gameObject) + "\" and stop to look at you", friend.Talking && IsLine(Said(friend.gameObject), Lingala.Answers[0]));
		yield return new WaitForSeconds(0.3f);
		game.TalkToNearest();
		yield return new WaitForSeconds(1.1f);
		Check("T again = the next line (\"" + Said(player.gameObject) + "\" -> \"" + Said(friend.gameObject) + "\")",
			Said(player.gameObject) == "Sango nini?" && IsLine(Said(friend.gameObject), Lingala.Answers[1]));
		friend.testHold = false;
		friend.testNoDodge = false;
		Check("The mission says it in Lingala too", missions.missionText.text.Contains("Kende kozwa paquet") || missions.missionText.text.Contains("Mema yango"));

		// ================= Version 5: voices and roulage =================
		Check("No speech bubbles: people talk out loud (no bubbles in the city)", GameObject.Find("Speech Bubble") == null && friend.GetComponent<VoiceSpeaker>() != null);
		VoiceSpeaker friendVoice = friend.GetComponent<VoiceSpeaker>();
		Check("People speak with your recorded voice (" + (friendVoice.CurrentClip != null ? friendVoice.CurrentClip.name : "none") + ")",
			friendVoice.LastWasRecording && friendVoice.CurrentClip != null && !friendVoice.CurrentClip.name.StartsWith("Murmur"));
		int withVoice = 0;
		foreach (var kv in Lingala.AllSpoken())
			if (Voice.HasRecording(kv.Value)) withVoice++;
		Check("Lines recorded in your voice: " + withVoice + " / " + Lingala.AllSpoken().Count, withVoice == Lingala.AllSpoken().Count);
		AudioClip murmur = Voice.Murmur(new Lingala.Line("Test murmur line", "test"));
		Check("A new line with no recording yet would play a soft murmur", murmur != null && murmur.length > 0.2f);
		var spoken = Lingala.AllSpoken();
		var keys = new System.Collections.Generic.HashSet<string>();
		bool keysOk = true;
		foreach (var kv in spoken)
			keysOk &= kv.Value.Key.Length > 0 && keys.Add(kv.Value.Key);
		Check("Every line has its own recording name (" + spoken.Count + " lines to record)", keysOk && spoken.Count >= 55);
		string wav = System.IO.Path.Combine(Application.temporaryCachePath, "tosakana_test.wav");
		float[] tone = new float[22050];
		for (int i = 0; i < tone.Length; i++) tone[i] = Mathf.Sin(i * 0.1f) * 0.5f;
		WavFile.Write(wav, tone, 22050);
		byte[] bytes = System.IO.File.ReadAllBytes(wav);
		Check("Recordings are saved as .wav files (" + bytes.Length + " bytes)", bytes.Length == 44 + tone.Length * 2 && System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WAVE");
		Check("Quiet start and end are cut from recordings", WavFile.Clean(new float[44100], 44100).Length == 0);

		// Roulage
		int switches = 0;
		foreach (TrafficOfficer o in TrafficOfficer.All) switches += o.Switches;
		Check("Roulage officers stand at " + TrafficOfficer.All.Count + " crossroads and switch the traffic (" + switches + " whistles)", TrafficOfficer.All.Count == 6 && switches >= 6);

		TrafficOfficer officer = TrafficOfficer.At(3, 2);
		officer.testFreeze = true;
		officer.SetPhase(1);   // east-west may go, so cars going north must wait
		TrafficDriver waiter = tm.Drivers.Find(d => d != null && d.enabled);
		Vector3 crossroads = tm.Node(3, 2);
		ClearAround(crossroads + new Vector3(0, 0, -25f), 30f, waiter.Car);
		waiter.PlaceAt(3, 1, 0, 6f);   // going north toward the officer
		yield return new WaitForSeconds(5f);
		float before2 = crossroads.z - waiter.transform.position.z;
		Check("Traffic stops when the roulage says stop (" + before2.ToString("0.0") + " m before the crossroads)", waiter.WaitingForRoulage && Mathf.Abs(waiter.Car.Speed) < 1f && before2 > 6f && before2 < 20f);
		officer.SetPhase(0);   // now north-south may go
		yield return new WaitForSeconds(3f);
		Check("...and goes when the roulage says go", !waiter.WaitingForRoulage && waiter.transform.position.z > crossroads.z - 5f);
		officer.testFreeze = false;

		// Don't hit the roulage!
		pm.ResetWanted();
		TrafficOfficer target = TrafficOfficer.All.Find(o => o.nodeA == 2 && o.nodeB == 4);
		float upWait = Time.time + 6f;
		while (target.IsDown && Time.time < upWait)
			yield return null;
		Vector3 spot = target.transform.position;
		if (!game.InCar)
		{
			player.Teleport(taxi.transform.position - taxi.transform.right * 2.3f, 0f);
			yield return null;
			game.ToggleCar();
		}
		CarController myCar = game.CurrentCar;
		ClearAround(spot, 25f, myCar);
		myCar.Teleport(new Vector3(spot.x - 18f, 0f, spot.z), 90f);
		myCar.testThrottle = 1;
		yield return new WaitForSeconds(1.7f);
		myCar.testThrottle = 0;
		myCar.testBrake = true;
		Check("Hitting a roulage officer: he falls (\"" + Said(target.gameObject) + "\") and you get 2 stars (" + pm.Stars + ")", target.IsDown && pm.Stars == 2 && IsLine(Said(target.gameObject), Lingala.Hit));
		yield return new WaitForSeconds(0.5f);
		myCar.testBrake = false;
		pm.ResetWanted();

		// ================= Version 6: fights, bigger Kinshasa, the airport =================
		if (game.InCar) game.ToggleCar();
		pm.ResetWanted();
		pm.enabled = false;   // keep the police away during the first fights
		yield return null;
		PlayerCombat fists = player.GetComponent<PlayerCombat>();
		fists.Heal();
		player.Teleport(new Vector3(58f, 0.05f, -94.6f), 90f);   // Lemba sidewalk

		// Punch someone who runs away
		Pedestrian runner = Pedestrian.All.Find(p => !p.IsDown && !p.Crossing && p.CurrentMood == Pedestrian.Mood.Walk);
		runner.testHold = true;
		runner.testNoDodge = true;
		runner.testForceFlee = true;
		runner.transform.position = player.transform.position + new Vector3(1.2f, 0.01f, 0);
		ClearPeopleAround(player.transform.position, 4f, runner);
		yield return null;
		Component hit = fists.Punch();
		Check("Left click / R = punch: the person falls (\"" + Said(runner.gameObject) + "\")", hit == runner && runner.IsDown && runner.Punched == 1);
		Check("Hitting a person = 1 wanted star (" + pm.Stars + ")", pm.Stars == 1);
		runner.testHold = false;
		Vector3 fleeFrom = runner.transform.position;
		yield return new WaitForSeconds(4.5f);
		Check("Some people run away shouting \"" + Said(runner.gameObject) + "\" (" + Flat(runner.transform.position - fleeFrom).ToString("0") + " m)",
			runner.CurrentMood == Pedestrian.Mood.Flee && Flat(runner.transform.position - fleeFrom) > 5f && IsLine(Said(runner.gameObject), Lingala.Help));
		runner.testForceFlee = false;
		runner.testNoDodge = false;

		// Punch someone who fights back
		Pedestrian boxer = Pedestrian.All.Find(p => p != runner && !p.IsDown && !p.Crossing && p.CurrentMood == Pedestrian.Mood.Walk);
		boxer.testHold = true;
		boxer.testNoDodge = true;
		boxer.testForceFight = true;
		boxer.transform.position = player.transform.position + new Vector3(-1.2f, 0.01f, 0);
		ClearPeopleAround(player.transform.position, 4f, boxer);
		fists.Heal();
		yield return new WaitForSeconds(0.5f);
		int healthBefore = fists.Health;
		fists.Punch();
		boxer.testHold = false;
		yield return new WaitForSeconds(5f);
		Check("Some people fight back (\"" + Said(boxer.gameObject) + "\"): your health " + healthBefore + " -> " + fists.Health,
			boxer.CurrentMood == Pedestrian.Mood.Fight && fists.Health < healthBefore && IsLine(Said(boxer.gameObject), Lingala.FightBack));
		Component again = fists.Punch();
		Check("You can punch back (they fall again)", again == boxer && boxer.IsDown && boxer.Punched == 2);
		fists.SetHealth(5);
		float koWait = Time.time + 8f;
		while (fists.KnockOuts == 0 && Time.time < koWait)
			yield return null;
		Check("When your health runs out: KNOCKED OUT (\"" + game.bigSubText.text + "\"), back at the start with full health",
			fists.KnockOuts == 1 && game.bigText.text == "KNOCKED OUT" && Flat(player.transform.position - game.RespawnPoint) < 1f && fists.Health == fists.maxHealth);
		boxer.testForceFight = false;
		boxer.testNoDodge = false;

		// Police on foot
		pm.ResetWanted();
		pm.enabled = true;
		pm.bustSeconds = 60f;   // no BUSTED while we fight
		fists.Heal();
		pm.ReportCrime(2, "Fight test");
		float copWait = Time.time + 40f;
		while (PoliceFoot.All.Count == 0 && Time.time < copWait)
			yield return null;
		Check("Wanted and walking: police get out of their car (" + PoliceFoot.All.Count + " on foot)", PoliceFoot.All.Count >= 1);
		PoliceFoot cop2 = PoliceFoot.All.Count > 0 ? PoliceFoot.All[0] : null;
		float near2 = Time.time + 12f;
		while (cop2 != null && Flat(cop2.transform.position - player.transform.position) > 2f && Time.time < near2)
			yield return null;
		Check("The officer runs to you and shouts (\"" + (cop2 != null ? Said(cop2.gameObject) : "") + "\")", cop2 != null && Flat(cop2.transform.position - player.transform.position) < 2.5f && IsLine(Said(cop2.gameObject), Lingala.PoliceOnFoot));
		int starsBefore = pm.Stars;
		for (int k = 0; k < 6 && cop2 != null && !cop2.IsDown; k++)
		{
			player.transform.rotation = Quaternion.LookRotation(cop2.transform.position - player.transform.position);
			// make sure he is the closest one
			foreach (PoliceFoot other in PoliceFoot.All)
				if (other != cop2) other.transform.position += Vector3.right * 6f;
			cop2.transform.position = player.transform.position + player.transform.forward * 1.1f;
			fists.Punch();
			yield return new WaitForSeconds(0.55f);
		}
		Check("Three punches knock a police officer down (stars " + starsBefore + " -> " + pm.Stars + ")", cop2 != null && cop2.IsDown && pm.Stars > starsBefore);
		pm.bustSeconds = 1.5f;
		int bustsBefore = pm.Busts;
		float bustWait = Time.time + 40f;
		while (pm.Busts == bustsBefore && Time.time < bustWait)
			yield return null;
		Check("Stand still next to the police = BUSTED", pm.Busts == bustsBefore + 1 && PoliceFoot.All.Count == 0);

		// Boulevard Lumumba to N'djili airport
		pm.ResetWanted();
		pm.enabled = false;
		player.Teleport(taxi.transform.position - taxi.transform.right * 2.3f, 0f);
		yield return null;
		game.ToggleCar();
		CarController ride = game.CurrentCar;
		ClearAround(new Vector3(570f, 0, 29f), 60f, ride);
		ride.Teleport(new Vector3(528f, 0f, 29f), 90f);
		ride.testThrottle = 1;
		yield return new WaitForSeconds(4f);
		ride.testThrottle = 0;
		ride.testBrake = true;
		yield return new WaitForSeconds(1f);
		Check("Boulevard Lumumba takes you out of town to the airport (x " + ride.transform.position.x.ToString("0") + ", \"" + game.zoneText.text + "\")",
			ride.transform.position.x > 600f && game.zoneText.text == "Aéroport de N'djili");
		Check("Welcome banner: \"" + game.areaBanner.text + "\"", game.areaBanner.text.Contains("N'DJILI"));
		int planes = 0;
		foreach (Transform t in GameObject.Find("City").GetComponentsInChildren<Transform>())
			if (t.name.StartsWith("Plane")) planes++;
		PlaneTakeoff flyer = FindObjectOfType<PlaneTakeoff>();
		float flyX = flyer.transform.position.x;
		yield return new WaitForSeconds(1f);
		Check("Planes at the airport (" + planes + "), one takes off again and again (" + flyer.Takeoffs + " takeoffs so far)",
			planes >= 4 && (flyer.Takeoffs >= 1 || Mathf.Abs(flyer.transform.position.x - flyX) > 5f));
		Place airport = System.Array.Find(missions.places, pl => pl.placeName == "Aéroport de N'djili");
		Check("The airport is a mission place", airport != null);

		// The police follow you there
		pm.enabled = true;
		pm.ReportCrime(2, "Airport test");
		float closestAir = float.MaxValue;
		float airWait = Time.time + 60f;
		while (closestAir > 30f && Time.time < airWait)
		{
			foreach (TrafficDriver d in pm.Police)
				if (d != null && d.Chasing)
					closestAir = Mathf.Min(closestAir, Flat(d.transform.position - ride.transform.position));
			yield return null;
		}
		Check("The police follow you to the airport (closest " + closestAir.ToString("0") + " m)", closestAir < 30f);
		ride.testBrake = false;
		pm.ResetWanted();

		// ================= Version 7: fly a plane =================
		pm.ResetWanted();
		pm.enabled = false;
		game.LeaveVehicle();
		yield return null;
		PlaneController jet = PlaneController.All.Find(pl => pl.OnGround && !pl.HasPilot);
		Check("You can fly " + PlaneController.All.Count + " planes at the airport", PlaneController.All.Count == 3 && jet != null);
		Vector3 nose = jet.transform.position + jet.transform.forward * 21f;
		player.Teleport(new Vector3(nose.x, 0.05f, nose.z), jet.transform.eulerAngles.y + 180f);
		ClearAround(nose, 10f);
		yield return null;
		yield return null;
		Check("Hint next to a plane: \"" + game.hintText.text + "\"", game.hintText.text.Contains("get in the plane") && game.hintText.text.Contains("Kota na avion"));
		bool boarded = game.ToggleCar();
		yield return null;
		Check("E next to a plane = you get in (the camera follows the plane)", boarded && game.InPlane && game.CurrentPlane == jet && !player.gameObject.activeSelf && rig.target == jet.transform);

		// Take off from the runway
		jet.Place(new Vector3(640f, jet.Home.y, -95f), 90f);
		jet.testThrottle = 1;
		yield return new WaitForSeconds(4f);
		Check("W = speed up on the runway (" + Mathf.RoundToInt(jet.Speed * 3.6f) + " km/h)", jet.Speed > 30f && jet.OnGround);
		jet.testPitch = 1;
		yield return new WaitForSeconds(4f);
		Check("↑ or Space = take off and climb (" + jet.Altitude.ToString("0") + " m high)", jet.Altitude > 15f && !jet.OnGround);
		Check("Speed and height on the screen (\"" + game.speedText.text + "\")", game.speedText.text.Contains("km/h") && game.speedText.text.EndsWith(" m"));
		float jetYaw = jet.transform.eulerAngles.y;
		jet.testPitch = 0;
		jet.testTurn = 1;
		yield return new WaitForSeconds(2f);
		jet.testTurn = 0;
		Check("A/D = turn in the air (" + Mathf.Abs(Mathf.DeltaAngle(jetYaw, jet.transform.eulerAngles.y)).ToString("0") + "°)", Mathf.Abs(Mathf.DeltaAngle(jetYaw, jet.transform.eulerAngles.y)) > 30f);
		Check("The mini-map and area name follow the plane", Flat(map.mapCam.transform.position - jet.transform.position) < 1f && game.Focus == jet.transform);
		Check("You can't get out while flying", !game.ToggleCar() && game.InPlane);

		// Crash into a Gombe tower
		int moneyBeforeCrash = game.Money;
		jet.testThrottle = 0;
		jet.Place(new Vector3(48f, 20f, 15f), 0f, 50f);   // straight at a Gombe tower
		float crashWait = Time.time + 4f;
		while (jet.Crashes == 0 && Time.time < crashWait)
			yield return null;
		yield return null;
		Check("Fly into a building = CRASHED (\"" + game.bigSubText.text + "\"), you wait at the airport",
			jet.Crashes == 1 && game.bigText.text == "CRASHED" && !game.InPlane && player.gameObject.activeSelf &&
			game.Money == moneyBeforeCrash - Mathf.Min(moneyBeforeCrash, 100) && player.transform.position.x > 600f && jet.OnGround);

		// Get in again and get out on the ground
		Vector3 nose2 = jet.transform.position + jet.transform.forward * 21f;
		player.Teleport(new Vector3(nose2.x, 0.05f, nose2.z), 0f);
		yield return null;
		game.ToggleCar();
		yield return null;
		bool leftPlane = game.ToggleCar();
		yield return null;
		Check("E on the ground, stopped = get out of the plane", leftPlane && !game.InPlane && player.gameObject.activeSelf);

		// ================= Version 8: a rich man with a goal =================
		game.LeaveVehicle();
		pm.ResetWanted();
		pm.enabled = false;
		yield return null;
		bool dressed = true;
		foreach (string part in new[] { "Hat", "Sunglasses", "Tie", "Gold Chain", "Jacket" })
			dressed &= FindDeep(player.transform, part) != null;
		Check("You are a rich sapeur (suit, hat, sunglasses, tie, gold chain)", dressed);
		Check("Your own car is a shiny black luxury car (no taxi stripe)", taxi.transform.Find("Stripe") == null && taxi.transform.Find("Body").GetComponent<Renderer>().sharedMaterial.color.grayscale < 0.1f);
		Check("The goal on screen: \"" + game.goalText.text + "\"", game.goalMoney == 25000 && game.goalText.text.Contains("25,000"));
		missions.StartMission(missions.Find("Gombe"), missions.Find("Matonge"));
		player.Teleport(new Vector3(-116f, 0.05f, -36.6f), 0f);
		yield return null;
		yield return null;
		TargetArrow ta = game.GetComponent<TargetArrow>();
		Vector3 toTarget = missions.Target.markerPosition - player.transform.position;
		toTarget.y = 0;
		float arrowAngle = Vector3.Angle(ta.PointsTo, toTarget.normalized);
		Check("A yellow arrow over your head points to the mission (off by " + arrowAngle.ToString("0") + "°)", ta.arrow.gameObject.activeSelf && arrowAngle < 5f && ta.arrow.position.y > player.transform.position.y + 2f);
		game.AddMoney(game.goalMoney - game.Money + 10);
		yield return null;
		Check("Earn $25,000 = \"" + game.bigText.text + "\" (" + game.bigSubText.text + ")", game.GoalReached && game.bigText.text.StartsWith("MOKONZI") && game.goalText.text.StartsWith("Mokonzi"));

		// Landing a plane is easy now
		PlaneController lander = PlaneController.All.Find(pl => !pl.HasPilot && pl.OnGround);
		Vector3 noseL = lander.transform.position + lander.transform.forward * 21f;
		player.Teleport(new Vector3(noseL.x, 0.05f, noseL.z), 0f);
		yield return null;
		game.ToggleCar();
		int crashesBefore = lander.Crashes;
		lander.Place(new Vector3(760f, lander.Home.y + 40f, -95f), 90f, 40f);
		lander.testPitch = -1;
		float landWait = Time.time + 15f;
		while (!lander.OnGround && Time.time < landWait)
			yield return null;
		Check("Hold ↓ to come down: the plane levels out near the ground and lands softly", lander.OnGround && lander.Crashes == crashesBefore && game.InPlane);
		lander.testPitch = 0;
		lander.testThrottle = -1;
		yield return new WaitForSeconds(3f);
		lander.testThrottle = 0;
		bool gotOut = game.ToggleCar();
		yield return null;
		Check("S to stop, then E to get out (speed " + Mathf.RoundToInt(lander.Speed * 3.6f) + " km/h)", gotOut && !game.InPlane);

		// ================= Version 9: the pistol =================
		player.Teleport(new Vector3(58f, 0.05f, -94.6f), 90f);
		rig.SetYaw(90f);
		rig.SetPitch(8f);
		ClearAround(player.transform.position, 25f);
		yield return null;
		yield return null;
		PlayerWeapons gun = player.GetComponent<PlayerWeapons>();
		gun.SetPistol(true);
		yield return null;
		Check("Q = take out the pistol (\"" + gun.weaponText.text + "\", + in the middle of the screen)", gun.PistolOut && gun.crosshair.enabled && gun.pistolModel.activeSelf && gun.weaponText.text.StartsWith("PISTOL"));
		Camera cam = rig.GetComponent<Camera>();
		Ray aim = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
		float tHit = aim.direction.y < -0.01f ? (aim.origin.y - 1f) / -aim.direction.y : 12f;
		Vector3 spot2 = aim.GetPoint(tHit);
		Pedestrian target2 = Pedestrian.All.Find(p => !p.IsDown && !p.Crossing && p.CurrentMood == Pedestrian.Mood.Walk);
		Pedestrian bystander = Pedestrian.All.Find(p => p != target2 && !p.IsDown && !p.Crossing && p.CurrentMood == Pedestrian.Mood.Walk);
		target2.testHold = true;
		target2.testNoDodge = true;
		ClearPeopleAround(spot2, 6f, target2);
		target2.transform.position = new Vector3(spot2.x, 0.06f, spot2.z);
		bystander.transform.position = player.transform.position + new Vector3(-4f, 0.01f, 3f);
		pm.ResetWanted();
		yield return null;
		Component shotHit = gun.Shoot();
		Check("Left click with the pistol = shoot: the person falls (" + (shotHit != null ? shotHit.name : "missed") + ")", shotHit == target2 && target2.IsDead);
		Check("Shooting gives wanted stars (" + pm.Stars + ")", pm.Stars >= 2);
		Check("Gunshots scare people: they run away", bystander.CurrentMood == Pedestrian.Mood.Flee);
		yield return new WaitForSeconds(3f);
		Check("Someone you shot doesn't get up", target2 != null && target2.IsDead);
		GameObject copGo = Instantiate(pm.officerPrefab, new Vector3(spot2.x, 0.06f, spot2.z), Quaternion.identity);
		PoliceFoot cop3 = copGo.GetComponent<PoliceFoot>();
		cop3.Init(null);
		Destroy(target2.gameObject);
		yield return new WaitForSeconds(0.3f);
		Component shotCop = gun.Shoot();
		Check("Shoot a police officer: he goes down (stars " + pm.Stars + ")", shotCop == cop3 && cop3.IsDown);
		gun.SetPistol(false);
		yield return null;
		Check("Q again = back to your fists", !gun.PistolOut && !gun.crosshair.enabled && gun.weaponText.text.StartsWith("FISTS"));
		pm.ResetWanted();

		// ================= Version 10: inside the car, which way to go, the mission card =================
		pm.ResetWanted();
		pm.enabled = false;
		game.LeaveVehicle();
		yield return null;
		missions.StartMission(missions.Find("Grand Marché"), missions.Find("Gombe"));
		yield return null;
		string card = missions.briefingText.text;
		Check("New mission card: what to pick up, for whom, where, the pay and your goal (" + missions.Item + " for " + missions.Customer + ")",
			missions.BriefingShowing && card.Contains("Pick up") && card.Contains(missions.Item) && card.Contains("Bring it to") && card.Contains(missions.Customer) &&
			card.Contains("Grand Marché") && card.Contains("Gombe") && card.Contains("Pay:") && card.Contains("goal"));
		Check("Mission box: \"" + missions.missionText.text.Split('\n')[0] + "\"", missions.missionText.text.StartsWith("STEP 1 of 2") && missions.missionText.text.Contains(missions.Item));

		TargetArrow guide = game.GetComponent<TargetArrow>();
		player.Teleport(new Vector3(10f, 0.05f, -36.6f), 0f);
		rig.SetYaw(0f);
		yield return null;
		yield return null;
		Vector3 camFlat = rig.transform.forward;
		camFlat.y = 0;
		Vector3 toPickup = missions.Pickup.markerPosition - player.transform.position;
		toPickup.y = 0;
		float wantAngle = Vector3.SignedAngle(camFlat, toPickup, Vector3.up);
		float shownAngle = -guide.hudArrow.localEulerAngles.z;
		Check("Arrow on the screen points to the mission (" + guide.hudText.text + ")",
			guide.hudArrow.gameObject.activeSelf && Mathf.Abs(Mathf.DeltaAngle(shownAngle, wantAngle)) < 3f && guide.hudText.text.Contains("Grand Marché") && guide.hudText.text.EndsWith(" m"));

		TrafficDriver cityCar = tm.Drivers.Find(d => d != null && d.enabled);
		Renderer window = cityCar.transform.Find("Glass").GetComponent<Renderer>();
		Check("City cars have a driver inside and see-through windows", cityCar.transform.Find("Driver").gameObject.activeSelf && window.sharedMaterial.shader.name.Contains("Transparent"));

		player.Teleport(taxi.transform.position - taxi.transform.right * 2.3f, 0f);
		ClearAround(taxi.transform.position, 8f);
		yield return null;
		game.ToggleCar();
		yield return null;
		Check("You sit at the wheel of your car (seen through the windows)", game.InCar && game.CurrentCar == taxi && game.seatedYou.activeSelf && game.seatedYou.transform.parent == taxi.transform);
		game.ToggleInsideView();
		yield return null;
		yield return null;
		Vector3 eyes = taxi.transform.TransformPoint(rig.driverEyes);
		Vector3 lookFlat = rig.transform.forward;
		lookFlat.y = 0;
		Check("V = view from inside the car, looking where you drive", rig.insideView && Vector3.Distance(rig.transform.position, eyes) < 0.05f &&
			Vector3.Dot(lookFlat.normalized, taxi.transform.forward) > 0.95f && !game.seatedYou.activeSelf);
		taxi.testThrottle = 1;
		yield return new WaitForSeconds(0.8f);
		taxi.testThrottle = 0;
		taxi.testBrake = true;
		Check("Driving from inside: the camera moves with the car", Vector3.Distance(rig.transform.position, taxi.transform.TransformPoint(rig.driverEyes)) < 0.05f);
		yield return new WaitForSeconds(0.8f);
		taxi.testBrake = false;
		game.ToggleInsideView();
		yield return null;
		Check("V again = view from behind (you can see yourself again)", !rig.insideView && game.seatedYou.activeSelf);
		game.ToggleCar();
		yield return null;
		Check("Get out: you are not sitting in the car anymore", !game.InCar && !game.seatedYou.activeSelf && player.gameObject.activeSelf);

		cityCar = tm.Drivers.Find(d => d != null && d.enabled && !d.police);
		player.Teleport(cityCar.transform.position - cityCar.transform.right * 2.4f, 0f);
		yield return null;
		ClearAround(cityCar.transform.position, 8f, cityCar.Car);
		game.ToggleCar();
		yield return null;
		Check("Take a city car: their driver gets out, you sit at the wheel", game.CurrentCar == cityCar.Car && !cityCar.transform.Find("Driver").gameObject.activeSelf && game.seatedYou.transform.parent == cityCar.transform);
		game.LeaveVehicle();
		pm.ResetWanted();

		Debug.Log("TOSAKANA-CHECK DONE: " + passed + " passed, " + failed + " failed");
	}

	void Check(string name, bool ok)
	{
		if (ok) passed++; else failed++;
		Debug.Log("TOSAKANA-CHECK " + (ok ? "PASS " : "FAIL ") + name);
	}

	// Moves other cars out of the way of a test
	void ClearAround(Vector3 p, float radius, CarController keep = null)
	{
		int k = 0;
		foreach (CarController car in CarController.All.ToArray())
			if (car != taxi && car != keep && Flat(car.transform.position - p) < radius)
				car.Teleport(new Vector3(-250f - 8f * k++, 0f, -250f), 0f);
	}

	static Transform FindDeep(Transform root, string name)
	{
		foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
			if (t.name == name) return t;
		return null;
	}

	// Moves other people a little away (so the right person is the closest)
	static void ClearPeopleAround(Vector3 p, float radius, Pedestrian keep)
	{
		foreach (Pedestrian other in Pedestrian.All)
			if (other != keep && Flat(other.transform.position - p) < radius)
				other.transform.position += (other.transform.position - p).normalized * radius + Vector3.forward * 0.1f;
	}

	// Inside the sidewalk ring of a block (between 19.5 and 23 m from the block center, along one side)
	static bool OnSidewalk(Vector3 p)
	{
		float bx = -116f + Mathf.Round((p.x + 116f) / 58f) * 58f;
		float bz = -116f + Mathf.Round((p.z + 116f) / 58f) * 58f;
		float dx = Mathf.Abs(p.x - bx), dz = Mathf.Abs(p.z - bz);
		return Mathf.Max(dx, dz) > 19.5f && Mathf.Max(dx, dz) < 23f;
	}

	// What this person, car or you said out loud in the last few seconds
	static string Said(GameObject who)
	{
		VoiceSpeaker v = who.GetComponent<VoiceSpeaker>();
		return v != null && v.LastLingala != null && Time.time - v.LastTime < 8f ? v.LastLingala : "";
	}

	static bool IsLine(string text, Lingala.Line[] lines)
	{
		foreach (Lingala.Line l in lines)
			if (l.lingala == text) return true;
		return false;
	}

	static int StarsLit(PoliceManager pm)
	{
		int lit = 0;
		foreach (RawImage star in pm.starIcons)
			if (star.color.a > 0.3f) lit++;
		return lit;
	}

	static bool ChaserLightsOn(PoliceManager pm)
	{
		foreach (TrafficDriver d in pm.Police)
			if (d != null && d.Chasing)
			{
				PoliceLights l = d.GetComponent<PoliceLights>();
				if (l.On && l.siren.clip != null) return true;
			}
		return false;
	}

	static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }

	static bool HasText(GameObject root, string value)
	{
		foreach (Text t in root.GetComponentsInChildren<Text>(true))
			if (t.text.Contains(value)) return true;
		return false;
	}
}
