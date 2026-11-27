using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Record the voices in Google Chrome (when Unity itself can't use the microphone), then bring them into the game.
//   Tools > Tosakana > Record Voices in Chrome        -> opens a page with every Lingala line and a Record button
//   Tools > Tosakana > Import Voices from Downloads   -> moves the recordings from Downloads into the game
public static class VoiceChromeRecorder
{
	const string Template = "Assets/Editor/VoiceRecorderPage.html";

	[MenuItem("Tools/Tosakana/Record Voices in Chrome")]
	public static void OpenInChrome()
	{
		string page = WritePage();
		try
		{
			System.Diagnostics.Process.Start("open", "-a \"Google Chrome\" \"" + page + "\"");
		}
		catch (Exception)
		{
			Application.OpenURL("file://" + page);
		}
	}

	// Makes the page with all the lines (it lives next to the project, not in the game)
	public static string WritePage()
	{
		var json = new StringBuilder("[");
		bool first = true;
		foreach (var kv in Lingala.AllSpoken())
		{
			if (!first) json.Append(",");
			first = false;
			json.Append("{\"group\":").Append(Quote(kv.Key))
				.Append(",\"lingala\":").Append(Quote(kv.Value.lingala))
				.Append(",\"english\":").Append(Quote(kv.Value.english))
				.Append(",\"key\":").Append(Quote(kv.Value.Key)).Append("}");
		}
		json.Append("]");
		string html = File.ReadAllText(Template).Replace("/*LINES*/[]", json.ToString());
		string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Voice Recorder.html"));
		File.WriteAllText(path, html, new UTF8Encoding(false));
		return path;
	}

	static string Quote(string s)
	{
		return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
	}

	[MenuItem("Tools/Tosakana/Import Voices from Downloads")]
	public static void ImportFromDownloads()
	{
		string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
		int n = ImportFrom(downloads);
		EditorUtility.DisplayDialog("Tosakana voices",
			n > 0 ? "Imported " + n + " recording" + (n == 1 ? "" : "s") + " into the game. Press Play to hear them!" :
				"No recordings found in Downloads. Record some lines in Chrome first (Tools > Tosakana > Record Voices in Chrome).", "OK");
	}

	// Moves "<line>.wav" (or Chrome's "<line> (2).wav") for every line into Assets/Resources/Voices. Returns how many.
	public static int ImportFrom(string folder)
	{
		if (!Directory.Exists(folder))
			return 0;
		Directory.CreateDirectory(VoiceRecorderWindow.Folder);
		int count = 0;
		foreach (var kv in Lingala.AllSpoken())
		{
			string key = kv.Value.Key;
			string newest = null;
			DateTime newestTime = DateTime.MinValue;
			foreach (string file in Directory.GetFiles(folder, key + "*.wav"))
			{
				string name = Path.GetFileNameWithoutExtension(file);
				bool match = name == key || (name.StartsWith(key + " (") && name.EndsWith(")"));
				if (!match)
					continue;
				DateTime t = File.GetLastWriteTime(file);
				if (t > newestTime)
				{
					newestTime = t;
					newest = file;
				}
			}
			if (newest == null)
				continue;
			string target = VoiceRecorderWindow.Folder + "/" + key + ".wav";
			File.Copy(newest, target, true);
			// Clean up Downloads so old takes don't come back later
			foreach (string file in Directory.GetFiles(folder, key + "*.wav"))
			{
				string name = Path.GetFileNameWithoutExtension(file);
				if (name == key || (name.StartsWith(key + " (") && name.EndsWith(")")))
					File.Delete(file);
			}
			AssetDatabase.ImportAsset(target);
			count++;
		}
		Debug.Log("TOSAKANA-IMPORT " + count + " voices");
		return count;
	}

	// Used by the checks: makes the page, imports two pretend takes of one line, then cleans up
	public static void SelfTest()
	{
		string page = WritePage();
		string html = File.ReadAllText(page);
		bool pageOk = html.Contains("\"key\":\"mbote\"") && html.Contains("Bokomi malamu na Lemba!") && !html.Contains("/*LINES*/");

		string temp = Path.Combine(Path.GetTempPath(), "tosakana_voice_test");
		Directory.CreateDirectory(temp);
		float[] tone = new float[22050];
		for (int i = 0; i < tone.Length; i++) tone[i] = Mathf.Sin(i * 0.05f) * 0.5f;
		WavFile.Write(Path.Combine(temp, "mbote.wav"), tone, 22050);
		WavFile.Write(Path.Combine(temp, "mbote (1).wav"), tone, 22050);
		File.SetLastWriteTime(Path.Combine(temp, "mbote (1).wav"), DateTime.Now.AddSeconds(5));
		WavFile.Write(Path.Combine(temp, "not_a_line.wav"), tone, 22050);
		int n = ImportFrom(temp);
		AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceRecorderWindow.Folder + "/mbote.wav");
		bool leftovers = File.Exists(Path.Combine(temp, "mbote.wav")) || File.Exists(Path.Combine(temp, "mbote (1).wav"));
		bool ignoredOther = File.Exists(Path.Combine(temp, "not_a_line.wav"));
		Debug.Log("TOSAKANA-CHROMETEST page=" + pageOk + " imported=" + n + " loaded=" + (clip != null) + " cleaned=" + !leftovers + " ignoredOther=" + ignoredOther);
		AssetDatabase.DeleteAsset(VoiceRecorderWindow.Folder + "/mbote.wav");
		if (AssetDatabase.IsValidFolder(VoiceRecorderWindow.Folder) && Directory.GetFiles(VoiceRecorderWindow.Folder, "*.wav").Length == 0)
			AssetDatabase.DeleteAsset(VoiceRecorderWindow.Folder);
		Directory.Delete(temp, true);
		File.Delete(page);
	}
}
