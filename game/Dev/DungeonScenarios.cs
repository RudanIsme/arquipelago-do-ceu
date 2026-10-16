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

	// A troca de Itens: o Marco barra sem o Item da outra torre; vencer a Sala do Item o dá, e então o Marco abre.
	public static async Task Items(DevDriver d)
	{
		await EnsureProfile(d);

		var letters = await Enter(d, letters: true);
		if (letters is null) return;
		await GoTo(d, letters, r => r.Kind == RoomKind.Marco);
		d.Expect(letters.Expedition.Current.Kind == RoomKind.Marco, "chegou ao Marco da Torre das Letras");
		d.Expect(!letters.Progress.HasItem(letters.ProfileId, "matematica", 1), "ainda não há Item de troca da Torre dos Números");
		var beyond = letters.Expedition.Floor.Neighbors(letters.Expedition.CurrentRoom).First(n => !letters.Expedition.Visited.Contains(n));
		await d.Shot("01-marco-sem-item");
		d.Expect(!await Step(d, letters, beyond), "o Marco barra quem não tem o Item de troca");
		await d.Shot("02-marco-barrado");
		letters.Leave("");
		await d.Wait(0.5);

		var math = await Enter(d);
		if (math is null) return;
		await GoTo(d, math, r => r.HoldsItem);
		d.Expect(math.Expedition.Current.HoldsItem, "chegou à Sala do Item da Torre dos Números");
		await d.Shot("03-sala-do-item");
		math.DevWinRoom();
		await d.Wait(0.4);
		d.Expect(math.Progress.HasItem(math.ProfileId, "matematica", 1), "vencer a Sala do Item deu o Item de troca");
		await d.Shot("04-item-ganho");
		math.Leave("");
		await d.Wait(0.5);

		var again = await Enter(d, letters: true);
		if (again is null) return;
		await GoTo(d, again, r => r.Kind == RoomKind.Marco);
		var next = again.Expedition.Floor.Neighbors(again.Expedition.CurrentRoom).First(n => !again.Expedition.Visited.Contains(n));
		d.Expect(await Step(d, again, next), "com o Item de troca o Marco abre");
		await d.Shot("05-marco-aberto");
	}

	// O Fast travel: o portal avisa quando não há ponto; achar o ponto salva; pelo portal a criança retoma ali; pela porta, recomeça do Andar mais fundo.
	public static async Task FastTravel(DevDriver d)
	{
		await EnsureProfile(d);
		await OpenPortal(d);
		d.Expect(d.HasVisible("Ainda não há nenhum ponto de viagem"), "sem Fast travel encontrado o portal avisa");
		await d.PressKey(Key.Escape);

		var run = await Enter(d);
		if (run is null) return;
		var expedition = run.Expedition;
		await GoTo(d, run, RoomKind.FastTravel);
		d.Expect(expedition.Current.Kind == RoomKind.FastTravel, "chegou ao Fast travel");
		var point = new ArquipelagoDoCeu.Saves.FastTravelPoint("matematica", 1, expedition.Floor.Number);
		d.Expect(run.Progress.FastTravels(run.ProfileId).Contains(point), "chegar ao Fast travel salvou o ponto");
		await d.Shot("01-fast-travel");
		var floor = expedition.Floor.Number;
		run.Leave("");
		await d.Wait(0.5);
		d.Expect(Runs(d).Count == 0, "voltou ao Lobby");

		await OpenPortal(d);
		await d.Shot("02-portal-com-ponto");
		await d.Press($"Torre dos Números · Nível 1 · Andar {floor}");
		await d.Wait(0.5);
		var resumed = Runs(d).FirstOrDefault();
		d.Expect(resumed is not null && resumed.Expedition.Current.Kind == RoomKind.FastTravel && resumed.Expedition.Floor.Number == floor, "o portal retomou no Fast travel do Andar");
		if (resumed is null) return;
		d.Expect(resumed.Expedition.Visited.Contains(resumed.Expedition.Floor.Entrance), "o caminho até o ponto conta como percorrido");
		await d.Shot("03-retomou");
		resumed.Leave("");
		await d.Wait(0.5);
	}

	// O Personagem anda devagar: clica no hexágono e espera chegar de verdade.
	static async Task WalkTo(DevDriver d, int q, int r)
	{
		await d.ClickHex(q, r);
		var target = Lobby.HexToWorld(q, r);
		for (var i = 0; i < 100; i++)
		{
			await d.Wait(0.25);
			if (d.Player is { } p && new Vector2(p.GroundPosition.X - target.X, p.GroundPosition.Y - target.Z).Length() < 0.3f) break;
		}
		await d.Wait(0.3);
	}

	static async Task OpenPortal(DevDriver d)
	{
		await WalkTo(d, -1, 3);
		await d.Tap(Controls.Interact);
		await d.Wait(0.3);
	}

	static async Task EnsureProfile(DevDriver d)
	{
		if (d.Button("Criar") is not null)
		{
			await d.Press("Criar");
			await d.Type("Luna");
			await d.Press("Criar");
		}
		d.Expect(d.Player is { Enabled: true }, "o Perfil está ativo no Lobby");
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
		await EnsureProfile(d);
		if (letters) await WalkTo(d, 2, -4);
		else await WalkTo(d, -3, 3);
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
	static Task GoTo(DevDriver d, DungeonRun run, RoomKind kind) => GoTo(d, run, r => r.Kind == kind);

	static async Task GoTo(DevDriver d, DungeonRun run, Func<Room, bool> goal)
	{
		var expedition = run.Expedition;
		for (var guard = 0; guard < 40 && !goal(expedition.Current); guard++)
		{
			var path = PathTo(expedition.Floor, expedition.CurrentRoom, goal);
			if (path.Count < 2)
			{
				// A Sala não está neste Andar: vai até a escada e sobe.
				if (expedition.CanAscend) { await Ascend(d, run); continue; }
				path = PathTo(expedition.Floor, expedition.CurrentRoom, r => r.Id == expedition.Floor.Exit);
				if (path.Count < 2) return;
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
