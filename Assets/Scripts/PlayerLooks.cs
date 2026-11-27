using UnityEngine;

// Your clothes: the sapeur suit, or the street look (Leopards football jersey, jeans, sneakers and a cap).
// O = switch (when you are walking). The you sitting in the car wears the same.
public class PlayerLooks : MonoBehaviour
{
	public GameObject[] sapeurParts;
	public GameObject[] streetParts;
	public bool street;

	public string LookName { get { return street ? "Street (Leopards jersey)" : "Sapeur (suit and hat)"; } }

	void Start() { Apply(); }

	public void SetStreet(bool on)
	{
		street = on;
		Apply();
	}

	public void Apply()
	{
		foreach (GameObject g in sapeurParts) if (g != null) g.SetActive(!street);
		foreach (GameObject g in streetParts) if (g != null) g.SetActive(street);
	}
}
