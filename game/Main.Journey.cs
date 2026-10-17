using Godot;
using ArquipelagoDoCeu.Equipment;

namespace ArquipelagoDoCeu;

public partial class Main
{
	JourneyService journeys = null!;
	JourneyPreferences journey = new();
	EquipmentBonuses equipmentBonuses = new();
	GearService gear = null!;
	// O Papel de agora (a torre ou o treino mais recentes) decide qual equipamento vale no Lobby; cada Papel tem o seu.
	string role = ArquipelagoDoCeu.Combat.TrainingSetup.Mathematics;

	// O que o Personagem veste: os itens do Papel de agora e as cores escolhidas na oficina.
	EquipmentLoadout ActiveEquipment
	{
		get
		{
			var worn = current is null ? new Gear() : gear.Get(current.Id, role);
			var colors = EquipmentCatalog.Normalize(journey.Equipment);
			return EquipmentCatalog.Normalize(colors with { Armor = worn.Armor ?? "original", Helmet = worn.Helmet ?? "none" });
		}
	}

	// Sem o catálogo o jogo segue sem equipamento: ele é um extra, e a criança não fica sem jogar por isso.
	GearCatalog LoadGear() => catalogs.Optional("equipamentos.json", GearCatalog.Parse, GearCatalog.Empty);

	EquipmentBonuses BonusesOf(string papel) => current is null ? new() : gear.Bonuses(current.Id, papel);

	string RoleName(string papel) => papel == ArquipelagoDoCeu.Combat.TrainingSetup.Mathematics ? rules.Roles.Mathematics : rules.Roles.Portuguese;

	// O card do canto mostra a Maestria do Papel de agora.
	void RefreshCard() => journeyHud?.SetCard(current?.Nickname ?? "Viajante", RoleName(role), current is null ? 0 : progress.Mastery(current.Id, role));

	// Sempre reaplica: o equipamento do Papel pode ter mudado desde a última vez.
	void ChooseRole(string papel)
	{
		role = papel;
		ApplyJourneyEquipment();
		RefreshCard();
	}
	JourneyHud? journeyHud;
	EquipmentWorkshop? workshop;
	Control? legacyHud;
	Button? returnJourney;
	ConceptAmbience? conceptAmbience;
	readonly System.Collections.Generic.Dictionary<Label3D, float> originalLabelPixels = new();

	void BuildJourneyHud()
	{
		journeyHud = new JourneyHud();
		uiRoot.AddChild(journeyHud);
		journeyHud.VariantRequested += ChangeJourneyVariant;
		journeyHud.RestoreBaseRequested += () => ChangeJourneyVariant("base");
		journeyHud.EquipmentRequested += ShowEquipment;

		journeyHud.ProfilesRequested += ShowProfiles;
		journeyHud.EditRequested += ShowEditor;
		journeyHud.Speak = text => voice.Say(text, interrupt: true);
		journeyHud.SettingsRequested += ShowSettings;
		journeyHud.InteractRequested += () => Interact(interactTarget);
		returnJourney = new Button { Text = "Voltar ao Livro", Position = new Vector2(12, 130), CustomMinimumSize = new Vector2(180, 44), Visible = false };
		returnJourney.Pressed += () => ChangeJourneyVariant("book");
		uiRoot.AddChild(returnJourney);
		conceptAmbience = new ConceptAmbience(lobby, () => player.ReduceMotion, () => settings.Current.ReduceEffects);
		lobby.AddChild(conceptAmbience);
	}

	void LoadJourney()
	{
		if (current is null) return;
		// Keep saved experiments intact; the approved presentation starts fresh each session.
		journey = journeys.Get(current.Id) with { Variant = "book" };
		ApplyJourneyEquipment();
		ConfigureJourney();
	}

	void ConfigureJourney()
	{
		UiKit.SetVariant(journey.Variant);
		training.RefreshPresentation();
		FitUiRoot();
		journeyHud?.Configure(journey.Variant, current?.Nickname ?? "Viajante", EquipmentCatalog.Summary(ActiveEquipment));
		RefreshCard();
		conceptAmbience?.SetVariant(journey.Variant);
		lobby.SetCompactSigns(journey.Variant != "base");
		foreach (var node in GetTree().GetNodesInGroup(CameraController.LabelGroup))
			if (node is Label3D label)
			{
				if (!originalLabelPixels.ContainsKey(label)) originalLabelPixels[label] = label.PixelSize;
				label.PixelSize = originalLabelPixels[label] * (journey.Variant == "base" ? 1f : .55f);
			}
		RefreshJourneyVisibility();
		interactTarget = null;
	}

	void ChangeJourneyVariant(string variant)
	{
		if (current is null || workshop is not null) return;
		var next = JourneyPreferences.Normalize(journey with { Variant = variant });
		var saved = journeys.Set(current.Id, next);
		if (!saved.IsOk) { Notify(saved.Error!); return; }
		journey = next; current = saved.Value!;
		DropTarget();
		// Interface choices never replace equipment or touch current Energy.
		ConfigureJourney();
	}

	void ApplyJourneyEquipment()
	{
		var loadout = ActiveEquipment;
		equipmentBonuses = BonusesOf(role);
		player.SpeedMultiplier = equipmentBonuses.SpeedMultiplier;
		player.ApplyEquipment(loadout);
		training.SetEquipmentEnergy(equipmentBonuses.ExtraEnergy);
	}

	void ShowEquipment()
	{
		if (current is null || workshop is not null || overlay is not null || screen is not null || editor is not null) return;
		DropTarget();
		if (overlay is not null) return;
		workshop = new EquipmentWorkshop(journey.Variant, ActiveEquipment, false);
		workshop.PreviewChanged += value => player.ApplyEquipment(value);
		workshop.Summarize = loadout => LoadGear().Summary(new Gear(Helmet: loadout.Helmet == "none" ? null : loadout.Helmet, Armor: loadout.Armor == "original" ? null : loadout.Armor, Accessory: gear.Get(current.Id, role).Accessory));
		workshop.Cancelled += CloseEquipment;
		workshop.Applied += value =>
		{
			// A Maestria do Papel decide o que se veste: quem não tem a Maestria vê o motivo e nada é salvo.
			var blocked = TryWear(value);
			if (blocked is not null)
			{
				workshop?.ShowError(blocked);
				voice.Say(blocked, interrupt: true);
				return;
			}
			var next = journey with { Equipment = EquipmentCatalog.Normalize(value) };
			var saved = journeys.Set(current.Id, next);
			if (!saved.IsOk)
			{
				const string message = "Não foi possível salvar o equipamento. A prévia foi mantida. Tente aplicar novamente ou cancele.";
				workshop?.ShowError(message);
				voice.Say(message, interrupt: true);
				GD.PushWarning(saved.Error!);
				// O equipamento já pode ter sido gravado: o Personagem na tela volta a mostrar o que está salvo.
				ApplyJourneyEquipment();
				return;
			}
			journey = next; current = saved.Value!;
			ApplyJourneyEquipment();
			CloseEquipment();
			ConfigureJourney();
			Notify("Equipamento salvo.");
		};
		uiRoot.AddChild(workshop);
		UpdatePlayerControl();
	}

	// Veste a armadura e o capacete escolhidos no Papel de agora; devolve o motivo se algum não puder, sem gravar nada.
	string? TryWear(EquipmentLoadout value)
	{
		var mastery = progress.Mastery(current!.Id, role);
		var now = gear.Get(current.Id, role);
		var armor = value.Armor == "original" ? Result<Gear>.Ok(LoadGear().Unequip(now, GearSlot.Armor)) : LoadGear().Equip(now, value.Armor, mastery);
		if (!armor.IsOk) return armor.Error;
		var helmet = value.Helmet == "none" ? Result<Gear>.Ok(LoadGear().Unequip(armor.Value!, GearSlot.Helmet)) : LoadGear().Equip(armor.Value!, value.Helmet, mastery);
		if (!helmet.IsOk) return helmet.Error;
		var saved = gear.Set(current.Id, role, helmet.Value!);
		return saved.IsOk ? null : "Não foi possível salvar o equipamento. Tente aplicar novamente.";
	}

	void CloseEquipment()
	{
		player.ApplyEquipment(ActiveEquipment);
		workshop?.QueueFree(); workshop = null;
		UpdatePlayerControl();
	}

	void RefreshJourneyVisibility()
	{
		bool playing = player is not null && player.Enabled;
		bool legacy = journey.Variant == "base";
		if (journeyHud is not null) journeyHud.Visible = !inDungeon;
		if (inDungeon)
		{
			if (legacyHud is not null) legacyHud.Visible = false;
			if (bottomBar is not null) bottomBar.Visible = false;
			if (returnJourney is not null) returnJourney.Visible = false;
			title.Visible = false;
			notice.Visible = false;
			return;
		}
		if (legacyHud is not null) legacyHud.Visible = legacy && workshop is null;
		if (bottomBar is not null) bottomBar.Visible = playing && legacy;
		title.Visible = legacy || current is null;
		notice.Visible = legacy || current is null;
		journeyHud?.SetPlayState(playing && !legacy);
		if (returnJourney is not null) returnJourney.Visible = playing && legacy;
	}
}
