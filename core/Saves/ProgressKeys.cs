namespace ArquipelagoDoCeu.Saves;

// Os identificadores do progresso. O texto gravado no save ("matematica:1:2") nasce e é lido só aqui, para o formato não se espalhar.
public readonly record struct LevelKey(string Discipline, int Level)
{
	public override string ToString() => $"{Discipline}:{Level}";
}

// Um Andar de um Nível de uma torre: também é o ponto de um Fast travel.
public readonly record struct FloorKey(string Discipline, int Level, int Floor)
{
	public LevelKey Of => new(Discipline, Level);

	public override string ToString() => $"{Discipline}:{Level}:{Floor}";

	public static FloorKey? TryParse(string text)
	{
		var parts = text.Split(':');
		return parts.Length == 3 && int.TryParse(parts[1], out var level) && int.TryParse(parts[2], out var floor)
			? new FloorKey(parts[0], level, floor)
			: null;
	}
}

// Um Conceito de um Papel (a torre): é a chave das vitórias e da Maestria.
public readonly record struct ConceptKey(string Discipline, string Concept)
{
	public override string ToString() => $"{Discipline}:{Concept}";
}
