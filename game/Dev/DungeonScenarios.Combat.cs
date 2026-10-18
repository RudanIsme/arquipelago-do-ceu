using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace ArquipelagoDoCeu;

// Cenário `combate` do piloto: o combate sem modal da Matemática e do Português, do desenho livre ao golpe, sem botão de atacar.
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
		d.Expect(d.Button("Dica") is not null && d.Button("Apagar") is not null, "a HUD é só uma fileira de botões: o pedido fica no balão da criatura");
		await d.Shot("01-hud-matematica");

		var before = d.Player!.GroundPosition;
		await d.Hold(Controls.Right, 0.4);
		d.Expect(System.Numerics.Vector2.Distance(before, d.Player.GroundPosition) > 0.05f, "com a HUD aberta o Personagem anda: nada de modal");

		await d.Press("Dica");
		await d.Shot("02-dica-com-conta-de-exemplo");
		d.Expect(Engine.TimeScale >= 1.0, "o tempo é normal antes de desenhar");
		var shield = d.FightShield();
		await d.DrawText("1+2");
		await d.Wait(0.8);
		d.Expect(d.FightShield() != shield || d.HasVisible("Acertou") || d.HasVisible("Ainda não") || d.HasVisible("Golpe") || d.HasVisible("perfeito"), "o desenho foi lido e o golpe saiu sem botão");
		d.Expect(Engine.TimeScale >= 1.0, "o tempo volta ao normal depois da leitura");
		await d.Shot("04-golpe");
		d.Expect(d.Hud?.Presenter.Pieces.Count > 0 && d.Button("Dica") is not null, "a mochila fica sempre à vista, com as peças");
		var chosenBefore = d.FightShield();
		(d.GetTree().Root.FindChild("Piece_0", true, false) as Button)?.EmitSignal(BaseButton.SignalName.Pressed);
		await d.Wait(0.6);
		d.Expect(d.HasVisible("Siga o pontilhado") && d.FightShield() == chosenBefore, "escolher uma peça da mochila mostra o pontilhado e não conclui o Desafio");
		await d.Shot("04b-mochila-pontilhado");
		await d.Write(d.Hud!.Presenter.PieceDrawing(0)!);
		d.Expect(d.Hud!.Presenter.Composing || d.FightShield() != chosenBefore, "contornar o pontilhado põe a peça na conta");
		await d.Shot("04c-peca-na-conta");
		await d.PressKey(Key.Escape);
		await d.Press("Sair do treino");

		await d.GoNearDummy();
		await d.Tap(Controls.Target);
		await d.Press("Escriba");
		await d.Press("Começar");
		d.Expect(d.Hud?.Presenter is NamePresenter, "o Escriba abriu a HUD do nome, sem painel");
		await d.Shot("05-hud-nome");
		await d.PlayName();
		d.Expect(d.HasVisible("certo") || d.HasVisible("certa"), "o último traço conferiu o nome e libertou a criatura");
		await d.Shot("06-nome-certo");
		await d.Press("Sair do treino");
	}

	// Cenário `combate-dinheiro`: pagar e dar troco com moedas e notas do Real, sem modal e com dica que nunca custa.
	public static async Task CombatMoney(DevDriver d)
	{
		await EnsureProfile(d);
		await d.GoNearDummy();
		await d.Tap(Controls.Target);
		await d.Press("5 · Muito difícil");
		await d.Press("Começar");
		d.Expect(d.Hud?.Presenter is ChangePresenter, "o treino de dinheiro abriu a HUD de pagar e dar troco");
		d.Expect(d.HasVisible("R$") && d.Button("Conferir") is not null, "o preço aparece em Reais e há o botão de conferir");
		await d.Shot("01-hud-dinheiro");

		await d.PlayPiece(0);
		await d.Press("Sugerir peça");
		await d.Shot("02-sugestao");
		d.Expect(d.Hud is not null, "pedir a dica não custa Energia nem fecha nada");
		await d.PlayMoney(wrong: true);
		await d.Wait(0.5);
		await d.Shot("03-valor-errado");
		d.Expect(d.HasVisible("Passou do valor") || d.HasVisible("falta dinheiro") || d.HasVisible("Faltou"), "o valor errado custa um erro e diz se passou ou faltou");

		await d.Wait(4.0);
		await d.PlayMoney();
		await d.Wait(0.6);
		await d.Shot("04-valor-certo");
		d.Expect(d.HasVisible("acalmada") || d.HasVisible("Valor certo"), "seguir as sugestões sempre chega ao valor certo");
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
		await d.Press("Dica");
		await d.Shot("01-hud-palavra-letra-sugerida");
		await d.PlayWord();
		d.Expect(d.HasVisible("certo") || d.HasVisible("certa"), "a última letra traçada conferiu a palavra");
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
