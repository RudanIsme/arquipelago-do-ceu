namespace ArquipelagoDoCeu;

// Como o jogo chama a criatura que a criança enfrenta: no Lobby é o Boneco de treino, e nas Dungeons é uma criatura da névoa.
// Guarda o artigo e o pronome certos para as frases não saírem tortas em português.
// `Kind` liga a criatura ao ritmo de ataque dos dados (combat.rhythm).
public sealed record CreatureWords(string Kind, string The, string Of, string Pronoun, string Title)
{
	public static readonly CreatureWords Dummy = new("dummy", "o Boneco", "do Boneco", "lo", "Boneco de treino");

	// No Puzzle o alvo é uma porta ou ponte trancada, sem criatura.
	public static readonly CreatureWords Gate = new("gate", "a porta", "da porta", "la", "Porta trancada");

	// No Desafio mestre o Boneco vira o Guardião do Nível.
	public static readonly CreatureWords Guardian = new("guardian", "o Guardião", "do Guardião", "lo", "Guardião do Nível");

	public static readonly CreatureWords Wild = new("wild", "a criatura", "da criatura", "la", "Criatura da névoa");

	// "o Boneco" no começo de uma frase.
	public string Start => char.ToUpperInvariant(The[0]) + The[1..];
}
