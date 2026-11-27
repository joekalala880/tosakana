using UnityEngine;
using UnityEngine.UI;

// Version 13: the big map (Tab) with names, icons and a waypoint, and the GPS line on the roads.
public static partial class CityBuilder
{
	static void MakeBigMap(TosakanaGame game, Transform canvas, Sprite dot)
	{
		BigMap big = game.gameObject.AddComponent<BigMap>();
		Vector2 viewSize = new Vector2(1760, 860);

		RenderTexture rt = new RenderTexture((int)viewSize.x, (int)viewSize.y, 16);
		rt.name = "BigMap";
		rt = SaveAsset(rt, "Assets/Textures/BigMap.renderTexture");
		GameObject camGo = new GameObject("Big Map Camera");
		Camera cam = camGo.AddComponent<Camera>();
		cam.orthographic = true;
		cam.orthographicSize = 150f;
		cam.clearFlags = CameraClearFlags.SolidColor;
		cam.backgroundColor = new Color(0.1f, 0.2f, 0.28f);
		cam.farClipPlane = 700f;
		cam.targetTexture = rt;
		cam.depth = -6;
		cam.enabled = false;
		camGo.transform.SetPositionAndRotation(new Vector3(420, 500, -30), Quaternion.Euler(90, 0, 0));
		big.mapCam = cam;

		RectTransform panel = Stretch(new GameObject("Big Map", typeof(RectTransform), typeof(Image)), canvas);
		panel.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 0.92f);
		big.panel = panel.gameObject;
		GameObject viewGo = new GameObject("Big Map View", typeof(RectTransform), typeof(RawImage));
		RectTransform view = (RectTransform)viewGo.transform;
		view.SetParent(panel, false);
		view.anchorMin = view.anchorMax = new Vector2(0.5f, 0.5f);
		view.sizeDelta = viewSize;
		view.anchoredPosition = new Vector2(0, -20);
		RawImage raw = viewGo.GetComponent<RawImage>();
		raw.texture = rt;
		big.view = raw;
		big.font = font;
		big.dot = dot;

		Label(panel, "Map Title", "KINSHASA", 44, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -12), new Vector2(600, 56), new Color(1f, 0.84f, 0.1f), FontStyle.Bold);
		Label(panel, "Map Help", "Click = waypoint   ·   Right click = remove   ·   Mouse wheel = zoom   ·   Drag or W A S D = move   ·   Tab = close", 24,
			TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(1700, 34), Color.white, FontStyle.Normal);
		// The legend (top right)
		RectTransform legend = Rect(panel, "Legend", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-90, -70), new Vector2(330, 290), new Color(0, 0, 0, 0.7f), null);
		string[] names = { "You", "Mission", "Waypoint", "Ring ya Kin ($)", "Kuluna (gangs)", "Airport", "Police" };
		Color[] colors = { Color.white, new Color(1f, 0.82f, 0.1f), new Color(0.7f, 0.25f, 1f), new Color(0.95f, 0.15f, 0.15f), new Color(0.7f, 0.05f, 0.05f), new Color(0.2f, 0.5f, 1f), new Color(0.15f, 0.35f, 1f) };
		for (int i = 0; i < names.Length; i++)
		{
			Rect(legend, "Legend Dot", new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(34, -28 - i * 37), new Vector2(24, 24), colors[i], dot);
			Label(legend, "Legend Name", names[i], 23, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(60, -28 - i * 37), new Vector2(260, 32), Color.white, FontStyle.Normal);
		}
		// The names and icons go on top of everything (made last), so the legend never hides the airport or another icon
		GameObject layerGo = new GameObject("Big Map Icons", typeof(RectTransform));
		RectTransform layer = (RectTransform)layerGo.transform;
		layer.SetParent(panel, false);
		layer.anchorMin = layer.anchorMax = view.anchorMin;
		layer.sizeDelta = view.sizeDelta;
		layer.anchoredPosition = view.anchoredPosition;
		big.iconLayer = layer;
		panel.gameObject.SetActive(false);
	}

	// The GPS line: on the map layer, so only the maps show it
	static void MakeGps(GameObject gameGo)
	{
		Gps gps = gameGo.AddComponent<Gps>();
		GameObject go = new GameObject("GPS Route");
		go.layer = MapLayer;
		LineRenderer line = go.AddComponent<LineRenderer>();
		line.sharedMaterial = Unlit("GPS Route", new Color(1f, 0.82f, 0.1f));
		line.widthMultiplier = 5f;
		line.numCornerVertices = 2;
		line.alignment = LineAlignment.TransformZ;
		go.transform.rotation = Quaternion.Euler(90f, 0, 0);   // lies flat, facing the sky (the map cameras look down)
		line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		line.receiveShadows = false;
		line.useWorldSpace = true;
		line.positionCount = 0;
		line.enabled = false;
		gps.line = line;
		gps.gridStart = FirstRoad;
		gps.gridStep = Pitch;
		gps.nodesX = BlocksX + 1;
		gps.nodesZ = BlocksZ + 1;
		gps.boulevardZ = Boulevard;
	}
}
