using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArquipelagoDoCeu.Generation;
using Godot;
using Vector2 = System.Numerics.Vector2;

namespace ArquipelagoDoCeu;

// Cenários do piloto para as torres. `torre` percorre a Expedição como uma criança faria (anda até a porta, entra,
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
				var floor = expedition.Floor.Number;
				run.Player.PlaceAt(DungeonWorld.StairsSpot + new Vector2(0.3f, 0.3f));
				await d.Wait(0.3);
				await d.Tap(Controls.Interact);
				d.Expect(expedition.Floor.Number == floor + 1, $"a escada levou ao Andar {floor + 1}");
				continue;
			}

			var path = PathToEnd(expedition.Floor, expedition.CurrentRoom);
			var next = path[1];
			if (expedition.NeedsClearing(expedition.Current) && !expedition.IsCleared(expedition.CurrentRoom) && expedition.Current.Kind != RoomKind.Marco)
			{
				if (!shotFight)
				{
					shotFight = true;
					run.Player.PlaceAt(new Vector2(0f, 1.5f));
					await d.Wait(0.3);
					await d.Tap(Controls.Target);
					d.Expect(run.Combat.HasTarget, "escolher a criatura abriu a montagem da resposta");
					await d.Shot("02-luta-na-sala");
					await d.PressKey(Key.Escape);
				}
				run.DevWinRoom();
				await d.Wait(0.3);
				d.Expect(expedition.IsCleared(expedition.CurrentRoom), "vencer a Sala a libera");
			}

			var door = run.World.DoorTo(next);
			if (door is null) { d.Expect(false, $"a Sala {expedition.CurrentRoom} tem porta para a {next}"); return; }
			var from = expedition.CurrentRoom;
			run.Player.PlaceAt(new Vector2(door.Ground.X * 0.85f, door.Ground.Z * 0.85f));
			await d.Wait(0.3);
			await d.Tap(Controls.Interact);

			if (expedition.CurrentRoom == from)
			{
				// Só o Marco barra o caminho, e a Dungeon ainda não tem o Item de troca.
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

	// Energia zerada numa luta volta ao Lobby sem mensagem que culpe a criança, e a Expedição acaba.
	public static async Task Defeat(DevDriver d)
	{
		var run = await Enter(d);
		if (run is null) return;
		var expedition = run.Expedition;
		var path = PathToEnd(expedition.Floor, expedition.CurrentRoom);
		var door = run.World.DoorTo(path[1])!;
		run.Player.PlaceAt(new Vector2(door.Ground.X * 0.85f, door.Ground.Z * 0.85f));
		await d.Wait(0.3);
		await d.Tap(Controls.Interact);
		d.Expect(expedition.Current.Kind is RoomKind.Combat or RoomKind.Puzzle or RoomKind.Rest or RoomKind.FastTravel, "chegou a uma Sala depois da entrada");

		for (var guard = 0; guard < 6 && !(expedition.Current.Kind is RoomKind.Combat or RoomKind.Puzzle); guard++)
		{
			var step = PathToEnd(expedition.Floor, expedition.CurrentRoom)[1];
			var way = run.World.DoorTo(step)!;
			run.Player.PlaceAt(new Vector2(way.Ground.X * 0.85f, way.Ground.Z * 0.85f));
			await d.Wait(0.3);
			await d.Tap(Controls.Interact);
		}
		d.Expect(expedition.Current.Kind is RoomKind.Combat or RoomKind.Puzzle, "há uma luta no caminho");

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

	static async Task<DungeonRun?> Enter(DevDriver d)
	{
		if (d.Button("Criar") is not null)
		{
			await d.Press("Criar");
			await d.Type("Luna");
			await d.Press("Criar");
		}
		d.Expect(d.Player is { Enabled: true }, "o Perfil está ativo no Lobby");
		await d.GoNearDummy(-3, 3);
		await d.Wait(3.0);
		await d.Shot("00-perto-da-torre");
		await d.Tap(Controls.Interact);
		await d.Wait(0.5);
		var run = Runs(d).FirstOrDefault();
		d.Expect(run is not null, "a placa da Torre dos Números abriu a Dungeon");
		return run;
	}

	static List<DungeonRun> Runs(DevDriver d) => d.GetTree().Root.FindChildren("*", "", true, false).OfType<DungeonRun>().ToList();

	// O caminho mais curto da Sala atual até a saída do Andar, sem olhar trancas.
	static List<int> PathToEnd(Floor floor, int from)
	{
		var previous = new Dictionary<int, int> { [from] = -1 };
		var queue = new Queue<int>([from]);
		while (queue.Count > 0)
		{
			var room = queue.Dequeue();
			if (room == floor.Exit) break;
			foreach (var next in floor.Neighbors(room).Where(n => !previous.ContainsKey(n)))
			{
				previous[next] = room;
				queue.Enqueue(next);
			}
		}
		var path = new List<int>();
		for (var room = floor.Exit; room != -1; room = previous[room]) path.Insert(0, room);
		return path;
	}
}
