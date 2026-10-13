using ArquipelagoDoCeu.Missions;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

// É este teste que deixa a CI vermelha quando uma Missão do repositório é inválida: a mensagem diz o arquivo e o campo.
public class MissionContentTests
{
	static readonly MissionLoad Loaded = MissionLoader.Create(File.ReadAllText(MissionFiles.SchemaPath)).Value!.Load(MissionFiles.All());

	[Fact]
	public void Todas_as_Missoes_do_repositorio_passam_no_schema()
	{
		Assert.True(Loaded.Rejected.Count == 0, string.Join(Environment.NewLine, Loaded.Rejected.Select(r => r.Reason)));
		Assert.NotEmpty(Loaded.Missions);
	}

	[Fact]
	public void Ids_sao_unicos()
	{
		Assert.Equal(Loaded.Missions.Count, Loaded.Missions.Select(m => m.Id).Distinct().Count());
	}

	[Fact]
	public void Missao_escrita_a_mao_fica_com_revisao_pendente_ate_um_profissional_aprovar()
	{
		Assert.All(Loaded.Missions, m => Assert.Equal("pending", m.Review.Status));
	}

	[Theory]
	[InlineData("composicao-ate-5")]
	[InlineData("composicao-ate-10")]
	[InlineData("adicao-ate-10")]
	public void Conceito_tem_Missoes_em_varios_niveis_de_dificuldade(string concept)
	{
		var missions = Loaded.Missions.Where(m => m.Concept == concept).ToList();

		Assert.True(missions.Count >= 6);
		Assert.True(missions.Select(m => m.Difficulty).Distinct().Count() >= 3);
		Assert.All(missions, m => Assert.Equal("compor_numero", m.Mechanic));
	}

	// A Mecânica é quem confere a resposta; aqui só se garante que o conteúdo escrito à mão tem solução,
	// com cada peça usada uma vez (vale também se a Mecânica deixar repetir peças).
	[Fact]
	public void Toda_Missao_de_compor_numero_tem_solucao()
	{
		var semSolucao = Loaded.Missions
			.Where(m => m.Mechanic == "compor_numero")
			.Where(m => !TemSolucao(
				m.Params["target"].GetInt32(),
				m.Params["pieces"].EnumerateArray().Select(p => p.GetInt32()).ToList(),
				m.Params["maxPieces"].GetInt32()))
			.Select(m => m.Id);

		Assert.Empty(semSolucao);
	}

	[Fact]
	public void Dicas_e_explicacao_nao_entregam_nada_vazio_e_a_BNCC_do_1o_ano_esta_no_ano_certo()
	{
		Assert.All(Loaded.Missions, m =>
		{
			// Uma Missão pode reaproveitar uma habilidade do ano anterior (por exemplo, escrever palavras do 1º ano no 2º).
			Assert.All(m.Bncc, code => Assert.True(code.StartsWith($"EF0{m.Year}") || code.StartsWith($"EF0{m.Year - 1}"), $"{m.Id}: {code}"));
			Assert.All(m.Hints, h => Assert.False(string.IsNullOrWhiteSpace(h)));
		});
	}

	// Com subtração (2º ano), vale juntar e tirar: o primeiro número entra somando e os outros entram com + ou -.
	static bool TemSolucao(int target, List<int> pieces, int maxPieces)
	{
		for (var mask = 1; mask < 1 << pieces.Count; mask++)
		{
			var chosen = Enumerable.Range(0, pieces.Count).Where(i => (mask & 1 << i) != 0).Select(i => pieces[i]).ToList();
			if (chosen.Count < 2 || chosen.Count > maxPieces) continue;
			for (var signs = 0; signs < 1 << (chosen.Count - 1); signs++)
			{
				var total = chosen[0];
				for (var i = 1; i < chosen.Count; i++) total += (signs & 1 << (i - 1)) != 0 ? -chosen[i] : chosen[i];
				if (total == target) return true;
			}
		}
		return false;
	}
}
