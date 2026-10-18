using System;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Rules;
using Godot;

namespace ArquipelagoDoCeu;

// O desenho livre da HUD de combate: a criança escreve em qualquer lugar da tela, o jogo lê e a luta reage.
// Ambiguidade e rabisco nunca custam Energia; só uma resposta errada reconhecida com segurança custa 1.
public partial class CombatHud
{
	FreeDrawLayer? layer;
	bool hintShown;

	// O que a criatura diz em voz e em texto (o balão dela) e o pedido que ela mostra, ou nulo para voltar ao normal.
	public event Action<string>? Said;

	public event Action<string?>? Requested;

	// Um toque curto no vazio não é traço: o Personagem anda até ali.
	public event Action<Vector2>? Tapped;

	void BuildLayer(CombatRules rules)
	{
		layer = new FreeDrawLayer(rules.FreeDraw, rules.Tracing, TrailSource.Shared, () => presenter.DrawAllowed);
		layer.Read += OnRead;
		layer.Tapped += point => Tapped?.Invoke(point);
		layer.ActiveChanged += _ => NotifyComposing();
	}

	// No desenho livre a HUD é só uma fileira baixa de botões: o pedido e os recados ficam no balão da criatura.
	Control BuildCompactBar()
	{
		undo = BookStyle.Button("Apagar", Undo, "Undo", PieceSize);
		var bar = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		bar.AddThemeConstantOverride("separation", 8);
		bar.AddChild(undo);
		bar.AddChild(BookStyle.Button("Dica", AskHint, "Hint", PieceSize));
		bar.AddChild(BookStyle.Button("Ouvir", () => ReadRequested?.Invoke(), "Read", PieceSize));
		bar.AddChild(BookStyle.Button("Soltar alvo", Abandon, "Release", PieceSize));
		bar.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom, LayoutPresetMode.Minsize);
		bar.OffsetBottom = -Margin;
		bar.GrowVertical = GrowDirection.Begin;
		bar.GrowHorizontal = GrowDirection.Both;
		return bar;
	}

	// Um aviso curto: no balão da criatura e em voz.
	void Notice(string text)
	{
		Said?.Invoke(text);
		voice.Say(text, interrupt: true);
	}

	void AskHint()
	{
		HintRequested?.Invoke();
		hintShown = true;
		RefreshGuide();
	}

	// O pontilhado fica sempre à vista onde a regra manda (Nível 1) e, fora isso, só depois de pedir a Dica.
	void RefreshGuide()
	{
		if (layer is null) return;
		var text = hintShown ? presenter.HintGuide ?? presenter.GuideText : presenter.GuideText;
		var shown = text is not null && (hintShown || presenter.GuideAlways);
		layer.SetGuide(text ?? "", shown);
	}

	void OnRead(TextGuess guess)
	{
		if (presenter.Resolved)
		{
			Notice(presenter.ResolvedText);
			return;
		}
		if (!inRange())
		{
			Notice($"Chegue mais perto {creature.Of} para atacar.");
			return;
		}
		if (!rhythm.PlayerReady)
		{
			Notice("Espere um instante.");
			return;
		}
		switch (guess.Kind)
		{
			case GuessKind.Unknown:
				Notice("Não consegui ler. Tente de novo.");
				return;
			case GuessKind.Ambiguous:
				Notice("Fiquei na dúvida. Tente de novo.");
				return;
		}
		if (presenter.Write(guess.Text, out var refusal))
		{
			rhythm.PlayerAnswered();
			hintShown = false;
		}
		else if (refusal is not null) Notice(refusal);
		Refresh();
	}

	// Digitar vale como escrever: cada tecla é um símbolo e, na pausa ou no Enter, o jogo lê.
	void TypeKey(InputEventKey key)
	{
		if (layer is null) return;
		bool handled;
		switch (key.PhysicalKeycode)
		{
			case Key.Backspace:
				handled = layer.IsActive;
				if (!layer.Backspace()) layer.Clear();
				break;
			case Key.Enter or Key.KpEnter:
				handled = layer.TypedText.Length > 0;
				layer.Submit();
				break;
			default:
				handled = key.Unicode != 0 && layer.Type((char)key.Unicode);
				break;
		}
		if (handled) GetViewport().SetInputAsHandled();
	}
}
