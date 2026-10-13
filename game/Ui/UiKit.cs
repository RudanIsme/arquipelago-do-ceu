using System;
using Godot;

namespace ArquipelagoDoCeu;

// Peças de interface compartilhadas pelas telas, seguindo o HUD da direção de arte:
// painel escuro com borda dourada, texto grande e alvos de toque de pelo menos 72 px.
public static class UiKit
{
	public const int MinTouch = 72;

	const int CardMargin = 28;
	const int CardBreathing = 24;

	static readonly Color PanelFill = Color.FromHtml("#1B1424");
	static readonly Color Gold = Color.FromHtml("#D9A64A");
	static readonly Color Soft = Color.FromHtml("#F1ECFA");
	static readonly Color Muted = Color.FromHtml("#C9BFDC");

	public static Button BigButton(string text, Action onPressed, int minWidth = 240)
	{
		var button = new Button { Text = text, CustomMinimumSize = new Vector2(minWidth, MinTouch) };
		button.AddThemeFontSizeOverride("font_size", 28);
		button.AddThemeColorOverride("font_color", Soft);
		button.AddThemeColorOverride("font_hover_color", Soft);
		button.AddThemeColorOverride("font_pressed_color", PanelFill);
		button.AddThemeColorOverride("font_hover_pressed_color", PanelFill);
		button.AddThemeStyleboxOverride("normal", ButtonStyle(Color.FromHtml("#2E2440"), Gold));
		button.AddThemeStyleboxOverride("hover", ButtonStyle(Color.FromHtml("#43335C"), Gold));
		button.AddThemeStyleboxOverride("pressed", ButtonStyle(Gold, Gold));
		button.AddThemeStyleboxOverride("hover_pressed", ButtonStyle(Gold, Gold));
		button.AddThemeStyleboxOverride("focus", ButtonStyle(Color.FromHtml("#43335C"), Soft));
		button.Pressed += onPressed;
		return button;
	}

	static StyleBoxFlat ButtonStyle(Color fill, Color border)
	{
		var style = new StyleBoxFlat { BgColor = fill, BorderColor = border };
		style.SetBorderWidthAll(3);
		style.SetCornerRadiusAll(14);
		return style;
	}

	// Quadradinho da cor de uma opção; o nome da opção sempre aparece junto, para a cor nunca ser a única informação.
	public static ImageTexture Swatch(string html)
	{
		var image = Image.CreateEmpty(36, 36, false, Image.Format.Rgba8);
		image.Fill(Color.FromHtml(html));
		return ImageTexture.CreateFromImage(image);
	}

	public static Label Heading(string text)
	{
		var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
		label.AddThemeFontSizeOverride("font_size", 34);
		label.AddThemeColorOverride("font_color", Soft);
		return label;
	}

	public static Label Hint(string text)
	{
		var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(360, 0) };
		label.AddThemeFontSizeOverride("font_size", 22);
		label.AddThemeColorOverride("font_color", Muted);
		return label;
	}

	// Fala escrita (legenda): maior que a dica, para uma criança que ainda aprende a ler.
	public static Label Caption(string text)
	{
		var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(560, 0) };
		label.AddThemeFontSizeOverride("font_size", 32);
		label.AddThemeColorOverride("font_color", Soft);
		return label;
	}

	// Texto sobre a cena: painel escuro translúcido para o texto ler bem sobre o céu e o chão claros.
	public static PanelContainer HintBar(Label label)
	{
		var style = new StyleBoxFlat { BgColor = new Color(PanelFill, 0.85f), BorderColor = Gold };
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(12);
		style.SetContentMarginAll(10);
		var bar = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		bar.AddThemeStyleboxOverride("panel", style);
		bar.AddChild(label);
		return bar;
	}

	// Título e avisos soltos sobre a cena ganham contorno escuro para ler em qualquer fundo.
	public static void Outline(Label label)
	{
		label.AddThemeColorOverride("font_outline_color", PanelFill);
		label.AddThemeConstantOverride("outline_size", 8);
		label.AddThemeColorOverride("font_color", Soft);
	}

	// Esvazia o host e monta um painel centralizado com o título; devolve a coluna onde entram os controles.
	// O conteúdo rola quando não cabe na tela (texto maior, janela pequena), para nada ficar inalcançável.
	public static VBoxContainer Card(Control host, string title) => Card(host, title, out _);

	// O rodapé fica fora da rolagem, sempre à vista (por exemplo, o botão Fechar).
	public static VBoxContainer Card(Control host, string title, out VBoxContainer footer)
	{
		foreach (var child in host.GetChildren())
		{
			host.RemoveChild(child);
			child.QueueFree();
		}

		var center = new CenterContainer();
		center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		host.AddChild(center);

		var style = new StyleBoxFlat { BgColor = PanelFill, BorderColor = Gold };
		style.SetBorderWidthAll(3);
		style.SetCornerRadiusAll(16);
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", style);
		center.AddChild(panel);

		var margin = new MarginContainer();
		foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
			margin.AddThemeConstantOverride(side, CardMargin);
		panel.AddChild(margin);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 16);
		margin.AddChild(column);
		var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		column.AddChild(scroll);
		footer = new VBoxContainer();
		footer.AddThemeConstantOverride("separation", 16);
		column.AddChild(footer);
		var footerBox = footer;

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 16);
		scroll.AddChild(box);
		box.AddChild(Heading(title));

		void Fit()
		{
			if (!GodotObject.IsInstanceValid(scroll) || !GodotObject.IsInstanceValid(box)) return;
			var footerHeight = footerBox.GetCombinedMinimumSize().Y;
			var room = host.Size.Y - 2 * CardMargin - CardBreathing - (footerHeight > 0f ? footerHeight + 16f : 0f);
			var wanted = box.GetCombinedMinimumSize();
			scroll.CustomMinimumSize = new Vector2(wanted.X, room > 0f ? Mathf.Min(wanted.Y, room) : wanted.Y);
		}
		// Reconstruir o cartão no mesmo host não pode empilhar ouvintes de cartões já descartados.
		const string FitMeta = "card_fit";
		if (host.HasMeta(FitMeta) && host.GetMeta(FitMeta).AsCallable() is var old && host.IsConnected(Control.SignalName.Resized, old))
			host.Disconnect(Control.SignalName.Resized, old);
		var fit = Callable.From(Fit);
		host.SetMeta(FitMeta, fit);
		host.Connect(Control.SignalName.Resized, fit);
		box.MinimumSizeChanged += Fit;
		footer.MinimumSizeChanged += Fit;
		Callable.From(Fit).CallDeferred();
		return box;
	}

	// Reconstruir uma tela destrói os botões; devolve o foco ao botão que estava no mesmo lugar, para o teclado não recomeçar do zero.
	public static int FocusIndex(Control root)
	{
		var owner = root.GetViewport().GuiGetFocusOwner();
		var buttons = root.FindChildren("*", "Button", true, false);
		return owner is null ? -1 : buttons.IndexOf(owner);
	}

	public static void RestoreFocus(Control root, int index)
	{
		if (index < 0) return;
		var buttons = root.FindChildren("*", "Button", true, false);
		if (index < buttons.Count) ((Control)buttons[index]).CallDeferred(Control.MethodName.GrabFocus);
	}

	// Botão que liga e desliga; o estado aparece em palavras, não só na cor.
	public static Button Toggle(string label, bool on, Action<bool> onChanged)
	{
		Button? button = null;
		string Text(bool value) => $"{label}: {(value ? "Ligado" : "Desligado")}";
		button = BigButton(Text(on), () => { }, 420);
		button.ToggleMode = true;
		button.ButtonPressed = on;
		button.Toggled += value =>
		{
			button.Text = Text(value);
			onChanged(value);
		};
		return button;
	}
}

// Base das telas que abrem por cima do Lobby: Esc (ou o botão Fechar) fecha, e o Lobby volta a valer.
public abstract partial class OverlayScreen : Control
{
	public event Action? Closed;

	public override void _Ready() => SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

	public void Close() => Closed?.Invoke();

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_cancel")) return;
		GetViewport().SetInputAsHandled();
		Close();
	}
}
