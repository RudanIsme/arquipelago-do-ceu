using System;
using Godot;
namespace ArquipelagoDoCeu;

public partial class JourneyHud : Control
{
 public event Action<string>? VariantRequested;
 public event Action? EquipmentRequested, RestoreBaseRequested, ProfilesRequested, SettingsRequested, InteractRequested;
 string variant="book", nickname="Viajante", bonus="", notice="Explore a ilha e converse com o Guia.";
 string? interactionText;
 Control? selector, identity, navigation, context;
 Label? noticeLabel; Button? interact; bool comparisonUi;
 public JourneyHud() { Name="JourneyHud"; MouseFilter=MouseFilterEnum.Ignore; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); Resized+=()=>{settleFrames=12;Arrange();}; }
 public override void _Ready() { Build(); }
 public void Configure(string variant,string nickname,string bonusSummary) { this.variant=variant; this.nickname=nickname; bonus=bonusSummary; if(IsInsideTree())Build(); }
 int settleFrames;
 public override void _Process(double delta) {if(settleFrames-- > 0)Arrange();}
 void Build() { settleFrames=12;
  foreach(Node n in GetChildren()) { RemoveChild(n); n.QueueFree(); }
  var selection=new HBoxContainer(); selection.AddThemeConstantOverride("separation",4);
  for(int i=0;i<4;i++) { var id=JourneyStyle.Ids[i]; if(!comparisonUi && id!="book")continue; var b=JourneyStyle.Button(variant,(id==variant?"• ":"")+JourneyStyle.Names[i],()=>{comparisonUi=false;VariantRequested?.Invoke(id);},"Variant_"+id); b.SizeFlagsHorizontal=SizeFlags.ExpandFill; selection.AddChild(b); }
  if(comparisonUi) selection.AddChild(JourneyStyle.Button(variant,"Base",()=>RestoreBaseRequested?.Invoke(),"RestoreBase")); else selection.AddChild(JourneyStyle.Button(variant,"Comparar UIs",()=>{comparisonUi=true;Build();},"CompareInterfaces")); selector=selection; AddChild(selector);
  var title=new VBoxContainer(); title.AddChild(JourneyStyle.Text(variant,variant=="book"?"Diário de "+nickname:variant=="crystal"?"◇ "+nickname:nickname,20));
  var subtitle=JourneyStyle.Text(variant,variant switch {"book"=>"Capítulo I · A ilha das descobertas","adventure"=>"ACAMPAMENTO · ARQUIPÉLAGO","crystal"=>"NÚCLEO DA ILHA",_=>"Arquipélago do Céu"},16); title.AddChild(subtitle); identity=JourneyStyle.Panel(variant,title); AddChild(identity);
  BoxContainer nav=(variant=="adventure"||variant=="book")?new VBoxContainer():new HBoxContainer(); nav.AddThemeConstantOverride("separation",4);
  nav.AddChild(JourneyStyle.Button(variant,"Comparar",()=>EquipmentRequested?.Invoke(),"Equipment"));
  nav.AddChild(JourneyStyle.Button(variant,"Perfil",()=>ProfilesRequested?.Invoke(),"Profiles"));
  nav.AddChild(JourneyStyle.Button(variant,"Ajustes",()=>SettingsRequested?.Invoke(),"Settings")); navigation=nav; AddChild(nav);
  var ctx=new VBoxContainer(); ctx.AddThemeConstantOverride("separation",4); noticeLabel=JourneyStyle.Text(variant,notice,18); noticeLabel.MaxLinesVisible=2; ctx.AddChild(noticeLabel);
  interact=JourneyStyle.Button(variant,"Conversar",()=>InteractRequested?.Invoke(),"Interact"); interact.Visible=!string.IsNullOrWhiteSpace(interactionText); interact.Text=interactionText??"Conversar";ctx.AddChild(interact);
  context=JourneyStyle.Panel(variant,ctx); AddChild(context); UpdateContext();
  Arrange();
 }
 void Arrange()
 {
  if(selector==null||identity==null||navigation==null||context==null)return;
  float w=Size.X,h=Size.Y; if(w<=0||h<=0)return; const float m=12;
  JourneyStyle.Place(selector,Math.Max(m,w-492),m,Math.Min(480,w-24),44);
  float infoY=w<1100?64:12;
  JourneyStyle.Place(identity,m,infoY,Math.Min(325,w-24),76);
  if(variant=="adventure") { JourneyStyle.Place(navigation,m,infoY+84,106,192); JourneyStyle.Place(context,130,h-112,Math.Min(410,w-272),100); }
  else if(variant=="book") { JourneyStyle.Place(navigation,w-130,h-266,118,192); JourneyStyle.Place(context,m,h-124,Math.Min(420,w-164),104); }
  else if(variant=="crystal") { JourneyStyle.Place(navigation,(w-370)/2,h-58,370,44); JourneyStyle.Place(context,(w-390)/2,h-170,390,104); }
  else { JourneyStyle.Place(navigation,m,h-58,Math.Min(370,w-24),44); JourneyStyle.Place(context,m,h-170,Math.Min(380,w-24),104); }
 }
 public void SetInteraction(string? text) { interactionText=text; if(interact==null)return; interact.Visible=!string.IsNullOrWhiteSpace(text);interact.Text=text??""; UpdateContext(); }
 public void SetNotice(string text) { notice=text;if(noticeLabel!=null)noticeLabel.Text=text; UpdateContext(); }
 void UpdateContext() {if(context!=null)context.Visible=!string.IsNullOrWhiteSpace(notice)||!string.IsNullOrWhiteSpace(interactionText);}
 public void SetPlayState(bool enabled) { Visible=enabled; }
 public Godot.Collections.Dictionary LayoutDiagnostics() { var result=new Godot.Collections.Dictionary(); foreach(Node n in GetChildren()) if(n is Control c)result[n.Name]=new Rect2(c.Position,c.Size); return result; }
}
