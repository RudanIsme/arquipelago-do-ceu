using System;
using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Rules;
using Godot;
using NumericsVector2 = System.Numerics.Vector2;

namespace ArquipelagoDoCeu;

// O desenho livre sobre a cena, sem painel nem moldura: o fim do símbolo é levantar o dedo e esperar. Quem lê é o GlyphRecognizer (core).
// Recebe também o teclado (digitar vale como escrever) e devolve um toque curto, para o Personagem andar até ali.
public partial class FreeDrawLayer : Control
{
	const float TapDistance = 14f;
	const float InkWidth = 10f;
	const float GuideDotRadius = 5f;
	const float StartRadius = 12f;
	const float ArrowLength = 34f;
	const float ArrowSize = 12f;
	// O pontilhado fica acima da HUD, no meio da cena.
	const float GuideWidth = 0.5f;
	const float GuideHeight = 0.4f;
	const float GuideCenterY = 0.38f;

	static readonly Color Ink = Color.FromHtml("#F2B632");
	static readonly Color InkEdge = Color.FromHtml("#3D2C25");
	static readonly Color GuideDot = new(0.96f, 0.92f, 0.8f, 0.9f);
	static readonly Color Typed = Color.FromHtml("#F6EACB");

	readonly FreeDrawing drawing;
	readonly GlyphRecognizer recognizer;
	readonly TraceTrails trails;
	readonly TracingRules tracing;
	readonly float idleSeconds;
	readonly Func<string> allowed;
	string typed = "";
	float typedIdle;
	bool wasActive;
	string guideText = "";
	bool guideShown;
	TraceGuide? guide;
	Vector2 guideSize;
	double clock;

	public FreeDrawLayer(FreeDrawRules rules, TracingRules tracing, TraceTrails trails, Func<string> allowed)
	{
		Name = "FreeDrawLayer";
		MouseFilter = MouseFilterEnum.Ignore;
		drawing = new FreeDrawing(rules);
		recognizer = new GlyphRecognizer(trails, rules);
		this.trails = trails;
		this.tracing = tracing;
		this.allowed = allowed;
		idleSeconds = rules.IdleSeconds;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	// Verdadeiro enquanto há desenho ou texto digitado esperando: é quando o mundo desacelera.
	public bool IsActive => drawing.IsActive || typed.Length > 0;

	// O que o teclado já escreveu e ainda não foi lido.
	public string TypedText => typed;

	public bool Enabled { get; set; } = true;

	public event Action<TextGuess>? Read;

	public event Action<Vector2>? Tapped;

	public event Action<bool>? ActiveChanged;

	// O pontilhado do que escrever agora; `shown` liga e desliga (sempre no Nível 1, só na Dica depois).
	public void SetGuide(string text, bool shown)
	{
		if (guideText == text && guideShown == shown) return;
		guideText = text;
		guideShown = shown;
		guide = null;
		QueueRedraw();
	}

	public void Clear()
	{
		drawing.Clear();
		typed = "";
		typedIdle = 0f;
		Notify();
		QueueRedraw();
	}

	// Digitar vale como escrever: cada tecla é um símbolo, lido quando a criança para por um instante (ou aperta Enter).
	public bool Type(char symbol)
	{
		var wanted = char.ToUpperInvariant(symbol);
		if (!allowed().Contains(wanted)) return false;
		typed += wanted;
		typedIdle = 0f;
		Notify();
		QueueRedraw();
		return true;
	}

	public bool Backspace()
	{
		if (typed.Length == 0) return false;
		typed = typed[..^1];
		typedIdle = 0f;
		Notify();
		QueueRedraw();
		return true;
	}

	public void Submit()
	{
		if (typed.Length == 0) return;
		var text = typed;
		typed = "";
		Read?.Invoke(new TextGuess(GuessKind.Sure, text));
		Notify();
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		clock += delta;
		// O mundo anda em câmera lenta enquanto se escreve, mas a espera de 0,8 s é de relógio.
		var real = (float)(Engine.TimeScale > 0.0 ? delta / Engine.TimeScale : delta);
		if (drawing.Tick(real) is { } reading) Read?.Invoke(recognizer.Read(reading, allowed()));
		if (typed.Length > 0 && (typedIdle += real) >= idleSeconds) Submit();
		Notify();
		if (guide is not null || drawing.IsActive || typed.Length > 0) QueueRedraw();
	}

	void Notify()
	{
		if (IsActive == wasActive) return;
		wasActive = IsActive;
		ActiveChanged?.Invoke(wasActive);
	}

	// Antes de tudo, para o clique no vazio virar traço em vez de andar; botões e painéis da HUD seguem recebendo os seus.
	public override void _Input(InputEvent @event)
	{
		if (!Enabled || !IsVisibleInTree()) return;
		switch (@event)
		{
			case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
				OnButton(button);
				break;
			case InputEventMouseMotion motion when drawing.IsDown:
				drawing.Move(LocalPoint(motion.Position));
				GetViewport().SetInputAsHandled();
				QueueRedraw();
				break;
		}
	}

	void OnButton(InputEventMouseButton button)
	{
		if (button.Pressed)
		{
			if (GetViewport().GuiGetHoveredControl() is not null) return;
			drawing.Down(LocalPoint(button.Position));
			GetViewport().SetInputAsHandled();
		}
		else if (drawing.IsDown)
		{
			GetViewport().SetInputAsHandled();
			Finish(button.Position);
		}
		QueueRedraw();
	}

	// Um toque curto não é traço: o Personagem anda até ali.
	void Finish(Vector2 screenPoint)
	{
		var stroke = drawing.Strokes[^1];
		var length = stroke.Zip(stroke.Skip(1), NumericsVector2.Distance).Sum();
		drawing.Up();
		if (length >= TapDistance) return;
		drawing.Discard();
		Tapped?.Invoke(screenPoint);
	}

	NumericsVector2 LocalPoint(Vector2 screenPoint)
	{
		var local = GetGlobalTransformWithCanvas().AffineInverse() * screenPoint;
		return new NumericsVector2(local.X, local.Y);
	}

	public override void _Draw()
	{
		if (guideShown && guideText.Length > 0) DrawGuide();
		foreach (var stroke in drawing.Strokes.Where(s => s.Count > 1))
		{
			var points = stroke.Select(p => new Vector2(p.X, p.Y)).ToArray();
			DrawPolyline(points, InkEdge, InkWidth + 6f, antialiased: true);
			DrawPolyline(points, Ink, InkWidth, antialiased: true);
		}
		if (typed.Length == 0) return;
		var font = ThemeDB.FallbackFont;
		var size = font.GetStringSize(typed, HorizontalAlignment.Left, -1, 96);
		DrawString(font, (Size - size) / 2f + new Vector2(0f, size.Y * 0.75f), typed, HorizontalAlignment.Left, -1, 96, Typed);
	}

	// A mesma referência da pesca: sem moldura, bolinhas no caminho e o início pulsando.
	void DrawGuide()
	{
		var area = new Vector2(Size.X * GuideWidth, Size.Y * GuideHeight);
		if (guide is null || guideSize != area)
		{
			guideSize = area;
			var built = trails.GuideRow(guideText, new NumericsVector2(area.X, area.Y), tracing);
			guide = built.IsOk ? built.Value : null;
		}
		if (guide?.Paths is null) return;

		var offset = new Vector2((Size.X - area.X) / 2f, Size.Y * GuideCenterY - area.Y / 2f);
		var pulse = 1f + 0.25f * Mathf.Sin((float)clock * 5f);
		foreach (var dot in guide.Dots) DrawCircle(offset + new Vector2(dot.X, dot.Y), GuideDotRadius, GuideDot);
		foreach (var path in guide.Paths) DrawStart(path, offset, pulse);
	}

	void DrawStart(TrailPath path, Vector2 offset, float pulse)
	{
		var start = offset + new Vector2(path.Points[0].X, path.Points[0].Y);
		var next = path.Points[Math.Min(2, path.Points.Count - 1)];
		var direction = (offset + new Vector2(next.X, next.Y) - start).Normalized();
		var tip = start + direction * (StartRadius + ArrowLength);
		var side = new Vector2(-direction.Y, direction.X);
		DrawLine(start + direction * StartRadius, tip, InkEdge, 3f, antialiased: true);
		DrawColoredPolygon([tip + direction * ArrowSize, tip + side * ArrowSize * 0.6f, tip - side * ArrowSize * 0.6f], InkEdge);
		DrawCircle(start, StartRadius * pulse + 3f, InkEdge);
		DrawCircle(start, StartRadius * pulse, Ink);
	}
}
