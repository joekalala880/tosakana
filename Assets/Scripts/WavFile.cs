using System.IO;
using UnityEngine;

// Writes sound to a .wav file (16-bit, one channel). Used by the voice recorder.
public static class WavFile
{
	public static void Write(string path, float[] samples, int rate)
	{
		using (var stream = new FileStream(path, FileMode.Create))
		using (var w = new BinaryWriter(stream))
		{
			int bytes = samples.Length * 2;
			w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
			w.Write(36 + bytes);
			w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
			w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
			w.Write(16);
			w.Write((short)1);        // PCM
			w.Write((short)1);        // mono
			w.Write(rate);
			w.Write(rate * 2);        // bytes per second
			w.Write((short)2);        // bytes per sample
			w.Write((short)16);       // bits
			w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
			w.Write(bytes);
			foreach (float s in samples)
				w.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1f, 1f) * 32767f));
		}
	}

	// Cut the quiet parts at the start and end, and make it loud enough
	public static float[] Clean(float[] samples, int rate)
	{
		float peak = 0;
		foreach (float s in samples) peak = Mathf.Max(peak, Mathf.Abs(s));
		if (peak < 0.005f)
			return new float[0];
		float threshold = Mathf.Max(0.02f, peak * 0.08f);
		int start = 0, end = samples.Length - 1;
		while (start < end && Mathf.Abs(samples[start]) < threshold) start++;
		while (end > start && Mathf.Abs(samples[end]) < threshold) end--;
		int pad = rate / 10;
		start = Mathf.Max(0, start - pad);
		end = Mathf.Min(samples.Length - 1, end + pad);
		float gain = 0.9f / peak;
		float[] result = new float[end - start + 1];
		for (int i = 0; i < result.Length; i++)
			result[i] = samples[start + i] * gain;
		return result;
	}
}
