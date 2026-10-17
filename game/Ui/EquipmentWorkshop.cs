using System;
using Godot;
using ArquipelagoDoCeu.Equipment;
namespace ArquipelagoDoCeu;

/// <summary>A local draft; only Apply commits. UI variants never replace the draft.</summary>
public partial class EquipmentWorkshop : Control
{
 public event Action<EquipmentLoadout>? Applied, PreviewChanged;
 public event Action? Cancelled;
 string variant;
 readonly bool comparisonOnly;
 EquipmentLoadout draft;
 EquipmentPreview? preview; Label? errorLabel; string saveError="";
 PanelContainer? shell; Control? body; Label? summary; string category="armor";
 public EquipmentWorkshop() : this("minimal", new EquipmentLoadout()) {}
 // O texto dos bônus vem de quem conhece o catálogo em dados; sem ele, o do experimento.
 public Func<EquipmentLoadout,string>? Summarize { get; set; }
 string Describe(EquipmentLoadout value) => (Summarize ?? EquipmentCatalog.Summary)(value);
 public void SetVariant(string value) {variant=Array.IndexOf(JourneyStyle.Ids,value)>=0?value:"minimal";if(IsInsideTree())Build();}
 public EquipmentWorkshop(string variant,EquipmentLoadout current, bool comparisonOnly = false)
 { this.comparisonOnly=comparisonOnly;this.variant=variant;draft=EquipmentCatalog.Normalize(current);Name="EquipmentWorkshop"; MouseFilter=MouseFilterEnum.Stop;SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);Resized+=()=>{settleFrames=12;Arrange();}; }
 public override void _Ready()
 {
  var shade=new ColorRect { Color=new Color(0.03f,0.06f,0.09f,0.35f),MouseFilter=MouseFilterEnum.Stop };shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(shade);preview=new EquipmentPreview(variant);AddChild(preview);preview.Apply(draft);Build();
 }
 int settleFrames; bool compactLayout;
 bool NeedsCompactLayout() => Size.X > 0 && (Size.X < 1100 || Size.Y < 620);
 public override void _Process(double delta) {if(compactLayout!=NeedsCompactLayout())Build();if(settleFrames-- > 0)Arrange();}
 void Build() { settleFrames=12;compactLayout=NeedsCompactLayout();
  string focusName = GetViewport().GuiGetFocusOwner()?.Name.ToString() ?? "";
  if(shell!=null) {RemoveChild(shell);shell.QueueFree();}
  var stack=new VBoxContainer();stack.AddThemeConstantOverride("separation",8);
  // In a small window, presets and categories scroll too; confirmation never leaves the screen.
  var contents=compactLayout?new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill}:stack;
  contents.AddThemeConstantOverride("separation",8);
  string title=variant switch {"book"=>"O livro do viajante","adventure"=>"Inventário de expedição","crystal"=>"◇ Bancada dos cristais",_=>"Seu equipamento"};
  contents.AddChild(JourneyStyle.Text(variant,title,26));contents.AddChild(JourneyStyle.Text(variant,comparisonOnly ? "Comparação não aprovada. Guardar preserva o traje original." : "Prévia temporária · Aplicar salva; Cancelar restaura.",16));
  var presetGrid=new GridContainer {Columns=2};presetGrid.AddThemeConstantOverride("h_separation",6);presetGrid.AddThemeConstantOverride("v_separation",4);
  for(int i=0;i<4;i++){ string id=JourneyStyle.Ids[i];var b=JourneyStyle.Button(variant,"Preset "+JourneyStyle.Names[i],()=>Change(EquipmentCatalog.Preset(id)),"Preset_"+id);b.SizeFlagsHorizontal=SizeFlags.ExpandFill;presetGrid.AddChild(b); }
  contents.AddChild(presetGrid);
  if(variant=="crystal")
  {
   var compass=new HBoxContainer { Alignment=BoxContainer.AlignmentMode.Center }; compass.AddThemeConstantOverride("separation",12);
   compass.AddChild(JourneyStyle.Button(variant,"Armadura",()=>{category="armor";Build();},"Tab_armor"));
   var axis=new VBoxContainer(); var glyph=JourneyStyle.Text(variant,"◇",28);glyph.HorizontalAlignment=HorizontalAlignment.Center;axis.AddChild(glyph);axis.AddChild(JourneyStyle.Button(variant,"Cores",()=>{category="colors";Build();},"Tab_colors"));compass.AddChild(axis);
   compass.AddChild(JourneyStyle.Button(variant,"Capacete",()=>{category="helmet";Build();},"Tab_helmet"));contents.AddChild(compass);
  }
  if(variant=="minimal")
  {
   var tabs=new HBoxContainer();foreach(var pair in new[]{("armor","Armadura"),("helmet","Capacete"),("colors","Cores")}) {var key=pair.Item1;var b=JourneyStyle.Button(variant,(key==category?"• ":"")+pair.Item2,()=>{category=key;Build();},"Tab_"+key);b.SizeFlagsHorizontal=SizeFlags.ExpandFill;tabs.AddChild(b);}contents.AddChild(tabs);
  }
  var scroll=new ScrollContainer {SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,FollowFocus=true,CustomMinimumSize=new Vector2(0,100)};
  if(variant=="book")
  {
   BoxContainer spread=compactLayout?new VBoxContainer():new HBoxContainer();spread.SizeFlagsHorizontal=SizeFlags.ExpandFill;spread.AddThemeConstantOverride("separation",20);
   var left=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};left.AddChild(JourneyStyle.Text(variant,"I · Objetos da jornada",20));AddArmor(left);AddHelmet(left);
   var right=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};right.AddChild(JourneyStyle.Text(variant,"II · Tintas e metais",20));AddColors(right);right.AddChild(JourneyStyle.Text(variant,"Cada peça tem um efeito único. Trocar equipamento não recupera vida nem soma bônus anteriores.",16));spread.AddChild(left);spread.AddChild(right);body=spread;
  }
  else
  {var list=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};list.AddThemeConstantOverride("separation",6);if(variant=="adventure"||category=="armor")AddArmor(list);if(variant=="adventure"||category=="helmet")AddHelmet(list);if(variant=="adventure"||category=="colors")AddColors(list);body=list;}
  if(compactLayout){contents.AddChild(body);scroll.AddChild(contents);}else scroll.AddChild(body);stack.AddChild(scroll);
  errorLabel=JourneyStyle.Text(variant,saveError,16);errorLabel.Name="EquipmentSaveError";errorLabel.Visible=!string.IsNullOrWhiteSpace(saveError);stack.AddChild(errorLabel);
  summary=JourneyStyle.Text(variant,(comparisonOnly ? "Bônus abaixo desativados no jogo.\n" : "")+Describe(draft),16);summary.Name="BonusSummary";stack.AddChild(summary);
  var actions=new HBoxContainer(); var cancel=JourneyStyle.Button(variant,"Cancelar",()=>Cancelled?.Invoke(),"CancelEquipment");cancel.SizeFlagsHorizontal=SizeFlags.ExpandFill;actions.AddChild(cancel);
  var apply=JourneyStyle.Button(variant,comparisonOnly ? "Guardar comparação" : "Aplicar e salvar",()=>{ShowError("");Applied?.Invoke(EquipmentCatalog.Normalize(draft));},"ApplyEquipment");apply.SizeFlagsHorizontal=SizeFlags.ExpandFill;actions.AddChild(apply);stack.AddChild(actions);
  shell=JourneyStyle.Panel(variant,stack);shell.Name="WorkshopPanel";AddChild(shell);Arrange();
  if(string.IsNullOrEmpty(focusName)||shell.FindChild(focusName,true,false) is not Control) focusName="Preset_minimal";
  if(!string.IsNullOrEmpty(focusName)) Callable.From(()=>{ if(IsInstanceValid(shell)) (shell.FindChild(focusName,true,false) as Control)?.GrabFocus(); }).CallDeferred();
 }
 void AddArmor(VBoxContainer list)
 {list.AddChild(JourneyStyle.Text(variant,"Armadura",20));foreach(var item in EquipmentCatalog.Armors){string id=item.Id;var b=JourneyStyle.Button(variant,(draft.Armor==id?"✓ ":"")+item.Name,()=>Change(draft with {Armor=id}),"Armor_"+id);b.TooltipText=item.Description;list.AddChild(b);list.AddChild(JourneyStyle.Text(variant,item.Description,16));}}
 void AddHelmet(VBoxContainer list)
 {list.AddChild(JourneyStyle.Text(variant,"Capacete",20));foreach(var item in EquipmentCatalog.Helmets){string id=item.Id;var b=JourneyStyle.Button(variant,(draft.Helmet==id?"✓ ":"")+item.Name,()=>Change(draft with {Helmet=id}),"Helmet_"+id);b.TooltipText=item.Description;list.AddChild(b);}}
 void AddColors(VBoxContainer list)
 {
  ColorRow(list,"Tecido",draft.ClothColor,c=>draft with {ClothColor=c},"ClothColor");ColorRow(list,"Metal",draft.MetalColor,c=>draft with {MetalColor=c},"MetalColor");ColorRow(list,"Detalhes",draft.AccentColor,c=>draft with {AccentColor=c},"AccentColor");
 }
 void ColorRow(VBoxContainer list,string label,string color,Func<string,EquipmentLoadout> mutate,string name)
 {
  list.AddChild(JourneyStyle.Text(variant,label,18));var row=new HBoxContainer();
  var picker=new ColorPickerButton {Color=Color.FromHtml(color),EditAlpha=false,CustomMinimumSize=new Vector2(64,44),SizeFlagsHorizontal=SizeFlags.ExpandFill,Name=name,TooltipText="Escolher cor de "+label};picker.ColorChanged+=c=>{draft=EquipmentCatalog.Normalize(mutate("#"+c.ToHtml(false)));if(summary!=null)summary.Text=(comparisonOnly ? "Bônus abaixo desativados no jogo.\n" : "")+Describe(draft);preview?.Apply(draft);PreviewChanged?.Invoke(draft);};row.AddChild(picker);list.AddChild(row);
 }
 void Change(EquipmentLoadout value) {draft=EquipmentCatalog.Normalize(value);preview?.Apply(draft);PreviewChanged?.Invoke(draft);Build();}
 void Arrange()
 {
  if(shell==null)return;float w=Size.X,h=Size.Y; if(w<=0||h<=0)return;
  float portrait=Math.Clamp(w*0.23f,145,320);
  float width=Math.Min(w-portrait-36,variant switch {"book"=>900,"adventure"=>430,"crystal"=>600,_=>560});
  float groupWidth=portrait+12+width;
  float left=variant=="adventure"?w-groupWidth-12:(w-groupWidth)/2;
  JourneyStyle.Place(shell,left+portrait+12,12,width,Math.Max(100,h-24));
  if(preview!=null)JourneyStyle.Place(preview,left,12,portrait,Math.Max(100,h-24));
 }
 public void ShowError(string message) {saveError=message;if(errorLabel!=null){errorLabel.Text=message;errorLabel.Visible=!string.IsNullOrWhiteSpace(message);}settleFrames=12;}
 public override void _UnhandledKeyInput(InputEvent @event)
 {if(@event is InputEventKey k && k.Pressed&&!k.Echo&&k.Keycode==Key.Escape){Cancelled?.Invoke();GetViewport().SetInputAsHandled();}}
 public Godot.Collections.Dictionary LayoutDiagnostics()=>new(){{"variant",variant},{"draft",Describe(draft)},{"panel",shell==null?new Rect2():new Rect2(shell.Position,shell.Size)}};
}
