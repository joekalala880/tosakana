using UnityEngine;

// All the sounds are made in code, so no sound files are needed.
public class GameSound : MonoBehaviour
{
	[Range(0, 1)] public float engineVolume = 0.35f;
	[Range(0, 1)] public float cityVolume = 0.08f;
	[Range(0, 1)] public float effectsVolume = 0.8f;

	AudioSource engine, city, effects;
	AudioClip doorClip, pickupClip, passedClip, failedClip, crashClip, hornClip, whistleClip, punchClip, whooshClip;
	float crashCooldown;

	public AudioClip SirenClip { get; private set; }
	public AudioClip JetClip { get; private set; }
	public float EngineVolume { get { return engine.volume; } }
	public float EnginePitch { get { return engine.pitch; } }

	const int Rate = 44100;

	void Awake()
	{
		engine = gameObject.AddComponent<AudioSource>();
		engine.loop = true;
		engine.volume = 0;
		engine.clip = Engine();
		engine.Play();

		city = gameObject.AddComponent<AudioSource>();
		city.loop = true;
		city.volume = cityVolume;
		city.clip = Noise("City", 3f, 0.04f, true);
		city.Play();

		effects = gameObject.AddComponent<AudioSource>();

		doorClip = Thump("Door", 0.15f, 120f);
		crashClip = Thump("Crash", 0.4f, 70f);
		pickupClip = Tones("Pickup", new[] { 660f, 990f }, 0.1f, 0.35f);
		passedClip = Tones("Passed", new[] { 523f, 659f, 784f, 659f, 1047f }, 0.14f, 0.4f);
		failedClip = Tones("Failed", new[] { 392f, 330f, 262f }, 0.25f, 0.4f);
		hornClip = MakeHorn();
		SirenClip = MakeSiren();
		whistleClip = MakeWhistle();
		punchClip = Thump("Punch", 0.12f, 160f);
		whooshClip = Noise("Whoosh", 0.18f, 0.12f, false);
		JetClip = Noise("Jet", 3f, 0.09f, true);
	}

	void Update()
	{
		TosakanaGame game = TosakanaGame.Instance;
		crashCooldown -= Time.deltaTime;
		bool driving = game != null && game.InCar;
		float target = driving ? engineVolume : 0f;
		engine.volume = Mathf.MoveTowards(engine.volume, target, Time.deltaTime * 1.5f);
		if (driving)
		{
			float s = Mathf.Abs(game.CurrentCar.Speed) / game.CurrentCar.maxSpeed;
			engine.pitch = Mathf.Lerp(engine.pitch, 0.7f + s * 1.5f, Time.deltaTime * 5f);
		}
	}

	public void Door() { effects.PlayOneShot(doorClip, effectsVolume * 0.7f); }
	public void Pickup() { effects.PlayOneShot(pickupClip, effectsVolume); }
	public void Passed() { effects.PlayOneShot(passedClip, effectsVolume); }
	public void Failed() { effects.PlayOneShot(failedClip, effectsVolume); }
	public void Horn() { effects.PlayOneShot(hornClip, effectsVolume * 0.6f); }

	// Sounds that come from a place in the city (a car honking at you, someone getting hit)
	public void HornAt(Vector3 position) { AudioSource.PlayClipAtPoint(hornClip, position, effectsVolume * 0.7f); }
	public void BumpAt(Vector3 position) { AudioSource.PlayClipAtPoint(doorClip, position, effectsVolume); }
	public void PunchAt(Vector3 position) { AudioSource.PlayClipAtPoint(punchClip, position, effectsVolume); }
	public void Whoosh() { effects.PlayOneShot(whooshClip, effectsVolume * 0.5f); }
	public void WhistleAt(Vector3 position) { AudioSource.PlayClipAtPoint(whistleClip, position, effectsVolume * 0.8f); }

	public void Crash()
	{
		if (crashCooldown > 0)
			return;
		crashCooldown = 0.4f;
		effects.PlayOneShot(crashClip, effectsVolume);
	}

	// ---------- Sounds made in code ----------

	// Engine hum: 1 second with whole waves, so it loops without a click
	static AudioClip Engine()
	{
		float[] data = new float[Rate];
		for (int i = 0; i < data.Length; i++)
		{
			float t = (float)i / Rate;
			float rumble = 0.7f + 0.3f * Mathf.Sin(2 * Mathf.PI * 25f * t);
			float wave = 0.5f * Mathf.Sin(2 * Mathf.PI * 50f * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * 100f * t) + 0.15f * Mathf.Sin(2 * Mathf.PI * 150f * t);
			data[i] = wave * rumble * 0.5f;
		}
		return Make("Engine", data);
	}

	// Police siren: the pitch goes up and down (wee-ooo)
	static AudioClip MakeSiren()
	{
		int count = Rate * 2;
		float[] data = new float[count];
		float phase = 0;
		for (int i = 0; i < count; i++)
		{
			float t = (float)i / Rate;
			float freq = 750f + 260f * Mathf.Sin(2 * Mathf.PI * 0.5f * t);
			phase += 2 * Mathf.PI * freq / Rate;
			data[i] = (Mathf.Sin(phase) * 0.6f + Mathf.Sin(phase * 2f) * 0.15f) * 0.35f;
		}
		return Make("Siren", data);
	}

	// The roulage whistle: two sharp trills
	static AudioClip MakeWhistle()
	{
		int count = (int)(Rate * 0.7f);
		float[] data = new float[count];
		float phase = 0;
		for (int i = 0; i < count; i++)
		{
			float t = (float)i / Rate;
			bool on = t < 0.25f || (t > 0.35f && t < 0.65f);
			float freq = 2800f + 180f * Mathf.Sin(2 * Mathf.PI * 32f * t);   // the little ball inside makes it trill
			phase += 2 * Mathf.PI * freq / Rate;
			data[i] = on ? Mathf.Sin(phase) * 0.3f : 0f;
		}
		return Make("Whistle", data);
	}

	static AudioClip MakeHorn()
	{
		int count = (int)(Rate * 0.45f);
		float[] data = new float[count];
		for (int i = 0; i < count; i++)
		{
			float t = (float)i / Rate;
			float edge = Mathf.Clamp01(Mathf.Min(t / 0.02f, (0.45f - t) / 0.05f));
			float wave = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 400f * t)) * 0.5f + Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 500f * t)) * 0.5f;
			data[i] = wave * edge * 0.25f;
		}
		return Make("Horn", data);
	}

	static AudioClip Tones(string name, float[] notes, float noteLength, float volume)
	{
		int perNote = (int)(Rate * noteLength);
		float[] data = new float[perNote * notes.Length];
		for (int n = 0; n < notes.Length; n++)
			for (int i = 0; i < perNote; i++)
			{
				float t = (float)i / Rate;
				float fade = 1f - (float)i / perNote;
				data[n * perNote + i] = Mathf.Sin(2 * Mathf.PI * notes[n] * t) * fade * volume;
			}
		return Make(name, data);
	}

	static AudioClip Thump(string name, float length, float pitch)
	{
		int count = (int)(Rate * length);
		float[] data = new float[count];
		System.Random random = new System.Random(7);
		for (int i = 0; i < count; i++)
		{
			float t = (float)i / Rate;
			float fade = Mathf.Pow(1f - (float)i / count, 2);
			float thump = Mathf.Sin(2 * Mathf.PI * pitch * t * (1.5f - t)) * 0.7f;
			float crack = ((float)random.NextDouble() * 2f - 1f) * 0.4f;
			data[i] = (thump + crack) * fade * 0.6f;
		}
		return Make(name, data);
	}

	static AudioClip Noise(string name, float length, float volume, bool loop)
	{
		int count = (int)(Rate * length);
		float[] data = new float[count];
		System.Random random = new System.Random(5);
		float smooth = 0;
		for (int i = 0; i < count; i++)
		{
			smooth = Mathf.Lerp(smooth, (float)random.NextDouble() * 2f - 1f, 0.08f);
			float fade = loop ? 1f : 1f - (float)i / count;
			data[i] = smooth * fade * volume * 4f;
		}
		return Make(name, data);
	}

	static AudioClip Make(string name, float[] data)
	{
		AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
		clip.SetData(data, 0);
		return clip;
	}
}
