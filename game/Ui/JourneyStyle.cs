using System;
using Godot;
namespace ArquipelagoDoCeu;

public static class JourneyStyle
{
 public static readonly string[] Ids = { "minimal", "book", "adventure", "crystal" };
 public static readonly string[] Names = { "Essencial", "Livro", "Aventura", "Cristais" };
 public static Color Ink(string v) => Color.FromHtml(v == "book" ? "#3D2C25" : "#F5F4EF");
 public static Color Fill(string v) => Color.FromHtml(v switch { "book" => "#EEDDB8", "adventure" => "#202E36", "crystal" => "#233147", _ => "#202936" });
 public static Color Accent(string v) => Color.FromHtml(v switch { "book" => "#946239", "adventure" => "#C8B579", "crystal" => "#85E6E6", _ => "#B6DCD1" });
 public static StyleBoxFlat Box(string v, int radius = 12)
 {
  var b = new StyleBoxFlat { BgColor = Fill(v), BorderColor = Accent(v) };
  b.SetBorderWidthAll(v == "minimal" ? 0 : 1); b.SetCornerRadiusAll(v == "book" ? 3 : radius); b.SetContentMarginAll(10); return b;
 }
 public static PanelContainer Panel(string v, Control child)
 { var p = new PanelContainer(); p.AddThemeStyleboxOverride("panel", Box(v)); p.AddChild(child); return p; }
 public static Label Text(string v, string text, int size = 18)
 { var l = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart }; l.AddThemeFontSizeOverride("font_size", size); l.AddThemeColorOverride("font_color", Ink(v)); return l; }
 public static Button Button(string v, string text, Action action, string name = "")
 {
  var b = new Button { Text = text, CustomMinimumSize = new Vector2(0,44), Name = string.IsNullOrEmpty(name) ? "Action" : name, MouseFilter = Control.MouseFilterEnum.Stop };
  b.AddThemeFontSizeOverride("font_size",16); b.AddThemeColorOverride("font_color",Ink(v)); b.AddThemeColorOverride("font_hover_color",Ink(v)); b.AddThemeColorOverride("font_focus_color",Ink(v));
  b.AddThemeStyleboxOverride("normal",Box(v)); var hover=Box(v); hover.BorderColor=Ink(v); hover.SetBorderWidthAll(2); b.AddThemeStyleboxOverride("hover",hover); b.AddThemeStyleboxOverride("focus",hover); b.Pressed += action; return b;
 }
 public static void Place(Control c,float x,float y,float w,float h) { c.Position=new Vector2(x,y); c.Size=new Vector2(Math.Max(0,w),Math.Max(0,h)); }
}
