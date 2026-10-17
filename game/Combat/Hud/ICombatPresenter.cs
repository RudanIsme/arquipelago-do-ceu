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

	// O que a criança traça para esta peça, ou null quando a peça entra direto, sem traço.
	string? TraceText(int piece);

	// A fala de quando a criança escolhe a peça.
	string Announce(int piece);

	string TraceLabel(int piece);

	void Place(int piece);

	bool Undo();

	// Solta a montagem sem responder: as peças voltam para a mão.
	void Cancel();

	// Confere e golpeia. Falso quando a conta não vale (`error` diz por quê) e a montagem deve recomeçar.
	bool Attack(out string? error);

	// O atalho de teclado: qual peça esta tecla escolhe.
	int? PieceForKey(InputEventKey key);

	// A ajuda extra da Mecânica (a sugestão de letra); devolve o que dizer.
	string? Extra();
}
