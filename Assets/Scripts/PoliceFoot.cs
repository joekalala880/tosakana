using System.Collections.Generic;
using UnityEngine;

// A police officer on foot. When you are wanted and walking, officers get out of their car and run at you.
// If one reaches you, he hits you and tries to arrest you (stand still too long = BUSTED).
// Three punches knock him down for a while.
public class PoliceFoot : MonoBehaviour
{
	public static readonly List<PoliceFoot> All = new List<PoliceFoot>();

	public float runSpeed = 5.5f;
	public int maxHealth = 3;
	public int punchDamage = 12;
	public Transform leftLeg, rightLeg, leftArm, rightArm;

	public TrafficDriver Car { get; private set; }
	public int Health { get; private set; }
	public bool IsDown { get { return downTimer > 0; } }
	public bool Returning { get; private set; }
	public int HitsGiven { get; private set; }

	float downTimer, staggerTimer, attackCooldown = 0.6f, shoutCooldown, cycle, punchAnim, yaw, lostTimer;

	void OnEnable() { All.Add(this); }
	void OnDisable() { All.Remove(this); }

	public void Init(TrafficDriver fromCar)
	{
		Car = fromCar;
		Health = maxHealth;
		if (Car != null)
			Car.OfficerOut = true;
		Voice.Say(gameObject, Lingala.Pick(Lingala.PoliceOnFoot));
		shoutCooldown = 4f;
	}

	// Go back to the car and drive away (the chase is over, or you got in a car)
	public void GoBack() { Returning = true; }

	void OnDestroy()
	{
		if (Car != null)
			Car.OfficerOut = false;
	}

	void Update()
	{
		TosakanaGame game = TosakanaGame.Instance;
		if (game == null)
			return;
		float dt = Time.deltaTime;

		if (downTimer > 0)
		{
			downTimer -= dt;
			transform.rotation = Quaternion.Euler(downTimer > 0 ? -82f : 0f, yaw, 0);
			if (downTimer <= 0)
				Health = maxHealth;
			Swing(0);
			return;
		}
		if (staggerTimer > 0)
		{
			staggerTimer -= dt;
			Swing(0);
			return;
		}

		PoliceManager pm = PoliceManager.Instance;
		if (pm == null || pm.Stars == 0 || game.InVehicle)
			Returning = true;

		if (Returning)
		{
			// Walk back to the car, then leave
			if (Car == null || !Car.enabled)
			{
				lostTimer += dt;
				if (lostTimer > 3f)
					Destroy(gameObject);
				return;
			}
			if (MoveTo(Car.transform.position, runSpeed * 0.7f, dt) < 2.5f)
				Destroy(gameObject);
			return;
		}

		Vector3 target = game.player.transform.position;
		float distance = MoveTo(target, runSpeed, dt, 1.2f);
		if (distance <= 1.3f)
		{
			attackCooldown -= dt;
			if (attackCooldown <= 0)
			{
				attackCooldown = 1.2f;
				punchAnim = 0.25f;
				HitsGiven++;
				PlayerCombat combat = game.player.GetComponent<PlayerCombat>();
				if (combat != null)
					combat.TakeDamage(punchDamage);
			}
		}
		shoutCooldown -= dt;
		if (shoutCooldown <= 0)
		{
			shoutCooldown = 5f;
			Voice.Say(gameObject, Lingala.Pick(Lingala.PoliceOnFoot));
		}
	}

	// Run toward a point; returns the distance left
	float MoveTo(Vector3 target, float speed, float dt, float stopAt = 0.5f)
	{
		Vector3 to = target - transform.position;
		to.y = 0;
		float distance = to.magnitude;
		if (distance > stopAt)
		{
			transform.position += Vector3.ClampMagnitude(to, speed * dt);
			Swing(speed);
		}
		else
			Swing(0);
		if (to.sqrMagnitude > 0.001f)
		{
			yaw = Quaternion.LookRotation(to).eulerAngles.y;
			transform.rotation = Quaternion.Euler(0, yaw, 0);
		}
		return distance;
	}

	// Shot: down for good, then taken away
	public void TakeShot()
	{
		if (IsDown)
			return;
		Health = 0;
		downTimer = 20f;
		Voice.Say(gameObject, Lingala.Pick(Lingala.Hit));
		if (Car != null)
			Car.OfficerOut = false;
		Destroy(gameObject, 20f);
	}

	public void TakePunch(PlayerCombat by)
	{
		if (IsDown)
			return;
		Health--;
		Vector3 away = transform.position - by.transform.position;
		away.y = 0;
		transform.position += away.normalized * 0.8f;
		Voice.Say(gameObject, Lingala.Pick(Lingala.Hit));
		if (PoliceManager.Instance != null)
			PoliceManager.Instance.ReportCrime(1, "You hit a police officer!");
		if (Health <= 0)
		{
			downTimer = 8f;
			yaw = Quaternion.LookRotation(-away.normalized).eulerAngles.y;
		}
		else
			staggerTimer = 0.5f;
	}

	void Swing(float speed)
	{
		cycle += Time.deltaTime * speed * 3f;
		float swing = Mathf.Sin(cycle) * Mathf.Clamp01(speed / 3f) * 40f;
		if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(swing, 0, 0);
		if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
		if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-swing * 0.8f, 0, 0);
		punchAnim -= Time.deltaTime;
		if (rightArm != null) rightArm.localRotation = punchAnim > 0 ? Quaternion.Euler(-90f, 0, 0) : Quaternion.Euler(swing * 0.8f, 0, 0);
	}
}
