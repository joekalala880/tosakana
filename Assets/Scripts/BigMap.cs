using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Tab = the big map of Kinshasa (the game stops while it is open).
//   Click = put a waypoint (purple). Right click = remove it.
//   Mouse wheel = zoom. Drag (or W A S D) = move the map. Tab or Esc = close.
// The GPS line on the roads shows the way to your mission (yellow) or your waypoint (purple).
public class BigMap : MonoBehaviour
{
	public static BigMap Instance { get; private set; }
	public static bool IsOpenNow { get { return Instance != null && Instance.IsOpen; } }

	public static bool HasWaypoint { get; private set; }
	public static Vector3 Waypoint { get; private set; }

	public Camera mapCam;
	public GameObject panel;
	public RawImage view;
	public RectTransform iconLayer;
	public Font font;
	public Sprite dot;
	public float minSize = 70f, maxSize = 340f;

	public bool IsOpen { get; private set; }
	public int LabelCount { get { return labels.Count; } }

	Vector3 center = new Vector3(420f, 0, -30f);
	float size = 300f;
	bool dragging;
	Vector2 dragStart, lastMouse;
	CursorLockMode lockBefore;

	class Icon { public RectTransform rt; public Image img; public Text text; public System.Func<Vector3?> where; }
	readonly List<Icon> icons = new List<Icon>();
	readonly List<KeyValuePair<RectTransform, Vector3>> labels = new List<KeyValuePair<RectTransform, Vector3>>();
	readonly List<Icon> policeIcons = new List<Icon>();

	void Awake()
	{
		Instance = this;
		HasWaypoint = false;
		if (panel != null) panel.SetActive(false);
		if (mapCam != null) mapCam.enabled = false;
	}

	void Start()
	{
		TosakanaGame game = TosakanaGame.Instance;
		if (game == null || iconLayer == null)
			return;
		// Names of the communes and famous places
		foreach (Place p in game.missions.places)
		{
			bool commune = p.radius == 0f;
			Text t = MakeText(p.placeName.ToUpper(), commune ? 17 : 15, commune ? Color.white : new Color(1f, 0.9f, 0.6f));
			labels.Add(new KeyValuePair<RectTransform, Vector3>(t.rectTransform, p.transform.position));
		}
		// Icons (on top of the names)
		if (FightClub.Instance != null)
			AddIcon("$", new Color(0.95f, 0.15f, 0.15f), () => FightClub.Instance.ringCenter);
		if (Kuluna.Instance != null && Kuluna.Instance.spots != null)
			foreach (Vector3 s in Kuluna.Instance.spots)
			{
				Vector3 spot = s;
				AddIcon("K", new Color(0.7f, 0.05f, 0.05f), () => spot);
			}
		AddIcon("✈", new Color(0.2f, 0.5f, 1f), () => new Vector3(850f, 0, 126f));
		AddIcon("!", new Color(1f, 0.82f, 0.1f), () => game.missions.Target != null ? (Vector3?)game.missions.Target.markerPosition : null);
		AddIcon("W", new Color(0.7f, 0.25f, 1f), () => HasWaypoint ? (Vector3?)Waypoint : null);
		AddIcon("▲", Color.white, () => game.Focus.position);   // you (turns the way you look)
	}

	Icon AddIcon(string letter, Color color, System.Func<Vector3?> where)
	{
		GameObject go = new GameObject("Map Icon " + letter, typeof(RectTransform), typeof(Image));
		RectTransform rt = (RectTransform)go.transform;
		rt.SetParent(iconLayer, false);
		rt.sizeDelta = new Vector2(30, 30);
		Image img = go.GetComponent<Image>();
		img.sprite = dot;
		img.color = color;
		img.raycastTarget = false;
		Text t = MakeText(letter, 18, letter == "▲" ? Color.black : Color.white);
		t.rectTransform.SetParent(rt, false);
		t.rectTransform.anchoredPosition = Vector2.zero;
		Icon icon = new Icon { rt = rt, img = img, text = t, where = where };
		icons.Add(icon);
		return icon;
	}

	Text MakeText(string value, int fontSize, Color color)
	{
		GameObject go = new GameObject("Map Label", typeof(RectTransform), typeof(Text), typeof(Outline));
		RectTransform rt = (RectTransform)go.transform;
		rt.SetParent(iconLayer, false);
		rt.sizeDelta = new Vector2(220, 26);
		Text t = go.GetComponent<Text>();
		t.font = font;
		t.text = value;
		t.fontSize = fontSize;
		t.fontStyle = FontStyle.Bold;
		t.alignment = TextAnchor.MiddleCenter;
		t.color = color;
		t.raycastTarget = false;
		go.GetComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
		return t;
	}

	void Update()
	{
		TosakanaGame game = TosakanaGame.Instance;
		if (game == null)
			return;
		if (!IsOpen)
		{
			if (game.IsPlaying && Input.GetKeyDown(KeyCode.Tab))
				Open();
			ArrivedAtWaypoint(game);
			return;
		}
		if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape))
		{
			Close();
			return;
		}
		float dt = Time.unscaledDeltaTime;
		// Zoom and move
		float wheel = Input.mouseScrollDelta.y;
		if (Mathf.Abs(wheel) > 0.01f)
			size = Mathf.Clamp(size * (1f - wheel * 0.1f), minSize, maxSize);
		Vector2 keys = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
		center += new Vector3(keys.x, 0, keys.y) * size * 1.2f * dt;
		Vector2 mouse = Input.mousePosition;
		if (Input.GetMouseButtonDown(0)) { dragging = false; dragStart = mouse; lastMouse = mouse; }
		if (Input.GetMouseButton(0))
		{
			if ((mouse - dragStart).magnitude > 6f) dragging = true;
			if (dragging)
			{
				float worldPerPixel = size * 2f / Mathf.Max(1f, Screen.height * view.rectTransform.rect.height / 900f);
				Vector2 d = mouse - lastMouse;
				center -= new Vector3(d.x, 0, d.y) * worldPerPixel;
			}
			lastMouse = mouse;
		}
		if (Input.GetMouseButtonUp(0) && !dragging)
		{
			Vector2 local;
			if (RectTransformUtility.ScreenPointToLocalPointInRectangle(view.rectTransform, mouse, null, out local))
			{
				Rect r = view.rectTransform.rect;
				Vector2 uv = new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
				if (uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1)
					SetWaypointAtViewport(uv);
			}
		}
		if (Input.GetMouseButtonDown(1))
			ClearWaypoint();
		center.x = Mathf.Clamp(center.x, -260f, 1150f);
		center.z = Mathf.Clamp(center.z, -260f, 230f);
		Place();
	}

	void LateUpdate()
	{
		if (IsOpen)
			Place();
	}

	public void Open()
	{
		TosakanaGame game = TosakanaGame.Instance;
		IsOpen = true;
		panel.SetActive(true);
		mapCam.enabled = true;
		Time.timeScale = 0f;
		lockBefore = Cursor.lockState;
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		if (game != null)
			center = new Vector3(game.Focus.position.x, 0, game.Focus.position.z);
		size = 150f;
		Place();
	}

	public void Close()
	{
		IsOpen = false;
		panel.SetActive(false);
		mapCam.enabled = false;
		Time.timeScale = 1f;
		Cursor.lockState = lockBefore;
		Cursor.visible = lockBefore != CursorLockMode.Locked;
	}

	void OnDisable() { if (IsOpen) Close(); }

	// Puts the map camera in place and moves every name and icon to the right spot on the screen
	void Place()
	{
		mapCam.orthographicSize = size;
		mapCam.transform.SetPositionAndRotation(new Vector3(center.x, 500f, center.z), Quaternion.Euler(90f, 0, 0));
		Rect r = iconLayer.rect;
		foreach (var kv in labels)
			Put(kv.Key, kv.Value, r, true);
		foreach (Icon icon in icons)
		{
			Vector3? w = icon.where();
			icon.rt.gameObject.SetActive(w.HasValue);
			if (w.HasValue)
				Put(icon.rt, w.Value, r, false);
		}
		// you: the arrow turns the way you look
		TosakanaGame game = TosakanaGame.Instance;
		if (game != null && icons.Count > 0)
			icons[icons.Count - 1].text.rectTransform.localEulerAngles = new Vector3(0, 0, -game.Focus.eulerAngles.y);
		// police cars: small blue dots
		int n = 0;
		if (PoliceManager.Instance != null)
			foreach (TrafficDriver d in PoliceManager.Instance.Police)
			{
				if (d == null) continue;
				if (n >= policeIcons.Count)
				{
					Icon pi = AddIcon("", new Color(0.15f, 0.35f, 1f), () => null);
					icons.Remove(pi);
					pi.rt.sizeDelta = new Vector2(16, 16);
					policeIcons.Add(pi);
				}
				policeIcons[n].rt.gameObject.SetActive(true);
				Put(policeIcons[n].rt, d.transform.position, r, false);
				n++;
			}
		for (int k = n; k < policeIcons.Count; k++)
			policeIcons[k].rt.gameObject.SetActive(false);
	}

	void Put(RectTransform rt, Vector3 world, Rect r, bool hideOutside)
	{
		Vector3 v = mapCam.WorldToViewportPoint(world);
		bool inside = v.x > 0.01f && v.x < 0.99f && v.y > 0.01f && v.y < 0.99f;
		if (hideOutside)
			rt.gameObject.SetActive(inside);
		v.x = Mathf.Clamp(v.x, 0.02f, 0.98f);
		v.y = Mathf.Clamp(v.y, 0.03f, 0.97f);
		rt.anchoredPosition = new Vector2(r.xMin + v.x * r.width, r.yMin + v.y * r.height);
	}

	// Click on the map: the world spot under that point of the map becomes the waypoint
	public Vector3 SetWaypointAtViewport(Vector2 uv)
	{
		Vector3 w = mapCam.ViewportToWorldPoint(new Vector3(uv.x, uv.y, 500f));
		SetWaypoint(new Vector3(w.x, 0, w.z));
		return Waypoint;
	}

	public static void SetWaypoint(Vector3 world)
	{
		Waypoint = new Vector3(world.x, 0, world.z);
		HasWaypoint = true;
	}

	public static void ClearWaypoint() { HasWaypoint = false; }

	// You got there: the waypoint goes away
	static void ArrivedAtWaypoint(TosakanaGame game)
	{
		if (!HasWaypoint || !game.IsPlaying)
			return;
		Vector3 d = game.Focus.position - Waypoint;
		d.y = 0;
		if (d.magnitude < 15f)
		{
			HasWaypoint = false;
			game.ShowBig("WAYPOINT", "You are there  ·  Okomi", new Color(0.8f, 0.5f, 1f), 1.5f);
		}
	}
}
