using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Rules;
using Godot;
using NumericsVector2 = System.Numerics.Vector2;

namespace ArquipelagoDoCeu;

// Onde a criança traça um número, sinal ou letra seguindo a trilha; quem confere é Tracing (core). Uma sílaba ou palavra
// vira uma trilha por letra. Nada depende de precisão: "Completar sozinho" ou Enter segurado anima a tinta, e digitar vale como escrever.
public partial class TracePad : VBoxContainer
{
	public static readonly Vector2I CanvasSize = new(392, 270);

	const double GlyphPauseSeconds = 0.35;

	readonly TracingRules rules;
	readonly TraceTrails trails;
	readonly TraceCanvas canvas;
	readonly Label prompt;
	readonly Label message;
	readonly List<string> glyphs = [];
	string expected = "";
	string typed = "";
	string label = "";
	int current;
	bool done;
	// Entre uma letra e a próxima o traço e a tinta ficam parados: nada conta duas vezes.
	bool between;
	bool guided;
	int version;

	public TracePad(TracingRules rules, TraceTrails? trails = null)
	{
		this.rules = rules;
		this.trails = trails ?? TrailSource.Shared;
		AddThemeConstantOverride("separation", 10);
		prompt = UiKit.Heading("");
		AddChild(prompt);

		canvas = new TraceCanvas(rules) { CustomMinimumSize = CanvasSize };
		canvas.StrokeEnded += OnStrokeEnded;
		var center = new CenterContainer();
		center.AddChild(canvas);
		AddChild(center);

		message = UiKit.Hint("");
		message.CustomMinimumSize = new Vector2(CanvasSize.X, 0);
		AddChild(message);

		var buttons = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center };
		buttons.AddThemeConstantOverride("h_separation", 8);
		buttons.AddThemeConstantOverride("v_separation", 8);
		buttons.AddChild(UiKit.BigButton("Apagar", Clear, 190));
		buttons.AddChild(UiKit.BigButton("Voltar", () => Cancelled?.Invoke(), 190));
		buttons.AddChild(UiKit.BigButton("Completar sozinho", () => guided = true, CanvasSize.X));
		AddChild(buttons);
	}

	public event Action? Completed;

	public event Action? Cancelled;

	// Mostra a trilha do primeiro glifo de `text` (um número, uma letra ou uma sílaba) e espera o traço.
	public Task Begin(string text, string label)
	{
		expected = text;
		typed = "";
		this.label = label;
		done = false;
		between = false;
		guided = false;
		version++;
		var usable = GlyphsOf(text).Where(g => trails.Find(g) is not null).ToList();
		glyphs.Clear();
		glyphs.AddRange(usable);
		current = 0;
		if (usable.Count == 0)
		{
			// Sem trilha para nada do pedido, a criança segue sem o desenho: o jogo nunca trava por isso.
			GD.PushWarning($"Trilha indisponível para '{text}'.");
			Finish();
			return Task.CompletedTask;
		}
		ShowGlyph();
		return Task.CompletedTask;
	}

	// Cancela um desenho que ainda estava em andamento.
	public void Abort() => version++;

	// Para o piloto de desenvolvimento: os pontos do guia em coordenadas de tela.
	public IReadOnlyList<Vector2> GuideOnScreen() => canvas.GuideOnScreen();

	// Letras e números sem acento: o acento não tem traço, e o desenho confere só o corpo da letra.
	static IEnumerable<string> GlyphsOf(string text) =>
		text.Normalize(NormalizationForm.FormD)
			.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && !char.IsWhiteSpace(c))
			.Select(c => char.ToUpperInvariant(c).ToString());

	void ShowGlyph()
	{
		var glyph = glyphs[current];
		var guide = trails.Guide(glyph, new NumericsVector2(CanvasSize.X, CanvasSize.Y), rules);
		canvas.Clear();
		canvas.SetGuide(guide.Value!);
		prompt.Text = glyphs.Count > 1 ? $"{label} · {glyph} ({current + 1} de {glyphs.Count})" : label;
		message.Text = guided ? "Olhe a tinta seguir a trilha." : "Siga a trilha, do número 1 na direção da seta. Se preferir, toque em Completar sozinho.";
	}

	void Clear()
	{
		canvas.Clear();
		message.Text = "Pode tentar de novo. Siga a trilha.";
	}

	void OnStrokeEnded(bool passed)
	{
		if (done || between) return;
		if (passed) GlyphDone();
		else message.Text = "Continue pela trilha. Se quiser, apague e tente de novo.";
	}

	void GlyphDone()
	{
		if (current + 1 >= glyphs.Count)
		{
			Finish("Muito bem!");
			return;
		}
		current++;
		between = true;
		message.Text = "Muito bem! Agora a próxima.";
		var mine = version;
		GetTree().CreateTimer(GlyphPauseSeconds, true, false, true).Timeout += () =>
		{
			if (mine != version || done) return;
			between = false;
			ShowGlyph();
		};
	}

	void Finish(string? text = null)
	{
		if (done) return;
		done = true;
		message.Text = text ?? "Tudo bem, vamos seguir.";
		// Um instante para a criança ver a tinta antes de a montagem continuar.
		var mine = version;
		GetTree().CreateTimer(text is null ? 0.05 : 0.5, true, false, true).Timeout += () =>
		{
			if (mine == version) Completed?.Invoke(); // voltar ou começar outro desenho cancela este
		};
	}

	// "Completar sozinho" (botão) ou Enter segurado: a tinta anda sozinha pela trilha.
	public override void _Process(double delta)
	{
		if (!IsVisibleInTree() || done || between || glyphs.Count == 0) return;
		var holding = Input.IsKeyPressed(Key.Enter) || Input.IsKeyPressed(Key.KpEnter);
		if (!guided && !holding) return;
		if (canvas.AutoFill((float)delta)) GlyphDone();
	}

	// No teclado, escrever é digitar: o que está pedido, tecla por tecla, vale como o desenho.
	public override void _Input(InputEvent @event)
	{
		if (!IsVisibleInTree() || done || expected.Length == 0) return;
		if (@event is not InputEventKey { Pressed: true, Echo: false } key || key.Unicode == 0) return;

		typed += char.ToUpperInvariant((char)key.Unicode);
		if (!expected.ToUpperInvariant().StartsWith(typed, StringComparison.Ordinal)) typed = "";
		else if (typed.Length == expected.Length) Finish("Muito bem!");
		if (typed.Length > 0 || done) GetViewport().SetInputAsHandled();
	}
}

// A tela de desenho em si: a trilha como faixa larga e translúcida, o início numerado com a seta, a tinta que
// preenche conforme a criança passa e o traço dela.
sealed partial class TraceCanvas : Control
{
	const float BandWidth = 46f;
	const float InkWidth = 22f;
	const float StrokeWidth = 8f;
	const float MarkerRadius = 15f;
	const float ArrowLength = 34f;
	const float ArrowSize = 12f;
	const float AutoDotsPerSecond = 22f;

	static readonly Color Paper = Color.FromHtml("#F6EACB");
	static readonly Color Border = Color.FromHtml("#946239");
	static readonly Color Band = new(0.24f, 0.17f, 0.15f, 0.16f);
	static readonly Color Ink = Color.FromHtml("#3FA796");
	static readonly Color Mark = Color.FromHtml("#3D2C25");
	static readonly Color Stroke = Color.FromHtml("#3D6EA8");

	readonly TracingRules rules;
	readonly List<List<NumericsVector2>> strokes = [];
	TraceGuide? guide;
	bool[] lit = [];
	bool drawing;
	float auto;

	public TraceCanvas(TracingRules rules)
	{
		this.rules = rules;
		MouseDefaultCursorShape = CursorShape.Cross;
	}

	public event Action<bool>? StrokeEnded;

	public void SetGuide(TraceGuide next)
	{
		guide = next;
		lit = new bool[next.Dots.Count];
		auto = 0f;
		QueueRedraw();
	}

	public void Clear()
	{
		strokes.Clear();
		Array.Clear(lit);
		drawing = false;
		auto = 0f;
		QueueRedraw();
	}

	public IReadOnlyList<Vector2> GuideOnScreen()
	{
		var transform = GetGlobalTransformWithCanvas();
		return guide is null ? [] : guide.Dots.Select(d => transform * new Vector2(d.X, d.Y)).ToList();
	}

	// Anima a tinta pela trilha, ponto a ponto; devolve verdadeiro quando a trilha toda está coberta.
	public bool AutoFill(float seconds)
	{
		if (guide is null) return false;
		auto += seconds * AutoDotsPerSecond;
		for (var i = 0; i < Math.Min(lit.Length, (int)auto); i++) lit[i] = true;
		QueueRedraw();
		return lit.All(l => l);
	}

	public override void _GuiInput(InputEvent @event)
	{
		switch (@event)
		{
			case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
				AcceptEvent();
				if (button.Pressed && guide is not null)
				{
					drawing = true;
					var start = new NumericsVector2(button.Position.X, button.Position.Y);
					strokes.Add([start]);
					Light(start);
				}
				else if (!button.Pressed && drawing)
				{
					drawing = false;
					StrokeEnded?.Invoke(guide is not null && Tracing.Check(guide, strokes.Select(s => (IReadOnlyList<NumericsVector2>)s).ToList(), rules).Passed);
				}
				QueueRedraw();
				break;
			case InputEventMouseMotion motion when drawing:
				AcceptEvent();
				var point = new NumericsVector2(motion.Position.X, motion.Position.Y);
				strokes[^1].Add(point);
				Light(point);
				QueueRedraw();
				break;
		}
	}

	// Acende os pontos perto do traço; o ponteiro anda aos saltos, então o caminho entre dois eventos também conta.
	void Light(NumericsVector2 point)
	{
		if (guide is null) return;
		var stroke = strokes[^1];
		var from = stroke.Count > 1 ? stroke[^2] : point;
		var length = NumericsVector2.Distance(from, point);
		var steps = Math.Max(1, (int)(length / (rules.Tolerance / 2f)));
		for (var s = 1; s <= steps; s++)
		{
			var at = NumericsVector2.Lerp(from, point, (float)s / steps);
			for (var i = 0; i < guide.Dots.Count; i++)
				if (!lit[i] && NumericsVector2.Distance(at, guide.Dots[i]) <= rules.Tolerance) lit[i] = true;
		}
	}

	public override void _Draw()
	{
		var rect = new Rect2(Vector2.Zero, Size);
		DrawRect(rect, Paper);
		DrawRect(rect, Border, filled: false, width: 3f);
		if (guide?.Paths is null) return;

		for (var index = 0; index < guide.Paths.Count; index++)
		{
			var path = guide.Paths[index];
			DrawPolyline(Convert(path.Points), Band, BandWidth, antialiased: true);
			DrawInk(path);
			DrawStart(path, index + 1);
		}
		foreach (var stroke in strokes.Where(s => s.Count > 1))
			DrawPolyline(Convert(stroke), Stroke, StrokeWidth, antialiased: true);
	}

	// A tinta cobre os trechos da trilha em que a criança já passou.
	void DrawInk(TrailPath path)
	{
		var run = new List<Vector2>();
		for (var i = 0; i < path.DotCount; i++)
		{
			if (lit[path.FirstDot + i])
			{
				run.Add(Convert(guide!.Dots[path.FirstDot + i]));
				continue;
			}
			FlushInk(run);
		}
		FlushInk(run);
	}

	void FlushInk(List<Vector2> run)
	{
		if (run.Count > 1) DrawPolyline([.. run], Ink, InkWidth, antialiased: true);
		else if (run.Count == 1) DrawCircle(run[0], InkWidth / 2f, Ink);
		run.Clear();
	}

	// O início numerado e a seta que mostra para onde o traço vai primeiro.
	void DrawStart(TrailPath path, int number)
	{
		var start = Convert(path.Points[0]);
		var direction = (Convert(path.Points[Math.Min(2, path.Points.Count - 1)]) - start).Normalized();
		var tip = start + direction * (MarkerRadius + ArrowLength);
		var side = new Vector2(-direction.Y, direction.X);
		DrawLine(start + direction * MarkerRadius, tip, Mark, 3f, antialiased: true);
		DrawColoredPolygon([tip + direction * ArrowSize, tip + side * ArrowSize * 0.6f, tip - side * ArrowSize * 0.6f], Mark);
		DrawCircle(start, MarkerRadius, Mark);
		var font = ThemeDB.FallbackFont;
		var text = number.ToString(CultureInfo.InvariantCulture);
		var size = font.GetStringSize(text, HorizontalAlignment.Center, -1, 20);
		DrawString(font, start + new Vector2(-size.X / 2f, size.Y / 3f), text, HorizontalAlignment.Left, -1, 20, Paper);
	}

	static Vector2[] Convert(IEnumerable<NumericsVector2> points) => [.. points.Select(Convert)];

	static Vector2 Convert(NumericsVector2 p) => new(p.X, p.Y);
}
