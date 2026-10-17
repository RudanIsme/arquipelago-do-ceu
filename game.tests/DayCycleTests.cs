using ArquipelagoDoCeu.Profiles;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Saves;
using ArquipelagoDoCeu.Settings;
using ArquipelagoDoCeu.World;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

// O dia e a noite do Lobby (#156): só visual, no tempo do jogo, com a duração em dados e a opção de fixar o dia.
public sealed class DayCycleTests : IDisposable
{
	readonly string pasta = Path.Combine(Path.GetTempPath(), "arquipelago-dia-" + Guid.NewGuid().ToString("N"));
	readonly GameRules regras = GameRules.Parse(TestJson.Rules).Value!;

	public void Dispose()
	{
		if (Directory.Exists(pasta)) Directory.Delete(pasta, true);
	}

	[Fact]
	public void A_duracao_do_ciclo_vem_dos_dados()
	{
		Assert.Equal(480f, regras.Lobby.DayCycleSeconds);
	}

	[Fact]
	public void A_luz_repete_a_cada_ciclo_e_fica_entre_zero_e_um()
	{
		var ciclo = new DayCycle(480f);

		Assert.Equal(ciclo.Daylight(100f), ciclo.Daylight(580f), 3);
		for (var t = 0f; t < 960f; t += 10f)
			Assert.InRange(ciclo.Daylight(t), 0f, 1f);
	}

	[Fact]
	public void Ao_longo_do_ciclo_amanhece_fica_dia_e_anoitece()
	{
		var ciclo = new DayCycle(480f);
		var luzes = Enumerable.Range(0, 48).Select(i => ciclo.Daylight(i * 10f)).ToList();

		Assert.True(luzes.Max() > 0.9f);
		Assert.True(luzes.Min() < 0.2f);
		Assert.True(ciclo.LampsOn(luzes.IndexOf(luzes.Min()) * 10f));
		Assert.False(ciclo.LampsOn(luzes.IndexOf(luzes.Max()) * 10f));
	}

	[Fact]
	public void Com_o_dia_fixo_e_sempre_dia_e_os_lampioes_ficam_apagados()
	{
		var ciclo = new DayCycle(480f, fixedDay: true);

		for (var t = 0f; t < 960f; t += 10f)
		{
			Assert.Equal(1f, ciclo.Daylight(t));
			Assert.False(ciclo.LampsOn(t));
		}
	}

	[Fact]
	public void Fixar_o_dia_e_salvo_e_volta_ao_reabrir()
	{
		Assert.False(Abrir().Current.FixedDay);

		Assert.True(Abrir().Change(s => s with { FixedDay = true }).IsOk);

		Assert.True(Abrir().Current.FixedDay);
	}

	SettingsService Abrir()
	{
		var store = new SaveStore(Path.Combine(pasta, "save.json"));
		return new SettingsService(new SaveSession(store, store.Load().Value!.Data), regras);
	}

	[Theory]
	[InlineData("0")]
	[InlineData("5")]
	[InlineData("99999")]
	public void Duracao_fora_do_razoavel_e_recusada(string valor)
	{
		var resultado = GameRules.Parse(TestJson.Rules.Replace("\"dayCycleSeconds\": 480", $"\"dayCycleSeconds\": {valor}"));

		Assert.False(resultado.IsOk);
		Assert.Contains("lobby.dayCycleSeconds", resultado.Error);
	}
}
