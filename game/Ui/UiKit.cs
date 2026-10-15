using System;
using Godot;

namespace ArquipelagoDoCeu;

// Peças de interface compartilhadas pelas telas, seguindo o HUD da direção de arte:
// painel escuro com borda dourada, texto grande e alvos de toque de pelo menos 72 px.
public static partial class UiKit
{
	public static string Variant { get; private set; } = "minimal";
 public static bool Compact => Variant != "base";
 public static int MinTouch => Compact ? 44 : 72;
 public static int BodyFont => Compact ? 20 : 28;
 public static void SetVariant(string variant) => Variant = Array.IndexOf(JourneyStyle.Ids, variant) >= 0 ? variant : "base";
 public static Color TextColor => Compact ? JourneyStyle.Ink(Variant) : Color.FromHtml("#F1ECFA");
 public static StyleBoxFlat Surface() => Compact ? JourneyStyle.Box(Variant) : new StyleBoxFlat { BgColor=Color.FromHtml("#1B1424"),BorderColor=Color.FromHtml("#D9A64A") };

	const int CardMargin = 28;
	const int CardBreathing = 24;

	static Color PanelFill => Compact ? JourneyStyle.Fill(Variant) : Color.FromHtml("#1B1424");
	static Color Gold => Compact ? JourneyStyle.Accent(Variant) : Color.FromHtml("#D9A64A");
	static Color Soft => TextColor;
	static Color Muted => Compact ? TextColor : Color.FromHtml("#C9BFDC");

	public static Button BigButton(string text, Action onPressed, int minWidth = 240)
	{
		if (Compact) {var compact=JourneyStyle.Button(Variant,text,onPressed);compact.CustomMinimumSize=new Vector2(Math.Min(minWidth,140),44);return compact;}
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
		button.AddThemeStyleboxOverride("disabled", ButtonStyle(Color.FromHtml("#241C31"), Color.FromHtml("#5A4B72")));
		button.AddThemeColorOverride("font_disabled_color", Color.FromHtml("#8D82A3"));
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
		var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = Compact ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off };
		label.AddThemeFontSizeOverride("font_size", Compact ? 26 : 34);
		label.AddThemeColorOverride("font_color", Soft);
		return label;
	}

	public static Label Hint(string text)
	{
		var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(Compact ? 0 : 360, 0) };
		label.AddThemeFontSizeOverride("font_size", Compact ? 18 : 22);
		label.AddThemeColorOverride("font_color", Muted);
		return label;
	}

	// Fala escrita (legenda): maior que a dica, para uma criança que ainda aprende a ler.
	public static Label Caption(string text)
	{
		var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(Compact ? 0 : 560, 0) };
		label.AddThemeFontSizeOverride("font_size", Compact ? 22 : 32);
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

	// Texto 3D sobre a cena: contorno escuro para ler sobre o céu e o chão.
	public static void Outline3D(Label3D label) => label.OutlineModulate = PanelFill;

	// Encaixa um painel na lateral direita (ou embaixo, no centro) e o encolhe quando o texto grande ou uma janela
	// pequena não deixam caber: o painel nunca fica cortado, e tudo continua à vista.
	public static void DockFit(Control host, Control panel, bool right)
 {
  if(Compact)
  {
   host.RemoveChild(panel);var scroll=new ScrollContainer{Name="TrainingScroll",HorizontalScrollMode=ScrollContainer.ScrollMode.Auto,FollowFocus=true};host.AddChild(scroll);scroll.AddChild(panel);panel.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
   scroll.AddChild(new DockLayoutDriver{Host=host,Panel=panel,Scroll=scroll,Right=right});return;
  }
		const float margin = 16f;
		void Fit()
		{
			if (!GodotObject.IsInstanceValid(panel) || !GodotObject.IsInstanceValid(host)) return;
			var min = panel.GetCombinedMinimumSize();
			if (min.X <= 0f || min.Y <= 0f) return;
			var room = host.Size - new Vector2(margin * 2f, margin * 2f);
			var maxWidth = right ? room.X * 0.55f : room.X;
			var scale = Mathf.Clamp(Mathf.Min(maxWidth / min.X, room.Y / min.Y), 0.3f, 1f);
			panel.Scale = Vector2.One * scale;
			panel.Size = min;
			var shown = min * scale;
			panel.Position = right
				? new Vector2(host.Size.X - margin - shown.X, (host.Size.Y - shown.Y) / 2f)
				: new Vector2((host.Size.X - shown.X) / 2f, host.Size.Y - margin - shown.Y);
		}
		host.Resized += Fit;
		panel.MinimumSizeChanged += Fit;
		Callable.From(Fit).CallDeferred();
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
        if(Compact)return CompactCard(host,title,out footer);
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

 static VBoxContainer CompactCard(Control host,string title,out VBoxContainer footer)
 {
  foreach(var child in host.GetChildren()){host.RemoveChild(child);child.QueueFree();}
  var shade=new ColorRect{Color=new Color(0.04f,0.06f,0.09f,0.30f),MouseFilter=Control.MouseFilterEnum.Stop};shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);host.AddChild(shade);
  var panel=new PanelContainer{Name="ThemedCard"};var style=JourneyStyle.Box(Variant);style.SetContentMarginAll(16);panel.AddThemeStyleboxOverride("panel",style);host.AddChild(panel);
  var column=new VBoxContainer();column.AddThemeConstantOverride("separation",10);panel.AddChild(column);
  var scroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,SizeFlagsVertical=Control.SizeFlags.ExpandFill,FollowFocus=true,CustomMinimumSize=new Vector2(0,80)};column.AddChild(scroll);
  var body=new VBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};body.AddThemeConstantOverride("separation",10);scroll.AddChild(body);body.AddChild(Heading(title));
  footer=new VBoxContainer();footer.AddThemeConstantOverride("separation",8);column.AddChild(footer);
  var driver=new CardLayoutDriver{Host=host,Panel=panel,Body=body,Footer=footer,StyleId=Variant};panel.AddChild(driver);
  return body;
 }
 private partial class DockLayoutDriver : Node
 {
  public Control Host=null!,Panel=null!,Scroll=null!;public bool Right;
  public override void _Process(double delta)
  {
   var min=Panel.GetCombinedMinimumSize();float width=Mathf.Min(Host.Size.X-32,Right?440:680);float height=Mathf.Min(Host.Size.Y-32,Mathf.Max(80,min.Y));
   Panel.Scale=Vector2.One;Scroll.Size=new Vector2(width,height);Scroll.Position=new Vector2(Right?Host.Size.X-width-16:(Host.Size.X-width)/2,(Host.Size.Y-height)/2);
  }
 }

 private partial class CardLayoutDriver : Node
 {
  public Control Host=null!,Panel=null!,Body=null!,Footer=null!;public string StyleId="minimal";
  public override void _Process(double delta)
  {
   float width=Mathf.Min(Host.Size.X-32,StyleId=="book"?760:680);
   float height=Mathf.Min(Host.Size.Y-32,Mathf.Max(200,Body.GetCombinedMinimumSize().Y+Footer.GetCombinedMinimumSize().Y+44));
   Panel.Size=new Vector2(width,height);
   Panel.Position=new Vector2(StyleId=="adventure"?Host.Size.X-width-16:(Host.Size.X-width)/2,(Host.Size.Y-height)/2);
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
