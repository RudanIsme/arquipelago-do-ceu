namespace ArquipelagoDoCeu.Saves;

// O que aconteceu ao registrar uma vitória num Conceito: se ele acabou de ser dominado e a Maestria do Papel agora.
public sealed record MasteryUpdate(bool NewlyMastered, int Mastery);

// A Maestria de cada Papel (a torre): quantos Conceitos ele já dominou.
public sealed class MasteryService(ProgressStore store)
{
	// Nunca cai: o Conceito dominado fica guardado, mesmo que a regra de quantas vitórias bastam mude depois,
	// e ficar sem jogar não tira nada.
	public int Of(string profileId, string discipline) =>
		(store.Of(profileId)?.Mastered ?? []).Count(key => key.StartsWith(discipline + ":", StringComparison.Ordinal));

	// Conta uma vitória no Conceito, em Dungeon. O que vale é o Desafio vencido, e não a pressa nem as tentativas até vencê-lo.
	public Result<MasteryUpdate> Record(string profileId, ConceptKey concept, int solvesToMaster)
	{
		var key = concept.ToString();
		if (store.Find(profileId) is null) return Result<MasteryUpdate>.Fail("Perfil não encontrado.");
		var current = store.Of(profileId);
		if ((current?.Mastered ?? []).Contains(key)) return Result<MasteryUpdate>.Ok(new MasteryUpdate(false, Of(profileId, concept.Discipline)));

		var solves = (current?.Solved ?? new Dictionary<string, int>()).GetValueOrDefault(key) + 1;
		var mastered = solves >= solvesToMaster;
		var saved = store.Update(profileId, p => p with
		{
			Solved = new Dictionary<string, int>(p.Solved ?? new Dictionary<string, int>()) { [key] = solves },
			Mastered = mastered ? [.. p.Mastered ?? [], key] : p.Mastered,
		});
		return saved.IsOk ? Result<MasteryUpdate>.Ok(new MasteryUpdate(mastered, Of(profileId, concept.Discipline))) : Result<MasteryUpdate>.Fail(saved.Error!);
	}

	// Quantas vitórias o Conceito já tem, dominado ou não.
	public int Solves(string profileId, ConceptKey concept) =>
		(store.Of(profileId)?.Solved ?? new Dictionary<string, int>()).GetValueOrDefault(concept.ToString());
}

// O saldo de moedas do Perfil: só do jogo, só soma, e nunca se perde.
public sealed class WalletService(ProgressStore store)
{
	public int Balance(string profileId) => store.Of(profileId)?.Coins ?? 0;

	// Sem teto de regra (só o do próprio número, que nunca vira negativo). Valor zero ou negativo não grava nada.
	public Result<ProfileSave> Add(string profileId, int amount)
	{
		if (store.Find(profileId) is not { } profile) return Result<ProfileSave>.Fail("Perfil não encontrado.");
		return amount <= 0 ? Result<ProfileSave>.Ok(profile) : store.Update(profileId, p => p with { Coins = (int)Math.Min((long)(p.Coins ?? 0) + amount, int.MaxValue) });
	}
}
