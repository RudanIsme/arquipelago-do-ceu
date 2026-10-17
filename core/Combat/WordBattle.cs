using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Missions;

namespace ArquipelagoDoCeu.Combat;

public enum WordResult { Incomplete, Freed, Mistaken }

// A luta do Escriba do 2º ano: a criança escreve a palavra inteira; o jogo só a confere (acento e cedilha contam).
// Escrever o que falta não é erro; só conferir uma palavra completa e errada é.
public sealed class WordBattle
{
	const int DefaultDistractors = 4;
	const string Alphabet = "abcdefghijklmnopqrstuvwxyz";

	readonly IReadOnlyList<Mission> missions;
	readonly Pcg32 rng;
	readonly MissionHistory? history;
	readonly List<char> typed = [];
	List<char> keys = [];
	char? hidden;

	WordBattle(IReadOnlyList<Mission> missions, Pcg32 rng, MissionHistory? history)
	{
		this.missions = missions;
		this.rng = rng;
		this.history = history;
		Mission = missions[0];
		Word = "";
	}

	public Mission Mission { get; private set; }

	public string Word { get; private set; }

	public string Image { get; private set; } = "";

	public string Typed => new(typed.ToArray());

	// As letras do teclado da tela: as da palavra, algumas a mais, embaralhadas pela seed.
	public IReadOnlyList<char> Keys => keys;

	public bool IsHidden(char key) => hidden == key;

	// Enquanto a palavra não for libertada falta 1 golpe; é o "escudo" do Escriba do 2º ano.
	public int Remaining { get; private set; } = 1;

	public static Result<WordBattle> Start(IEnumerable<Mission> available, Pcg32 rng, MissionHistory? history = null)
	{
		var usable = available.Where(m => m.Mechanic == "escrever_palavra").ToList();
		if (usable.Count == 0) return Result<WordBattle>.Fail("Não há Missão de escrever palavra para o Boneco.");

		var battle = new WordBattle(usable, rng, history);
		var first = MissionHistory.Narrow(history, usable);
		var dealt = battle.Deal(first[rng.NextInt(first.Count)]);
		return dealt.IsOk ? Result<WordBattle>.Ok(battle) : Result<WordBattle>.Fail(dealt.Error!);
	}

	// Outra palavra, de outra Missão quando houver mais de uma. Missão mal escrita é pulada.
	public void Next()
	{
		var others = missions.Where(m => m != Mission).ToList();
		var pool = MissionHistory.Narrow(history, others.Count > 0 ? others : missions);
		for (var attempt = 0; attempt < pool.Count; attempt++)
			if (Deal(pool[(rng.NextInt(pool.Count) + attempt) % pool.Count]).IsOk) return;
		Deal(Mission);
	}

	public bool Type(char letter)
	{
		if (!char.IsLetter(letter) || typed.Count >= Word.Length || hidden == char.ToLowerInvariant(letter)) return false;
		typed.Add(char.ToLowerInvariant(letter));
		return true;
	}

	public bool Backspace()
	{
		if (typed.Count == 0) return false;
		typed.RemoveAt(typed.Count - 1);
		return true;
	}

	public WordResult Check()
	{
		if (typed.Count < Word.Length) return WordResult.Incomplete;
		if (!string.Equals(Typed, Word, StringComparison.OrdinalIgnoreCase)) return WordResult.Mistaken;
		Remaining = 0;
		return WordResult.Freed;
	}

	// A letra sugerida quando a criança trava: a próxima da palavra, ou, se já escreveu uma errada, a que ela deve apagar.
	// Pedir nunca custa Energia.
	public (char Letter, bool Erase) Suggest()
	{
		for (var i = 0; i < typed.Count; i++)
			if (char.ToLowerInvariant(Word[i]) != typed[i]) return (typed[i], true);
		return typed.Count < Word.Length ? (char.ToLowerInvariant(Word[typed.Count]), false) : (char.ToLowerInvariant(Word[^1]), false);
	}

	// Travessura do Boneco: embaralha as letras da tela ou esconde uma delas por alguns segundos.
	public Prank PlayPrank()
	{
		if (keys.Count > 1 && rng.NextInt(2) == 0)
		{
			rng.Shuffle(keys);
			return Prank.Shuffled;
		}
		hidden = keys[rng.NextInt(keys.Count)];
		return Prank.Hidden;
	}

	public void Reveal() => hidden = null;

	Result<bool> Deal(Mission mission)
	{
		try
		{
			var word = mission.Params["word"].GetString()!.ToLowerInvariant();
			var image = mission.Params["image"].GetString()!;
			var extra = mission.Params.TryGetValue("distractors", out var d) ? d.GetInt32() : DefaultDistractors;
			if (word.Length == 0) return Result<bool>.Fail($"{mission.Id}: a palavra está vazia.");

			var letters = word.Distinct().ToList();
			var spare = Alphabet.Where(c => !letters.Contains(c)).ToList();
			rng.Shuffle(spare);
			var chosen = letters.Concat(spare.Take(Math.Max(0, extra))).ToList();
			rng.Shuffle(chosen);

			Mission = mission;
			history?.Add(mission.Id);
			Word = word;
			Image = image;
			keys = chosen;
			typed.Clear();
			hidden = null;
			Remaining = 1;
			return Result<bool>.Ok(true);
		}
		catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException)
		{
			return Result<bool>.Fail($"{mission.Id}: parâmetros de escrever palavra inválidos ({e.Message}).");
		}
	}
}
