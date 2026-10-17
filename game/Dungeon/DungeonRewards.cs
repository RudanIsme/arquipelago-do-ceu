using System;
using System.Collections.Generic;
using System.Linq;
using ArquipelagoDoCeu.Dungeon;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;
using ArquipelagoDoCeu.Rewards;
using ArquipelagoDoCeu.Saves;
using Godot;

namespace ArquipelagoDoCeu;

// As moedas, os Cosméticos e a Maestria que a criança ganha numa Expedição, já em texto para a Sala dizer.
// Quem decide o que cada acontecimento paga são os dados; aqui só se pede, se conta uma vez e se avisa.
public sealed class DungeonRewards
{
	readonly DungeonContext ctx;
	readonly Func<bool> inShortcut;
	readonly Action<string> showCoins;
	// Na Expedição livre cada Andar paga as moedas dele uma vez só.
	readonly HashSet<int> paidFloors = [];
	string? masteryNote;

	public DungeonRewards(DungeonContext ctx, Func<bool> inShortcut, Action<string> showCoins)
	{
		this.ctx = ctx;
		this.inShortcut = inShortcut;
		this.showCoins = showCoins;
	}

	public static string? Join(params string?[] parts) => parts.Any(p => !string.IsNullOrEmpty(p)) ? string.Join(" ", parts.Where(p => !string.IsNullOrEmpty(p))) : null;

	// A vitória num Conceito conta para a Maestria do Papel (Dungeon, nunca treino); o aviso sai junto com o resto da Sala.
	public void RecordSolved(Mission mission)
	{
		// O Atalho pula o caminho: não rende moedas nem Maestria, para as duas recompensas seguirem a mesma regra.
		if (inShortcut()) return;
		var update = ctx.Progress.Mastery.Record(ctx.ProfileId, new ConceptKey(ctx.Discipline, mission.Concept), ctx.Rules.Dungeon.ConceptSolves);
		if (!update.IsOk)
		{
			GD.PushWarning(update.Error!);
			return;
		}
		if (!update.Value!.NewlyMastered) return;
		var name = (ctx.Concepts ?? ConceptNames.Empty).NameOf(mission.Concept);
		masteryNote = Join($"Você dominou \"{name}\"! Maestria {update.Value.Mastery}.", Reward("concept-mastered", concept: mission.Concept));
	}

	// O aviso de Maestria da Sala que acabou de ser vencida; sai uma vez só.
	public string? TakeMasteryNote()
	{
		var note = masteryNote;
		masteryNote = null;
		return note;
	}

	// As moedas de uma Sala vencida: a criatura acalmada e a Sala do Item.
	public string? ForRoom(Room room, int floor) =>
		Join(room.Kind is RoomKind.Combat or RoomKind.Epic ? Reward("creature-calmed") : null, room.HoldsItem ? Reward("item-room", floor) : null);

	// Registra o Andar completo uma única vez e diz o que a criança ganhou; falha de gravação não trava ninguém.
	public string? ForFloor(Expedition expedition)
	{
		var number = expedition.Floor.Number;
		if (ctx.Shortcut || !expedition.IsFloorComplete(number)) return null;
		// A Livre paga o Andar uma vez só por Expedição e não registra Andar completo nem Cosmético: isso é do Plano do Nível.
		if (ctx.Free && !paidFloors.Add(number)) return null;
		var rewards = Reward(ctx.Free ? "free-floor-complete" : "floor-complete", number);
		return rewards is null && !ctx.Free ? null : Join($"Andar {number} completo!", rewards);
	}

	// Pede as recompensas do gatilho e devolve o que a criança ganhou, em texto.
	// Devolve null quando o acontecimento já tinha sido pago; falha de gravação não trava a criança.
	string? Reward(string trigger, int? floor = null, string? concept = null)
	{
		var granted = ctx.Granter.Grant(ctx.ProfileId, new RewardContext(trigger, ctx.Discipline, ctx.Level, floor, concept), ctx.Rewards);
		if (!granted.IsOk)
		{
			GD.PushWarning(granted.Error!);
			return null;
		}
		var outcome = granted.Value!;
		if (!outcome.Fired) return null;
		if (outcome.Coins > 0) showCoins($"+{Coins(outcome.Coins)}");
		var prizes = outcome.Cosmetics.Select(c => $"Você ganhou um Cosmético: {c.Name}.");
		return string.Join(" ", prizes.Append(outcome.Coins > 0 ? $"Você ganhou {Coins(outcome.Coins)}." : "").Where(t => t.Length > 0));
	}

	static string Coins(int amount) => amount == 1 ? "1 moeda" : $"{amount} moedas";
}
