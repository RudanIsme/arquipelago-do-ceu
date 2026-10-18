using Godot;
using System.Collections.Generic;

namespace ArquipelagoDoCeu;

// Uma peça da bolsa na HUD de combate. `Dots` é a quantidade em bolinhas (só números); `Name` diz a peça em voz e no nome do botão.
public sealed record HudPiece(string Label, string Name, bool Enabled, int Dots = 0, bool Highlighted = false);

// O que cada Mecânica mostra e faz na HUD de combate. A HUD escolhe e traça as peças; o apresentador sabe de qual peça se trata,
// o que a criança traça, quando a montagem está completa e como atacar. Uma Mecânica nova é um apresentador novo.
public interface ICombatPresenter
{
	// O nome da criatura e o que falta vencer, para a faixa do pedido.
	string Heading { get; }

	string Prompt { get; }

	// O que a criança já montou (a conta, o nome, a palavra), sem o resultado: somar e escrever é com ela.
	string Assembled { get; }

	IReadOnlyList<HudPiece> Pieces { get; }

	// Verdadeiro com peças montadas: é quando o tempo desacelera.
	bool Composing { get; }

	// O Desafio já foi vencido e espera o próximo.
	bool Resolved { get; }

	string ResolvedText { get; }

	// A montagem está completa: o próximo passo é o golpe, sem botão de confirmar.
	bool Complete { get; }

	// Só a Mecânica de palavra usa letras do teclado: com ela aberta, WASD não anda.
	bool TypesLetters { get; }

	// O texto de ajuda do teclado desta Mecânica.
	string KeyHelp { get; }

	string? ExtraLabel { get; }

	// Mecânicas com número livre de peças (o dinheiro) conferem por um botão; as outras conferem na última ação.
	string? ConfirmLabel => null;

	bool CanConfirm => false;

	// A fala de quando a criança escolhe a peça.
	string Announce(int piece);

	void Place(int piece);

	bool Undo();

	// Solta a montagem sem responder: as peças voltam para a mão.
	void Cancel();

	// Confere e golpeia. Falso quando a conta não vale (`error` diz por quê) e a montagem deve recomeçar.
	bool Attack(out string? error);

	// O pedido curto que a criatura mostra no balão dela; nulo deixa a legenda de sempre.
	string? Request => null;

	// Matemática e Português por sílaba se escrevem desenhando em qualquer lugar da tela, sem bolsa nem painel.
	bool FreeDraw => false;

	// Os caracteres que o desenho pode ser (o 0 e a letra O têm a mesma forma; o que a Missão aceita decide).
	string DrawAllowed => "";

	// O que o pontilhado mostra agora (a sílaba que falta) e se ele fica sempre à vista neste Nível; fora disso, só na Dica.
	string? GuideText => null;

	bool GuideAlways => false;

	// O que a Dica mostra em pontilhado: uma conta de exemplo ou a sílaba. Pedir nunca custa Energia.
	string? HintGuide => null;

	// Recebe o que a criança escreveu. Verdadeiro quando valeu como resposta (a luta reage); falso com o motivo em `message`, sem custo.
	bool Write(string text, out string? message)
	{
		message = null;
		return false;
	}

	// O atalho de teclado: qual peça esta tecla escolhe.
	int? PieceForKey(InputEventKey key);

	// A ajuda extra da Mecânica (a sugestão de letra); devolve o que dizer.
	string? Extra();
}
