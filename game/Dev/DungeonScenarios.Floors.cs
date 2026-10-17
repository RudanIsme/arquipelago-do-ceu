using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Saves;
using Godot;
using Vector2 = System.Numerics.Vector2;

namespace ArquipelagoDoCeu;

// Cenários do piloto dos Andares: torre das Letras, exploração, Expedição livre, equipamento e derrota.
static partial class DungeonScenarios
{
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
		PilotPort.WinRoom(run);
		await d.Wait(0.3);
		d.Expect(expedition.IsCleared(expedition.CurrentRoom), "vencer a Sala a libera");
		await GoTo(d, run, RoomKind.Exit);
		await d.Shot("02-saida");
	}

	// A escada livre, o mapa com névoa e cadeado, a barra de progresso e o Cosmético do Andar completo (#112 a #114 e #116).
	public static async Task Floors(DevDriver d)
	{
		var run = await Enter(d);
		if (run is null) return;
		var expedition = run.Expedition;
		var progress = run.Progress;
		var profile = run.ProfileId;
		var floor = expedition.Floor;
		var rewards = d.Port.Rewards;

		d.Expect(run.World.Doors.All(door => door.Label.StartsWith("Sala nova")), "a porta de uma Sala nova não diz o que há do outro lado");
		d.Expect(floor.Rooms.Any(r => expedition.SightOf(r.Id) == ArquipelagoDoCeu.Dungeon.Sight.Hidden), "as Salas distantes nem aparecem no mapa");
		d.Expect(rewards.Cosmetics(profile).Count == 0, "o Perfil novo não tem Cosmético");
		await d.Shot("01-nevoa");

		await GoTo(d, run, RoomKind.Combat);
		var unvisited = floor.Neighbors(expedition.CurrentRoom).Where(n => !expedition.Visited.Contains(n)).ToList();
		d.Expect(unvisited.Count == 0 || unvisited.Any(expedition.IsLocked), "a Sala de combate por vencer põe cadeado nas Salas novas ao lado");
		await d.Shot("02-cadeado");

		var content = floor.Rooms.Where(r => r.Kind is not (RoomKind.Entrance or RoomKind.Exit)).Select(r => r.Id).ToList();
		for (var i = 0; i < content.Count; i++)
		{
			var id = content[i];
			await GoTo(d, run, r => r.Id == id);
			d.Expect(expedition.CurrentRoom == id, $"chegou à Sala {id}");
			if (NeedsFight(expedition))
			{
				PilotPort.WinRoom(run);
				await d.Wait(0.3);
			}
			if (i == content.Count / 2 - 1)
			{
				d.Expect(!progress.Explored.IsFloorDone(profile, new FloorKey("matematica", 1, 1)), "no meio do Andar ele ainda não está completo");
				await d.Shot("03-barra-parcial");
			}
		}
		var total = expedition.ProgressOf(floor.Number);
		d.Expect(total.Resolved == total.Total, "todas as Salas do Andar foram resolvidas");
		d.Expect(progress.Explored.IsFloorDone(profile, new FloorKey("matematica", 1, 1)), "o Andar completo foi registrado");
		d.Expect(rewards.Cosmetics(profile).Contains("matematica-1-1"), "completar o Andar deu o Cosmético dele");
		await d.Shot("04-selo-andar-completo");

		await GoTo(d, run, RoomKind.Exit);
		var wonBefore = expedition.Cleared.ToList();
		await Ascend(d, run);
		d.Expect(expedition.Floor.Number == 2 && expedition.Visited.Count == 1, "o Andar 2 começa pela entrada, em névoa");
		await d.Shot("05-andar-2");
		run.Player.PlaceAt(DungeonWorld.StairsSpot + new Vector2(0.3f, 0.3f));
		await d.Wait(0.3);
		await d.Tap(Controls.Interact);
		d.Expect(expedition.Floor.Number == 1 && expedition.Current.Kind == RoomKind.Exit, "a escada para baixo leva à saída do Andar 1");
		d.Expect(wonBefore.All(expedition.Cleared.Contains), "as Salas vencidas continuam vencidas depois de subir e descer");
		d.Expect(rewards.Cosmetics(profile).Count == 1, "voltar ao Andar não repete o Cosmético");
		await d.Shot("06-voltou-ao-andar-1");
		run.Leave("");
		await d.Wait(0.5);

		await WalkTo(d, -2, -2);
		await d.Tap(Controls.Interact);
		await d.Wait(0.3);
		await d.Press("Cosméticos");
		d.Expect(d.HasVisible("Lacinho dos Números"), "o Álbum lista o Cosmético ganho");
		await d.Shot("07-album");
	}

	// O mapa explorado sobrevive a zerar a Energia (#115): os Descansos voltam, e o recomeço é no último Fast travel achado.
	public static async Task Explored(DevDriver d)
	{
		var run = await Enter(d);
		if (run is null) return;
		var expedition = run.Expedition;

		await GoTo(d, run, RoomKind.Rest);
		var rest = expedition.CurrentRoom;
		expedition.Energy.Lose(2);
		run.Player.PlaceAt(DungeonWorld.FountainSpot + new Vector2(0.5f, 0.5f));
		await d.Wait(0.3);
		await d.Tap(Controls.Interact);
		d.Expect(expedition.Rested(rest), "o Descanso foi usado");

		await GoTo(d, run, r => r.Kind is RoomKind.Combat or RoomKind.Puzzle);
		PilotPort.WinRoom(run);
		await d.Wait(0.3);
		await GoTo(d, run, r => r.Kind is RoomKind.Combat or RoomKind.Puzzle && !expedition.IsCleared(r.Id));
		d.Expect(NeedsFight(expedition), "há mais uma luta por vencer");
		var visited = expedition.Visited.Order().ToList();
		var cleared = expedition.Cleared.Order().ToList();
		var floor = expedition.Floor.Number;
		run.Player.PlaceAt(new Vector2(0f, 1.5f));
		await d.Wait(0.3);
		await d.Tap(Controls.Target);
		expedition.Energy.Lose(expedition.Energy.Max - 1);
		await d.OvershootWithKeys();
		await d.Wait(1.0);
		d.Expect(Runs(d).Count == 0, "Energia zerada encerrou a Expedição");

		var again = await Enter(d);
		if (again is null) return;
		d.Expect(again.Expedition.Floor.Number == floor && again.Expedition.Current.Kind == RoomKind.Entrance, "sem Fast travel a criança recomeça na entrada do Andar");
		d.Expect(again.Expedition.Visited.Order().SequenceEqual(visited), "as Salas visitadas voltaram");
		d.Expect(again.Expedition.Cleared.Order().SequenceEqual(cleared), "as Salas vencidas voltaram");
		d.Expect(!again.Expedition.Rested(rest), "o Descanso volta a valer numa Expedição nova");
		await d.Shot("01-mapa-explorado");

		await GoTo(d, again, RoomKind.FastTravel);
		d.Expect(again.Expedition.Current.Kind == RoomKind.FastTravel, "chegou ao Fast travel");
		again.Leave("");
		await d.Wait(0.5);

		var last = await Enter(d);
		if (last is null) return;
		d.Expect(last.Expedition.Current.Kind == RoomKind.FastTravel, "pela porta da torre a criança recomeça no último Fast travel achado");
		d.Expect(visited.All(last.Expedition.Visited.Contains), "o mapa explorado continua lá");
		await d.Shot("02-recomeco-no-fast-travel");
		last.Leave("");
		await d.Wait(0.5);
	}

	// A Expedição livre, as moedas, a Maestria e o HUD por contexto (#120, #121, #131 e #135).
	public static async Task FreeExpedition(DevDriver d)
	{
		var run = await Enter(d, free: true);
		if (run is null) return;
		var expedition = run.Expedition;
		var progress = run.Progress;
		var profile = run.ProfileId;
		d.Expect(expedition.Plan.Endless, "a Expedição livre tem Plano sem fim");
		d.Expect(progress.Wallet.Balance(profile) == 0, "o Perfil novo não tem moedas");

		// Exploração: placa, mapa e nada de bolsa nem pedido.
		d.Expect(d.HasVisible("Livre") && d.HasVisible("Matemática:"), "a placa diz o modo e o objetivo (o Conceito do Andar)");
		d.Expect(!d.GetTree().Root.FindChildren("*", "", true, false).OfType<StaffPanel>().Any(), "em exploração não há bolsa de números");
		await d.Shot("01-exploracao");

		await GoTo(d, run, RoomKind.Combat);
		run.Player.PlaceAt(new Vector2(0f, 1.5f));
		await d.Wait(0.3);
		await d.Tap(Controls.Target);
		d.Expect(d.GetTree().Root.FindChildren("*", "", true, false).OfType<StaffPanel>().Any(), "em combate a bolsa de números aparece");
		await d.Shot("02-combate");
		var concept = run.Combat.CurrentMission!.Concept;
		await d.PressKey(Key.Escape);
		d.Expect(!d.GetTree().Root.FindChildren("*", "", true, false).OfType<StaffPanel>().Any(), "sair do combate fecha a bolsa");
		PilotPort.WinRoom(run);
		await d.Wait(0.3);
		d.Expect(progress.Mastery.Solves(profile, new ConceptKey("matematica", concept)) >= 1, "vencer o Desafio conta uma vitória no Conceito");
		d.Expect(progress.Wallet.Balance(profile) >= 1, "acalmar a criatura rendeu moedas");

		for (var floor = 1; floor <= 5; floor++)
		{
			await GoTo(d, run, RoomKind.Combat);
			if (NeedsFight(expedition))
			{
				PilotPort.WinRoom(run);
				await d.Wait(0.3);
			}
			await GoTo(d, run, RoomKind.Exit);
			d.Expect(expedition.Current.Kind == RoomKind.Exit, $"chegou à saída do Andar {floor}");
			if (floor == 1) await d.Shot("03-andar-1-saida");
			await Ascend(d, run);
		}
		d.Expect(expedition.Floor.Number == 6 && !expedition.Finished, "a Livre passa do fim de qualquer Plano e não termina");
		var seen = run.History.Seen;
		d.Expect(seen.Count > 1 && Enumerable.Range(1, seen.Count - 1).All(i => seen[i] != seen[i - 1]), "dois problemas seguidos nunca se repetem");
		d.Expect(progress.Wallet.Balance(profile) > 1, "os Andares seguintes renderam mais moedas");
		d.Expect(!progress.Levels.FloorDiscovered(profile, new FloorKey("matematica", 1, 2)) && !progress.Levels.EpicDone(profile, new LevelKey("matematica", 1)) && !progress.Levels.HasItem(profile, new LevelKey("matematica", 1)),
			"a Livre não avança o Plano do Nível, o Item nem o épico");
		d.Expect(progress.Explored.Get(profile, new FloorKey("matematica", 1, 1), 1) is null && !progress.Explored.IsFloorDone(profile, new FloorKey("matematica", 1, 1)), "a Livre não guarda exploração nem Andar completo");
		await d.Shot("04-andar-6");
		run.Leave("");
		await d.Wait(0.5);
		d.Expect(progress.Wallet.Balance(profile) > 1, "as moedas continuam depois de voltar ao Lobby");
		await DismissCoins(d);
	}

	// O equipamento vale em combate e a Maestria decide o que se veste (#126), com o card do avatar (#122) e o aviso das moedas (#132).
	public static async Task Equipment(DevDriver d)
	{
		await EnsureProfile(d);
		var main = d.Port;
		var profile = main.ProfileId;
		d.Expect(d.HasVisible("Maestria 0") && d.Button("Editar") is not null, "no Lobby o card mostra a Maestria e o lápis de Editar");
		await d.Shot("01-card-no-lobby");

		var mastery = main.Progress.Mastery.Of(profile, "matematica");
		d.Expect(!main.Gear.Equip(profile, "matematica", "guardian", mastery).IsOk, "sem a Maestria necessária o equipamento não veste");
		d.Expect(main.Gear.Equip(profile, "matematica", "open", 1).IsOk, "com a Maestria o Elmo aberto veste");
		d.Expect(!main.Gear.Get(profile, "portugues").Equals(main.Gear.Get(profile, "matematica")), "o equipamento é de cada Papel");

		var run = await Enter(d);
		if (run is null) return;
		d.Expect(run.Expedition.Energy.Max == 6, "o Elmo aberto soma 1 de Energia máxima na Expedição");
		d.Expect(d.HasVisible("Maestria 0") && d.Button("Editar") is null, "na Dungeon o card mostra a Maestria e não tem o lápis de Editar");
		d.Expect(run.CoinToast == "Moedas: 0", "ao ir para a Expedição aparece o total de moedas");
		await d.Shot("02-card-na-dungeon");

		await GoTo(d, run, RoomKind.Combat);
		d.Expect(!run.Expedition.IsCleared(run.Expedition.CurrentRoom), "chegou a uma luta por vencer");
		PilotPort.WinRoom(run);
		await d.Wait(0.3);
		d.Expect(run.CoinToast == "+1 moeda" && d.HasVisible("+1 moeda"), "ganhar moedas na Dungeon mostra um aviso curto");
		await d.Shot("03-aviso-de-moedas");
		var here = run.Expedition.CurrentRoom;
		d.Expect(run.RoomSpeech(here).Contains("Você está aqui") && run.RoomSpeech(here).Contains("vencida"), "a voz descreve a Sala atual e diz que está vencida");
		var fog = run.Expedition.Floor.Rooms.First(r => run.Expedition.SightOf(r.Id) == ArquipelagoDoCeu.Dungeon.Sight.Fog);
		d.Expect(run.RoomSpeech(fog.Id).Contains("névoa", StringComparison.Ordinal) && !run.RoomSpeech(fog.Id).Contains(FloorMap.NameOf(fog.Kind).ToLowerInvariant(), StringComparison.Ordinal), "a voz não revela o que há sob a névoa");
		run.Leave("");
		await d.Wait(0.5);
		d.Expect(d.HasVisible("De volta ao Lobby") && d.HasVisible("+1"), "ao voltar aparece o total com o +X");
		await d.Shot("04-moedas-ao-voltar");
		await d.Tap("ui_accept");
		await d.Wait(0.3);
		d.Expect(d.Button("Continuar") is not null, "Enter pulou a animação e a tela continua para o jogador fechar");
		await d.Press("Continuar");
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
}
