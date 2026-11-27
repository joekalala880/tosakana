using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The car radio. It turns on when you get in a car and off when you get out. N = next station.
// Your own music: put MP3 files (songs you own) in these folders, then press Play:
//    Assets/Resources/Radio/Rumba      Assets/Resources/Radio/Ndombolo      Assets/Resources/Radio/Mix
// Those folders are not put on GitHub (the songs belong to the artists).
// "Tosakana FM" always works: it plays rumba that the game makes itself.
// "Spotify" (only on a Mac with the Spotify app): the game tells YOUR Spotify app to play when you get in a car
// and to pause when you get out. Your playlists, legal, nothing copied. The first time, macOS asks
// "Unity wants to control Spotify" - click OK.
public class CarRadio : MonoBehaviour
{
	public Text radioText;
	[Range(0, 1)] public float musicVolume = 0.3f;          // music volume
	[Range(0, 1)] public float duckedVolume = 0.08f;   // while someone talks, so you hear the voices
	[Range(0, 100)] public int spotifyVolume = 40;     // Spotify app volume while you drive
	[Range(0, 100)] public int spotifyDucked = 12;     // Spotify volume while someone talks

	public class Station
	{
		public string name;
		public bool spotify;
		public List<AudioClip> songs = new List<AudioClip>();
	}

	public readonly List<Station> Stations = new List<Station>();
	public int StationIndex { get; private set; }
	public Station Current { get { return StationIndex < Stations.Count ? Stations[StationIndex] : null; } }
	public bool On { get; private set; }
	public string NowPlaying { get; private set; }

	static readonly string[][] Folders = { new[] { "Rumba", "Kin Rumba FM" }, new[] { "Ndombolo", "Ndombolo Radio" }, new[] { "Mix", "Mboka Mix" } };

	AudioSource source;
	int song;
	float textTimer, songEnds;
	bool wasInCar;

	void Awake()
	{
		source = gameObject.AddComponent<AudioSource>();
		source.spatialBlend = 0f;
		source.volume = musicVolume;
		source.playOnAwake = false;

		Station own = new Station { name = "Tosakana FM" };
		own.songs.Add(RumbaMaker.Make("Sebene ya Tosakana", 72, 0));
		own.songs.Add(RumbaMaker.Make("Rumba ya Kinshasa", 68, 5));
		Stations.Add(own);
		foreach (string[] f in Folders)
		{
			AudioClip[] clips = Resources.LoadAll<AudioClip>("Radio/" + f[0]);
			if (clips.Length == 0)
				continue;
			Station st = new Station { name = f[1] };
			st.songs.AddRange(clips);
			Stations.Add(st);
		}
		if (SpotifyInstalled())
			Stations.Insert(1, new Station { name = "Spotify" , spotify = true });
	}

	// ---------- Spotify (Mac app) ----------
	public static bool SpotifyInstalled()
	{
		if (Application.isBatchMode)
			return false;   // never during the automatic tests
		if (Application.platform != RuntimePlatform.OSXEditor && Application.platform != RuntimePlatform.OSXPlayer)
			return false;
		string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
		return System.IO.Directory.Exists("/Applications/Spotify.app") || System.IO.Directory.Exists(home + "/Applications/Spotify.app");
	}

	bool spotifyPlaying;
	int spotifyVolumeBefore = -1;   // your own Spotify volume, put back when you get out
	int spotifySetTo = -1;
	float nextTrackCheck;
	volatile string spotifyTrack;

	static void AskSpotify(string command, System.Action<string> answer = null)
	{
		System.Threading.ThreadPool.QueueUserWorkItem(_ =>
		{
			try
			{
				var psi = new System.Diagnostics.ProcessStartInfo("/usr/bin/osascript", "-e \"tell application \\\"Spotify\\\" to " + command + "\"")
				{
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				};
				using (var p = System.Diagnostics.Process.Start(psi))
				{
					string output = p.StandardOutput.ReadToEnd().Trim();
					p.WaitForExit(4000);
					if (answer != null) answer(output);
				}
			}
			catch { }
		});
	}

	void SpotifyPlay()
	{
		source.Stop();
		if (spotifyVolumeBefore < 0)
			AskSpotify("sound volume", v => { int n; if (int.TryParse(v, out n)) spotifyVolumeBefore = n; });
		spotifySetTo = -1;
		AskSpotify("play");
		spotifyPlaying = true;
		On = true;
		NowPlaying = "your Spotify music";
		songEnds = float.MaxValue;
		textTimer = 5f;
		nextTrackCheck = Time.time + 1f;
	}

	void SpotifyPause()
	{
		if (!spotifyPlaying)
			return;
		AskSpotify("pause");
		if (spotifyVolumeBefore >= 0)
			AskSpotify("set sound volume to " + spotifyVolumeBefore);
		spotifyVolumeBefore = -1;
		spotifyPlaying = false;
	}

	void OnDisable() { SpotifyPause(); }
	void OnApplicationQuit() { SpotifyPause(); }

	void Update()
	{
		TosakanaGame game = TosakanaGame.Instance;
		bool inCar = game != null && game.IsPlaying && game.InCar;
		if (inCar && !wasInCar)
			PlaySong(Random.Range(0, Mathf.Max(1, Current.songs.Count)));
		if (!inCar && wasInCar)
		{
			source.Stop();
			SpotifyPause();
			On = false;
		}
		// Quieter music while someone talks, so you always hear the voices
		bool talking = Voice.Busy;
		source.volume = Mathf.MoveTowards(source.volume, talking ? duckedVolume : musicVolume, Time.deltaTime * 1.5f);
		if (spotifyPlaying)
		{
			int want = talking ? spotifyDucked : spotifyVolume;
			if (want != spotifySetTo)
			{
				spotifySetTo = want;
				AskSpotify("set sound volume to " + want);
			}
		}
		// Show the Spotify song name when it changes
		if (spotifyPlaying && Time.time > nextTrackCheck)
		{
			nextTrackCheck = Time.time + 4f;
			AskSpotify("(artist of current track) & \\\" - \\\" & (name of current track)", t => spotifyTrack = t);
		}
		string track = spotifyTrack;
		if (spotifyPlaying && !string.IsNullOrEmpty(track) && track != NowPlaying)
		{
			NowPlaying = track;
			textTimer = 5f;
		}
		wasInCar = inCar;

		if (inCar && Input.GetKeyDown(KeyCode.N))
			NextStation();
		// The song ended: play the next one on this station
		if (inCar && On && Time.time > songEnds && !source.isPlaying)
			PlaySong(song + 1);

		if (radioText != null)
		{
			textTimer -= Time.deltaTime;
			radioText.text = inCar && textTimer > 0 ? "RADIO  " + Current.name + "  ·  " + NowPlaying + "   (N = next station)" : "";
		}
	}

	public void NextStation()
	{
		SpotifyPause();
		StationIndex = (StationIndex + 1) % Stations.Count;
		PlaySong(Random.Range(0, Mathf.Max(1, Current.songs.Count)));
	}

	void PlaySong(int i)
	{
		Station st = Current;
		if (st != null && st.spotify)
		{
			SpotifyPlay();
			return;
		}
		if (st == null || st.songs.Count == 0)
			return;
		song = ((i % st.songs.Count) + st.songs.Count) % st.songs.Count;
		AudioClip clip = st.songs[song];
		source.clip = clip;
		source.volume = musicVolume;
		source.Play();
		On = true;
		songEnds = Time.time + clip.length;
		NowPlaying = clip.name.Replace('_', ' ');
		textTimer = 5f;
	}
}

// Makes a Congolese-rumba-style song: plucked sebene guitar, a rhythm guitar, bass, and drums.
// Everything is made from numbers, so it belongs to the game (no one else's music).
public static class RumbaMaker
{
	const int Rate = 22050;

	public static AudioClip Make(string name, float seconds, int seed)
	{
		System.Random rng = new System.Random(seed);
		float bpm = 128f;
		float beat = 60f / bpm;
		int total = (int)(seconds * Rate);
		float[] mix = new float[total];
		// I - IV - V - IV in C or G, like many rumba songs
		int root = seed % 2 == 0 ? 60 : 67;
		int[][] chords = { new[] { 0, 4, 7 }, new[] { 5, 9, 12 }, new[] { 7, 11, 14 }, new[] { 5, 9, 12 } };
		int beats = (int)(seconds / beat);
		for (int b = 0; b < beats; b++)
		{
			int[] chord = chords[(b / 4) % chords.Length];
			float t0 = b * beat;
			// Sebene: fast high guitar notes, four per beat
			for (int k = 0; k < 4; k++)
			{
				int idx = (k + b) % 3;
				int note = root + 12 + chord[idx] + (rng.NextDouble() < 0.15 ? 12 : 0);
				Pluck(mix, t0 + k * beat / 4f, Freq(note), 0.35f, 0.16f);
			}
			// Rhythm guitar on the off-beat
			foreach (int c in chord)
				Pluck(mix, t0 + beat / 2f, Freq(root + c), 0.5f, 0.06f);
			// Bass: root on the beat, fifth before the next bar
			int bassNote = root - 24 + (b % 4 == 3 ? chord[2] : chord[0]);
			Tone(mix, t0, Freq(bassNote), beat * 0.9f, 0.22f);
			// Drums: hi-hat on every half beat, snare on 2 and 4
			Noise(mix, t0, 0.03f, 0.05f, rng);
			Noise(mix, t0 + beat / 2f, 0.03f, 0.035f, rng);
			if (b % 2 == 1)
				Noise(mix, t0, 0.12f, 0.12f, rng);
		}
		float peak = 0.01f;
		foreach (float v in mix) peak = Mathf.Max(peak, Mathf.Abs(v));
		for (int i = 0; i < total; i++)
			mix[i] = mix[i] / peak * 0.8f;
		AudioClip clip = AudioClip.Create(name, total, 1, Rate, false);
		clip.SetData(mix, 0);
		return clip;
	}

	static float Freq(int midi) { return 440f * Mathf.Pow(2f, (midi - 69) / 12f); }

	// A plucked string (Karplus-Strong)
	static void Pluck(float[] mix, float start, float freq, float length, float volume)
	{
		int n = Mathf.Max(2, (int)(Rate / freq));
		float[] ring = new float[n];
		System.Random r = new System.Random((int)(freq * 1000) + (int)(start * 100));
		for (int i = 0; i < n; i++) ring[i] = (float)(r.NextDouble() * 2 - 1);
		int s = (int)(start * Rate), count = (int)(length * Rate);
		for (int i = 0; i < count && s + i < mix.Length; i++)
		{
			int a = i % n, b = (i + 1) % n;
			float v = ring[a];
			ring[a] = 0.996f * 0.5f * (ring[a] + ring[b]);
			mix[s + i] += v * volume;
		}
	}

	static void Tone(float[] mix, float start, float freq, float length, float volume)
	{
		int s = (int)(start * Rate), count = (int)(length * Rate);
		for (int i = 0; i < count && s + i < mix.Length; i++)
		{
			float t = (float)i / Rate;
			float env = Mathf.Exp(-t * 5f);
			mix[s + i] += (Mathf.Sin(2 * Mathf.PI * freq * t) + 0.3f * Mathf.Sin(4 * Mathf.PI * freq * t)) * env * volume;
		}
	}

	static void Noise(float[] mix, float start, float length, float volume, System.Random r)
	{
		int s = (int)(start * Rate), count = (int)(length * Rate);
		for (int i = 0; i < count && s + i < mix.Length; i++)
			mix[s + i] += (float)(r.NextDouble() * 2 - 1) * (1f - (float)i / count) * volume;
	}
}
