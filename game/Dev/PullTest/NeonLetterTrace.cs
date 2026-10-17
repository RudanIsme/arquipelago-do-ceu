using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArquipelagoDoCeu;

// Standalone interactive counterpart of ConceptHud's B. Coordinates are 1280x720 design pixels.
public partial class NeonLetterTrace : Control
{
	public bool Completed { get; private set; }
	public float Progress { get; private set; }
	public bool ReduceMotion { get; set; }
	public bool ReduceEffects { get; set; }
	public Action? CompletedAction { get; set; }
	public Action? Cancelled { get; set; }
	public Action? Cleared { get; set; }
	public const float Tolerance = 24f;
	public const float RequiredCoverage = .85f;
	readonly List<Vector2[]> guide = BuildGuide();
	readonly List<List<Vector2>> ink = new();
	bool[] covered = Array.Empty<bool>();
	Vector2[] samples = Array.Empty<Vector2>();
	bool dragging;
	Vector2 previous;
	float validLength, time;
	string hint = "Segure e arraste sobre a luz";
	readonly Rect2 region = new(95, 150, 335, 220);
	readonly Rect2 erase = new(105, 464, 125, 34), cancel = new(245, 464, 155, 34);
	readonly Color paper = new("f9edce"), gold = new("cbb775"), brown = new("514331");
	readonly Color cyan = new("72fff1"), lilac = new("bba4ff");

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore; // Only the explicit drawing area and buttons intercept events.
		samples = guide.SelectMany(x => x).ToArray();
		covered = new bool[samples.Length];
	}
	public override void _Process(double delta)
	{
		if (!IsVisibleInTree()) { dragging = false; return; }
		time += (float)delta;
		QueueRedraw();
	}
	float ViewScale => Mathf.Max(.01f, Mathf.Min(Size.X / 1280f, Size.Y / 720f));
	Vector2 ViewOffset => (Size - new Vector2(1280, 720) * ViewScale) * .5f;
	Vector2 ToDesign(Vector2 screen) => (GetGlobalTransformWithCanvas().AffineInverse() * screen - ViewOffset) / ViewScale;
	Vector2 ToScreen(Vector2 design) => GetGlobalTransformWithCanvas() * (design * ViewScale + ViewOffset);

	// Three physical strokes; do not connect strokes when automating the pointer.
	public Vector2[][] GetGuidePointsScreen() => guide.Select(s => s.Select(ToScreen).ToArray()).ToArray();
	public Vector2 GetErasePointScreen() => ToScreen(erase.GetCenter());
	public Vector2 GetCancelPointScreen() => ToScreen(cancel.GetCenter());

	public override void _Input(InputEvent e)
	{
		if (!IsVisibleInTree()) return;
		if (e is InputEventMouseButton b && b.ButtonIndex == MouseButton.Left)
		{
			var p = ToDesign(b.Position);
			if (b.Pressed && erase.HasPoint(p)) { ResetTrace(); GetViewport().SetInputAsHandled(); return; }
			if (b.Pressed && cancel.HasPoint(p)) { CancelTrace(); GetViewport().SetInputAsHandled(); return; }
			if (b.Pressed && region.HasPoint(p) && !Completed)
			{
				dragging = true;
				previous = p;
				ink.Add(new List<Vector2>());
				hint = "Pode levantar o mouse entre os traços";
				GetViewport().SetInputAsHandled();
			}
			else if (!b.Pressed && dragging)
			{
				AddSegment(previous, p);
				dragging = false;
				FinishStroke();
				GetViewport().SetInputAsHandled();
			}
		}
		else if (e is InputEventMouseMotion m && dragging)
		{
			// Missing release on focus changes must not leave a phantom drawing gesture.
			if ((m.ButtonMask & MouseButtonMask.Left) == 0) { dragging = false; return; }
			var p = ToDesign(m.Position);
			AddSegment(previous, p);
			previous = p;
			GetViewport().SetInputAsHandled();
		}
	}
	void AddSegment(Vector2 a, Vector2 b)
	{
		float distance = a.DistanceTo(b);
		if (distance < .2f) return; // Clicking without moving never contributes coverage.
		int steps = Math.Max(1, (int)Mathf.Ceil(distance / 4f));
		var last = a;
		for (int i = 1; i <= steps; i++)
		{
			var p = a.Lerp(b, i / (float)steps);
			if (region.HasPoint(p) && samples.Any(g => g.DistanceSquaredTo(p) <= Tolerance * Tolerance))
			{
				if (ink.Count == 0 || ink[^1].Count > 3500) ink.Add(new List<Vector2>());
				var stroke = ink[^1];
				if (stroke.Count == 0) stroke.Add(last);
				stroke.Add(p);
				validLength += last.DistanceTo(p);
				for (int n = 0; n < samples.Length; n++)
					if (samples[n].DistanceSquaredTo(p) <= Tolerance * Tolerance) covered[n] = true;
			}
			else if (ink.Count > 0 && ink[^1].Count > 0)
			{
				ink.Add(new List<Vector2>()); // Never draw a bridge across an off-guide shortcut.
				hint = "Volte para o contorno luminoso";
			}
			last = p;
		}
		Progress = covered.Length == 0 ? 0 : covered.Count(v => v) / (float)covered.Length;
	}
	void FinishStroke()
	{
		int offset = 0;
		bool eachPart = true;
		foreach (var part in guide)
		{
			int hit = 0;
			for (int i = 0; i < part.Length; i++) if (covered[offset + i]) hit++;
			eachPart &= hit >= part.Length * .75f;
			offset += part.Length;
		}
		// All three regions and a real amount of dragging are required, not a click or a diagonal swipe.
		if (!Completed && Progress >= RequiredCoverage && eachPart && validLength >= 360f)
		{
			Completed = true;
			hint = "Letra desenhada!";
			CompletedAction?.Invoke();
		}
		else if (!Completed) hint = "Continue onde a luz ainda está suave";
	}
	public void ResetTrace()
	{
		dragging = Completed = false;
		Progress = validLength = 0;
		Array.Clear(covered, 0, covered.Length);
		ink.Clear();
		hint = "Segure e arraste sobre a luz";
		Cleared?.Invoke();
		QueueRedraw();
	}
	public void CancelTrace()
	{
		dragging = false;
		Visible = false;
		Cancelled?.Invoke();
	}
	public override void _Notification(int what)
	{
		if (what == NotificationVisibilityChanged) dragging = false;
	}
	static List<Vector2[]> BuildGuide()
	{
		var result = new List<Vector2[]>();
		result.Add(Enumerable.Range(0, 56).Select(i => new Vector2(250, 350 - 180 * i / 55f)).ToArray());
		result.Add(Enumerable.Range(0, 71).Select(i => { float a = -Mathf.Pi / 2 + Mathf.Pi * i / 70; return new Vector2(250 + 100 * Mathf.Cos(a), 215 + 45 * Mathf.Sin(a)); }).ToArray());
		result.Add(Enumerable.Range(0, 86).Select(i => { float a = -Mathf.Pi / 2 + Mathf.Pi * i / 85; return new Vector2(250 + 116 * Mathf.Cos(a), 305 + 45 * Mathf.Sin(a)); }).ToArray());
		return result;
	}
	void Panel(Rect2 rect)
	{
		var style = new StyleBoxFlat { BgColor = paper, BorderColor = gold };
		style.SetCornerRadiusAll(16);
		style.SetBorderWidthAll(1);
		DrawStyleBox(style, rect);
		style.Dispose();
	}
	void Text(string text, Vector2 at, int size, bool centered = true)
	{
		var font = ThemeDB.FallbackFont;
		if (centered) at.X -= font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X * .5f;
		DrawString(font, at, text, HorizontalAlignment.Left, -1, size, brown);
	}
	public override void _Draw()
	{
		DrawSetTransform(ViewOffset, 0, Vector2.One * ViewScale);
		float pulse = ReduceMotion ? 1f : 1f + .12f * Mathf.Sin(time * 3f);
		foreach (var part in guide)
		{
			if (!ReduceEffects) DrawPolyline(part, new Color(lilac, .13f), 16f * pulse, true);
			DrawPolyline(part, new Color(cyan, .38f), 3, true);
		}
		foreach (var stroke in ink)
		{
			if (stroke.Count < 2) continue;
			var points = stroke.ToArray();
			if (!ReduceEffects) DrawPolyline(points, new Color(lilac, .18f), 19f * pulse, true);
			DrawPolyline(points, cyan, ReduceEffects ? 4 : 6, true);
			DrawPolyline(points, new Color(1, 1, 1, .95f), 1.6f, true);
		}
		DrawCircle(guide[0][0], 5, cyan);
		Panel(new Rect2(95, 378, 325, 77));
		Text(Completed ? "Muito bem!" : "B de borboleta", new Vector2(257, 405), 25);
		Text(hint, new Vector2(257, 429), 16);
		DrawRect(new Rect2(114, 441, 285, 4), new Color(gold, .4f));
		DrawRect(new Rect2(114, 441, 285 * Progress, 4), new Color(.28f, .68f, .62f));
		Panel(erase); Panel(cancel);
		Text("Apagar", erase.GetCenter() + new Vector2(0, 6), 17);
		Text("Cancelar", cancel.GetCenter() + new Vector2(0, 6), 17);
	}
}
