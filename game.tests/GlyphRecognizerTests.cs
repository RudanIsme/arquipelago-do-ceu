using System.Numerics;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Rules;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class GlyphRecognizerTests
{
	static string Data(string name) => File.ReadAllText(Path.Combine(MissionFiles.RepositoryRoot(), "game", "data", name));

	static readonly TraceTrails Trails = TraceTrails.Parse(Data("trilhas.json"), Data("trilhas.schema.json")).Value!;
	static readonly FreeDrawRules Rules = FreeDrawRules.Default;
	static readonly GlyphRecognizer Recognizer = new(Trails, Rules);
	const string Digits = "0123456789+-";
	const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

	// Um traço torto de criança: a trilha com tremor, inclinação e proporção mudada, em pixels.
	static List<IReadOnlyList<Vector2>> Crooked(string glyph, Vector2 origin, float size, int seed)
	{
		const float wobble = 0.035f;
		return [.. Trails.Find(glyph)!.Strokes.Select((stroke, s) => (IReadOnlyList<Vector2>)[.. stroke.Points.Select((p, i) =>
		{
			var jitter = new Vector2(MathF.Sin((i + seed) * 1.7f + s), MathF.Cos((i * 2.3f) + seed)) * wobble;
			var q = p + jitter;
			return origin + new Vector2((q.X + (q.Y - 0.5f) * 0.12f) * 0.85f, q.Y) * size;
		})])];
	}

	// Ler errado custa Energia, e pedir de novo não custa: o jogo pode duvidar, mas quando afirma não pode errar.
	[Fact]
	public void Numeros_e_sinais_tortos_quase_sempre_sao_lidos_e_nunca_lidos_errado()
	{
		var sure = 0;
		var total = 0;
		foreach (var glyph in Digits.Select(c => c.ToString()))
			foreach (var seed in new[] { 1, 2, 4 })
			{
				var guess = Recognizer.Recognize(Crooked(glyph, new Vector2(100, 80), 200, seed), Digits);
				total++;
				Assert.NotEqual(GuessKind.Unknown, guess.Kind);
				Assert.Equal(glyph, guess.Glyph);
				if (guess.Kind == GuessKind.Sure) sure++;
			}
		Assert.True(sure >= total * 0.9, $"{sure} de {total} lidos com segurança");
	}

	[Fact]
	public void Letras_tortas_nunca_sao_lidas_com_segurança_erradas()
	{
		var sure = 0;
		foreach (var glyph in Letters.Select(c => c.ToString()))
		{
			var guess = Recognizer.Recognize(Crooked(glyph, new Vector2(40, 60), 150, 2), Letters);
			Assert.NotEqual(GuessKind.Unknown, guess.Kind);
			if (guess.Kind != GuessKind.Sure) continue;
			sure++;
			Assert.Equal(glyph, guess.Glyph);
		}
		Assert.True(sure >= Letters.Length * 0.7, $"{sure} de {Letters.Length} lidas com segurança");
	}

	[Fact]
	public void A_ordem_e_a_direcao_dos_tracos_nao_importam()
	{
		var strokes = Crooked("4", new Vector2(0, 0), 200, 1);
		strokes.Reverse();
		IReadOnlyList<IReadOnlyList<Vector2>> flipped = [.. strokes.Select(s => (IReadOnlyList<Vector2>)[.. s.Reverse()])];

		Assert.Equal("4", Recognizer.Recognize(flipped, Digits).Glyph);
	}

	[Fact]
	public void O_tamanho_e_a_posicao_na_tela_nao_importam()
	{
		Assert.Equal("5", Recognizer.Recognize(Crooked("5", new Vector2(900, 400), 60, 3), Digits).Glyph);
		Assert.Equal("5", Recognizer.Recognize(Crooked("5", new Vector2(10, 10), 600, 3), Digits).Glyph);
	}

	[Fact]
	public void Um_1_com_a_bandeira_pela_metade_e_um_7_nunca_vira_resposta_segura()
	{
		var half = new List<IReadOnlyList<Vector2>> { new Vector2[] { new(0.285f, 0.2f), new(0.65f, 0.11f), new(0.47f, 0.9f) }.Select(p => p * 200).ToList() };

		var guess = Recognizer.Recognize(half, Digits);

		Assert.NotEqual(GuessKind.Sure, guess.Kind);
	}

	[Fact]
	public void Rabisco_sem_forma_nao_e_reconhecido()
	{
		var scribble = new List<IReadOnlyList<Vector2>> { Enumerable.Range(0, 60).Select(i => new Vector2(i * 37 % 91, i * 53 % 83) * 3).ToList() };

		Assert.NotEqual(GuessKind.Sure, Recognizer.Recognize(scribble, Digits).Kind);
	}

	[Fact]
	public void Sem_nenhum_traco_nao_ha_o_que_reconhecer()
	{
		Assert.Equal(GuessKind.Unknown, Recognizer.Recognize([], Digits).Kind);
	}

	[Fact]
	public void So_os_glifos_permitidos_entram_na_disputa()
	{
		var seven = Crooked("7", new Vector2(0, 0), 200, 1);

		Assert.Equal("7", Recognizer.Recognize(seven, Digits).Glyph);
		Assert.DoesNotContain(Recognizer.Rank(seven, "ABC").Select(r => r.Glyph), g => g == "7");
	}

	// Um 3 de verdade, feito de um traço só com o topo reto: o 8 e o 5 chegam perto, mas o 3 não pode ficar em dúvida.
	[Fact]
	public void Um_3_de_topo_reto_feito_num_traco_so_e_reconhecido()
	{
		Vector2[] points = [new(810, 305), new(900, 303), new(950, 315), new(970, 350), new(955, 400), new(900, 428), new(850, 432), new(900, 438), new(975, 465), new(1010, 520), new(985, 565), new(930, 590), new(860, 590), new(800, 575)];

		var guess = Recognizer.Recognize([points], Digits);

		Assert.Equal(GuessKind.Sure, guess.Kind);
		Assert.Equal("3", guess.Glyph);
	}
}
