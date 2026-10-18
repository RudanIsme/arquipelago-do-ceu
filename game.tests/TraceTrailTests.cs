using System.Numerics;
using ArquipelagoDoCeu.Combat;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class TraceTrailTests
{
	static readonly Vector2 Canvas = new(392, 270);

	static string Data(string name) => File.ReadAllText(Path.Combine(MissionFiles.RepositoryRoot(), "game", "data", name));

	static readonly TraceTrails Trails = TraceTrails.Parse(Data("trilhas.json"), Data("trilhas.schema.json")).Value!;

	static TraceGuide GuideOf(string glyph) => Trails.Guide(glyph, Canvas, TrainingFixtures.Rules.Tracing).Value!;

	[Fact]
	public void Os_numeros_de_0_a_9_e_os_sinais_tem_trilha_com_inicio_e_direcao()
	{
		foreach (var glyph in "0123456789+-".Select(c => c.ToString()))
		{
			var trail = Trails.Find(glyph);
			Assert.NotNull(trail);
			Assert.All(trail.Strokes, s => Assert.True(s.Points.Count >= 2 && s.Points[0] != s.Points[1], glyph));
		}
	}

	[Fact]
	public void As_letras_de_A_a_Z_tem_trilha_com_inicio_e_direcao()
	{
		foreach (var letter in Enumerable.Range('A', 26).Select(c => ((char)c).ToString()))
		{
			var trail = Trails.Find(letter);
			Assert.NotNull(trail);
			Assert.All(trail.Strokes, s => Assert.True(s.Points.Count >= 2 && s.Points[0] != s.Points[1], letter));
		}
	}

	[Fact]
	public void Seguir_a_propria_trilha_passa_em_todos_os_glifos()
	{
		foreach (var glyph in Trails.Glyphs)
		{
			var guide = GuideOf(glyph);
			IReadOnlyList<IReadOnlyList<Vector2>> strokes = [.. guide.Paths!.Select(p => p.Points)];

			Assert.True(Tracing.Check(guide, strokes, TrainingFixtures.Rules.Tracing).Passed, glyph);
		}
	}

	[Fact]
	public void Trilhas_de_letras_e_de_numeros_somam_38_glifos()
	{
		Assert.Equal(38, Trails.Glyphs.Count());
	}

	[Fact]
	public void O_guia_segue_a_trilha_traco_por_traco_dentro_da_area_de_desenho()
	{
		var guide = GuideOf("4");

		Assert.Equal(2, guide.Paths!.Count);
		Assert.Equal(guide.Dots.Count, guide.Paths.Sum(p => p.DotCount));
		Assert.Equal(0, guide.Paths[0].FirstDot);
		Assert.Equal(guide.Paths[0].DotCount, guide.Paths[1].FirstDot);
		Assert.Equal(guide.Paths[0].Points[0], guide.Dots[0]);
		Assert.All(guide.Dots, d => Assert.True(d.X >= 0 && d.X <= Canvas.X && d.Y >= 0 && d.Y <= Canvas.Y));
	}

	[Fact]
	public void Os_pontos_do_guia_ficam_a_um_espacamento_do_outro()
	{
		var spacing = TrainingFixtures.Rules.Tracing.DotSpacing;
		var guide = GuideOf("1");
		var gaps = guide.Dots.Zip(guide.Dots.Skip(1), Vector2.Distance).ToList();

		// Numa quina o espaço encurta, mas nunca passa do espaçamento.
		Assert.All(gaps, d => Assert.InRange(d, 1f, spacing * 1.05f));
		Assert.True(gaps.Count(d => d >= spacing * 0.9f) >= gaps.Count / 2);
	}

	[Fact]
	public void Traco_torto_com_o_glifo_certo_passa()
	{
		var guide = GuideOf("4");
		var wobble = new Vector2(7, -6);
		var strokes = guide.Paths!.Select(p => (IReadOnlyList<Vector2>)p.Points.Select((pt, i) => pt + (i % 2 == 0 ? wobble : -wobble)).ToList()).ToList();

		var check = Tracing.Check(guide, strokes, TrainingFixtures.Rules.Tracing);

		Assert.True(check.Passed);
	}

	[Fact]
	public void Cobertura_insuficiente_pede_nova_tentativa_e_o_glifo_errado_nao_passa()
	{
		var guide = GuideOf("1");
		var half = guide.Paths![0].Points.Take(2).ToList();
		var wrong = Trails.Guide("7", Canvas, TrainingFixtures.Rules.Tracing).Value!.Paths![0].Points;

		Assert.False(Tracing.Check(guide, [half], TrainingFixtures.Rules.Tracing).Passed);
		Assert.False(Tracing.Check(guide, [wrong], TrainingFixtures.Rules.Tracing).Passed);
		Assert.False(Tracing.Check(guide, [], TrainingFixtures.Rules.Tracing).Passed);
	}

	[Fact]
	public void Trilha_que_nao_existe_vira_resultado_de_falha()
	{
		Assert.False(Trails.Guide("@", Canvas, TrainingFixtures.Rules.Tracing).IsOk);
		Assert.False(TraceTrails.Empty.Guide("1", Canvas, TrainingFixtures.Rules.Tracing).IsOk);
	}

	[Theory]
	[InlineData("{")]
	[InlineData("""{"version":1,"glyphs":[]}""")]
	[InlineData("""{"version":2,"glyphs":[{"glyph":"1","strokes":[[[0,0],[1,1]]]}]}""")]
	[InlineData("""{"version":1,"glyphs":[{"glyph":"1","strokes":[[[0,0]]]}]}""")]
	[InlineData("""{"version":1,"glyphs":[{"glyph":"1","strokes":[[[0,0],[2,1]]]}]}""")]
	[InlineData("""{"version":1,"glyphs":[{"glyph":"12","strokes":[[[0,0],[1,1]]]}]}""")]
	[InlineData("""{"version":1,"glyphs":[{"glyph":"1","strokes":[[[0.5,0.5],[0.51,0.5]]]}]}""")]
	[InlineData("""{"version":1,"glyphs":[{"glyph":"1","strokes":[[[0,0],[1,1]]]},{"glyph":"1","strokes":[[[0,0],[1,0]]]}]}""")]
	public void Trilha_invalida_vira_resultado_de_falha_e_nunca_excecao(string json)
	{
		Assert.False(TraceTrails.Parse(json, Data("trilhas.schema.json")).IsOk);
	}

	[Fact]
	public void Schema_quebrado_tambem_vira_falha()
	{
		Assert.False(TraceTrails.Parse(Data("trilhas.json"), "{").IsOk);
	}

	[Fact]
	public void Duas_trilhas_validas_pequenas_carregam()
	{
		var json = """{"version":1,"glyphs":[{"glyph":"i","strokes":[[[0.5,0.1],[0.5,0.9]]]},{"glyph":"-","strokes":[[[0.2,0.5],[0.8,0.5]]]}]}""";

		var parsed = TraceTrails.Parse(json, Data("trilhas.schema.json"));

		Assert.True(parsed.IsOk);
		Assert.NotNull(parsed.Value!.Find("I"));
		Assert.NotNull(parsed.Value.Find("i"));
	}

	[Fact]
	public void A_conta_de_exemplo_vira_uma_fileira_de_glifos_da_esquerda_para_a_direita()
	{
		var row = Trails.GuideRow("3+5", Canvas, TrainingFixtures.Rules.Tracing).Value!;

		Assert.Equal(GuideOf("3").Paths!.Count + GuideOf("+").Paths!.Count + GuideOf("5").Paths!.Count, row.Paths!.Count);
		var firstOfEach = row.Paths.Select(p => p.Points[0].X).ToList();
		Assert.True(firstOfEach[0] < firstOfEach[^1]);
		Assert.All(row.Dots, d => Assert.InRange(d.X, 0f, Canvas.X));
		Assert.Equal(row.Paths.Sum(p => p.DotCount), row.Dots.Count);
	}

	[Fact]
	public void A_fileira_ignora_o_acento_e_o_cedilha_e_mostra_a_letra()
	{
		var plain = Trails.GuideRow("CAO", Canvas, TrainingFixtures.Rules.Tracing).Value!;
		var accented = Trails.GuideRow("ÇÃO", Canvas, TrainingFixtures.Rules.Tracing).Value!;

		Assert.Equal(plain.Dots.Count, accented.Dots.Count);
	}

	[Fact]
	public void A_fileira_ignora_o_que_nao_tem_trilha_e_falha_se_nada_tem()
	{
		Assert.True(Trails.GuideRow("a b", Canvas, TrainingFixtures.Rules.Tracing).IsOk);
		Assert.False(Trails.GuideRow("  ", Canvas, TrainingFixtures.Rules.Tracing).IsOk);
	}
}
