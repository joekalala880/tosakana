using UnityEngine;

// A plane at N'djili airport: it waits, rolls down the runway faster and faster, takes off and flies away.
// Then it comes back to the start and does it again.
public class PlaneTakeoff : MonoBehaviour
{
	public float runwayLength = 480f;
	public float waitSeconds = 35f;
	public float firstWait = 6f;

	public int Takeoffs { get; private set; }
	public bool Flying { get { return state != State.Waiting; } }

	enum State { Waiting, Rolling, Climbing }
	State state;
	Vector3 start;
	Quaternion startRotation;
	float speed, wait;
	AudioSource engine;

	void Start()
	{
		start = transform.position;
		startRotation = transform.rotation;
		wait = firstWait;
		engine = gameObject.AddComponent<AudioSource>();
		engine.loop = true;
		engine.spatialBlend = 1f;
		engine.rolloffMode = AudioRolloffMode.Linear;
		engine.minDistance = 30f;
		engine.maxDistance = 450f;
		engine.volume = 0.7f;
		if (TosakanaGame.Instance != null)
			engine.clip = TosakanaGame.Instance.sound.JetClip;
	}

	void Update()
	{
		float dt = Time.deltaTime;
		switch (state)
		{
			case State.Waiting:
				wait -= dt;
				if (wait <= 0)
				{
					state = State.Rolling;
					speed = 0;
					if (engine.clip != null) engine.Play();
				}
				break;
			case State.Rolling:
				speed = Mathf.Min(80f, speed + 9f * dt);
				transform.position += transform.forward * speed * dt;
				if (Vector3.Distance(transform.position, start) > runwayLength * 0.65f)
					state = State.Climbing;
				break;
			case State.Climbing:
				speed = Mathf.Min(95f, speed + 4f * dt);
				transform.rotation = Quaternion.RotateTowards(transform.rotation, startRotation * Quaternion.Euler(-11f, 0, 0), 8f * dt);
				transform.position += transform.forward * speed * dt;
				engine.pitch = 1f + transform.position.y / 400f;
				if (transform.position.y > 240f)
				{
					Takeoffs++;
					state = State.Waiting;
					wait = waitSeconds;
					engine.Stop();
					engine.pitch = 1f;
					transform.SetPositionAndRotation(start, startRotation);
				}
				break;
		}
	}
}
