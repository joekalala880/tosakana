using UnityEngine;
using UnityEngine.UI;

// A speech bubble over someone's head: Lingala in big letters, English under it.
// It always turns to face the camera and goes away after a few seconds.
public class SpeechBubble : MonoBehaviour
{
	public float height = 2.6f;
	public float size = 1f;

	public bool Showing { get { return timer > 0; } }
	public string Text { get { return main != null ? main.text : ""; } }

	GameObject root;
	Text main, sub;
	Image box;
	float timer;
	static Font font;

	// Show a line on anything (person, police car, you)
	public static SpeechBubble Say(GameObject who, Lingala.Line line, float seconds = 3f, float height = 2.6f, float size = 1f)
	{
		SpeechBubble b = who.GetComponent<SpeechBubble>();
		if (b == null)
		{
			b = who.AddComponent<SpeechBubble>();
			b.height = height;
			b.size = size;
		}
		b.Show(line, seconds);
		return b;
	}

	public void Show(Lingala.Line line, float seconds)
	{
		if (root == null)
			Build();
		main.text = line.lingala;
		sub.text = line.english;
		float width = Mathf.Max(line.lingala.Length * 34f, line.english.Length * 20f) + 60f;
		((RectTransform)root.transform).sizeDelta = new Vector2(width, 140f);
		timer = seconds;
		root.SetActive(true);
	}

	void Build()
	{
		if (font == null)
			font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		root = new GameObject("Speech Bubble", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(Image));
		root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
		root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4;
		RectTransform rt = (RectTransform)root.transform;
		rt.SetParent(transform, false);
		rt.localScale = Vector3.one * 0.0048f * size;
		box = root.GetComponent<Image>();
		box.color = new Color(0.05f, 0.05f, 0.08f, 0.78f);
		box.raycastTarget = false;
		main = MakeText("Lingala", 60, FontStyle.Bold, new Color(1f, 0.86f, 0.25f), new Vector2(0, 18));
		sub = MakeText("English", 34, FontStyle.Italic, new Color(1f, 1f, 1f, 0.9f), new Vector2(0, -38));
	}

	Text MakeText(string name, int size, FontStyle style, Color color, Vector2 pos)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
		RectTransform rt = (RectTransform)go.transform;
		rt.SetParent(root.transform, false);
		rt.sizeDelta = new Vector2(1400, size + 16);
		rt.anchoredPosition = pos;
		Text t = go.GetComponent<Text>();
		t.font = font;
		t.fontSize = size;
		t.fontStyle = style;
		t.color = color;
		t.alignment = TextAnchor.MiddleCenter;
		t.horizontalOverflow = HorizontalWrapMode.Overflow;
		t.raycastTarget = false;
		return t;
	}

	void LateUpdate()
	{
		if (root == null || !root.activeSelf)
			return;
		timer -= Time.deltaTime;
		if (timer <= 0)
		{
			root.SetActive(false);
			return;
		}
		// Stay upright over the head (even if the person fell down), facing the camera
		root.transform.position = transform.position + Vector3.up * height;
		Camera cam = Camera.main;
		if (cam != null)
			root.transform.rotation = Quaternion.LookRotation(root.transform.position - cam.transform.position);
	}
}
