using System;
using ArquipelagoDoCeu.Rules;
using Godot;

namespace ArquipelagoDoCeu;

// A área de traço da HUD de combate: ao lado da bolsa, sem tapar o centro da cena. A peça escolhida sobe da bolsa até aqui, animada,
// e a criança a traça. Não é modal: o Personagem continua à vista e o tempo só desacelera.
public partial class TraceZone : PanelContainer
{
	const double RiseSeconds = 0.35;
	const int FlyerFontSize = 54;

	readonly TracePad pad;

	public TraceZone(TracingRules rules)
	{
		Name = "TraceZone";
		Visible = false;
		MouseFilter = MouseFilterEnum.Stop;
		AddThemeStyleboxOverride("panel", BookStyle.Box());
		pad = new TracePad(rules);
		pad.Completed += () => Completed?.Invoke();
		pad.Cancelled += () => Cancelled?.Invoke();
		AddChild(pad);
		SizeFlagsVertical = SizeFlags.ShrinkEnd;
	}

	public event Action? Completed;

	public event Action? Cancelled;

	public TracePad Pad => pad;

	// Mostra o traço de `text`; `from` é de onde a peça sobe (o centro do botão dela), e sem `animate` ela aparece direto.
	public void Begin(string text, string label, Vector2 from, bool animate)
	{
		Visible = true;
		_ = pad.Begin(text, label);
		if (animate) Rise(text, from);
	}

	public void End()
	{
		pad.Abort();
		Visible = false;
	}

	// A peça é só enfeite de movimento: o traço já está pronto desde o primeiro quadro.
	void Rise(string text, Vector2 from)
	{
		var flyer = BookStyle.Text(text, FlyerFontSize);
		flyer.AutowrapMode = TextServer.AutowrapMode.Off;
		flyer.ZIndex = 10;
		GetParent().AddChild(flyer);
		flyer.GlobalPosition = from;
		var to = pad.GlobalPosition + new Vector2(TracePad.CanvasSize.X, TracePad.CanvasSize.Y) / 2f;
		// O tempo do jogo desacelera durante o traço; a animação segue em tempo de relógio.
		var tween = CreateTween().SetIgnoreTimeScale(true).SetParallel();
		tween.TweenProperty(flyer, "global_position", to, RiseSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(flyer, "modulate:a", 0f, RiseSeconds).SetDelay(RiseSeconds * 0.5);
		tween.Chain().TweenCallback(Callable.From(flyer.QueueFree));
	}
}
