using System;
using Godot;

namespace ArquipelagoDoCeu;

// O tema Livro: página de pergaminho, tinta escura e borda marrom. É o tema das telas e do HUD.
public static class BookStyle
{
	public static readonly Color Ink = Color.FromHtml("#3D2C25");
	public static readonly Color Page = Color.FromHtml("#EEDDB8");
	public static readonly Color Border = Color.FromHtml("#946239");
	public static readonly Color Muted = Color.FromHtml("#7A6A5C");
	public static readonly Color Selected = Color.FromHtml("#F6EACB");
	public static readonly Color Disabled = Color.FromHtml("#CDBFA1");

	const int Radius = 3;
	const int Margin = 10;
	const int TouchHeight = 44;

	public static StyleBoxFlat Box(Color? fill = null, int borderWidth = 1)
	{
		var box = new StyleBoxFlat { BgColor = fill ?? Page, BorderColor = Border };
		box.SetBorderWidthAll(borderWidth);
		box.SetCornerRadiusAll(Radius);
		box.SetContentMarginAll(Margin);
		return box;
	}

	public static PanelContainer Panel(Control child)
	{
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", Box());
		panel.AddChild(child);
		return panel;
	}

	public static Label Text(string text, int size = 18)
	{
		var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", Ink);
		return label;
	}

	public static Button Button(string text, Action action, string name = "", int height = TouchHeight)
	{
		var button = new Button { Text = text, Name = string.IsNullOrEmpty(name) ? "Action" : name, CustomMinimumSize = new Vector2(0, height) };
		button.AddThemeFontSizeOverride("font_size", 16);
		foreach (var color in new[] { "font_color", "font_hover_color", "font_focus_color", "font_pressed_color" })
			button.AddThemeColorOverride(color, Ink);
		button.AddThemeColorOverride("font_disabled_color", Muted);
		StyleStates(button);
		button.Pressed += action;
		return button;
	}

	// O foco e o mouse por cima engrossam a borda, para a seleção nunca depender só da cor.
	static void StyleStates(Button button)
	{
		button.AddThemeStyleboxOverride("normal", Box());
		button.AddThemeStyleboxOverride("hover", Emphasized(Page));
		button.AddThemeStyleboxOverride("focus", Emphasized(Page));
		button.AddThemeStyleboxOverride("pressed", Emphasized(Selected));
		button.AddThemeStyleboxOverride("disabled", Box(Disabled));
	}

	public static StyleBoxFlat Emphasized(Color fill)
	{
		var box = Box(fill, 3);
		box.BorderColor = Ink;
		return box;
	}

	public static void Place(Control control, float x, float y, float width, float height)
	{
		control.Position = new Vector2(x, y);
		control.Size = new Vector2(Math.Max(0, width), Math.Max(0, height));
	}
}
