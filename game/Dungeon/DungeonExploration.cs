using ArquipelagoDoCeu.Dungeon;
using Godot;

namespace ArquipelagoDoCeu;

// O que a Expedição guarda no save: o Andar descoberto e as Salas visitadas e vencidas (ADR 0004).
// O Atalho e a Livre não guardam nada, e falha de gravação não trava a criança.
public sealed class DungeonExploration
{
	readonly DungeonContext ctx;
	(int Floor, int Visited, int Cleared) lastSaved;

	public DungeonExploration(DungeonContext ctx) => this.ctx = ctx;

	bool Persists => !ctx.Shortcut && !ctx.Free;

	public FloorExploration? Get(int floor) =>
		!Persists ? null : ctx.Progress.Explored.Get(ctx.ProfileId, ctx.FloorKeyOf(floor), DungeonContext.GeneratorVersion);

	// Grava a cada passo: ver o mapa sumir ao zerar a Energia seria punição.
	public void Save(Expedition expedition)
	{
		if (!Persists) return;
		// Só grava quando algo mudou: voltar a uma Sala já visitada não precisa reescrever o save.
		var stamp = (expedition.Floor.Number, expedition.Visited.Count, expedition.Cleared.Count);
		if (stamp == lastSaved) return;
		var saved = ctx.Progress.Explored.Save(ctx.ProfileId, ctx.FloorKeyOf(expedition.Floor.Number), expedition.Exploration(), DungeonContext.GeneratorVersion);
		if (saved.IsOk) lastSaved = stamp;
		else GD.PushWarning(saved.Error!);
	}

	public void Discover(int floor)
	{
		if (ctx.Free) return;
		var saved = ctx.Progress.Levels.DiscoverFloor(ctx.ProfileId, ctx.FloorKeyOf(floor));
		if (!saved.IsOk) GD.PushWarning(saved.Error!);
	}

	public bool IsDone(int floor) => !ctx.Free && ctx.Progress.Explored.IsFloorDone(ctx.ProfileId, ctx.FloorKeyOf(floor));
}
