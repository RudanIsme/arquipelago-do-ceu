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

	// Um toque curto no vazio não é traço: o Personagem anda até ali.
	public event Action<Vector2>? Tapped;

	void BuildLayer(CombatRules rules)
	{
		layer = new FreeDrawLayer(rules.FreeDraw, rules.Tracing, TrailSource.Shared, () => presenter.DrawAllowed);
		layer.Read += OnRead;
		layer.Tapped += point => Tapped?.Invoke(point);
		layer.ActiveChanged += _ => NotifyComposing();
	}

	// No desenho livre não há bolsa: só o que a criança escreveu e os botões.
	Control BuildDrawBar()
	{
		var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		column.AddThemeConstantOverride("separation", 8);
		column.AddChild(assembled);
		column.AddChild(BuildActions());
		var panel = BookStyle.Panel(column);
		panel.Name = "DrawBar";
		return panel;
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
			Tell(presenter.ResolvedText);
			return;
		}
		if (!inRange())
		{
			Tell($"Chegue mais perto {creature.Of} para atacar.");
			return;
		}
		if (!rhythm.PlayerReady)
		{
			Tell("Espere um instante e escreva de novo.");
			return;
		}
		switch (guess.Kind)
		{
			case GuessKind.Unknown:
				Tell("Não consegui ler. Escreva de novo, sem pressa. Isso não custa Energia.");
				return;
			case GuessKind.Ambiguous:
				Tell("Fiquei na dúvida do que você escreveu. Escreva de novo, mais devagar. Isso não custa Energia.");
				return;
		}
		if (presenter.Write(guess.Text, out var refusal))
		{
			rhythm.PlayerAnswered();
			hintShown = false;
		}
		else if (refusal is not null) Tell(refusal);
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
