using UnityEngine;
using UnityEngine.UI;

// The sun goes around: morning, afternoon, sunset, night, sunrise. One full day takes about 12 minutes.
// At night the windows glow, the street lamps shine, and your car turns its headlights on.
public class DayNight : MonoBehaviour
{
	public Light sun;
	public float dayMinutes = 12f;          // real minutes for one full day
	[Range(0, 24)] public float hour = 9f;  // starts in the morning
	public Material lampGlow;               // street lamp bulbs
	public Light carLights;                 // your car's headlights (follows the car you drive)
	public Text clockText;

	public bool IsNight { get { return hour < 6f || hour > 19f; } }
	public float Darkness { get; private set; }   // 0 = full day, 1 = deep night
	public float WindowGlow { get { return Shader.GetGlobalFloat("_TosakanaNight"); } }

	static readonly Color daySky = new Color(0.62f, 0.68f, 0.8f), nightSky = new Color(0.08f, 0.1f, 0.2f);
	static readonly Color dayFog = new Color(0.78f, 0.82f, 0.86f), duskFog = new Color(0.95f, 0.6f, 0.4f), nightFog = new Color(0.05f, 0.06f, 0.12f);
	static readonly Color daySun = new Color(1f, 0.95f, 0.84f), duskSun = new Color(1f, 0.55f, 0.3f), moon = new Color(0.55f, 0.65f, 1f);

	void Start()
	{
		RenderSettings.sun = sun;
		// A copy of the lamp material, so playing does not change the saved one
		if (lampGlow != null)
		{
			Material saved = lampGlow;
			lampGlow = new Material(saved);
			foreach (Renderer r in FindObjectsOfType<Renderer>())
				if (r.sharedMaterial == saved) r.sharedMaterial = lampGlow;
		}
		Apply();
	}

	void OnDestroy() { Shader.SetGlobalFloat("_TosakanaNight", 0f); }

	void Update()
	{
		TosakanaGame game = TosakanaGame.Instance;
		if (game != null && game.IsPlaying)
			hour = (hour + Time.deltaTime * 24f / (dayMinutes * 60f)) % 24f;
		Apply();
	}

	public void SetHour(float h)
	{
		hour = ((h % 24f) + 24f) % 24f;
		Apply();
	}

	void Apply()
	{
		// e = how high the sun is: 1 at noon, 0 at 6:00 and 18:00, below 0 at night
		float e = Mathf.Sin((hour - 6f) / 12f * Mathf.PI);
		Darkness = Mathf.Clamp01(0.45f - e * 3f);
		float sunsetGlow = Mathf.Clamp01(1f - Mathf.Abs(e) * 4f);   // strong when the sun is near the horizon

		if (sun != null)
		{
			if (e > 0f)
			{
				float elevation = Mathf.Max(3f, Mathf.Asin(e) * Mathf.Rad2Deg);
				float yaw = -90f + (hour - 6f) / 12f * 180f;              // from the east in the morning to the west at night
				sun.transform.rotation = Quaternion.Euler(elevation, yaw, 0);
				sun.color = Color.Lerp(duskSun, daySun, Mathf.Clamp01(e * 2.5f));
				sun.intensity = Mathf.Lerp(0.3f, 1.15f, Mathf.Clamp01(e * 2f));
			}
			else
			{
				sun.transform.rotation = Quaternion.Euler(50f, 30f, 0);   // the moon
				sun.color = moon;
				sun.intensity = Mathf.Lerp(0.3f, 0.16f, Darkness);
			}
		}
		float sunset = sunsetGlow;
		RenderSettings.ambientSkyColor = Color.Lerp(daySky, nightSky, Darkness);
		RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.55f, 0.55f, 0.52f), new Color(0.07f, 0.08f, 0.14f), Darkness);
		RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.36f, 0.33f, 0.28f), new Color(0.04f, 0.04f, 0.06f), Darkness);
		Color fog = Color.Lerp(dayFog, nightFog, Darkness);
		RenderSettings.fogColor = Color.Lerp(fog, duskFog, sunset * (1f - Darkness * 0.7f));
		Camera cam = Camera.main;
		if (cam != null)
		{
			cam.clearFlags = Darkness > 0.75f ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
			cam.backgroundColor = RenderSettings.fogColor;
		}

		float glow = Mathf.Clamp01((Darkness - 0.35f) / 0.4f);
		Shader.SetGlobalFloat("_TosakanaNight", glow * 0.9f);
		if (lampGlow != null)
			lampGlow.color = Color.Lerp(new Color(0.55f, 0.52f, 0.45f), new Color(1f, 0.85f, 0.45f), glow);

		TosakanaGame game = TosakanaGame.Instance;
		if (carLights != null)
		{
			bool on = glow > 0.2f && game != null && game.InCar;
			carLights.enabled = on;
			if (on)
			{
				Transform car = game.CurrentCar.transform;
				carLights.transform.SetPositionAndRotation(car.position + car.forward * 2.3f + Vector3.up * 0.9f, car.rotation * Quaternion.Euler(8f, 0, 0));
			}
		}
		if (clockText != null)
		{
			int h = Mathf.FloorToInt(hour), m = Mathf.FloorToInt((hour - h) * 60f);
			clockText.text = (IsNight ? "☾ " : "☀ ") + h.ToString("00") + ":" + m.ToString("00");
		}
	}
}
