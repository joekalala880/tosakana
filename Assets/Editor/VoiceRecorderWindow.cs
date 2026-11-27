using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > Tosakana > Record Voices
// Record every Lingala line with your own voice. Click ● to record, ■ to stop, ▶ to listen.
// The recordings are saved in Assets/Resources/Voices and the game uses them right away.
public class VoiceRecorderWindow : EditorWindow
{
	public const string Folder = "Assets/Resources/Voices";
	const int Rate = 44100;
	const int MaxSeconds = 8;

	string recordingKey;
	string device;
	AudioClip micClip;
	Vector2 scroll;
	AudioSource preview;
	string message = "";

	[MenuItem("Tools/Tosakana/Record Voices")]
	public static void Open()
	{
		VoiceRecorderWindow w = GetWindow<VoiceRecorderWindow>("Record Voices");
		w.minSize = new Vector2(560, 400);
	}

	public static string PathFor(Lingala.Line line) { return Folder + "/" + line.Key + ".wav"; }
	public static bool IsRecorded(Lingala.Line line) { return File.Exists(PathFor(line)); }

	void OnEnable() { EditorApplication.update += Tick; }
	void OnDisable()
	{
		EditorApplication.update -= Tick;
		if (recordingKey != null && device != null)
			Microphone.End(device);
		if (preview != null)
			DestroyImmediate(preview.gameObject);
	}

	void Tick()
	{
		if (recordingKey == null)
			return;
		// Stop by itself after 8 seconds
		if (!Microphone.IsRecording(device))
			StopRecording();
		Repaint();
	}

	void OnGUI()
	{
		List<KeyValuePair<string, Lingala.Line>> lines = Lingala.AllSpoken();
		int done = 0;
		foreach (var kv in lines)
			if (IsRecorded(kv.Value)) done++;

		EditorGUILayout.LabelField("Tosakana voices", EditorStyles.boldLabel);
		EditorGUILayout.HelpBox("Record each line in Lingala with your voice. Click ● Record, say the line, then click ■ Stop. " +
			"Click ▶ to listen, ● again to record it again.\n✔ = your voice.  PC = computer voice (your recording replaces it).  Empty = a soft murmur plays.", MessageType.Info);

		string[] devices = Microphone.devices;
		if (devices.Length == 0)
		{
			EditorGUILayout.HelpBox("No microphone found. Plug one in (or allow Unity to use the microphone in System Settings > Privacy & Security > Microphone).", MessageType.Warning);
			return;
		}
		if (device == null || System.Array.IndexOf(devices, device) < 0)
			device = devices[0];
		int pick = EditorGUILayout.Popup("Microphone", System.Array.IndexOf(devices, device), devices);
		if (recordingKey == null)
			device = devices[pick];

		EditorGUILayout.LabelField("Recorded: " + done + " / " + lines.Count, EditorStyles.boldLabel);
		if (message != "")
			EditorGUILayout.LabelField(message, EditorStyles.miniLabel);

		scroll = EditorGUILayout.BeginScrollView(scroll);
		string group = null;
		foreach (var kv in lines)
		{
			if (kv.Key != group)
			{
				group = kv.Key;
				EditorGUILayout.Space(6);
				EditorGUILayout.LabelField(group, EditorStyles.boldLabel);
			}
			Lingala.Line line = kv.Value;
			bool recorded = IsRecorded(line);
			bool thisOne = recordingKey == line.Key;
			EditorGUILayout.BeginHorizontal();
			bool computer = File.Exists(Folder + "/Computer/" + line.Key + ".wav");
			GUILayout.Label(recorded ? "✔" : computer ? "PC" : "  ", GUILayout.Width(22));
			EditorGUILayout.BeginVertical();
			GUILayout.Label(line.lingala, EditorStyles.boldLabel);
			GUILayout.Label(line.english, EditorStyles.miniLabel);
			EditorGUILayout.EndVertical();
			GUILayout.FlexibleSpace();
			if (thisOne)
			{
				GUI.color = new Color(1f, 0.5f, 0.5f);
				if (GUILayout.Button("■ Stop", GUILayout.Width(80), GUILayout.Height(30)))
					StopRecording();
				GUI.color = Color.white;
			}
			else
			{
				GUI.enabled = recordingKey == null;
				if (GUILayout.Button("● Record", GUILayout.Width(80), GUILayout.Height(30)))
					StartRecording(line);
				GUI.enabled = recordingKey == null && recorded;
				if (GUILayout.Button("▶", GUILayout.Width(34), GUILayout.Height(30)))
					Play(line);
				GUI.enabled = true;
			}
			EditorGUILayout.EndHorizontal();
		}
		EditorGUILayout.EndScrollView();
	}

	void StartRecording(Lingala.Line line)
	{
		recordingKey = line.Key;
		micClip = Microphone.Start(device, false, MaxSeconds, Rate);
		message = "Recording... say: " + line.lingala;
	}

	void StopRecording()
	{
		if (recordingKey == null)
			return;
		int length = Microphone.GetPosition(device);
		Microphone.End(device);
		string key = recordingKey;
		recordingKey = null;
		if (micClip == null || length <= 0)
		{
			message = "Nothing was recorded. Check the microphone.";
			return;
		}
		float[] samples = new float[length * micClip.channels];
		micClip.GetData(samples, 0);
		message = Save(key, samples, Rate) ? "Saved " + key + ".wav" : "It was too quiet. Try again a little closer to the microphone.";
	}

	// Saves a recording and tells Unity about it. Returns false if it was silent.
	public static bool Save(string key, float[] samples, int rate)
	{
		float[] clean = WavFile.Clean(samples, rate);
		if (clean.Length == 0)
			return false;
		Directory.CreateDirectory(Folder);
		string path = Folder + "/" + key + ".wav";
		WavFile.Write(path, clean, rate);
		AssetDatabase.ImportAsset(path);
		return true;
	}

	void Play(Lingala.Line line)
	{
		AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(PathFor(line));
		if (clip == null)
			return;
		if (preview == null)
		{
			GameObject go = EditorUtility.CreateGameObjectWithHideFlags("Voice Preview", HideFlags.HideAndDontSave, typeof(AudioSource));
			preview = go.GetComponent<AudioSource>();
		}
		preview.clip = clip;
		preview.Play();
	}

	// Used by the checks: save a pretend recording, load it, then remove it
	public static void SelfTest()
	{
		Lingala.Line line = new Lingala.Line("Test voice", "test");
		float[] tone = new float[Rate];
		for (int i = 0; i < tone.Length; i++)
			tone[i] = (i > Rate / 4 && i < Rate * 3 / 4) ? Mathf.Sin(i * 0.05f) * 0.5f : 0f;
		bool saved = Save(line.Key, tone, Rate);
		AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(PathFor(line));
		bool silentRejected = !Save("silent_test", new float[Rate], Rate);
		Debug.Log("TOSAKANA-VOICETEST saved=" + saved + " loaded=" + (clip != null) + " seconds=" + (clip != null ? clip.length.ToString("0.00") : "0") + " silentRejected=" + silentRejected);
		AssetDatabase.DeleteAsset(PathFor(line));
		if (AssetDatabase.IsValidFolder(Folder) && Directory.GetFiles(Folder, "*.wav").Length == 0)
			AssetDatabase.DeleteAsset(Folder);
	}
}
