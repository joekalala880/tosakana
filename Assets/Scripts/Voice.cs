using System.Collections.Generic;
using UnityEngine;

// Plays the Lingala lines out loud.
// Your recordings live in Assets/Resources/Voices/<line name>.wav (make them with Tools > Tosakana > Record Voices).
// A computer voice can live in Assets/Resources/Voices/Computer/<line name>.wav. Your own recording always wins.
// A line you did not record yet plays a soft "murmur" that follows the vowels of the words, so the city is never silent.
public static class Voice
{
	static readonly Dictionary<string, AudioClip> recordings = new Dictionary<string, AudioClip>();
	static readonly Dictionary<string, AudioClip> murmurs = new Dictionary<string, AudioClip>();
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
	public static VoiceSpeaker Say(GameObject who, Lingala.Line line, bool murmurIfNotRecorded = true)
	{
		VoiceSpeaker speaker = who.GetComponent<VoiceSpeaker>();
		if (speaker == null)
			speaker = who.AddComponent<VoiceSpeaker>();
		speaker.Speak(line, murmurIfNotRecorded);
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

	public static void SayToYou(Lingala.Line line, bool murmurIfNotRecorded = true)
	{
		Narrator.Speak(line, murmurIfNotRecorded);
	}

	// A made-up voice sound that follows the vowels of the line (a, e, i, o, u)
	public static AudioClip Murmur(Lingala.Line line)
	{
		AudioClip clip;
		if (murmurs.TryGetValue(line.Key, out clip))
			return clip;

		const int rate = 22050;
		string text = line.lingala.ToLowerInvariant();
		List<char> vowels = new List<char>();
		foreach (char ch in text)
			if ("aeiou".IndexOf(ch) >= 0) vowels.Add(ch);
		if (vowels.Count == 0) vowels.Add('a');
		if (vowels.Count > 10) vowels.RemoveRange(10, vowels.Count - 10);

		float syllable = 0.13f;
		int per = (int)(rate * syllable);
		float[] data = new float[per * vowels.Count + rate / 10];
		float phase = 0;
		for (int v = 0; v < vowels.Count; v++)
		{
			float f1, f2;
			switch (vowels[v])
			{
				case 'i': f1 = 300; f2 = 2300; break;
				case 'e': f1 = 480; f2 = 1900; break;
				case 'o': f1 = 500; f2 = 900; break;
				case 'u': f1 = 350; f2 = 800; break;
				default: f1 = 700; f2 = 1200; break;
			}
			float pitch = 165f + 25f * Mathf.Sin(v * 1.7f) - v * 3f;
			for (int i = 0; i < per; i++)
			{
				float t = (float)i / per;
				float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 1.15f));
				phase += 2 * Mathf.PI * pitch / rate;
				// A voice-like buzz, louder near the two "formant" pitches of the vowel
				float s = 0;
				for (int h = 1; h <= 14; h++)
				{
					float freq = pitch * h;
					float w = Mathf.Exp(-Mathf.Pow((freq - f1) / 180f, 2)) + 0.6f * Mathf.Exp(-Mathf.Pow((freq - f2) / 260f, 2)) + 0.05f;
					s += Mathf.Sin(phase * h) * w / h;
				}
				data[v * per + i] = s * env * 0.25f;
			}
		}
		clip = AudioClip.Create("Murmur " + line.Key, data.Length, 1, rate, false);
		clip.SetData(data, 0);
		murmurs[line.Key] = clip;
		return clip;
	}
}

// Put on anything that talks. Each person gets a slightly different voice pitch, so one recorded voice sounds like many people.
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
		source.pitch = GetComponent<PlayerController>() != null ? 1f : Random.Range(0.9f, 1.14f);
	}

	// The narrator is heard the same everywhere
	public void MakeFlat()
	{
		if (source == null) Awake();
		source.spatialBlend = 0f;
		source.pitch = 1f;
	}

	public void Speak(Lingala.Line line, bool murmurIfNotRecorded)
	{
		if (source == null) Awake();
		AudioClip clip = Voice.Recording(line);
		LastWasRecording = clip != null;
		if (clip == null)
		{
			if (!murmurIfNotRecorded)
				return;
			clip = Voice.Murmur(line);
		}
		LastLingala = line.lingala;
		LastTime = Time.time;
		source.Stop();
		source.clip = clip;
		source.Play();
	}
}
