using Godot;
using ArquipelagoDoCeu.Equipment;

namespace ArquipelagoDoCeu;

public partial class Main
{
 JourneyService journeys = null!;
 JourneyPreferences journey = new();
 EquipmentBonuses equipmentBonuses = new();
 bool experimentalEquipment;
 EquipmentLoadout ActiveEquipment => experimentalEquipment ? EquipmentCatalog.Normalize(journey.Equipment) : new();
 JourneyHud? journeyHud;
 EquipmentWorkshop? workshop;
 Control? legacyHud;
 Button? returnJourney;
 ConceptAmbience? conceptAmbience;
 readonly System.Collections.Generic.Dictionary<Label3D,float> originalLabelPixels = new();

 void BuildJourneyHud()
 {
  journeyHud = new JourneyHud();
  uiRoot.AddChild(journeyHud);
  journeyHud.VariantRequested += ChangeJourneyVariant;
  journeyHud.RestoreBaseRequested += () => ChangeJourneyVariant("base");
  journeyHud.EquipmentRequested += ShowEquipment;

  journeyHud.ProfilesRequested += ShowProfiles;
  journeyHud.SettingsRequested += ShowSettings;
  journeyHud.InteractRequested += () => Interact(interactTarget);
  returnJourney = new Button { Text="Voltar ao Livro", Position=new Vector2(12,130), CustomMinimumSize=new Vector2(180,44), Visible=false };
  returnJourney.Pressed += () => ChangeJourneyVariant("book");
  uiRoot.AddChild(returnJourney);
  conceptAmbience = new ConceptAmbience(lobby, () => player.ReduceMotion, () => settings.Current.ReduceEffects);
  lobby.AddChild(conceptAmbience);
 }

 void LoadJourney()
 {
  if(current is null)return;
  // Keep saved experiments intact; the approved presentation starts fresh each session.
  journey = journeys.Get(current.Id) with { Variant = "book" };
  experimentalEquipment = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--experimental-equipment") >= 0;
  ApplyJourneyEquipment();
  ConfigureJourney();
 }

 void ConfigureJourney()
 {
  UiKit.SetVariant(journey.Variant);
  training.RefreshPresentation();
  FitUiRoot();
  journeyHud?.Configure(journey.Variant,current?.Nickname??"Viajante",EquipmentCatalog.Summary(ActiveEquipment));
  conceptAmbience?.SetVariant(journey.Variant);
  lobby.SetCompactSigns(journey.Variant != "base");
  foreach(var node in GetTree().GetNodesInGroup(CameraController.LabelGroup))
   if(node is Label3D label)
   {
    if(!originalLabelPixels.ContainsKey(label))originalLabelPixels[label]=label.PixelSize;
    label.PixelSize=originalLabelPixels[label]*(journey.Variant=="base"?1f:.55f);
   }
  RefreshJourneyVisibility();
  interactTarget=null;
 }

 void ChangeJourneyVariant(string variant)
 {
  if(current is null || workshop is not null)return;
  var next=JourneyPreferences.Normalize(journey with { Variant=variant });
  var saved=journeys.Set(current.Id,next);
  if(!saved.IsOk){Notify(saved.Error!);return;}
  journey=next;current=saved.Value!;
  DropTarget();
  // Interface choices never replace equipment or touch current Energy.
  ConfigureJourney();
 }

 void ApplyJourneyEquipment()
 {
  var loadout=ActiveEquipment;
  equipmentBonuses=EquipmentCatalog.Bonuses(loadout);
  player.SpeedMultiplier=equipmentBonuses.SpeedMultiplier;
  player.ApplyEquipment(loadout);
  training.SetEquipmentEnergy(equipmentBonuses.ExtraEnergy);
 }

 void ShowEquipment()
 {
  if(current is null || workshop is not null || overlay is not null || screen is not null || editor is not null)return;
  DropTarget();
  if(overlay is not null)return;
  workshop=new EquipmentWorkshop(journey.Variant,EquipmentCatalog.Normalize(journey.Equipment), !experimentalEquipment);
  if(experimentalEquipment) workshop.PreviewChanged += value => player.ApplyEquipment(value);
  workshop.Cancelled += CloseEquipment;
  workshop.Applied += value => {
   var next=journey with { Equipment=EquipmentCatalog.Normalize(value) };
   var saved=journeys.Set(current.Id,next);
   if(!saved.IsOk)
   {
    const string message = "Não foi possível salvar o equipamento. A prévia foi mantida. Tente aplicar novamente ou cancele.";
    workshop?.ShowError(message);
    voice.Say(message, interrupt:true);
    GD.PushWarning(saved.Error!);
    return;
   }
   journey=next;current=saved.Value!;
   ApplyJourneyEquipment();
   CloseEquipment();
   ConfigureJourney();
   Notify(experimentalEquipment ? "Equipamento salvo." : "Comparação salva. Traje original mantido.");
  };
  uiRoot.AddChild(workshop);
  UpdatePlayerControl();
 }

 void CloseEquipment()
 {
  player.ApplyEquipment(ActiveEquipment);
  workshop?.QueueFree();workshop=null;
  UpdatePlayerControl();
 }

 void RefreshJourneyVisibility()
 {
  bool playing=player is not null && player.Enabled;
  bool legacy=journey.Variant=="base";
  if(journeyHud is not null)journeyHud.Visible=!inDungeon;
  if(inDungeon)
  {
   if(legacyHud is not null)legacyHud.Visible=false;
   if(bottomBar is not null)bottomBar.Visible=false;
   if(returnJourney is not null)returnJourney.Visible=false;
   title.Visible=false;
   notice.Visible=false;
   return;
  }
  if(legacyHud is not null)legacyHud.Visible=legacy && workshop is null;
  if(bottomBar is not null)bottomBar.Visible=playing && legacy;
  title.Visible=legacy || current is null;
  notice.Visible=legacy || current is null;
  journeyHud?.SetPlayState(playing && !legacy);
  if(returnJourney is not null)returnJourney.Visible=playing && legacy;
 }
}
