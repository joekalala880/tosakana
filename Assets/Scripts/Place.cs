using UnityEngine;

// A named place in the city. Used for missions, the area name on the screen and the map.
public class Place : MonoBehaviour
{
	public string placeName = "Place";
	public Vector3 markerPosition;   // where the mission marker goes (on the road next to the place)
	public float radius = 40f;       // how big the area is (for the area name)
}
