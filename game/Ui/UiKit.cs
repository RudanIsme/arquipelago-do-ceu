using System;
using Godot;

namespace ArquipelagoDoCeu;

// Peças de interface compartilhadas pelas telas, seguindo o HUD da direção de arte:
// painel escuro com borda dourada, texto grande e alvos de toque de pelo menos 72 px.
public static class UiKit
{
	public const int MinTouch = 72;

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
		button.AddThemeStyleboxOverride("normal", ButtonStyle(Color.FromHtml("#2E2440"), Gold));
		button.AddThemeStyleboxOverride("hover", ButtonStyle(Color.FromHtml("#43335C"), Gold));
		button.AddThemeStyleboxOverride("pressed", ButtonStyle(Gold, Gold));
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

	// Esvazia o host e monta um painel centralizado com o título; devolve a coluna onde entram os controles.
	public static VBoxContainer Card(Control host, string title)
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
			margin.AddThemeConstantOverride(side, 28);
		panel.AddChild(margin);

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 16);
		margin.AddChild(box);
		box.AddChild(Heading(title));
		return box;
	}
}
