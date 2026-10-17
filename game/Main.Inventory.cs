using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Equipment;
using ArquipelagoDoCeu.Saves;
using Godot;

namespace ArquipelagoDoCeu;

// O equipamento, os Cosméticos e o Inventário: o que o Personagem veste, os atributos que isso dá e o HUD do Lobby.
public partial class Main
{
	const float CompactLabelScale = .55f;

	EquipmentBonuses equipmentBonuses = new();
	GearCatalog? gearCatalog;
	CosmeticCatalog? cosmeticCatalog;
	GearService gear = null!;
	InventoryService inventory = null!;
	LobbyHud? lobbyHud;
	ConceptAmbience? conceptAmbience;
	readonly Dictionary<Label3D, float> originalLabelPixels = new();

	// O Papel de agora (a torre ou o treino mais recentes) decide qual equipamento vale no Lobby; cada Papel tem o seu.
	string role = "";

	// Sem o catálogo o jogo segue sem equipamento: ele é um extra, e a criança não fica sem jogar por isso.
	GearCatalog LoadGear() => gearCatalog ??= catalogs.Optional("equipamentos.json", json => GearCatalog.Parse(json, GearEffects.Default), GearCatalog.Empty);

	EquipmentLoadout ActiveEquipment
	{
		get
		{
			if (current is null) return new EquipmentLoadout();
			return EquipmentLooks.For(gear.Get(current.Id, role), inventory.WornLooks(current.Id));
		}
	}

	EquipmentBonuses BonusesOf(string papel) => current is null ? new() : gear.Bonuses(current.Id, papel);

	string RoleName(string papel) => towers.Find(papel)?.Role ?? papel;

	// O card do canto mostra a Maestria do Papel de agora, e o dock, o nome dele.
	void RefreshCard() => lobbyHud?.SetCard(current?.Nickname ?? "Viajante", RoleName(role), current is null ? 0 : progress.Mastery.Of(current.Id, role));

	// Sempre reaplica: o equipamento do Papel pode ter mudado desde a última vez.
	void ChooseRole(string papel)
	{
		role = papel;
		ApplyEquipment();
		RefreshCard();
		RefreshDock();
	}

	void BuildLobbyHud()
	{
		lobbyHud = new LobbyHud(text => voice.Say(text, interrupt: true));
		uiRoot.AddChild(lobbyHud);
		lobbyHud.ProfilesRequested += ShowProfiles;
		lobbyHud.EditRequested += ShowEditor;
		lobbyHud.SettingsRequested += ShowSettings;
		lobbyHud.InteractRequested += () => Interact(interactTarget);
		lobbyHud.InventoryRequested += ShowInventory;
		conceptAmbience = new ConceptAmbience(lobby, () => player.ReduceMotion, () => settings.Current.ReduceEffects);
		lobby.AddChild(conceptAmbience);
	}

	// O tema Livro vale para todas as telas: textos 3D menores para liberar a vista do cenário.
	void ApplyTheme()
	{
		training.RefreshPresentation();
		FitUiRoot();
		conceptAmbience?.SetVariant("book");
		lobby.SetCompactSigns(true);
		foreach (var node in GetTree().GetNodesInGroup(CameraController.LabelGroup))
		{
			if (node is not Label3D label) continue;
			if (!originalLabelPixels.ContainsKey(label)) originalLabelPixels[label] = label.PixelSize;
			label.PixelSize = originalLabelPixels[label] * CompactLabelScale;
		}
	}

	void LoadProfileLook()
	{
		if (current is null) return;
		ApplyEquipment();
		ApplyTheme();
		RefreshCard();
		RefreshDock();
		interactTarget = null;
	}

	void ApplyEquipment()
	{
		equipmentBonuses = BonusesOf(role);
		player.SpeedMultiplier = equipmentBonuses.SpeedMultiplier;
		player.ApplyEquipment(ActiveEquipment);
		training.SetEquipmentEnergy(equipmentBonuses.ExtraEnergy);
		if (dungeon is not null) ConfigureDungeonPlayer(dungeon.Player);
	}

	// Na Dungeon o HUD é o dela; sem Perfil só o título e o recado aparecem; no Lobby o HUD some enquanto há uma luta ou tela aberta.
	void RefreshLobbyVisibility()
	{
		var playing = player is not null && player.Enabled;
		lobbyHud?.SetPlayState(playing && !inDungeon);
		title.Visible = !inDungeon && current is null;
		notice.Visible = !inDungeon && current is null;
	}

	void RefreshDock()
	{
		var count = current is null ? 0 : NewInventoryKeys().Count();
		lobbyHud?.Dock.SetNewCount(count);
	}

	IEnumerable<string> NewInventoryKeys() =>
		current is null ? [] : InventoryHost.NewKeys(inventory, current.Id, LoadGear(), rewardService.Cosmetics(current.Id));

	// Quantos itens do Inventário a criança ainda não viu; a Dungeon mostra o mesmo selo no dock dela.
	int NewInventoryCount() => NewInventoryKeys().Count();

	void ShowInventory()
	{
		if (current is null || overlay is not null || screen is not null || editor is not null) return;
		if (inDungeon ? dungeon is null || dungeon.Combat.HasTarget : training.HasTarget) return;
		var screenToOpen = new InventoryScreen(BuildInventoryHost(canChangeGear: !inDungeon));
		screenToOpen.Closed += () =>
		{
			RefreshDock();
			dungeon?.RefreshDock();
		};
		ShowOverlay(screenToOpen);
	}

	InventoryHost BuildInventoryHost(bool canChangeGear) => new()
	{
		ProfileId = current!.Id,
		RoleId = role,
		RoleName = RoleName(role),
		Catalog = LoadGear(),
		Cosmetics = LoadCosmetics(),
		Gear = gear,
		Inventory = inventory,
		Mastery = () => progress.Mastery.Of(current.Id, role),
		OwnedCosmetics = () => rewardService.Cosmetics(current.Id),
		HeldItems = HeldItems,
		Say = text => voice.Say(text, interrupt: true),
		Changed = ApplyEquipment,
		CanChangeGear = canChangeGear,
	};

	// Os Itens de troca que a criança guarda: um por Nível de cada torre, só para ler.
	IReadOnlyList<HeldItem> HeldItems() =>
		towers.All
			.SelectMany(tower => Enumerable.Range(1, tower.Levels).Select(level => (tower, level)))
			.Where(pair => progress.Levels.HasItem(current!.Id, new LevelKey(pair.tower.Id, pair.level)))
			.Select(pair => new HeldItem($"Item de troca · {pair.tower.Name}", $"Nível {pair.level}"))
			.ToList();
}
