using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Dungeon;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using ArquipelagoDoCeu.Rewards;
using ArquipelagoDoCeu.Saves;
using Godot;

namespace ArquipelagoDoCeu;

// A Main liga a Dungeon ao Lobby: a criança entra pela porta da torre, o Lobby e a interface dele ficam de lado
// durante a Expedição, e ao sair tudo volta como estava.
public partial class Main
{

	ProgressServices progress = null!;
	RewardService rewardService = null!;
	TowerCatalog towers = TowerCatalog.Empty;
	DungeonRun? dungeon;
	(string Profile, int Coins)? coinsAtStart;
	bool inDungeon;

	string TowerName(string discipline) => towers.Find(discipline)?.Name ?? discipline;

	// O portal do Lobby: um destino por Fast travel encontrado.
	PortalScreen BuildPortal()
	{
		var points = current is null ? [] : progress.Levels.FastTravels(current.Id);
		return new PortalScreen(points.Select(p => ($"{TowerName(p.Discipline)} · Nível {p.Level} · Andar {p.Floor}",
			(System.Action)(() => EnterDungeon(p.Discipline, p.Level, p.Floor, atFastTravel: true)))).ToList());
	}

	void SetPullBlocked(bool blocked)
	{
		foreach (var gate in GetTree().Root.GetChildren().OfType<PullTestGate>()) gate.Blocked = blocked;
	}

	MasterChallenge master = null!;

	void ShowMaster()
	{
		if (current is null) return;
		var level = UnlockedLevel();
		ShowOverlay(new MasterScreen(level, towers.All.Select(t => (t.Name, progress.Levels.EpicDone(current.Id, new LevelKey(t.Id, level)))).ToList(),
			() =>
			{
				if (master.Start(current.Id, level) is { } problem) Notify(problem);
			}));
	}


	void DropTarget()
	{
		training.DropTarget();
		dungeon?.Combat.DropTarget();
	}

	// Pela porta da torre a criança começa no Andar mais fundo que já descobriu; pelo portal, no Fast travel que escolheu.
	// Pela porta da torre, com o Atalho de Nível ainda não tentado, a criança escolhe antes de entrar.
	void EnterTower(string discipline)
	{
		if (current is null || inDungeon) return;
		ShowOverlay(new ExpeditionModeScreen(TowerName(discipline), () => EnterProgression(discipline), () => EnterDungeon(discipline, free: true)));
	}

	void EnterProgression(string discipline)
	{
		if (current is null || inDungeon) return;
		var level = UnlockedLevel();
		if (progress.Levels.ShortcutTried(current.Id, new LevelKey(discipline, level)) || progress.Levels.EpicDone(current.Id, new LevelKey(discipline, level)))
		{
			EnterDungeon(discipline);
			return;
		}
		ShowOverlay(new ShortcutOfferScreen(TowerName(discipline), level, rules.Dungeon.ShortcutChallenges,
			() => EnterDungeon(discipline, shortcut: true), () => EnterDungeon(discipline)));
	}

	void EnterDungeon(string discipline, int? level = null, int startFloor = 0, bool atFastTravel = false, bool shortcut = false, bool free = false)
	{
		if (current is null || inDungeon) return;
		var floors = LoadFallbackFloors();
		if (!floors.IsOk)
		{
			Notify("Não foi possível abrir a torre agora.");
			GD.PushError(floors.Error!);
			return;
		}

		training.End();
		var tower = TowerName(discipline);
		ChooseRole(discipline);
		var towerLevel = TowerCatalog.LevelFor(towers.Find(discipline)!, level ?? UnlockedLevel());
		// Pela porta da torre a criança recomeça no último Fast travel que achou, em qualquer Andar; sem nenhum, na entrada do
		// Andar mais fundo que visitou. Como a escada vale nos dois sentidos, ela volta para trás quando quiser.
		if (startFloor == 0 && !shortcut && !free && progress.Levels.LastFastTravel(current.Id, new LevelKey(discipline, towerLevel)) is { } last)
		{
			startFloor = last.Floor;
			atFastTravel = true;
		}
		var floor = startFloor > 0 ? startFloor : progress.Levels.HighestFloor(current.Id, new LevelKey(discipline, towerLevel));
		coinsAtStart = (current.Id, progress.Wallet.Balance(current.Id));
		var run = new DungeonRun(new DungeonContext(rules, current.Id, discipline, tower, towerLevel, voice, settings, uiRoot,
			progress, floors.Value!, LoadRewards(), rewardService, current.Nickname, RoleName(discipline), towers, equipmentBonuses.ExtraEnergy, equipmentBonuses.InteractionReachBonus, ConfigureDungeonPlayer, random, floor, atFastTravel, ShowOverlay, shortcut, free, LoadConcepts(), ShowInventory, NewInventoryCount));
		run.Left += LeaveDungeon;
		dungeon = run;
		inDungeon = true;

		SetPullBlocked(true);
		lobby.Visible = false;
		lobby.ProcessMode = ProcessModeEnum.Disabled;
		player.Visible = false;
		player.Enabled = false;
		AddChild(run);
		UpdatePlayerControl();
		GD.Print($"Dungeon aberta: {tower}");
	}

	void LeaveDungeon(string reason)
	{
		inDungeon = false;
		dungeon = null;
		SetPullBlocked(false);
		lobby.ProcessMode = ProcessModeEnum.Inherit;
		lobby.Visible = true;
		lobby.Camera.MakeCurrent();
		player.Visible = true;
		UpdatePlayerControl();
		interactTarget = null;
		RefreshCard();
		if (reason.Length != 0) Notify(reason);
		// O que a criança ganhou na Expedição aparece ao voltar, com o número subindo até o saldo novo.
		// Só vale a Expedição que acabou e do mesmo Perfil: nunca mostra moedas que a criança não ganhou ali.
		if (coinsAtStart is { } start && current is not null && start.Profile == current.Id && progress.Wallet.Balance(current.Id) is var coinsNow && coinsNow > start.Coins)
			ShowOverlay(new CoinsArrivalScreen(start.Coins, coinsNow, player.ReduceMotion, text => voice.Say(text, interrupt: true)));
		coinsAtStart = null;
		GD.Print("De volta ao Lobby.");
	}

	// O Personagem da Dungeon é outro nó, mas é a mesma criança: mesma aparência, equipamento e opções.
	void ConfigureDungeonPlayer(Player dungeonPlayer)
	{
		dungeonPlayer.Apply(profiles.AppearanceOf(profiles.Find(current!.Id).Value!));
		dungeonPlayer.ApplyEquipment(ActiveEquipment);
		dungeonPlayer.SpeedMultiplier = equipmentBonuses.SpeedMultiplier;
		dungeonPlayer.ReduceMotion = player.ReduceMotion;
	}

	// Sem o catálogo o jogo segue, só sem Cosméticos: eles são um extra, e a criança não pode ficar sem a torre por isso.
	CosmeticCatalog LoadCosmetics() => cosmeticCatalog ??= catalogs.Optional("cosmeticos.json", CosmeticCatalog.Parse, CosmeticCatalog.Empty);

	// Sem as recompensas o jogo segue, só sem moedas nem Cosméticos: elas são um extra.
	RewardCatalog LoadRewards() => catalogs.Optional("recompensas.json", json => RewardCatalog.Parse(json, RewardTriggers.Default, LoadCosmetics()), RewardCatalog.Empty);

	// Sem os nomes dos Conceitos a placa mostra o id em texto legível: o jogo segue.
	ConceptNames LoadConcepts() => catalogs.Optional("conceitos.json", ConceptNames.Parse, ConceptNames.Empty);

	// Os Cosméticos que o Perfil já ganhou, com nome e descrição, para o Álbum.
	IReadOnlyList<Cosmetic> OwnedCosmetics()
	{
		var catalog = LoadCosmetics();
		return current is null ? [] : rewardService.Cosmetics(current.Id).Select(catalog.Find).OfType<Cosmetic>().ToList();
	}

	// Sem os Andares de reserva a torre não abre: o validador precisa deles como último recurso.
	Result<FallbackFloors> LoadFallbackFloors() => catalogs.Required("andares-reserva.json", FallbackFloors.Parse);
}
