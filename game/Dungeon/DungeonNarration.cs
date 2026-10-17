using System;
using System.Globalization;
using System.Linq;
using ArquipelagoDoCeu.Dungeon;
using ArquipelagoDoCeu.Generation;

namespace ArquipelagoDoCeu;

// O que a Dungeon diz da Expedição, na tela e por voz: Salas, portas, mapa e progresso do Andar.
// Só monta texto a partir do estado da Expedition; nada aqui muda regra.
public sealed class DungeonNarration
{
	readonly DungeonContext ctx;
	readonly Expedition expedition;

	public DungeonNarration(DungeonContext ctx, Expedition expedition)
	{
		this.ctx = ctx;
		this.expedition = expedition;
	}

	string OtherName => ctx.TowerNameOf(ctx.OtherTower);

	// A fala de quem entra na Sala; o Fast travel tem fala própria, porque achá-lo salva o ponto.
	public string? RoomLine(RoomType kind) =>
		(kind.LineOpen is { } open && ctx.HasTradeItem() ? open : kind.Line)?.Replace("{other}", OtherName, StringComparison.Ordinal);

	// O que a criança precisa saber desta Sala sobre o Item de troca, em texto e com a estrela.
	public string? RoomNote()
	{
		var current = expedition.Current;
		if (current.Kind.OpensWithItem)
			return ctx.HasTradeItem() ? $"★ O Item da {OtherName} abriu o Marco" : $"★ Falta o Item de troca da {OtherName}";
		return current.HoldsItem && !expedition.IsCleared(expedition.CurrentRoom) ? "★ Vença esta Sala para ganhar o Item de troca" : null;
	}

	// A porta de uma Sala que a criança ainda não visitou não diz o que há lá dentro.
	public (string Label, bool Locked, bool Known) Describe(int room)
	{
		var known = expedition.Visited.Contains(room);
		var name = known ? expedition.Floor.Rooms[room].Kind.Name : "Sala nova";
		return expedition.CanMoveTo(room) switch
		{
			Passage.MustClearRoom => ($"{name}\n(vença esta Sala)", true, known),
			Passage.NeedsItem => ($"{name}\n(precisa do Item)", true, known),
			_ => (name, false, known),
		};
	}

	public string PromptFor(string id)
	{
		if (id == "rest") return "Descansar [E]";
		if (id == "stairs") return $"Subir para o Andar {expedition.Floor.Number + 1} [E]";
		if (id == "stairs-down") return $"Descer para o Andar {expedition.Floor.Number - 1} [E]";
		var room = int.Parse(id["door:".Length..], CultureInfo.InvariantCulture);
		return expedition.Visited.Contains(room) ? $"Ir para {expedition.Floor.Rooms[room].Kind.Name.ToLowerInvariant()} [E]" : "Ir para a sala nova [E]";
	}

	public string ProgressSpeech()
	{
		var progress = expedition.ProgressOf(expedition.Floor.Number);
		return $"{progress.Resolved} de {progress.Total} Salas resolvidas.";
	}

	public string OfTotal() => ctx.Free ? "" : $" de {expedition.Plan.Floors.Count}";

	// O que a voz diz de uma Sala do mapa: o que é (se já visitada), se está vencida, se tem cadeado. Sala sob névoa não revela o tipo.
	public string RoomSpeech(int room)
	{
		var kind = expedition.Floor.Rooms[room];
		var where = expedition.CurrentRoom == room ? " Você está aqui." : "";
		return expedition.SightOf(room) switch
		{
			Sight.Seen => $"Sala de {kind.Kind.Name.ToLowerInvariant()}{(expedition.IsCleared(room) ? ", vencida" : "")}.{where}",
			Sight.Fog => expedition.IsLocked(room) ? "Sala nova sob névoa, com cadeado: falta vencer uma Sala." : "Sala nova sob névoa.",
			_ => "Ainda não se vê nada aqui.",
		};
	}

	// O mapa dito em voz alta, para quem não lê a névoa e o cadeado.
	public string MapSpeech(bool floorDone)
	{
		var rooms = expedition.Floor.Rooms.Select(r => r.Id).ToList();
		var fog = rooms.Count(r => expedition.SightOf(r) == Sight.Fog);
		var locked = rooms.Count(expedition.IsLocked);
		var done = floorDone ? " Este Andar está completo." : "";
		var item = ctx.Free ? "" : $" Item de troca da {OtherName}: {(ctx.HasTradeItem() ? "você tem" : "ainda não")}.";
		return $"Andar {expedition.Floor.Number}{OfTotal()}. {ProgressSpeech()} Salas novas sob névoa: {fog}. Salas com cadeado: {locked}.{item}{done}";
	}
}
