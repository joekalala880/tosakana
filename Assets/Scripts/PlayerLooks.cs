using UnityEngine;

// Your clothes: the sapeur suit, or the street look (Leopards football jersey, jeans, sneakers and a cap).
// O = switch (when you are walking). The you sitting in the car wears the same.
public class PlayerLooks : MonoBehaviour
{
	public GameObject[] sapeurParts;   // hat, tie, sunglasses, gold chain...
	public GameObject[] streetParts;   // cap, jersey stripe and star...
	[Header("Body parts that change color")]
	public Renderer[] swap;            // jacket/jersey, sleeves/arms, trousers/jeans, shoes/sneakers
	public Material[] sapeurMats;
	public Material[] streetMats;
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
		if (sapeurParts != null)
			foreach (GameObject g in sapeurParts) if (g != null) g.SetActive(!street);
		if (streetParts != null)
			foreach (GameObject g in streetParts) if (g != null) g.SetActive(street);
		if (swap == null)
			return;
		Material[] mats = street ? streetMats : sapeurMats;
		for (int i = 0; i < swap.Length && mats != null && i < mats.Length; i++)
			if (swap[i] != null && mats[i] != null)
				swap[i].sharedMaterial = mats[i];
	}

	// What you wear on your body right now (for the checks): "Leopards Blue", "Sapeur Jacket"...
	public string TopMaterial { get { return swap != null && swap.Length > 0 && swap[0] != null ? swap[0].sharedMaterial.name : ""; } }
}
