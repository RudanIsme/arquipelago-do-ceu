using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Rules;
using Godot;
using NumericsVector2 = System.Numerics.Vector2;

namespace ArquipelagoDoCeu;

// A área onde a criança liga o pontilhado de um número ou letra; quem confere é Tracing (core).
// Há sempre "Pular o desenho", e no teclado digitar o pedido vale como escrever.
public partial class TracePad : VBoxContainer
{
	public static readonly Vector2I CanvasSize = new(392, 270);


	readonly TracingRules rules;
	readonly TraceCanvas canvas;
	readonly Label prompt;
	readonly Label message;
	string expected = "";
	string typed = "";
	bool done;
	int version;

	public TracePad(TracingRules rules)
	{
		this.rules = rules;
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
		buttons.AddChild(UiKit.BigButton("Pular o desenho", () => Finish(), CanvasSize.X));
		AddChild(buttons);
	}

	public event Action? Completed;

	public event Action? Cancelled;

	// Mostra o pontilhado de `text` (um número, uma letra ou uma sílaba) e espera o desenho.
	public async Task Begin(string text, string label)
	{
		expected = text;
		typed = "";
		done = false;
		prompt.Text = label;
		message.Text = "Ligue os pontinhos com o dedo ou o mouse.";
		canvas.Clear();

		var mine = ++version;
		var mask = await GlyphMask.Render(this, text, GetThemeDefaultFont(), CanvasSize);
		if (mine != version) return; // a criança voltou antes de o pontilhado ficar pronto
		var guide = mask is null ? Result<TraceGuide>.Fail("A imagem do pontilhado não ficou pronta.") : Tracing.GuideFromMask(mask, rules);
		if (!guide.IsOk)
		{
			// Sem como desenhar o pontilhado, a criança segue sem ele: o jogo nunca trava por isso.
			GD.PushWarning($"Pontilhado indisponível para '{text}'.");
			Finish();
			return;
		}
		canvas.SetGuide(guide.Value!);
	}

	// Cancela um pontilhado que ainda estava sendo preparado.
	public void Abort() => version++;

	// Para o piloto de desenvolvimento: os pontos do guia em coordenadas de tela.
	public IReadOnlyList<Vector2> GuideOnScreen() => canvas.GuideOnScreen();

	void Clear()
	{
		canvas.Clear();
		message.Text = "Pode tentar de novo. Ligue os pontinhos.";
	}

	void OnStrokeEnded(bool passed)
	{
		if (done) return;
		if (passed) Finish("Muito bem!");
		else message.Text = "Continue ligando os pontinhos. Se quiser, apague e tente de novo.";
	}

	void Finish(string? text = null)
	{
		if (done) return;
		done = true;
		message.Text = text ?? "Tudo bem, vamos seguir.";
		// Um instante para a criança ver os pontos acesos antes de a montagem continuar.
		var mine = version;
		GetTree().CreateTimer(text is null ? 0.05 : 0.5, true, false, true).Timeout += () =>
		{
			if (mine == version) Completed?.Invoke(); // voltar ou começar outro desenho cancela este
		};
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

// A tela de desenho em si: pontos-guia, o traço da criança e o aviso de quais pontos já foram ligados.
sealed partial class TraceCanvas : Control
{
	const float DotRadius = 7f;
	const float LitRadius = 11f;
	const float StrokeWidth = 14f;

	static readonly Color Paper = Color.FromHtml("#2A2138");
	static readonly Color Border = Color.FromHtml("#D9A64A");
	static readonly Color Dot = Color.FromHtml("#F2D27A");
	static readonly Color Lit = Color.FromHtml("#7BD88F");
	static readonly Color Ink = Color.FromHtml("#8FD3FF");

	readonly TracingRules rules;
	readonly List<List<NumericsVector2>> strokes = [];
	TraceGuide? guide;
	bool[] lit = [];
	bool drawing;

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
		QueueRedraw();
	}

	public void Clear()
	{
		strokes.Clear();
		Array.Clear(lit);
		drawing = false;
		QueueRedraw();
	}

	public IReadOnlyList<Vector2> GuideOnScreen()
	{
		var transform = GetGlobalTransformWithCanvas();
		return guide is null ? [] : guide.Dots.Select(d => transform * new Vector2(d.X, d.Y)).ToList();
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
		if (guide is null) return;

		for (var i = 0; i < guide.Dots.Count; i++)
		{
			var dot = guide.Dots[i];
			// Ponto ligado fica maior e cheio: a pista não depende só da cor.
			DrawCircle(new Vector2(dot.X, dot.Y), lit[i] ? LitRadius : DotRadius, lit[i] ? Lit : Dot);
		}
		foreach (var stroke in strokes.Where(s => s.Count > 1))
			DrawPolyline(stroke.Select(p => new Vector2(p.X, p.Y)).ToArray(), Ink, StrokeWidth, antialiased: true);
	}

}

// Desenha o texto com a fonte do jogo fora da tela e devolve onde há tinta, recortada e ampliada para a área,
// então qualquer fonte gera um pontilhado grande, inteiro e centralizado.
static class GlyphMask
{
	const float Fill = 0.88f;
	const int Oversample = 2;
	const float RenderFontSize = 360f;

	public static async Task<bool[,]?> Render(Node host, string text, Font font, Vector2I size)
	{
		var big = size * Oversample;
		var measured = font.GetStringSize(text, HorizontalAlignment.Left, -1, (int)RenderFontSize);
		var fontSize = (int)Mathf.Min(RenderFontSize, RenderFontSize * big.X * 0.9f / Mathf.Max(measured.X, 1f));

		var viewport = new SubViewport { Size = big, TransparentBg = true, Disable3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Once };
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			LabelSettings = new LabelSettings { Font = font, FontSize = fontSize, FontColor = Colors.White },
		};
		label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		viewport.AddChild(label);
		host.AddChild(viewport);

		// A imagem só fica pronta depois de o quadro ser desenhado.
		await host.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		await host.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		var image = GodotObject.IsInstanceValid(viewport) ? viewport.GetTexture()?.GetImage() : null;
		if (GodotObject.IsInstanceValid(viewport)) viewport.QueueFree();
		if (image is null || image.IsEmpty() || image.GetSize() != big) return null;
		return FitToCanvas(image, size);
	}

	static bool[,]? FitToCanvas(Image image, Vector2I size)
	{
		int minX = image.GetWidth(), maxX = -1, minY = image.GetHeight(), maxY = -1;
		for (var y = 0; y < image.GetHeight(); y++)
			for (var x = 0; x < image.GetWidth(); x++)
				if (image.GetPixel(x, y).A > 0.5f)
				{
					minX = Mathf.Min(minX, x);
					maxX = Mathf.Max(maxX, x);
					minY = Mathf.Min(minY, y);
					maxY = Mathf.Max(maxY, y);
				}
		if (maxX < 0) return null;

		var ink = image.GetRegion(new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1));
		var scale = Mathf.Min(size.X * Fill / ink.GetWidth(), size.Y * Fill / ink.GetHeight());
		var width = Mathf.Max(1, (int)(ink.GetWidth() * scale));
		var height = Mathf.Max(1, (int)(ink.GetHeight() * scale));
		ink.Resize(width, height, Image.Interpolation.Bilinear);

		var offsetX = (size.X - width) / 2;
		var offsetY = (size.Y - height) / 2;
		var mask = new bool[size.X, size.Y];
		for (var y = 0; y < height; y++)
			for (var x = 0; x < width; x++)
				mask[offsetX + x, offsetY + y] = ink.GetPixel(x, y).A > 0.5f;
		return mask;
	}
}
