using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Generation;
using ArquipelagoDoCeu.Rules;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class DrawnMathTests
{
	static readonly MathLevel Level1 = new(["+"], 2);
	static readonly MathLevel Level2 = new(["+", "-"], 2);
	static readonly MathLevel Level3 = new(["+", "-"], 3);

	[Theory]
	[InlineData("3+5")]
	[InlineData("4+4")]
	[InlineData("10-2")]
	public void Pedido_de_resultado_aceita_qualquer_conta_que_de_o_resultado(string drawn)
	{
		var verdict = DrawnMath.Judge(drawn, 8, MathAsk.Result, Level2);

		Assert.Equal(DrawnKind.Right, verdict.Kind);
		Assert.Equal(8, verdict.Total);
	}

	[Fact]
	public void A_conta_e_lida_da_esquerda_para_a_direita()
	{
		var verdict = DrawnMath.Judge("10-3+1", 8, MathAsk.Result, Level3);

		Assert.Equal(DrawnKind.Right, verdict.Kind);
		Assert.Equal("10 - 3 + 1 = 8", verdict.Equation);
	}

	[Fact]
	public void Conta_valida_com_resultado_errado_e_resposta_errada_e_mostra_a_conta()
	{
		var verdict = DrawnMath.Judge("3+4", 8, MathAsk.Result, Level1);

		Assert.Equal(DrawnKind.Wrong, verdict.Kind);
		Assert.True(verdict.IsAnswer);
		Assert.Equal("3 + 4 = 7", verdict.Equation);
	}

	[Fact]
	public void Numero_sozinho_e_recusado_sem_custo_com_aviso_de_que_e_preciso_uma_conta()
	{
		var verdict = DrawnMath.Judge("8", 8, MathAsk.Result, Level1);

		Assert.Equal(DrawnKind.NeedsOperation, verdict.Kind);
		Assert.False(verdict.IsAnswer);
		Assert.Contains("conta", verdict.Message);
	}

	[Theory]
	[InlineData("8+0")]
	[InlineData("0+8")]
	[InlineData("9-0")]
	[InlineData("4+0+4")]
	public void Zero_nao_entra_como_parcela_e_o_aviso_explica_o_motivo(string drawn)
	{
		var verdict = DrawnMath.Judge(drawn, 8, MathAsk.Result, Level3);

		Assert.Equal(DrawnKind.ZeroTerm, verdict.Kind);
		Assert.False(verdict.IsAnswer);
		Assert.Contains("zero", verdict.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void Dez_nao_e_zero_e_vale_como_parcela()
	{
		Assert.Equal(DrawnKind.Right, DrawnMath.Judge("10-2", 8, MathAsk.Result, Level2).Kind);
	}

	[Fact]
	public void No_nivel_1_so_o_mais_vale()
	{
		var verdict = DrawnMath.Judge("10-2", 8, MathAsk.Result, Level1);

		Assert.Equal(DrawnKind.SignNotAllowed, verdict.Kind);
		Assert.False(verdict.IsAnswer);
	}

	[Fact]
	public void No_nivel_2_o_menos_vale_mas_tres_parcelas_nao()
	{
		Assert.Equal(DrawnKind.Right, DrawnMath.Judge("10-2", 8, MathAsk.Result, Level2).Kind);
		Assert.Equal(DrawnKind.TooManyTerms, DrawnMath.Judge("2+3+3", 8, MathAsk.Result, Level2).Kind);
	}

	[Fact]
	public void No_nivel_3_valem_tres_parcelas()
	{
		Assert.Equal(DrawnKind.Right, DrawnMath.Judge("2+3+3", 8, MathAsk.Result, Level3).Kind);
		Assert.Equal(DrawnKind.TooManyTerms, DrawnMath.Judge("1+2+2+3", 8, MathAsk.Result, Level3).Kind);
	}

	[Theory]
	[InlineData("+8")]
	[InlineData("3+")]
	[InlineData("3++5")]
	[InlineData("3+-5")]
	[InlineData("")]
	public void Conta_mal_formada_e_recusada_sem_custo(string drawn)
	{
		var verdict = DrawnMath.Judge(drawn, 8, MathAsk.Result, Level2);

		Assert.Equal(DrawnKind.Malformed, verdict.Kind);
		Assert.False(verdict.IsAnswer);
	}

	[Fact]
	public void Numeros_de_dois_algarismos_sao_um_numero_so()
	{
		var verdict = DrawnMath.Judge("12+30", 42, MathAsk.Result, Level1);

		Assert.Equal(DrawnKind.Right, verdict.Kind);
	}

	[Fact]
	public void Pedido_de_conta_pronta_aceita_so_o_numero()
	{
		Assert.Equal(DrawnKind.Right, DrawnMath.Judge("8", 8, MathAsk.Answer, Level1).Kind);
		Assert.Equal(DrawnKind.Wrong, DrawnMath.Judge("7", 8, MathAsk.Answer, Level1).Kind);
	}

	[Fact]
	public void Pedido_de_conta_pronta_recusa_sinais_sem_custo()
	{
		var verdict = DrawnMath.Judge("3+5", 8, MathAsk.Answer, Level3);

		Assert.Equal(DrawnKind.NumberOnly, verdict.Kind);
		Assert.False(verdict.IsAnswer);
	}

	[Fact]
	public void Pedido_de_conta_pronta_aceita_zero_como_resposta()
	{
		Assert.Equal(DrawnKind.Right, DrawnMath.Judge("0", 0, MathAsk.Answer, Level2).Kind);
	}

	[Fact]
	public void Os_glifos_permitidos_seguem_o_pedido_e_o_nivel()
	{
		Assert.Equal("0123456789+", DrawnMath.Allowed(MathAsk.Result, Level1));
		Assert.Equal("0123456789+-", DrawnMath.Allowed(MathAsk.Result, Level2));
		Assert.Equal("0123456789", DrawnMath.Allowed(MathAsk.Answer, Level3));
	}

	[Theory]
	[InlineData(8, 1)]
	[InlineData(8, 2)]
	[InlineData(8, 3)]
	[InlineData(2, 3)]
	[InlineData(14, 2)]
	public void A_conta_de_exemplo_da_Dica_da_o_resultado_e_respeita_o_Nivel(int target, int year)
	{
		var level = year switch { 1 => Level1, 2 => Level2, _ => Level3 };
		for (ulong seed = 1; seed <= 20; seed++)
		{
			var example = DrawnMath.Example(target, level, new Pcg32(seed));

			Assert.NotNull(example);
			Assert.Equal(DrawnKind.Right, DrawnMath.Judge(example, target, MathAsk.Result, level).Kind);
		}
	}

	[Fact]
	public void Nao_ha_conta_de_exemplo_para_o_1_porque_o_zero_nao_entra()
	{
		Assert.Null(DrawnMath.Example(1, Level1, new Pcg32(1)));
	}

	[Fact]
	public void Os_sinais_e_as_parcelas_de_cada_Nivel_vem_dos_dados_do_jogo()
	{
		var json = File.ReadAllText(Path.Combine(MissionFiles.RepositoryRoot(), "game", "data", "rules.json"));
		var combat = GameRules.Parse(json).Value!.Combat;

		Assert.Equal(Level1, combat.ForYear(1).MathLevel, new LevelComparer());
		Assert.Equal(Level2, combat.ForYear(2).MathLevel, new LevelComparer());
		Assert.Equal(Level3, combat.ForYear(3).MathLevel, new LevelComparer());
	}

	[Fact]
	public void Regras_com_parcelas_fora_do_limite_ou_desenho_livre_incompleto_sao_recusadas()
	{
		var valid = TestJson.Rules;

		Assert.True(GameRules.Parse(valid).IsOk);
		Assert.False(GameRules.Parse(valid.Replace("\"combat\": {", "\"combat\": { \"maxTerms\": 5,")).IsOk);
		Assert.False(GameRules.Parse(valid.Replace("\"combat\": {", "\"combat\": { \"freeDraw\": { \"idleSeconds\": 0.8 },")).IsOk);
	}

	sealed class LevelComparer : IEqualityComparer<MathLevel>
	{
		public bool Equals(MathLevel? a, MathLevel? b) => a is not null && b is not null && a.MaxTerms == b.MaxTerms && a.Operators.SequenceEqual(b.Operators);

		public int GetHashCode(MathLevel level) => level.MaxTerms;
	}
}
