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

	void SetPullBlocked(bool blocked)
	{
		foreach (var gate in GetTree().Root.GetChildren().OfType<PullTestGate>()) gate.Blocked = blocked;
	}

	void DropTarget()
	{
		training.DropTarget();
		dungeon?.Combat.DropTarget();
	}

	void EnterDungeon(string discipline)
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
		var tower = discipline == TrainingSetup.Mathematics ? "Torre dos Números" : "Torre das Letras";
		var run = new DungeonRun(new DungeonContext(rules, current.Id, discipline, tower, UnlockedLevel(), voice, settings, uiRoot,
			progress, floors.Value!, equipmentBonuses.ExtraEnergy, ConfigureDungeonPlayer));
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
