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
			buildings > 200 && CarController.All.Count >= 25 && missions.places.Length == 16);

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
		pm.enabled = true;
		yield return null;
		Check("2 police cars patrol the city (" + pm.Police.Count + "), no stars yet", pm.Police.Count >= 2 && pm.Stars == 0 && pm.ChasingCount == 0 && StarsLit(pm) == 0);
		PoliceLights firstLights = pm.Police[0].GetComponent<PoliceLights>();
		Check("Police cars show on the mini-map", firstLights.mapIcon.gameObject.layer == 31 && firstLights.mapIcon.gameObject.activeInHierarchy);

		// Crime: hit a person with your car
		Pedestrian victim = Pedestrian.All.Find(p => p != unlucky && !p.Crossing && !p.IsDown);
		victim.testHold = true;
		victim.testNoDodge = true;
		ClearAround(new Vector3(-8f, 0, -87f), 25f, stopCar);
		stopCar.Teleport(new Vector3(-20f, 0f, -87f), 90f);
		victim.transform.position = new Vector3(-8f, 0.06f, -87f);
		stopCar.testThrottle = 1;
		yield return new WaitForSeconds(1.5f);
		stopCar.testThrottle = 0;
		stopCar.testBrake = true;
		Check("Hitting a person with your car = 1 wanted star (\"" + pm.wantedText.text + "\")", victim.IsDown && pm.Stars == 1 && StarsLit(pm) == 1);
		victim.testHold = false;
		victim.testNoDodge = false;

		yield return new WaitForSeconds(2.5f);
		Check("A police car chases you with flashing lights and siren", pm.ChasingCount >= 1 && ChaserLightsOn(pm));
		pm.ReportCrime(1, "Another crime");
		yield return new WaitForSeconds(2.5f);
		Check("More stars = more police (" + pm.Stars + " stars, " + pm.ChasingCount + " chasing)", pm.Stars == 2 && pm.ChasingCount >= 2 && StarsLit(pm) == 2);

		// Stay stopped: the police come and catch you
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
		string[] communes = { "Gombe", "Matonge", "Lemba", "Ngiri-Ngiri", "Bandal", "Bumbu", "Selembao", "Mont-Ngafula", "Kintambo", "Limete", "Kalamu", "Lingwala" };
		int found = 0;
		string missing = "";
		foreach (string name in communes)
		{
			Place place = System.Array.Find(missions.places, pl => pl.placeName == name);
			if (place != null && missions.CommuneAt(place.transform.position) == name) found++;
			else missing += name + " ";
		}
		Check("All 12 communes are in the city, each with a mission place (" + found + "/12) " + missing, found == 12);

		if (game.InCar) game.ToggleCar();
		yield return null;
		player.Teleport(new Vector3(58f, 0.05f, -94.6f), 0f);   // a sidewalk in Lemba
		yield return null;
		yield return null;
		Check("Entering a commune: \"" + game.areaBanner.text + "\" / \"" + game.areaBannerSub.text + "\"", game.areaBanner.text == "Boyei malamu na LEMBA" && game.areaBannerSub.text == "Welcome to Lemba");
		Check("Area name at the bottom right says Lemba (" + game.zoneText.text + ")", game.zoneText.text == "Lemba");

		// Talk to someone with T
		Pedestrian friend = Pedestrian.All.Find(p => !p.IsDown && !p.Crossing && !p.carriesBasin);
		friend.testHold = true;
		friend.testNoDodge = true;
		friend.transform.position = player.transform.position + new Vector3(1.8f, 0.01f, 0.5f);
		ClearAround(player.transform.position, 8f);
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
		Check("A line that is not recorded yet plays a soft murmur (" + (friendVoice.CurrentClip != null ? friendVoice.CurrentClip.name : "none") + ")",
			!friendVoice.LastWasRecording && friendVoice.CurrentClip != null && friendVoice.CurrentClip.name.StartsWith("Murmur") && friendVoice.CurrentClip.length > 0.2f);
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
		Check("Roulage officers stand at " + TrafficOfficer.All.Count + " crossroads and switch the traffic (" + switches + " whistles)", TrafficOfficer.All.Count == 5 && switches >= 5);

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
		TrafficOfficer target = TrafficOfficer.At(2, 4);
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
