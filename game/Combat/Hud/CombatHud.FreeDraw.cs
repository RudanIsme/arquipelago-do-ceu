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
	// A peça da mochila que a criança escolheu e ainda vai contornar no pontilhado: (índice, o que desenhar).
	(int Piece, string Drawing)? pending;
	const int ButtonSize = 56;
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

	// Como no mockup: a mochila fica sempre à vista, numa fileira baixa embaixo, com os botões pequenos ao lado. O pedido e os recados
	// ficam no balão da criatura. A criança escolhe entre atacar na hora (as peças) e desenhar.
	Control BuildCompactBar()
	{
		undo = BookStyle.Button("Apagar", Undo, "Undo", ButtonSize);
		pieces = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center, Name = "Pieces" };
		pieces.AddThemeConstantOverride("h_separation", 12);
		var bar = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		bar.AddThemeConstantOverride("separation", 8);
		var bag = BookStyle.Panel(pieces);
		bag.Name = "Bag";
		bag.CustomMinimumSize = new Vector2(0, PieceSize + 28);
		bag.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		bar.SizeFlagsVertical = SizeFlags.ShrinkEnd;
		bar.AddChild(bag);
		Button[] buttons = [undo, BookStyle.Button("Dica", AskHint, "Hint", ButtonSize), BookStyle.Button("Ouvir", () => ReadRequested?.Invoke(), "Read", ButtonSize), BookStyle.Button("Soltar alvo", Abandon, "Release", ButtonSize)];
		foreach (var button in buttons)
		{
			button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			bar.AddChild(button);
		}
		bar.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide, LayoutPresetMode.Minsize);
		bar.OffsetLeft = Margin;
		bar.OffsetRight = -Margin;
		bar.OffsetBottom = -Margin;
		bar.GrowVertical = GrowDirection.Begin;
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
		if (pending is { } waiting)
		{
			layer.SetGuide(waiting.Drawing, true);
			return;
		}
		var text = hintShown ? presenter.HintGuide ?? presenter.GuideText : presenter.GuideText;
		var shown = text is not null && (hintShown || presenter.GuideAlways);
		layer.SetGuide(text ?? "", shown);
	}

	// Tocar na peça mostra o pontilhado dela no centro; a peça entra na conta quando a criança o contorna.
	void AskToTrace(int piece, string drawing)
	{
		pending = (piece, drawing);
		layer!.Clear();
		RefreshGuide();
		Notice("Siga o pontilhado.");
	}

	void DropPending()
	{
		pending = null;
		layer?.Clear();
		RefreshGuide();
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
		if (pending is { } waiting)
		{
			TraceChecked(waiting, guess.Text);
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

	// A peça escolhida foi desenhada: se bate, entra na conta; se não, a criança tenta de novo, sem custo.
	void TraceChecked((int Piece, string Drawing) waiting, string drawn)
	{
		if (!string.Equals(drawn, waiting.Drawing, StringComparison.OrdinalIgnoreCase))
		{
			Notice("Quase! Siga o pontilhado.");
			return;
		}
		pending = null;
		presenter.Place(waiting.Piece);
		AfterPlace();
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
