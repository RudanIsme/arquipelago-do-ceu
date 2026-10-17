using System;
using Godot;

namespace ArquipelagoDoCeu;

// Peças de interface compartilhadas pelas telas, seguindo o HUD da direção de arte:
// painel escuro com borda dourada, texto grande e alvos de toque de pelo menos 72 px.
public static partial class UiKit
{
	public static int MinTouch => 44;
	public static int BodyFont => 20;
	public static Color TextColor => BookStyle.Ink;
	public static StyleBoxFlat Surface() => BookStyle.Box();

	static Color PanelFill => BookStyle.Page;
	static Color Soft => TextColor;
	static Color Muted => TextColor;

	public static Button BigButton(string text, Action onPressed, int minWidth = 240)
	{
		var button = BookStyle.Button(text, onPressed);
		button.CustomMinimumSize = new Vector2(Math.Min(minWidth, 140), MinTouch);
		return button;
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
		var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		label.AddThemeFontSizeOverride("font_size", 26);
		label.AddThemeColorOverride("font_color", Soft);
		return label;
	}

	public static Label Hint(string text)
	{
		var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(0, 0) };
		label.AddThemeFontSizeOverride("font_size", 18);
		label.AddThemeColorOverride("font_color", Muted);
		return label;
	}

	// Fala escrita (legenda): maior que a dica, para uma criança que ainda aprende a ler.
	public static Label Caption(string text)
	{
		var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(0, 0) };
		label.AddThemeFontSizeOverride("font_size", 22);
		label.AddThemeColorOverride("font_color", Soft);
		return label;
	}

	// Texto sobre a cena: painel escuro translúcido para o texto ler bem sobre o céu e o chão claros.
	public static PanelContainer HintBar(Label label)
	{
		var style = new StyleBoxFlat { BgColor = new Color(PanelFill, 0.85f), BorderColor = BookStyle.Border };
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(12);
		style.SetContentMarginAll(10);
		var bar = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		bar.AddThemeStyleboxOverride("panel", style);
		bar.AddChild(label);
		return bar;
	}

	// Texto 3D sobre a cena: contorno escuro para ler sobre o céu e o chão.
	public static void Outline3D(Label3D label) => label.OutlineModulate = PanelFill;

	// Encaixa um painel na lateral direita (ou embaixo, no centro) e o encolhe quando o texto grande ou uma janela
	// pequena não deixam caber: o painel nunca fica cortado, e tudo continua à vista.
	public static void DockFit(Control host, Control panel, bool right)
	{
		host.RemoveChild(panel);
		var scroll = new ScrollContainer { Name = "TrainingScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, FollowFocus = true };
		host.AddChild(scroll);
		scroll.AddChild(panel);
		panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scroll.AddChild(new DockLayoutDriver { Host = host, Panel = panel, Scroll = scroll, Right = right });
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
		ClearChildren(host);
		host.AddChild(Shade());
		var panel = new PanelContainer { Name = "ThemedCard" };
		var style = BookStyle.Box();
		style.SetContentMarginAll(16);
		panel.AddThemeStyleboxOverride("panel", style);
		host.AddChild(panel);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 10);
		panel.AddChild(column);
		var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = Control.SizeFlags.ExpandFill, FollowFocus = true, CustomMinimumSize = new Vector2(0, 80) };
		column.AddChild(scroll);
		var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 10);
		scroll.AddChild(body);
		body.AddChild(Heading(title));
		footer = new VBoxContainer();
		footer.AddThemeConstantOverride("separation", 8);
		column.AddChild(footer);
		panel.AddChild(new CardLayoutDriver { Host = host, Panel = panel, Body = body, Footer = footer });
		return body;
	}

	static void ClearChildren(Control host)
	{
		foreach (var child in host.GetChildren())
		{
			host.RemoveChild(child);
			child.QueueFree();
		}
	}

	static ColorRect Shade()
	{
		var shade = new ColorRect { Color = new Color(0.04f, 0.06f, 0.09f, 0.30f), MouseFilter = Control.MouseFilterEnum.Stop };
		shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		return shade;
	}

	private sealed partial class DockLayoutDriver : Node
	{
		const float Margin = 16f;

		public Control Host = null!;
		public Control Panel = null!;
		public Control Scroll = null!;
		public bool Right;

		public override void _Process(double delta)
		{
			var width = Mathf.Min(Host.Size.X - 2 * Margin, Right ? 440 : 680);
			var height = Mathf.Min(Host.Size.Y - 2 * Margin, Mathf.Max(80, Panel.GetCombinedMinimumSize().Y));
			Panel.Scale = Vector2.One;
			Scroll.Size = new Vector2(width, height);
			var left = Right ? Host.Size.X - width - Margin : (Host.Size.X - width) / 2;
			Scroll.Position = new Vector2(left, (Host.Size.Y - height) / 2);
		}
	}

	private sealed partial class CardLayoutDriver : Node
	{
		public Control Host = null!, Panel = null!, Body = null!, Footer = null!;
		public override void _Process(double delta)
		{
			float width = Mathf.Min(Host.Size.X - 32, 760);
			float height = Mathf.Min(Host.Size.Y - 32, Mathf.Max(200, Body.GetCombinedMinimumSize().Y + Footer.GetCombinedMinimumSize().Y + 44));
			Panel.Size = new Vector2(width, height);
			Panel.Position = new Vector2((Host.Size.X - width) / 2, (Host.Size.Y - height) / 2);
		}
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
