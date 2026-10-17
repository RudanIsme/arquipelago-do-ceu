using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace ArquipelagoDoCeu;

// Os auxiliares do piloto para o combate: jogam como a criança, escolhendo a peça e traçando o que ela pede (digitar vale como traçar).
public partial class DevDriver
{
	const double TraceWaitSeconds = 0.8;

	public CombatHud? Hud => GetTree().Root.FindChildren("*", "", true, false).OfType<CombatHud>().FirstOrDefault(h => h.IsVisibleInTree());

	// A luta aberta (a do Treino ou a da Sala da Dungeon): o piloto lê dela a resposta certa, como um professor leria.
	TrainingArea? Fighting => GetTree().Root.FindChildren("*", "", true, false).OfType<TrainingArea>().FirstOrDefault(a => a.HasTarget);

	// Toca na peça e, se pedido, traça o que ela pede. Sem `trace`, para no começo do traço.
	public async Task PlayPiece(int index, bool trace = true)
	{
		if (Hud is not { } hud) { GD.PushError("Piloto: não há HUD de combate na tela."); return; }
		var text = hud.Presenter.TraceText(index);
		(GetTree().Root.FindChild($"Piece_{index}", true, false) as Button)?.EmitSignal(BaseButton.SignalName.Pressed);
		await Wait(0.3);
		if (!trace || text is null) return;
		foreach (var letter in text) await PressKey((Key)char.ToUpperInvariant(letter));
		await Wait(TraceWaitSeconds);
	}

	public int FightShield() => Fighting?.Session?.Math?.Shield.Remaining ?? -1;

	// Joga o nome certo, peça por peça; com `wrong`, troca a última por uma que não é do nome.
	public async Task PlayName(bool wrong = false)
	{
		if (Fighting?.Session?.Name is not { } name) { GD.PushError("Piloto: não há montagem de nome na tela."); return; }
		var pieces = name.Pieces.ToList();
		var plan = name.Name.Select(syllable => pieces.IndexOf(syllable)).ToList();
		if (wrong) plan[^1] = pieces.FindIndex(p => !name.Name.Contains(p));
		foreach (var index in plan) await PlayPiece(index);
	}

	// Escreve a palavra do Desafio, letra por letra (só letras sem acento).
	public async Task PlayWord()
	{
		if (Fighting?.Session?.Word is not { } word) { GD.PushError("Piloto: não há escrita de palavra na tela."); return; }
		var keys = word.Keys.ToList();
		foreach (var letter in word.Word) await PlayPiece(keys.IndexOf(letter));
	}

	// Monta o valor do desafio de dinheiro seguindo as sugestões do jogo e confere; com `wrong`, põe a maior nota e confere.
	public async Task PlayMoney(bool wrong = false)
	{
		if (Fighting?.Session?.Change is not { } battle) { GD.PushError("Piloto: não há desafio de dinheiro na tela."); return; }
		if (wrong)
		{
			await PlayPiece(battle.Pieces.Count - 1);
			await Press("Conferir");
			return;
		}
		for (var step = 0; step < 40 && battle.Total != battle.Target; step++)
		{
			var (piece, undo) = battle.Suggest();
			if (undo || piece is null) break;
			await PlayPiece(piece.Value);
		}
		await Press("Conferir");
	}

	// Um erro de Matemática: monta a maior conta que a mão deixa. Se ela ainda não passa do escudo (seria um acerto), o erro
	// vem pela mesma saída da luta, para o roteiro testar a Energia e a travessura sem depender da mão sorteada.
	public async Task OvershootWithKeys()
	{
		if (Fighting?.Session?.Change is not null) { await PlayMoney(wrong: true); await Wait(0.4); return; }
		if (Fighting is not { Session.Math: { } math } area) { GD.PushError("Piloto: não há luta de Matemática na tela."); return; }
		var hand = Enumerable.Range(0, math.Bag.Hand.Count).Where(slot => math.Bag.Hand[slot] is not null).OrderByDescending(slot => math.Bag.Hand[slot]).Take(2).ToList();
		var sum = hand.Sum(slot => math.Bag.Hand[slot]!.Value);
		if (hand.Count < 2 || sum <= math.Shield.Remaining)
		{
			area.Mistake("Passou do valor!", "O Boneco fez uma travessura.");
			await Wait(0.4);
			return;
		}
		await PlayPiece(hand[0]);
		await PlayPiece(math.Bag.Hand.Count);
		await PlayPiece(hand[1]);
		await Wait(0.4);
	}
}
