using UnityEngine;

// The red and blue lights on top of a police car, its dot on the mini-map, and the siren.
public class PoliceLights : MonoBehaviour
{
	public Renderer redLight;
	public Renderer blueLight;
	public Renderer mapIcon;
	public Material redMaterial;
	public Material blueMaterial;
	public AudioSource siren;
	public float flashSeconds = 0.22f;

	public bool On { get; private set; }

	float timer;
	bool flip;

	public void Set(bool on)
	{
		On = on;
		if (siren != null)
		{
			if (siren.clip == null && TosakanaGame.Instance != null)
				siren.clip = TosakanaGame.Instance.sound.SirenClip;
			if (on && siren.clip != null && !siren.isPlaying)
				siren.Play();
			else if (!on)
				siren.Stop();
		}
		if (!on)
		{
			redLight.enabled = true;
			blueLight.enabled = true;
			mapIcon.sharedMaterial = blueMaterial;
		}
	}

	void Update()
	{
		if (!On)
			return;
		timer -= Time.deltaTime;
		if (timer > 0)
			return;
		timer = flashSeconds;
		flip = !flip;
		redLight.enabled = flip;
		blueLight.enabled = !flip;
		mapIcon.sharedMaterial = flip ? redMaterial : blueMaterial;
	}
}
