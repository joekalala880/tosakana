using System.Collections.Generic;
using UnityEngine;

// Plays the Lingala lines out loud.
// Your recordings live in Assets/Resources/Voices/<line name>.wav (make them with Tools > Tosakana > Record Voices).
// A computer voice can live in Assets/Resources/Voices/Computer/<line name>.wav. Your own recording always wins.
// A line you did not record stays quiet (no fake voice). Only one voice talks at a time.
public static class Voice
{
	static readonly Dictionary<string, AudioClip> recordings = new Dictionary<string, AudioClip>();
	static VoiceSpeaker narrator;

	public static AudioClip Recording(Lingala.Line line)
	{
		string key = line.Key;
		AudioClip clip;
		if (!recordings.TryGetValue(key, out clip))
		{
			clip = Resources.Load<AudioClip>("Voices/" + key);
			if (clip == null)
				clip = Resources.Load<AudioClip>("Voices/Computer/" + key);
			recordings[key] = clip;
		}
		return clip;
	}

	public static bool HasRecording(Lingala.Line line) { return Recording(line) != null; }
	public static void ForgetRecordings() { recordings.Clear(); }

	// Someone (a person, a car, you) says a line. The sound comes from where they are.
	// important (the default) = a reaction to what you do (hit, police, answers, horns): it waits its turn and is never skipped.
	// important = false (people saying hello as you walk by): only said when nobody else is talking and you are close.
	public static VoiceSpeaker Say(GameObject who, Lingala.Line line, bool important = true)
	{
		VoiceSpeaker speaker = who.GetComponent<VoiceSpeaker>();
		if (speaker == null)
			speaker = who.AddComponent<VoiceSpeaker>();
		speaker.Speak(line, important || who.GetComponent<PlayerController>() != null);
		return speaker;
	}

	// A voice you hear the same everywhere (the customer's blessing, the police "Bakangi yo!")
	public static VoiceSpeaker Narrator
	{
		get
		{
			if (narrator == null)
			{
				GameObject go = new GameObject("Narrator Voice");
				narrator = go.AddComponent<VoiceSpeaker>();
				narrator.MakeFlat();
			}
			return narrator;
		}
	}

	// important = false: only if nobody is talking (used for the commune welcomes, so they never interrupt a blessing or BUSTED)
	public static void SayToYou(Lingala.Line line, bool important = true)
	{
		Narrator.Speak(line, important);
	}

	// One voice at a time in the whole city
	public static VoiceSpeaker Talking;
	public static float QuietUntil;
	public static bool Busy { get { return (Talking != null && Talking.Speaking) || Time.time < QuietUntil; } }
}

// Put on anything that talks. Everyone uses your recorded voice, exactly as you recorded it.
public class VoiceSpeaker : MonoBehaviour
{
	public string LastLingala { get; private set; }
	public float LastTime { get; private set; }
	public bool LastWasRecording { get; private set; }
	public AudioClip CurrentClip { get { return source != null ? source.clip : null; } }
	public bool Speaking { get { return source != null && source.isPlaying; } }

	AudioSource source;

	void Awake()
	{
		source = gameObject.AddComponent<AudioSource>();
		source.playOnAwake = false;
		source.spatialBlend = 1f;
		source.rolloffMode = AudioRolloffMode.Linear;
		source.minDistance = 4f;
		source.maxDistance = 45f;
		source.volume = 1f;
		source.pitch = 1f;
	}

	// The narrator is heard the same everywhere
	public void MakeFlat()
	{
		if (source == null) Awake();
		source.spatialBlend = 0f;
		source.pitch = 1f;
	}

	Lingala.Line waiting;
	bool hasWaiting;
	float waitUntil;

	public void Speak(Lingala.Line line, bool important)
	{
		if (source == null) Awake();
		AudioClip clip = Voice.Recording(line);
		bool busy = Voice.Busy;
		// A small line that can't be said now (someone else is talking, or you are far) is just skipped
		if (!important && (clip == null || busy || (source.spatialBlend > 0.5f && TooFar(30f))))
			return;
		LastWasRecording = clip != null;
		LastLingala = line.lingala;
		LastTime = Time.time;
		if (clip == null)
			return;
		if (source.spatialBlend > 0.5f && TooFar(45f))
			return;                      // too far to hear anyway
		if (busy)
		{
			waiting = line;              // wait for the other voice to finish, then talk
			hasWaiting = true;
			waitUntil = Time.time + (source.spatialBlend > 0.5f ? 4f : 8f);
			return;
		}
		Play(clip);
	}

	bool TooFar(float meters)
	{
		TosakanaGame game = TosakanaGame.Instance;
		return game != null && game.Focus != null && Vector3.Distance(game.Focus.position, transform.position) > meters;
	}

	void Play(AudioClip clip)
	{
		source.Stop();
		source.clip = clip;
		source.Play();
		Voice.Talking = this;
		Voice.QuietUntil = Time.time + clip.length + 0.4f;   // a short pause between voices
	}

	void Update()
	{
		if (!hasWaiting)
			return;
		if (Time.time > waitUntil)
			hasWaiting = false;
		else if (!Voice.Busy)
		{
			AudioClip clip = Voice.Recording(waiting);
			hasWaiting = false;
			if (clip != null) Play(clip);
		}
	}
}
