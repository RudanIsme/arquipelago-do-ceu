using System;
using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Voice;
using Godot;

namespace ArquipelagoDoCeu;

// O Escriba do 2º ano: a criança escreve o nome inteiro, no teclado ou nas letras da tela. Não é modal e nada exige velocidade.
// "Sugerir letra" mostra a próxima com forma e texto, nunca custa Energia; só Conferir conta como resposta.
public partial class WordPanel : OverlayScreen
{
	static int KeySize => 48;
	static int PanelWidth => 340;

	static readonly Color PanelFill = Color.FromHtml("#1B1424");
	static readonly Color Gold = Color.FromHtml("#D9A64A");

	readonly WordBattle battle;
	readonly VoiceService voice;
	readonly List<Button> keyButtons = [];
	HFlowContainer keyRow = null!;
	Label wordLabel = null!;
	Label resultLabel = null!;
	Button eraseButton = null!;
	bool composing;
	char? suggested;

	public WordPanel(WordBattle battle, VoiceService voice)
	{
		this.battle = battle;
		this.voice = voice;
	}

	// A palavra foi conferida; quem trata o resultado, a Energia e a travessura é o treino.
	public event Action<WordResult>? Checked;

	public event Action<bool>? ComposingChanged;

	public event Action? HintRequested;

	public event Action? ReadRequested;

	// A letra e o número de letras certas do que está escrito; só o piloto de desenvolvimento usa, para jogar sem saber a palavra.
	public string ExpectedWord => battle.Word;

	public override void _EnterTree() => Controls.TypingPanels++;

	public override void _ExitTree()
	{
		Controls.TypingPanels--;
		base._ExitTree();
	}

	public override void _Ready()
	{
		base._Ready();
		MouseFilter = MouseFilterEnum.Ignore;
		Build();
		Reload();
		// Sem foco de teclado: as setas continuam movendo o Personagem com o painel aberto.
		foreach (var button in FindChildren("*", "Button", true, false)) ((Button)button).FocusMode = FocusModeEnum.None;
	}

	// Perde o alvo sem responder: o que estava escrito se apaga.
	public void Abandon()
	{
		battle.Clear();
		Close();
	}

	void Build()
	{
		var style = new StyleBoxFlat { BgColor = PanelFill, BorderColor = Gold };
		style.SetBorderWidthAll(3);
		style.SetCornerRadiusAll(16);
		style.SetContentMarginAll(18);
		var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
		panel.AddThemeStyleboxOverride("panel", UiKit.Surface());
		AddChild(panel);
		UiKit.DockFit(this, panel, right: true);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 10);
		panel.AddChild(column);

		var heading = UiKit.Heading("Escreva o nome da criatura");
		heading.CustomMinimumSize = new Vector2(PanelWidth, 0);
		heading.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		column.AddChild(heading);
		wordLabel = UiKit.Caption("");
		wordLabel.CustomMinimumSize = new Vector2(PanelWidth, 0);
		column.AddChild(wordLabel);

		keyRow = Flow();
		column.AddChild(keyRow);

		resultLabel = UiKit.Hint("");
		resultLabel.CustomMinimumSize = new Vector2(PanelWidth, 0);
		column.AddChild(resultLabel);

		var help = Flow();
		help.AddChild(UiKit.BigButton("Ouvir", () => ReadRequested?.Invoke(), 150));
		help.AddChild(UiKit.BigButton("Dica", () => HintRequested?.Invoke(), 150));
		help.AddChild(UiKit.BigButton("Sugerir letra", Suggest, 260));
		eraseButton = UiKit.BigButton("Apagar", Erase, 180);
		help.AddChild(eraseButton);
		column.AddChild(help);

		var actions = Flow();
		actions.AddChild(UiKit.BigButton("Soltar alvo", Abandon, 196));
		actions.AddChild(UiKit.BigButton("Conferir", Check, 196));
		column.AddChild(actions);

		var keys = UiKit.Hint("Teclado: digite as letras, Backspace apaga, Enter confere, Esc solta o alvo. Para andar, use as setas.");
		keys.CustomMinimumSize = new Vector2(PanelWidth, 0);
		column.AddChild(keys);
	}

	static HFlowContainer Flow()
	{
		var flow = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center, CustomMinimumSize = new Vector2(PanelWidth, 0) };
		flow.AddThemeConstantOverride("h_separation", 8);
		flow.AddThemeConstantOverride("v_separation", 8);
		return flow;
	}

	// Mostra a palavra do Desafio atual: as letras da tela podem ter mudado (nova palavra, embaralhada, letra escondida).
	public void Reload()
	{
		foreach (var button in keyButtons)
		{
			keyRow.RemoveChild(button);
			button.QueueFree();
		}
		keyButtons.Clear();
		suggested = null;
		foreach (var letter in battle.Keys)
		{
			var key = letter;
			var button = UiKit.BigButton("", () => TypeLetter(key), KeySize);
			button.CustomMinimumSize = new Vector2(KeySize, KeySize);
			button.AddThemeFontSizeOverride("font_size", 24);
			button.FocusMode = FocusModeEnum.None;
			keyRow.AddChild(button);
			keyButtons.Add(button);
		}
		resultLabel.Text = "";
		Refresh();
	}

	// Uma mensagem escrita que também é dita pela voz: toda instrução tem as duas versões.
	public void Tell(string text)
	{
		resultLabel.Text = text;
		voice.Say(text, interrupt: true);
	}

	public void Refresh()
	{
		var shown = Enumerable.Range(0, battle.Word.Length).Select(i => i < battle.Typed.Length ? char.ToUpperInvariant(battle.Typed[i]).ToString() : "_");
		wordLabel.Text = string.Join(" ", shown);
		for (var i = 0; i < keyButtons.Count; i++)
		{
			var letter = battle.Keys[i];
			var hidden = battle.IsHidden(letter);
			var isSuggested = suggested == letter;
			// A letra sugerida aparece entre colchetes e com fundo dourado: forma, texto e cor.
			keyButtons[i].Text = hidden ? "?" : isSuggested ? $"[{char.ToUpperInvariant(letter)}]" : char.ToUpperInvariant(letter).ToString();
			keyButtons[i].Disabled = hidden;
			keyButtons[i].Modulate = new Color(1f, 1f, 1f, hidden ? 0.4f : 1f);
			if (isSuggested) Highlight(keyButtons[i]);
		}
		eraseButton.Disabled = battle.Typed.Length == 0;
		NotifyComposing();
	}

	static void Highlight(Button button)
	{
		var style = new StyleBoxFlat { BgColor = Gold, BorderColor = Colors.White };
		style.SetBorderWidthAll(4);
		style.SetCornerRadiusAll(14);
		button.AddThemeStyleboxOverride("normal", style);
		button.AddThemeStyleboxOverride("hover", style);
		button.AddThemeColorOverride("font_color", PanelFill);
		button.AddThemeColorOverride("font_hover_color", PanelFill);
	}

	void TypeLetter(char letter)
	{
		if (!battle.Type(letter)) return;
		voice.Say(letter.ToString(), interrupt: true);
		ClearSuggestion();
	}

	// A sugestão some quando a criança age; as teclas são refeitas para perder o destaque dourado.
	void ClearSuggestion()
	{
		resultLabel.Text = "";
		if (suggested is null)
		{
			Refresh();
			return;
		}
		Reload();
	}

	void Erase()
	{
		if (!battle.Backspace()) return;
		ClearSuggestion();
	}

	void Suggest()
	{
		var (letter, erase) = battle.Suggest();
		var shown = char.ToUpperInvariant(letter);
		if (erase)
		{
			Tell($"A letra {shown} não está certa. Apague com o botão Apagar e tente de novo.");
			return;
		}
		suggested = letter;
		Refresh();
		Tell($"Tente a letra {shown}.");
	}

	void Check()
	{
		if (battle.Remaining == 0)
		{
			Tell("A criatura já foi libertada. Espere o novo nome.");
			return;
		}
		var result = battle.Check();
		if (result == WordResult.Incomplete)
		{
			Tell(battle.Typed.Length == 0 ? "Escreva o nome da criatura antes de conferir." : "Ainda faltam letras no nome. Continue escrevendo.");
			return;
		}
		Refresh();
		Checked?.Invoke(result);
	}

	void NotifyComposing()
	{
		var now = battle.Typed.Length > 0;
		if (now == composing) return;
		composing = now;
		ComposingChanged?.Invoke(now);
	}

	// No teclado físico, cada letra digitada vale como tocar na tecla da tela (letras com acento vêm do próprio teclado).
	public override void _Input(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
		switch (key.Keycode)
		{
			case Key.Enter or Key.KpEnter:
				GetViewport().SetInputAsHandled();
				Check();
				return;
			case Key.Backspace:
				GetViewport().SetInputAsHandled();
				Erase();
				return;
		}
		if (key.Unicode == 0 || !char.IsLetter((char)key.Unicode)) return;
		GetViewport().SetInputAsHandled();
		TypeLetter(char.ToLowerInvariant((char)key.Unicode));
	}

	// Esc solta o alvo: o que estava escrito se apaga e nada conta como resposta.
	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_cancel")) return;
		GetViewport().SetInputAsHandled();
		Abandon();
	}
}
