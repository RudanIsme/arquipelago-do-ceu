using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace ArquipelagoDoCeu;

// Cenário `combate` do piloto: o combate sem modal da Matemática e do Português, do toque na peça ao golpe, sem botão de atacar.
static partial class DungeonScenarios
{
	public static async Task Combat(DevDriver d)
	{
		await EnsureProfile(d);
		await d.GoNearDummy();
		await d.Tap(Controls.Target);
		await d.Press("Começar");
		d.Expect(d.Hud is not null, "o treino de Matemática abriu a HUD de combate");
		d.Expect(d.Button("Atacar") is null && d.Button("Conferir") is null, "não existe botão de atacar nem de conferir");
		d.Expect(d.HasVisible("Escudo:") && d.HasVisible("Monte uma conta"), "a bolsa e o pedido ficam na HUD");
		await d.Shot("01-hud-matematica");

		var before = d.Player!.GroundPosition;
		await d.Hold(Controls.Right, 0.4);
		d.Expect(System.Numerics.Vector2.Distance(before, d.Player.GroundPosition) > 0.05f, "com a HUD aberta o Personagem anda: nada de modal");

		await d.PlayPiece(0, trace: false);
		await d.Wait(0.6);
		await d.Shot("02-peca-subiu-e-trilha");
		d.Expect(d.Pad is not null, "a peça escolhida abre a trilha para traçar");
		await d.TraceGuide();
		await d.Wait(0.9);
		d.Expect(!d.HasVisible("Monte uma conta"), "o traço certo põe a peça na conta");

		await d.PlayPiece(d.Hud!.Presenter.Pieces.Count - 1);
		await d.Shot("03-conta-pela-metade");
		var shield = d.FightShield();
		await d.PlayPiece(1);
		await d.Wait(0.8);
		d.Expect(d.FightShield() != shield || d.HasVisible("Acertou") || d.HasVisible("Passou") || d.HasVisible("Golpe"), "o terceiro traço confere e o golpe sai sem botão");
		await d.Shot("04-golpe");
		await d.PressKey(Key.Escape);
		await d.Press("Sair do treino");

		await d.GoNearDummy();
		await d.Tap(Controls.Target);
		await d.Press("Escriba");
		await d.Press("Começar");
		d.Expect(d.Hud?.Presenter is NamePresenter, "o Escriba abriu a HUD do nome, sem painel");
		await d.Shot("05-hud-nome");
		await d.PlayName();
		d.Expect(d.HasVisible("libertada") || d.HasVisible("certa"), "o último traço conferiu o nome e libertou a criatura");
		await d.Shot("06-nome-certo");
		await d.Press("Sair do treino");
	}

	// Cenário `combate-niveis`: a palavra inteira do 2º ano e a frase do 3º, sem modal. Precisa de um save com Perfil no Nível 3.
	public static async Task CombatLevels(DevDriver d)
	{
		await EnsureProfile(d);
		await d.GoNearDummy();
		await d.Tap(Controls.Target);
		await d.Press("Escriba");
		await d.Press("Nível 2");
		await d.Press("Começar");
		d.Expect(d.Hud?.Presenter is WordPresenter, "o Escriba do 2º ano abriu a HUD da palavra, sem painel");
		await d.Press("Sugerir letra");
		await d.Shot("01-hud-palavra-letra-sugerida");
		await d.PlayWord();
		d.Expect(d.HasVisible("libertada") || d.HasVisible("certa"), "a última letra traçada conferiu a palavra");
		await d.Shot("02-palavra-certa");
		await d.Press("Sair do treino");

		await d.GoNearDummy();
		await d.Tap(Controls.Target);
		await d.Press("Escriba");
		await d.Press("Nível 3");
		await d.Press("Começar");
		d.Expect(d.Hud?.Presenter is NamePresenter, "o Escriba do 3º ano abriu a HUD da frase, sem painel");
		await d.Shot("03-hud-frase");
		await d.PlayName();
		await d.Shot("04-frase-certa");
		await d.Press("Sair do treino");
	}
}
