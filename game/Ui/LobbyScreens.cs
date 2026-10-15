using System.Collections.Generic;
using ArquipelagoDoCeu.Voice;
using Godot;

namespace ArquipelagoDoCeu;

// Álbum: criaturas e Cosméticos. Ainda não há nada para guardar, então as duas abas só mostram
// uma mensagem amigável.
public partial class AlbumScreen : OverlayScreen
{
	string tab = "criaturas";

	public override void _Ready()
	{
		base._Ready();
		Show(tab);
	}

	void Show(string current)
	{
		var focus = UiKit.FocusIndex(this);
		tab = current;
		var box = UiKit.Card(this, "Álbum");

		var tabs = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		tabs.AddThemeConstantOverride("separation", 12);
		var group = new ButtonGroup();
		foreach (var (id, name) in new[] { ("criaturas", "Criaturas"), ("cosmeticos", "Cosméticos") })
		{
			var tabId = id;
			var button = UiKit.BigButton(name, () => Show(tabId), 220);
			button.ToggleMode = true;
			button.ButtonGroup = group;
			button.ButtonPressed = id == current;
			tabs.AddChild(button);
		}
		box.AddChild(tabs);

		box.AddChild(UiKit.Hint(current == "criaturas"
			? "Você ainda não encontrou nenhuma criatura. Explore as torres para descobrir!"
			: "Você ainda não ganhou nenhum Cosmético. Aprender e explorar rendem novidades!"));
		box.AddChild(UiKit.BigButton("Fechar", Close));
		UiKit.RestoreFocus(this, focus);
	}
}

// Portal de viagem rápida: lista os pontos já descobertos; por enquanto não há nenhum.
public partial class PortalScreen : OverlayScreen
{
	public override void _Ready()
	{
		base._Ready();
		var box = UiKit.Card(this, "Portal de viagem");
		box.AddChild(UiKit.Hint("Ainda não há nenhum ponto de viagem. Quando você chegar a um ponto de uma torre, ele aparece aqui."));
		box.AddChild(UiKit.BigButton("Fechar", Close));
	}
}

// Conversa com o Guia: cada fala aparece escrita (a legenda) e é dita pela voz, se estiver ligada.
public partial class GuideScreen : OverlayScreen
{
 readonly string name;
 readonly IReadOnlyList<string> lines;
 readonly VoiceService voice;
 int index;
 Label? caption;
 Button? advance;
 PanelContainer? panel;
 double revealed;
 public GuideScreen(string name,IReadOnlyList<string> lines,VoiceService voice)
 { this.name=name;this.lines=lines;this.voice=voice; }
 public override void _Ready()
 {
  base._Ready();MouseFilter=MouseFilterEnum.Stop;
  var box=new VBoxContainer();box.AddThemeConstantOverride("separation",8);
  box.AddChild(JourneyStyle.Text(UiKit.Compact ? UiKit.Variant : "minimal",name,24));
  caption=JourneyStyle.Text(UiKit.Compact ? UiKit.Variant : "minimal","",20);caption.Name="GuideCaption";caption.CustomMinimumSize=new Vector2(0,78);box.AddChild(caption);
  var footer=new HBoxContainer();footer.AddThemeConstantOverride("separation",8);
  footer.AddChild(JourneyStyle.Button(UiKit.Compact ? UiKit.Variant : "minimal","Ouvir de novo",()=>{if(lines.Count>0)voice.Say(lines[index],interrupt:true);},"ReplayGuide"));
  advance=JourneyStyle.Button(UiKit.Compact ? UiKit.Variant : "minimal","Revelar",Advance,"AdvanceGuide");advance.SizeFlagsHorizontal=SizeFlags.ExpandFill;footer.AddChild(advance);
  footer.AddChild(JourneyStyle.Button(UiKit.Compact ? UiKit.Variant : "minimal","Sair",Close,"CloseGuide"));box.AddChild(footer);
  panel=JourneyStyle.Panel(UiKit.Compact ? UiKit.Variant : "minimal",box);panel.Name="GuideDialogue";AddChild(panel);Resized+=Arrange;Arrange();ShowLine();advance.GrabFocus();
 }
 void Arrange() { if(panel==null)return;float width=System.Math.Min(850,Size.X-24);JourneyStyle.Place(panel,(Size.X-width)/2,Size.Y-224,width,212); }
 void ShowLine()
 {
  if(lines.Count==0){Close();return;}revealed=0;caption!.Text=lines[index];caption.VisibleCharacters=0;advance!.Text="Revelar · Enter";voice.Say(lines[index],interrupt:true);
 }
 public override void _Process(double delta)
 {
  Arrange(); if(caption==null||lines.Count==0||caption.VisibleCharacters<0)return;
  revealed+=delta*48;caption.VisibleCharacters=(int)revealed;
  if(caption.VisibleCharacters>=caption.GetTotalCharacterCount()) {caption.VisibleCharacters=-1;advance!.Text=index==lines.Count-1?"Concluir · Enter":"Continuar · Enter";}
 }
 void Advance()
 {
  if(caption==null||lines.Count==0)return;
  if(caption.VisibleCharacters>=0){caption.VisibleCharacters=-1;advance!.Text=index==lines.Count-1?"Concluir · Enter":"Continuar · Enter";return;}
  if(index==lines.Count-1)Close();else {index++;ShowLine();}
 }
 public override void _Input(InputEvent @event)
 {
  if(@event is InputEventKey key&&key.Pressed&&!key.Echo&&(key.Keycode==Key.Enter||key.Keycode==Key.KpEnter)) {Advance();GetViewport().SetInputAsHandled();}
 }
 public override void _ExitTree(){base._ExitTree();voice.Stop();}
}
