using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace ArquipelagoDoCeu;

// Os auxiliares do piloto para o combate: jogam como a criança. Matemática e Português por sílaba se escrevem digitando (vale como
// desenhar) ou desenhando com o mouse; o dinheiro e as frases ainda escolhem a peça.
public partial class DevDriver
{
	const double TraceWaitSeconds = 0.8;

	public CombatHud? Hud => GetTree().Root.FindChildren("*", "", true, false).OfType<CombatHud>().FirstOrDefault(h => h.IsVisibleInTree());

	// A luta aberta (a do Treino ou a da Sala da Dungeon): o piloto lê dela a resposta certa, como um professor leria.
	TrainingArea? Fighting => GetTree().Root.FindChildren("*", "", true, false).OfType<TrainingArea>().FirstOrDefault(a => a.HasTarget);

	// Toca na peça (o dinheiro e as frases ainda escolhem peças).
	public async Task PlayPiece(int index)
	{
		if (Hud is null) { GD.PushError("Piloto: não há HUD de combate na tela."); return; }
		(GetTree().Root.FindChild($"Piece_{index}", true, false) as Button)?.EmitSignal(BaseButton.SignalName.Pressed);
		await Wait(0.3);
	}

	public int FightShield() => Fighting?.Session?.Math?.Shield.Remaining ?? -1;

	// Escreve como a criança que digita: cada tecla é um símbolo e, na pausa, o jogo lê.
	public async Task Write(string text)
	{
		foreach (var letter in Plain(text)) await PressKey((Key)letter);
		await Wait(TraceWaitSeconds + 0.4);
	}

	// Desenha de verdade, com o mouse, os glifos de `text` lado a lado no meio da tela, pelas trilhas, e espera o jogo ler.
	public async Task DrawText(string text)
	{
		var size = GetViewport().GetVisibleRect().Size;
		var area = new System.Numerics.Vector2(size.X * 0.5f, size.Y * 0.4f);
		var row = TrailSource.Shared.GuideRow(text, area, new ArquipelagoDoCeu.Rules.TracingRules(16, 30, 0.8f, 0.35f));
		if (!row.IsOk) { GD.PushError($"Piloto: sem trilha para '{text}'."); return; }
		var offset = new Vector2(size.X * 0.25f, size.Y * 0.38f - area.Y / 2f);
		foreach (var path in row.Value!.Paths!)
		{
			var points = path.Points.Select(p => offset + new Vector2(p.X, p.Y)).ToList();
			Click(points[0], true);
			foreach (var point in points.Skip(1))
			{
				Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point, ButtonMask = MouseButtonMask.Left });
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			}
			Click(points[^1], false);
		}
		await Wait(TraceWaitSeconds + 0.5);
	}

	// Joga o nome certo; com `wrong`, escreve uma sílaba que não é do nome. Frases ainda escolhem as peças.
	public async Task PlayName(bool wrong = false)
	{
		if (Fighting?.Session?.Name is not { } name) { GD.PushError("Piloto: não há montagem de nome na tela."); return; }
		if (name.Kind == ArquipelagoDoCeu.Combat.NameKind.Syllables)
		{
			await Write(wrong ? name.Pieces.First(p => !name.Name.Contains(p)) : string.Concat(name.Name));
			return;
		}
		var pieces = name.Pieces.ToList();
		var plan = name.Name.Select(syllable => pieces.IndexOf(syllable)).ToList();
		if (wrong) plan[^1] = pieces.FindIndex(p => !name.Name.Contains(p));
		foreach (var index in plan) await PlayPiece(index);
	}

	// Escreve a palavra do Desafio inteira.
	public async Task PlayWord()
	{
		if (Fighting?.Session?.Word is not { } word) { GD.PushError("Piloto: não há escrita de palavra na tela."); return; }
		await Write(word.Word);
	}

	// Uma resposta de Matemática: a conta de exemplo (ou o número, na conta pronta) e, com `wrong`, uma resposta que não dá o pedido.
	public async Task PlayMath(bool wrong = false)
	{
		if (Fighting is not { Session.Math: { } math } area) { GD.PushError("Piloto: não há luta de Matemática na tela."); return; }
		var target = math.StartingShield;
		if (math.Ask == ArquipelagoDoCeu.Combat.MathAsk.Answer) await Write((wrong ? target + 1 : target).ToString(System.Globalization.CultureInfo.InvariantCulture));
		else await Write(wrong ? $"1+{target}" : math.Example(area.Session!.Rules.MathLevel) ?? $"1+{target}");
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

	// Um erro: escreve uma resposta de Matemática que não dá o pedido, ou paga o valor errado no desafio de dinheiro.
	public async Task OvershootWithKeys()
	{
		if (Fighting?.Session?.Change is not null) { await PlayMoney(wrong: true); await Wait(0.4); return; }
		await PlayMath(wrong: true);
		await Wait(0.4);
	}

	static IEnumerable<char> Plain(string text) =>
		text.Normalize(System.Text.NormalizationForm.FormD).Where(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '-').Select(char.ToUpperInvariant);
}
