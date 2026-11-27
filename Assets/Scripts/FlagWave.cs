using UnityEngine;

// Makes a flag move a little in the wind.
public class FlagWave : MonoBehaviour
{
	public float speed = 2.2f;
	public float amount = 12f;
	float offset;
	Quaternion start;

	void Start()
	{
		start = transform.localRotation;
		offset = Random.value * 10f;
	}

	void Update()
	{
		float t = Time.time * speed + offset;
		transform.localRotation = start * Quaternion.Euler(0, Mathf.Sin(t) * amount + Mathf.Sin(t * 2.3f) * amount * 0.3f, 0);
	}
}
