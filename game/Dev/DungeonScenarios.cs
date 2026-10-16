using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArquipelagoDoCeu.Generation;
using Godot;
using Vector2 = System.Numerics.Vector2;

namespace ArquipelagoDoCeu;

// Cenários do piloto para as torres. Eles percorrem a Expedição como uma criança faria (anda até a porta, entra,
// escolhe a criatura, segue até a escada); a luta em si é encerrada pelo gancho de depuração, e o combate real tem
// os próprios testes e cenários. Use `--seed-do-mundo=N` para repetir sempre o mesmo Plano.
static class DungeonScenarios
{
	public static async Task Walkthrough(DevDriver d)
	{
		var run = await Enter(d);
		if (run is null) return;
		await d.Shot("01-sala-de-entrada");
		d.Expect(run.Expedition.Current.Kind == RoomKind.Entrance && run.Expedition.Floor.Number == 1, "a Expedição começa na entrada do Andar 1");
		d.Expect(run.Expedition.Energy.Current == run.Expedition.Energy.Max, "a Energia da Expedição começa cheia");

		var shotFight = false;
		while (!run.Expedition.Finished)
		{
			var expedition = run.Expedition;
			if (expedition.CanAscend)
			{
				await Ascend(d, run);
				continue;
			}

			var next = PathTo(expedition.Floor, expedition.CurrentRoom, r => r.Id == expedition.Floor.Exit)[1];
			if (NeedsFight(expedition) && !shotFight)
			{
				shotFight = true;
				run.Player.PlaceAt(new Vector2(0f, 1.5f));
				await d.Wait(0.3);
				await d.Tap(Controls.Target);
				d.Expect(run.Combat.HasTarget, "escolher a criatura abriu a montagem da resposta");
				await d.Shot("02-luta-na-sala");
				await d.PressKey(Key.Escape);
			}

			if (!await Step(d, run, next))
			{
				// Só o Marco barra o caminho, e esta torre ainda não tem o Item de troca.
				d.Expect(expedition.Current.Kind == RoomKind.Marco, "só o Marco barra o caminho sem o Item de troca");
				await d.Shot("03-marco-barrado");
				run.Leave("");
				await d.Wait(0.5);
				d.Expect(Runs(d).Count == 0, "voltar ao Lobby encerrou a Expedição");
				return;
			}
		}

		await d.Shot("04-fim-da-torre");
		d.Expect(run.Expedition.Floor.Number == run.Expedition.Plan.Floors.Count, "o fim da torre é no último Andar");
		await d.Wait(0.2);
	}

	// O Descanso devolve Energia uma vez; o Puzzle é uma porta trancada, sem criatura e sem bolhas.
	public static async Task Rooms(DevDriver d)
	{
		var run = await Enter(d);
		if (run is null) return;
		var expedition = run.Expedition;

		await GoTo(d, run, RoomKind.Rest);
		d.Expect(expedition.Current.Kind == RoomKind.Rest, "chegou à Sala de Descanso");
		expedition.Energy.Lose(3);
		var before = expedition.Energy.Current;
		run.Player.PlaceAt(DungeonWorld.FountainSpot + new Vector2(0.5f, 0.5f));
		await d.Wait(0.3);
		await d.Tap(Controls.Interact);
		d.Expect(expedition.Energy.Current == before + 2, "o Descanso devolveu 2 de Energia");
		await d.Shot("01-descanso");
		await d.Tap(Controls.Interact);
		d.Expect(expedition.Energy.Current == before + 2, "o Descanso só vale uma vez");

		await GoTo(d, run, RoomKind.Puzzle);
		d.Expect(expedition.Current.Kind == RoomKind.Puzzle, "chegou a uma Sala de Puzzle");
		d.Expect(!run.Combat.Bubbles, "o Puzzle não lança bolhas");
		d.Expect(run.World.Creature.Visible, "a porta mostra o escudo a resolver");
		run.Player.PlaceAt(new Vector2(0f, 1.5f));
		await d.Wait(0.3);
		await d.Tap(Controls.Target);
		d.Expect(run.Combat.HasTarget, "escolher a porta abriu a montagem da resposta");
		await d.Shot("02-puzzle");
		await d.PressKey(Key.Escape);
		var energy = expedition.Energy.Current;
		d.Expect(!expedition.IsCleared(expedition.CurrentRoom), "a porta continua trancada até resolver");
		run.DevWinRoom();
		await d.Wait(0.3);
		d.Expect(expedition.IsCleared(expedition.CurrentRoom) && expedition.Energy.Current == energy, "resolver o Puzzle abre a porta");
		await d.Shot("03-porta-aberta");
	}

	// A torre das Letras usa o Escriba do Nível: sílabas no 1º, palavra inteira no 2º e frase no 3º.
	public static async Task Letters(DevDriver d)
	{
		var run = await Enter(d, letters: true);
		if (run is null) return;
		var expedition = run.Expedition;
		d.Expect(expedition.Plan.Discipline == "portugues", "a Expedição é de Português");

		await GoTo(d, run, RoomKind.Combat);
		d.Expect(expedition.Current.Kind == RoomKind.Combat, "há uma luta no caminho");
		run.Player.PlaceAt(new Vector2(0f, 1.5f));
		await d.Wait(0.3);
		await d.Tap(Controls.Target);
		d.Expect(run.Combat.HasTarget, "escolher a criatura abriu a montagem do Escriba");
		var level = expedition.Plan.Level;
		var panels = d.GetTree().Root.FindChildren("*", "", true, false);
		d.Expect(level == 2 ? panels.OfType<WordPanel>().Any() : panels.OfType<NamePanel>().Any(), $"o Escriba do {level}º ano abriu o painel certo");
		await d.Shot("01-escriba");
		await d.PressKey(Key.Escape);
		run.DevWinRoom();
		await d.Wait(0.3);
		d.Expect(expedition.IsCleared(expedition.CurrentRoom), "vencer a Sala a libera");
		await GoTo(d, run, RoomKind.Exit);
		await d.Shot("02-saida");
	}

	// Energia zerada numa luta volta ao Lobby sem mensagem que culpe a criança, e a Expedição acaba.
	public static async Task Defeat(DevDriver d)
	{
		var run = await Enter(d);
		if (run is null) return;
		var expedition = run.Expedition;

		await GoTo(d, run, RoomKind.Combat);
		d.Expect(expedition.Current.Kind == RoomKind.Combat, "há uma luta no caminho");

		run.Player.PlaceAt(new Vector2(0f, 1.5f));
		await d.Wait(0.3);
		await d.Tap(Controls.Target);
		await d.Shot("01-luta");
		expedition.Energy.Lose(expedition.Energy.Max - 1);
		await d.OvershootWithKeys();
		await d.Wait(1.0);

		d.Expect(Runs(d).Count == 0, "Energia zerada encerrou a Expedição");
		d.Expect(d.Player is { Visible: true }, "a criança voltou ao Lobby");
		await d.Shot("02-de-volta-ao-lobby");
	}

	// Cada torre tem a porta na beira da ilha; o piloto anda até um hexágono perto dela.
	static async Task<DungeonRun?> Enter(DevDriver d, bool letters = false)
	{
		if (d.Button("Criar") is not null)
		{
			await d.Press("Criar");
			await d.Type("Luna");
			await d.Press("Criar");
		}
		d.Expect(d.Player is { Enabled: true }, "o Perfil está ativo no Lobby");
		if (letters) await d.GoNearDummy(2, -4);
		else await d.GoNearDummy(-3, 3);
		// O Personagem anda devagar: espera chegar perto da porta da torre.
		await d.Wait(3.0);
		await d.Shot("00-perto-da-torre");
		await d.Tap(Controls.Interact);
		await d.Wait(0.5);
		var run = Runs(d).FirstOrDefault();
		d.Expect(run is not null, letters ? "a porta da Torre das Letras abriu a Dungeon" : "a porta da Torre dos Números abriu a Dungeon");
		return run;
	}

	static List<DungeonRun> Runs(DevDriver d) => d.GetTree().Root.FindChildren("*", "", true, false).OfType<DungeonRun>().ToList();

	static bool NeedsFight(ArquipelagoDoCeu.Dungeon.Expedition e) =>
		e.NeedsClearing(e.Current) && !e.IsCleared(e.CurrentRoom) && e.Current.Kind != RoomKind.Marco;

	// Vence a Sala, se for preciso, anda até a porta da próxima e passa por ela; devolve se a criança mudou de Sala.
	static async Task<bool> Step(DevDriver d, DungeonRun run, int next)
	{
		var expedition = run.Expedition;
		if (NeedsFight(expedition))
		{
			run.DevWinRoom();
			await d.Wait(0.3);
			d.Expect(expedition.IsCleared(expedition.CurrentRoom), "vencer a Sala a libera");
		}

		var door = run.World.DoorTo(next);
		if (door is null) { d.Expect(false, $"a Sala {expedition.CurrentRoom} tem porta para a {next}"); return false; }
		var from = expedition.CurrentRoom;
		run.Player.PlaceAt(new Vector2(door.Ground.X * 0.85f, door.Ground.Z * 0.85f));
		await d.Wait(0.3);
		await d.Tap(Controls.Interact);
		return expedition.CurrentRoom != from;
	}

	static async Task Ascend(DevDriver d, DungeonRun run)
	{
		var floor = run.Expedition.Floor.Number;
		run.Player.PlaceAt(DungeonWorld.StairsSpot + new Vector2(0.3f, 0.3f));
		await d.Wait(0.3);
		await d.Tap(Controls.Interact);
		d.Expect(run.Expedition.Floor.Number == floor + 1, $"a escada levou ao Andar {floor + 1}");
	}

	// Anda de Sala em Sala, subindo de Andar se preciso, até uma Sala da função pedida.
	static async Task GoTo(DevDriver d, DungeonRun run, RoomKind kind)
	{
		var expedition = run.Expedition;
		for (var guard = 0; guard < 40 && expedition.Current.Kind != kind; guard++)
		{
			var path = PathTo(expedition.Floor, expedition.CurrentRoom, r => r.Kind == kind);
			if (path.Count < 2)
			{
				if (!expedition.CanAscend) return;
				await Ascend(d, run);
				continue;
			}
			if (!await Step(d, run, path[1])) return;
		}
	}

	// O caminho mais curto da Sala atual até a primeira que atende ao pedido, sem olhar trancas; vazio se não houver.
	static List<int> PathTo(Floor floor, int from, Func<Room, bool> goal)
	{
		var previous = new Dictionary<int, int> { [from] = -1 };
		var queue = new Queue<int>([from]);
		int? found = null;
		while (queue.Count > 0 && found is null)
		{
			var room = queue.Dequeue();
			if (goal(floor.Rooms[room])) { found = room; break; }
			foreach (var next in floor.Neighbors(room).Where(n => !previous.ContainsKey(n)))
			{
				previous[next] = room;
				queue.Enqueue(next);
			}
		}
		var path = new List<int>();
		if (found is null) return path;
		for (var room = found.Value; room != -1; room = previous[room]) path.Insert(0, room);
		return path;
	}
}
