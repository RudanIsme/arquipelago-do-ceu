using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using ArquipelagoDoCeu.Saves;
using Godot;

namespace ArquipelagoDoCeu;

// A Main liga a Dungeon ao Lobby: a criança entra pela porta da torre, o Lobby e a interface dele ficam de lado
// durante a Expedição, e ao sair tudo volta como estava.
public partial class Main
{
	const string FallbackFloorsPath = "res://data/andares-reserva.json";
	const string CosmeticsPath = "res://data/cosmeticos.json";
	const string ConceptsPath = "res://data/conceitos.json";

	ProgressService progress = null!;
	FallbackFloors? fallbackFloors;
	CosmeticCatalog? cosmetics;
	ConceptNames? concepts;
	DungeonRun? dungeon;
	(string Profile, int Coins)? coinsAtStart;
	bool inDungeon;

	static string TowerName(string discipline) => DungeonRun.TowerName(discipline);

	// O portal do Lobby: um destino por Fast travel encontrado.
	PortalScreen BuildPortal()
	{
		var points = current is null ? [] : progress.FastTravels(current.Id);
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
		ShowOverlay(new MasterScreen(level, progress.EpicDone(current.Id, TrainingSetup.Mathematics, level), progress.EpicDone(current.Id, TrainingSetup.Portuguese, level),
			() =>
			{
				if (master.Start(current.Id, level) is { } problem) Notify(problem);
			}));
	}

	// Ganchos só do piloto de desenvolvimento.
	public ProgressService DevProgress => progress;
	public string DevProfileId => current?.Id ?? "";
	public int DevLevel => UnlockedLevel();
	public MasterChallenge DevMaster => master;
	public ArquipelagoDoCeu.Equipment.GearService DevGear => gear;

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
		if (progress.ShortcutTried(current.Id, discipline, level) || progress.EpicDone(current.Id, discipline, level))
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
		var towerLevel = level ?? UnlockedLevel();
		// Pela porta da torre a criança recomeça no último Fast travel que achou, em qualquer Andar; sem nenhum, na entrada do
		// Andar mais fundo que visitou. Como a escada vale nos dois sentidos, ela volta para trás quando quiser.
		if (startFloor == 0 && !shortcut && !free && progress.LastFastTravel(current.Id, discipline, towerLevel) is { } last)
		{
			startFloor = last.Floor;
			atFastTravel = true;
		}
		var floor = startFloor > 0 ? startFloor : progress.HighestFloor(current.Id, discipline, towerLevel);
		coinsAtStart = (current.Id, progress.Coins(current.Id));
		var run = new DungeonRun(new DungeonContext(rules, current.Id, discipline, tower, towerLevel, voice, settings, uiRoot,
			progress, floors.Value!, LoadCosmetics(), current.Nickname, RoleName(discipline), equipmentBonuses.ExtraEnergy, equipmentBonuses.InteractionReachBonus, ConfigureDungeonPlayer, floor, atFastTravel, ShowOverlay, shortcut, free, LoadConcepts()));
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
		if (coinsAtStart is { } start && current is not null && start.Profile == current.Id && progress.Coins(current.Id) is var coinsNow && coinsNow > start.Coins)
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
	CosmeticCatalog LoadCosmetics()
	{
		if (cosmetics is not null) return cosmetics;
		using var file = FileAccess.Open(CosmeticsPath, FileAccess.ModeFlags.Read);
		var parsed = file is null ? Result<CosmeticCatalog>.Fail($"Não foi possível abrir {CosmeticsPath}: {FileAccess.GetOpenError()}") : CosmeticCatalog.Parse(file.GetAsText());
		if (!parsed.IsOk) GD.PushWarning(parsed.Error!);
		return cosmetics = parsed.IsOk ? parsed.Value! : CosmeticCatalog.Empty;
	}

	// Sem os nomes dos Conceitos a placa mostra o id em texto legível: o jogo segue.
	ConceptNames LoadConcepts()
	{
		if (concepts is not null) return concepts;
		using var file = FileAccess.Open(ConceptsPath, FileAccess.ModeFlags.Read);
		var parsed = file is null ? Result<ConceptNames>.Fail($"Não foi possível abrir {ConceptsPath}: {FileAccess.GetOpenError()}") : ConceptNames.Parse(file.GetAsText());
		if (!parsed.IsOk) GD.PushWarning(parsed.Error!);
		return concepts = parsed.IsOk ? parsed.Value! : ConceptNames.Empty;
	}

	// Os Cosméticos que o Perfil já ganhou, com nome e descrição, para o Álbum.
	IReadOnlyList<Cosmetic> OwnedCosmetics()
	{
		var catalog = LoadCosmetics();
		return current is null ? [] : progress.Cosmetics(current.Id).Select(catalog.Find).OfType<Cosmetic>().ToList();
	}

	Result<FallbackFloors> LoadFallbackFloors()
	{
		if (fallbackFloors is not null) return Result<FallbackFloors>.Ok(fallbackFloors);
		using var file = FileAccess.Open(FallbackFloorsPath, FileAccess.ModeFlags.Read);
		if (file is null) return Result<FallbackFloors>.Fail($"Não foi possível abrir {FallbackFloorsPath}: {FileAccess.GetOpenError()}");
		var parsed = FallbackFloors.Parse(file.GetAsText());
		if (parsed.IsOk) fallbackFloors = parsed.Value;
		return parsed;
	}
}
