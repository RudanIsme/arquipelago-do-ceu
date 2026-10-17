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

// Cenários do piloto para as torres: percorrem a Expedição como uma criança; a luta é encerrada pelo gancho de depuração.
// Use `--seed-do-mundo=N` para repetir o mesmo Plano.
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
		d.Expect(!letters.Progress.Levels.HasItem(letters.ProfileId, new LevelKey("matematica", 1)), "ainda não há Item de troca da Torre dos Números");
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
		d.Expect(math.Progress.Levels.HasItem(math.ProfileId, new LevelKey("matematica", 1)), "vencer a Sala do Item deu o Item de troca");
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

	// Ponta a ponta do Nível 1 para a CI (--seed-do-mundo=1 e --save=<arquivo>, sem janela): Atalho, a outra torre e o Desafio mestre.
	// O `e2e-4` reabre o jogo e confere que tudo continua no save.
	public static async Task E2eLevelFirst(DevDriver d)
	{
		var save = d.Arg("save");
		d.Expect(save.Length != 0, "o roteiro recebeu --save=<arquivo>");
		await EnsureProfile(d);
		var main = d.GetParent<Main>();
		var profile = main.DevProfileId;
		var progress = main.DevProgress;

		// 1. Torre das Letras pelo Atalho de Nível: os Desafios sorteados, o épico, o Nível concluído e o Item.
		var letters = await Enter(d, letters: true, shortcut: true);
		if (letters is null) return;
		d.Expect(letters.Expedition.Current.Kind == RoomKind.Epic, "o Atalho começa direto no Desafio épico");
		for (var i = 0; i < 3; i++) { letters.DevWinRoom(); await d.Wait(0.4); }
		d.Expect(progress.Levels.EpicDone(profile, new LevelKey("portugues", 1)) && progress.Levels.HasItem(profile, new LevelKey("portugues", 1)), "o Atalho concluiu a Torre das Letras e deu o Item");
		await d.Press("Voltar ao Lobby");
		await d.Wait(0.5);
		d.Expect(Runs(d).Count == 0, "voltou ao Lobby");

		// 2. Torre dos Números jogada inteira.
		var math = await Enter(d);
		if (math is null) return;
		var expedition = math.Expedition;
		await GoTo(d, math, RoomKind.Rest);
		d.Expect(expedition.Current.Kind == RoomKind.Rest, "chegou ao Descanso");
		expedition.Energy.Lose(3);
		var before = expedition.Energy.Current;
		math.Player.PlaceAt(DungeonWorld.FountainSpot + new Vector2(0.5f, 0.5f));
		await d.Wait(0.3);
		await d.Tap(Controls.Interact);
		d.Expect(expedition.Energy.Current == before + 2, "o Descanso devolveu Energia");

		await GoTo(d, math, RoomKind.Puzzle);
		d.Expect(expedition.Current.Kind == RoomKind.Puzzle, "chegou a um Puzzle");
		math.DevWinRoom();
		await d.Wait(0.3);
		d.Expect(expedition.IsCleared(expedition.CurrentRoom), "o Puzzle abriu");

		await GoTo(d, math, RoomKind.FastTravel);
		d.Expect(progress.Levels.FastTravels(profile).Any(p => p.Discipline == "matematica"), "o Fast travel foi salvo");

		await GoTo(d, math, r => r.HoldsItem);
		if (!progress.Levels.HasItem(profile, new LevelKey("matematica", 1))) { math.DevWinRoom(); await d.Wait(0.3); }
		d.Expect(progress.Levels.HasItem(profile, new LevelKey("matematica", 1)), "a Sala do Item deu o Item de troca");

		await GoTo(d, math, r => r.Kind == RoomKind.Marco);
		var beyond = expedition.Floor.Neighbors(expedition.CurrentRoom).First(n => !expedition.Visited.Contains(n));
		d.Expect(await Step(d, math, beyond), "o Item da outra torre abriu o Marco");

		await GoTo(d, math, RoomKind.Epic);
		d.Expect(expedition.Current.Kind == RoomKind.Epic, "chegou ao Desafio épico");
		math.DevWinRoom();
		await d.Wait(0.5);
		d.Expect(progress.Levels.EpicDone(profile, new LevelKey("matematica", 1)), "o épico concluiu a Torre dos Números");
		await d.Press("Voltar ao Lobby");
		await d.Wait(0.5);
		d.Expect(Runs(d).Count == 0, "voltou ao Lobby");

		// 3. Os dois épicos concluídos abrem o Desafio mestre; vencê-lo libera o Nível 2.
		d.Expect(progress.Levels.MasterOpen(profile, 1), "o Desafio mestre está aberto");
		await WalkTo(d, 4, -4);
		await d.Tap(Controls.Interact);
		await d.Press("Começar o Desafio mestre");
		d.Expect(main.DevMaster.Active, "o Desafio mestre começou");
		for (var i = 0; i < 4; i++) { main.DevMaster.DevWinRound(); await d.Wait(0.3); }
		d.Expect(main.DevLevel == 2, "vencer o Desafio mestre liberou o Nível 2");

		if (save.Length != 0) File.WriteAllBytes(save + ".snapshot", File.ReadAllBytes(save));
	}

	public static async Task E2eLevelReopen(DevDriver d)
	{
		await d.Wait(0.5);
		var main = d.GetParent<Main>();
		var profile = main.DevProfileId;
		var progress = main.DevProgress;
		d.Expect(d.Player is { Enabled: true }, "o Perfil abriu sozinho ao reabrir o jogo");
		d.Expect(main.DevLevel == 2, "o Nível liberado continua 2");
		d.Expect(progress.Levels.EpicDone(profile, new LevelKey("matematica", 1)) && progress.Levels.EpicDone(profile, new LevelKey("portugues", 1)), "os dois épicos continuam concluídos");
		d.Expect(progress.Levels.HasItem(profile, new LevelKey("matematica", 1)) && progress.Levels.HasItem(profile, new LevelKey("portugues", 1)), "os dois Itens de troca continuam com a criança");
		d.Expect(progress.Levels.ShortcutTried(profile, new LevelKey("portugues", 1)), "o Atalho de Nível continua gasto");
		d.Expect(progress.Levels.FastTravels(profile).Count > 0, "o Fast travel continua guardado");
		d.Expect(!progress.Levels.MasterOpen(profile, 2), "no Nível 2 o Desafio mestre volta a pedir os épicos");
	}

	// O Desafio mestre: o portal avisa o que falta, abre com os dois épicos, perder não pune e vencer sobe o Nível.
	public static async Task Master(DevDriver d)
	{
		await EnsureProfile(d);
		var main = d.GetParent<Main>();
		var profile = main.DevProfileId;

		await WalkTo(d, 4, -4);
		await d.Tap(Controls.Interact);
		d.Expect(d.HasVisible("O portal só abre quando"), "o portal fechado diz o que falta");
		await d.Shot("01-portal-fechado");
		await d.PressKey(Key.Escape);

		main.DevProgress.Levels.MarkEpicDone(profile, new LevelKey("matematica", 1));
		main.DevProgress.Levels.MarkEpicDone(profile, new LevelKey("portugues", 1));
		await d.Tap(Controls.Interact);
		d.Expect(d.Button("Começar o Desafio mestre") is not null, "com os dois épicos o portal deixa começar");
		await d.Shot("02-portal-aberto");
		await d.Press("Começar o Desafio mestre");
		d.Expect(main.DevMaster.Active, "o Desafio mestre começou");

		main.DevMaster.DevLose();
		await d.Wait(0.3);
		d.Expect(!main.DevMaster.Active && main.DevLevel == 1, "perder encerra o desafio sem punir: o Nível continua o mesmo");
		d.Expect(main.DevProgress.Levels.EpicDone(profile, new LevelKey("matematica", 1)) && main.DevProgress.Levels.EpicDone(profile, new LevelKey("portugues", 1)), "os épicos continuam concluídos");

		await d.Tap(Controls.Interact);
		await d.Press("Começar o Desafio mestre");
		d.Expect(main.DevMaster.Active && main.DevMaster.Rounds == 4, "dá para tentar de novo");
		await d.GoNearDummy();
		await d.ClickCreature();
		await d.Shot("03-luta-do-mestre");
		await d.PressKey(Key.Escape);
		for (var i = 0; i < 4; i++)
		{
			main.DevMaster.DevWinRound();
			await d.Wait(0.3);
		}
		d.Expect(!main.DevMaster.Active, "vencer os quatro Desafios termina o Desafio mestre");
		d.Expect(main.DevLevel == 2, "vencer subiu o Nível liberado");
		await d.Shot("04-nivel-2");

		await WalkTo(d, 4, -4);
		await d.Tap(Controls.Interact);
		d.Expect(d.HasVisible("O portal só abre quando") && d.HasVisible("Nível 2"), "no Nível 2 o portal volta a pedir os dois épicos");
	}

	// O Desafio épico conclui o Nível e a criança escolhe continuar ou voltar.
	public static async Task Epic(DevDriver d)
	{
		var run = await Enter(d);
		if (run is null) return;
		// O Marco desta torre pede o Item da outra: o piloto o entrega para chegar ao épico.
		run.Progress.Levels.GiveItem(run.ProfileId, new LevelKey("portugues", 1));
		await GoTo(d, run, RoomKind.Epic);
		d.Expect(run.Expedition.Current.Kind == RoomKind.Epic, "chegou ao Desafio épico");
		d.Expect(!run.Progress.Levels.EpicDone(run.ProfileId, new LevelKey("matematica", 1)), "o Nível ainda não está concluído");
		run.DevWinRoom();
		await d.Wait(0.5);
		d.Expect(run.Progress.Levels.EpicDone(run.ProfileId, new LevelKey("matematica", 1)), "vencer o épico concluiu o Nível");
		d.Expect(d.HasVisible("Nível concluído!"), "a criança vê a escolha de continuar ou voltar");
		await d.Shot("01-nivel-concluido");
		await d.Press("Continuar explorando");
		d.Expect(Runs(d).Count == 1 && !d.HasVisible("Nível concluído!"), "continuar explorando deixa a criança na torre");
		await d.Press("Voltar ao Lobby");
		await d.Wait(0.5);
		d.Expect(Runs(d).Count == 0, "voltar ao Lobby encerra a Expedição");
	}

	// O Atalho de Nível: uma só tentativa; acertar conclui o Nível e dá o Item, errar encerra a tentativa.
	public static async Task Shortcut(DevDriver d, bool fail)
	{
		var run = await Enter(d, shortcut: true);
		if (run is null) return;
		var expedition = run.Expedition;
		d.Expect(expedition.Current.Kind == RoomKind.Epic && expedition.Floor.Number == expedition.Plan.Floors.Count, "o Atalho começa direto no Desafio épico");
		d.Expect(run.Progress.Levels.ShortcutTried(run.ProfileId, new LevelKey("matematica", 1)), "a tentativa do Atalho foi gravada ao começar");
		run.Player.PlaceAt(new Vector2(0f, 1.5f));
		await d.Wait(0.3);
		await d.Tap(Controls.Target);
		d.Expect(run.Combat.HasTarget, "escolher a criatura abriu a montagem da resposta");
		await d.Shot("01-atalho");

		if (fail)
		{
			var energy = expedition.Energy.Current;
			await d.OvershootWithKeys();
			await d.Wait(1.0);
			d.Expect(Runs(d).Count == 0, "errar encerrou o Atalho");
			d.Expect(expedition.Energy.Current == energy, "errar no Atalho não tirou Energia da Expedição");
			d.Expect(!run.Progress.Levels.EpicDone(run.ProfileId, new LevelKey("matematica", 1)) && !run.Progress.Levels.HasItem(run.ProfileId, new LevelKey("matematica", 1)), "o Nível não foi concluído");
			d.Expect(run.Progress.Levels.HighestFloor(run.ProfileId, new LevelKey("matematica", 1)) == 1, "o Atalho não descobriu Andares: a torre normal recomeça do Andar 1");
			await d.Shot("02-de-volta-ao-lobby");
			var again = await Enter(d);
			d.Expect(again is not null && d.Button("Tentar o Atalho") is null, "não há segunda tentativa: a porta entra direto");
			again?.Leave("");
			return;
		}

		for (var i = 0; i < 3; i++)
		{
			run.DevWinRoom();
			await d.Wait(0.5);
		}
		d.Expect(run.Progress.Levels.EpicDone(run.ProfileId, new LevelKey("matematica", 1)), "acertar o Atalho concluiu o Nível");
		d.Expect(run.Progress.Levels.HasItem(run.ProfileId, new LevelKey("matematica", 1)), "e entregou o Item de troca");
		await d.Shot("03-atalho-vencido");
		await d.Press("Voltar ao Lobby");
		await d.Wait(0.5);
		var next = await Enter(d);
		d.Expect(next is not null && d.Button("Tentar o Atalho") is null, "depois do Atalho a porta entra direto");
		next?.Leave("");
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
		var point = new ArquipelagoDoCeu.Saves.FloorKey("matematica", 1, expedition.Floor.Number);
		d.Expect(run.Progress.Levels.FastTravels(run.ProfileId).Contains(point), "chegar ao Fast travel salvou o ponto");
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
	// Ao voltar com moedas novas o jogo mostra o total; o piloto fecha essa tela para seguir.
	static async Task DismissCoins(DevDriver d)
	{
		if (d.Button("Continuar") is null || !d.HasVisible("Suas moedas")) return;
		await d.Press("Continuar");
		await d.Wait(0.2);
	}

	static async Task WalkTo(DevDriver d, int q, int r)
	{
		await DismissCoins(d);
		// Sem janela não há mouse: o piloto coloca o Personagem no hexágono, e o resto (interagir) usa as ações de sempre.
		if (DisplayServer.GetName() == "headless")
		{
			var spot = Lobby.HexToWorld(q, r);
			d.Player?.PlaceAt(new Vector2(spot.X, spot.Z));
			await d.Wait(0.4);
			return;
		}
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
		await DismissCoins(d);
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

	// A escada livre, o mapa com névoa e cadeado, a barra de progresso e o Cosmético do Andar completo (#112 a #114 e #116).
	public static async Task Floors(DevDriver d)
	{
		var run = await Enter(d);
		if (run is null) return;
		var expedition = run.Expedition;
		var progress = run.Progress;
		var profile = run.ProfileId;
		var floor = expedition.Floor;

		d.Expect(run.World.Doors.All(door => door.Label.StartsWith("Sala nova")), "a porta de uma Sala nova não diz o que há do outro lado");
		d.Expect(floor.Rooms.Any(r => expedition.SightOf(r.Id) == ArquipelagoDoCeu.Dungeon.Sight.Hidden), "as Salas distantes nem aparecem no mapa");
		d.Expect(progress.Rewards.Cosmetics(profile).Count == 0, "o Perfil novo não tem Cosmético");
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
				run.DevWinRoom();
				await d.Wait(0.3);
			}
			if (i == content.Count / 2 - 1)
			{
				d.Expect(!progress.Rewards.IsFloorDone(profile, new FloorKey("matematica", 1, 1)), "no meio do Andar ele ainda não está completo");
				await d.Shot("03-barra-parcial");
			}
		}
		var total = expedition.ProgressOf(floor.Number);
		d.Expect(total.Resolved == total.Total, "todas as Salas do Andar foram resolvidas");
		d.Expect(progress.Rewards.IsFloorDone(profile, new FloorKey("matematica", 1, 1)), "o Andar completo foi registrado");
		d.Expect(progress.Rewards.Cosmetics(profile).Contains("matematica-1-1"), "completar o Andar deu o Cosmético dele");
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
		d.Expect(progress.Rewards.Cosmetics(profile).Count == 1, "voltar ao Andar não repete o Cosmético");
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
		run.DevWinRoom();
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
		run.DevWinRoom();
		await d.Wait(0.3);
		d.Expect(progress.Mastery.Solves(profile, new ConceptKey("matematica", concept)) >= 1, "vencer o Desafio conta uma vitória no Conceito");
		d.Expect(progress.Wallet.Balance(profile) >= 1, "acalmar a criatura rendeu moedas");

		for (var floor = 1; floor <= 5; floor++)
		{
			await GoTo(d, run, RoomKind.Combat);
			if (NeedsFight(expedition))
			{
				run.DevWinRoom();
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
		d.Expect(progress.Explored.Get(profile, new FloorKey("matematica", 1, 1), 1) is null && !progress.Rewards.IsFloorDone(profile, new FloorKey("matematica", 1, 1)), "a Livre não guarda exploração nem Andar completo");
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
		var main = d.GetParent<Main>();
		var profile = main.DevProfileId;
		d.Expect(d.HasVisible("Maestria 0") && d.Button("Editar") is not null, "no Lobby o card mostra a Maestria e o lápis de Editar");
		await d.Shot("01-card-no-lobby");

		var mastery = main.DevProgress.Mastery.Of(profile, "matematica");
		d.Expect(!main.DevGear.Equip(profile, "matematica", "guardian", mastery).IsOk, "sem a Maestria necessária o equipamento não veste");
		d.Expect(main.DevGear.Equip(profile, "matematica", "open", 1).IsOk, "com a Maestria o Elmo aberto veste");
		d.Expect(!main.DevGear.Get(profile, "portugues").Equals(main.DevGear.Get(profile, "matematica")), "o equipamento é de cada Papel");

		var run = await Enter(d);
		if (run is null) return;
		d.Expect(run.Expedition.Energy.Max == 6, "o Elmo aberto soma 1 de Energia máxima na Expedição");
		d.Expect(d.HasVisible("Maestria 0") && d.Button("Editar") is null, "na Dungeon o card mostra a Maestria e não tem o lápis de Editar");
		d.Expect(run.CoinToast == "Moedas: 0", "ao ir para a Expedição aparece o total de moedas");
		await d.Shot("02-card-na-dungeon");

		await GoTo(d, run, RoomKind.Combat);
		d.Expect(!run.Expedition.IsCleared(run.Expedition.CurrentRoom), "chegou a uma luta por vencer");
		run.DevWinRoom();
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

	// Cada torre tem a porta na beira da ilha; o piloto anda até um hexágono perto dela.
	static async Task<DungeonRun?> Enter(DevDriver d, bool letters = false, bool shortcut = false, bool free = false)
	{
		await EnsureProfile(d);
		if (letters) await WalkTo(d, 2, -4);
		else await WalkTo(d, -3, 3);
		await d.Shot("00-perto-da-torre");
		await d.Tap(Controls.Interact);
		await d.Wait(0.5);
		// A porta da torre pergunta o modo: Progressão (o Plano do Nível) ou Livre.
		if (d.Button("Progressão") is not null)
		{
			await d.Shot("00-escolha-do-modo");
			await d.Press(free ? "Livre" : "Progressão");
			await d.Wait(0.5);
		}
		// Com o Atalho de Nível ainda não tentado, a porta da torre pergunta antes de entrar.
		if (!free && d.Button("Entrar na torre") is not null)
		{
			await d.Shot("00-oferta-do-atalho");
			await d.Press(shortcut ? "Tentar o Atalho" : "Entrar na torre");
			await d.Wait(0.5);
		}
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
