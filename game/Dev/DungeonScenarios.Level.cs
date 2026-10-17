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

// Cenários do piloto do Nível: o Desafio mestre, o Fast travel e o roteiro de ponta a ponta da CI.
static partial class DungeonScenarios
{
	// Ponta a ponta do Nível 1 para a CI (--seed-do-mundo=1 e --save=<arquivo>, sem janela): Atalho, a outra torre e o Desafio mestre.
	// O `e2e-4` reabre o jogo e confere que tudo continua no save.
	public static async Task E2eLevelFirst(DevDriver d)
	{
		var save = d.Arg("save");
		d.Expect(save.Length != 0, "o roteiro recebeu --save=<arquivo>");
		await EnsureProfile(d);
		var main = d.Port;
		var profile = main.ProfileId;
		var progress = main.Progress;

		// 1. Torre das Letras pelo Atalho de Nível: os Desafios sorteados, o épico, o Nível concluído e o Item.
		var letters = await Enter(d, letters: true, shortcut: true);
		if (letters is null) return;
		d.Expect(letters.Expedition.Current.Kind == RoomTypes.Epic, "o Atalho começa direto no Desafio épico");
		for (var i = 0; i < 3; i++) { PilotPort.WinRoom(letters); await d.Wait(0.4); }
		d.Expect(progress.Levels.EpicDone(profile, new LevelKey("portugues", 1)) && progress.Levels.HasItem(profile, new LevelKey("portugues", 1)), "o Atalho concluiu a Torre das Letras e deu o Item");
		await d.Press("Voltar ao Lobby");
		await d.Wait(0.5);
		d.Expect(Runs(d).Count == 0, "voltou ao Lobby");

		// 2. Torre dos Números jogada inteira.
		var math = await Enter(d);
		if (math is null) return;
		var expedition = math.Expedition;
		await GoTo(d, math, RoomTypes.Rest);
		d.Expect(expedition.Current.Kind == RoomTypes.Rest, "chegou ao Descanso");
		expedition.Energy.Lose(3);
		var before = expedition.Energy.Current;
		math.Player.PlaceAt(DungeonWorld.FountainSpot + new Vector2(0.5f, 0.5f));
		await d.Wait(0.3);
		await d.Tap(Controls.Interact);
		d.Expect(expedition.Energy.Current == before + 2, "o Descanso devolveu Energia");

		await GoTo(d, math, RoomTypes.Puzzle);
		d.Expect(expedition.Current.Kind == RoomTypes.Puzzle, "chegou a um Puzzle");
		PilotPort.WinRoom(math);
		await d.Wait(0.3);
		d.Expect(expedition.IsCleared(expedition.CurrentRoom), "o Puzzle abriu");

		await GoTo(d, math, RoomTypes.FastTravel);
		d.Expect(progress.Levels.FastTravels(profile).Any(p => p.Discipline == "matematica"), "o Fast travel foi salvo");

		await GoTo(d, math, r => r.HoldsItem);
		if (!progress.Levels.HasItem(profile, new LevelKey("matematica", 1))) { PilotPort.WinRoom(math); await d.Wait(0.3); }
		d.Expect(progress.Levels.HasItem(profile, new LevelKey("matematica", 1)), "a Sala do Item deu o Item de troca");

		await GoTo(d, math, r => r.Kind == RoomTypes.Marco);
		var beyond = expedition.Floor.Neighbors(expedition.CurrentRoom).First(n => !expedition.Visited.Contains(n));
		d.Expect(await Step(d, math, beyond), "o Item da outra torre abriu o Marco");

		await GoTo(d, math, RoomTypes.Epic);
		d.Expect(expedition.Current.Kind == RoomTypes.Epic, "chegou ao Desafio épico");
		PilotPort.WinRoom(math);
		await d.Wait(0.5);
		d.Expect(progress.Levels.EpicDone(profile, new LevelKey("matematica", 1)), "o épico concluiu a Torre dos Números");
		await d.Press("Voltar ao Lobby");
		await d.Wait(0.5);
		d.Expect(Runs(d).Count == 0, "voltou ao Lobby");

		// 3. Os dois épicos concluídos abrem o Desafio mestre; vencê-lo libera o Nível 2.
		d.Expect(progress.Levels.MasterOpen(profile, 1, ["matematica", "portugues"]), "o Desafio mestre está aberto");
		await WalkTo(d, 4, -4);
		await d.Tap(Controls.Interact);
		await d.Press("Começar o Desafio mestre");
		d.Expect(main.Master.Active, "o Desafio mestre começou");
		for (var i = 0; i < 4; i++) { main.WinMasterRound(); await d.Wait(0.3); }
		d.Expect(main.Level == 2, "vencer o Desafio mestre liberou o Nível 2");

		if (save.Length != 0) File.WriteAllBytes(save + ".snapshot", File.ReadAllBytes(save));
	}

	public static async Task E2eLevelReopen(DevDriver d)
	{
		await d.Wait(0.5);
		var main = d.Port;
		var profile = main.ProfileId;
		var progress = main.Progress;
		d.Expect(d.Player is { Enabled: true }, "o Perfil abriu sozinho ao reabrir o jogo");
		d.Expect(main.Level == 2, "o Nível liberado continua 2");
		d.Expect(progress.Levels.EpicDone(profile, new LevelKey("matematica", 1)) && progress.Levels.EpicDone(profile, new LevelKey("portugues", 1)), "os dois épicos continuam concluídos");
		d.Expect(progress.Levels.HasItem(profile, new LevelKey("matematica", 1)) && progress.Levels.HasItem(profile, new LevelKey("portugues", 1)), "os dois Itens de troca continuam com a criança");
		d.Expect(progress.Levels.ShortcutTried(profile, new LevelKey("portugues", 1)), "o Atalho de Nível continua gasto");
		d.Expect(progress.Levels.FastTravels(profile).Count > 0, "o Fast travel continua guardado");
		d.Expect(!progress.Levels.MasterOpen(profile, 2, ["matematica", "portugues"]), "no Nível 2 o Desafio mestre volta a pedir os épicos");
	}

	// O Desafio mestre: o portal avisa o que falta, abre com os dois épicos, perder não pune e vencer sobe o Nível.
	public static async Task Master(DevDriver d)
	{
		await EnsureProfile(d);
		var main = d.Port;
		var profile = main.ProfileId;

		await WalkTo(d, 4, -4);
		await d.Tap(Controls.Interact);
		d.Expect(d.HasVisible("O portal só abre quando"), "o portal fechado diz o que falta");
		await d.Shot("01-portal-fechado");
		await d.PressKey(Key.Escape);

		main.Progress.Levels.MarkEpicDone(profile, new LevelKey("matematica", 1));
		main.Progress.Levels.MarkEpicDone(profile, new LevelKey("portugues", 1));
		await d.Tap(Controls.Interact);
		d.Expect(d.Button("Começar o Desafio mestre") is not null, "com os dois épicos o portal deixa começar");
		await d.Shot("02-portal-aberto");
		await d.Press("Começar o Desafio mestre");
		d.Expect(main.Master.Active, "o Desafio mestre começou");

		main.LoseMaster();
		await d.Wait(0.3);
		d.Expect(!main.Master.Active && main.Level == 1, "perder encerra o desafio sem punir: o Nível continua o mesmo");
		d.Expect(main.Progress.Levels.EpicDone(profile, new LevelKey("matematica", 1)) && main.Progress.Levels.EpicDone(profile, new LevelKey("portugues", 1)), "os épicos continuam concluídos");

		await d.Tap(Controls.Interact);
		await d.Press("Começar o Desafio mestre");
		d.Expect(main.Master.Active && main.Master.Rounds == 4, "dá para tentar de novo");
		await d.GoNearDummy();
		await d.ClickCreature();
		await d.Shot("03-luta-do-mestre");
		await d.PressKey(Key.Escape);
		for (var i = 0; i < 4; i++)
		{
			main.WinMasterRound();
			await d.Wait(0.3);
		}
		d.Expect(!main.Master.Active, "vencer os quatro Desafios termina o Desafio mestre");
		d.Expect(main.Level == 2, "vencer subiu o Nível liberado");
		await d.Shot("04-nivel-2");

		await WalkTo(d, 4, -4);
		await d.Tap(Controls.Interact);
		d.Expect(d.HasVisible("O portal só abre quando") && d.HasVisible("Nível 2"), "no Nível 2 o portal volta a pedir os dois épicos");
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
		await GoTo(d, run, RoomTypes.FastTravel);
		d.Expect(expedition.Current.Kind == RoomTypes.FastTravel, "chegou ao Fast travel");
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
		d.Expect(resumed is not null && resumed.Expedition.Current.Kind == RoomTypes.FastTravel && resumed.Expedition.Floor.Number == floor, "o portal retomou no Fast travel do Andar");
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
}
