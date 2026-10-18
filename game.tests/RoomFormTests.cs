using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.World;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

// O catálogo de formas de Sala (#150): em dados, feitas de hexágonos e validadas na carga.
public class RoomFormTests
{
	public static readonly RoomForms Forms =
		RoomForms.Parse(File.ReadAllText(Path.Combine(MissionFiles.RepositoryRoot(), "game", "data", "formas-de-sala.json"))).Value!;

	[Fact]
	public void O_catalogo_traz_pequena_media_grande_alongada_em_L_anel_e_desnivel()
	{
		Assert.Equal(["pequena", "media", "grande", "alongada", "em-l", "anel", "desnivel"], Forms.All.Select(f => f.Id));
	}

	[Fact]
	public void Pequena_media_e_grande_crescem_e_a_media_e_a_Sala_de_sempre()
	{
		var counts = new[] { "pequena", "media", "grande" }.Select(id => Forms.Find(id)!.Cells.Count).ToList();

		Assert.True(counts[0] < counts[1] && counts[1] < counts[2]);
		Assert.Equal(37, counts[1]);
		Assert.Same(Forms.Find("media"), Forms.Legacy);
	}

	[Fact]
	public void Toda_forma_e_uma_so_peca_de_hexagonos_com_o_centro_pisavel()
	{
		foreach (var form in Forms.All)
		{
			Assert.Contains(form.Center, form.Cells);
			var seen = new HashSet<HexCell> { form.Center };
			var queue = new Queue<HexCell>([form.Center]);
			while (queue.Count > 0)
				foreach (var next in queue.Dequeue().Neighbors().Where(form.Cells.Contains))
					if (seen.Add(next)) queue.Enqueue(next);
			Assert.True(seen.Count == form.Cells.Count, $"{form.Id} tem hexágonos soltos");
		}
	}

	[Fact]
	public void A_alongada_e_mais_comprida_que_larga_e_o_anel_tem_buraco()
	{
		var strip = Forms.Find("alongada")!;
		var xs = strip.Cells.Select(c => HexGrid.ToWorld(c).X).ToList();
		var ys = strip.Cells.Select(c => HexGrid.ToWorld(c).Y).ToList();
		Assert.True(ys.Max() - ys.Min() > 1.5f * (xs.Max() - xs.Min()));

		var ring = Forms.Find("anel")!;
		Assert.DoesNotContain(new HexCell(0, 0), ring.Cells);
		Assert.Contains(ring.Center, ring.Cells);
	}

	[Fact]
	public void O_desnivel_tem_mais_de_uma_altura_e_as_outras_formas_sao_planas()
	{
		Assert.True(Forms.Find("desnivel")!.Heights.Values.Distinct().Count() > 1);
		Assert.All(Forms.All.Where(f => f.Id != "desnivel"), f => Assert.Empty(f.Heights));
	}

	[Theory]
	[InlineData("{ \"forms\": [] }", "nenhuma forma")]
	[InlineData("{ \"forms\": [{ \"id\": \"\", \"name\": \"x\", \"parts\": [{ \"a\": 1, \"b\": 1, \"c\": 1 }] }] }", "id")]
	[InlineData("{ \"forms\": [{ \"id\": \"x\", \"name\": \"x\", \"parts\": [] }] }", "parts")]
	[InlineData("{ \"forms\": [{ \"id\": \"x\", \"name\": \"x\", \"parts\": [{ \"a\": 0, \"b\": 1, \"c\": 1 }] }] }", "a, b e c")]
	[InlineData("{ \"forms\": [{ \"id\": \"x\", \"name\": \"x\", \"parts\": [{ \"a\": 1, \"b\": 1, \"c\": 1 }, { \"a\": 1, \"b\": 1, \"c\": 1, \"q\": 9, \"r\": 0 }] }] }", "solto")]
	[InlineData("{ \"forms\": [{ \"id\": \"x\", \"name\": \"x\", \"parts\": [{ \"a\": 1, \"b\": 1, \"c\": 1 }], \"center\": [5, 5] }] }", "centro")]
	[InlineData("{ \"forms\": [{ \"id\": \"media\", \"name\": \"x\", \"parts\": [{ \"a\": 1, \"b\": 1, \"c\": 1 }] }, { \"id\": \"media\", \"name\": \"y\", \"parts\": [{ \"a\": 1, \"b\": 1, \"c\": 1 }] }] }", "mais de uma vez")]
	[InlineData("{ \"forms\": [{ \"id\": \"x\", \"name\": \"x\", \"parts\": [{ \"a\": 1, \"b\": 1, \"c\": 1 }] }] }", "media")]
	[InlineData("não é json", "JSON")]
	public void Forma_ruim_e_recusada_dizendo_o_que_esta_errado(string json, string trecho)
	{
		var result = RoomForms.Parse(json);

		Assert.False(result.IsOk);
		Assert.Contains(trecho, result.Error);
	}
}
