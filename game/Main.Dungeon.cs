using System.Linq;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Saves;
using Godot;

namespace ArquipelagoDoCeu;

// A Main liga a Dungeon ao Lobby: a criança entra pela porta da torre, o Lobby e a interface dele ficam de lado
// durante a Expedição, e ao sair tudo volta como estava.
public partial class Main
{
	const string FallbackFloorsPath = "res://data/andares-reserva.json";

	ProgressService progress = null!;
	FallbackFloors? fallbackFloors;
	DungeonRun? dungeon;
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
		var level = UnlockedLevel();
		if (progress.ShortcutTried(current.Id, discipline, level) || progress.EpicDone(current.Id, discipline, level))
		{
			EnterDungeon(discipline);
			return;
		}
		ShowOverlay(new ShortcutOfferScreen(TowerName(discipline), level, rules.Dungeon.ShortcutChallenges,
			() => EnterDungeon(discipline, shortcut: true), () => EnterDungeon(discipline)));
	}

	void EnterDungeon(string discipline, int? level = null, int startFloor = 0, bool atFastTravel = false, bool shortcut = false)
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
		var towerLevel = level ?? UnlockedLevel();
		var floor = startFloor > 0 ? startFloor : progress.HighestFloor(current.Id, discipline, towerLevel);
		var run = new DungeonRun(new DungeonContext(rules, current.Id, discipline, tower, towerLevel, voice, settings, uiRoot,
			progress, floors.Value!, equipmentBonuses.ExtraEnergy, ConfigureDungeonPlayer, floor, atFastTravel, ShowOverlay, shortcut));
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
		if (reason != "") Notify(reason);
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
