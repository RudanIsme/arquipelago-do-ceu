using ArquipelagoDoCeu.Rules;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public sealed class LobbyESettingsRulesTests
{
	static readonly GameRules Regras = GameRules.Parse(TestJson.Rules).Value!;

	// --- #21 e #22: placas dos lugares vêm dos dados

	[Fact]
	public void Cada_lugar_traz_o_texto_da_placa_dos_dados()
	{
		Assert.Equal(["Em construção", "Em breve"], Regras.Lobby.Places.Select(p => p.Sign));
		Assert.Equal("torre-numeros", Regras.Lobby.Places[0].Model);
		Assert.Null(Regras.Lobby.Places[1].Model);
	}

	[Fact]
	public void Trocar_o_texto_no_arquivo_troca_a_placa()
	{
		var regras = GameRules.Parse(TestJson.Rules.Replace("Em breve", "Chegando")).Value!;

		Assert.Equal("Chegando", regras.Lobby.Places[1].Sign);
	}

	[Fact]
	public void Objetos_do_Lobby_e_o_alcance_vem_dos_dados()
	{
		Assert.Equal(2.0f, Regras.Lobby.InteractionReach);
		Assert.Equal(["album", "portal"], Regras.Lobby.Objects.Select(o => o.Id));
		Assert.Equal("Abrir o Álbum", Regras.Lobby.Objects[0].Prompt);
		Assert.Equal((-2, -2), (Regras.Lobby.Objects[0].Q, Regras.Lobby.Objects[0].R));
	}

	[Theory]
	[InlineData("\"sign\": \"Em breve\"", "\"sign\": \"  \"", "lobby.places[1].sign")]
	[InlineData("\"id\": \"ciencias\"", "\"id\": \"matematica\"", "matematica")]
	[InlineData("\"interactionReach\": 2.0", "\"interactionReach\": 0", "lobby.interactionReach")]
	[InlineData("\"interactionReach\": 2.0", "\"interactionReach\": 50", "lobby.interactionReach")]
	[InlineData("\"prompt\": \"Usar o portal\"", "\"prompt\": \"\"", "lobby.objects[1].prompt")]
	[InlineData("\"id\": \"portal\"", "\"id\": \"album\"", "album")]
	public void Lobby_ruim_e_recusado_dizendo_qual_campo(string original, string novo, string trecho)
	{
		var resultado = GameRules.Parse(TestJson.Rules.Replace(original, novo));

		Assert.False(resultado.IsOk);
		Assert.Contains(trecho, resultado.Error);
	}

	// --- #26 a #31: catálogo e padrões das configurações

	[Fact]
	public void Le_os_tamanhos_de_texto_os_controles_e_os_padroes()
	{
		Assert.Equal(["normal", "grande"], Regras.Settings.TextSizes.Select(t => t.Id));
		Assert.Equal(1.25f, Regras.Settings.TextSizes[1].Scale);
		Assert.Equal(["mouse", "teclado", "toque"], Regras.Settings.Controls.Select(c => c.Id));
		Assert.Equal(new SettingsDefaults("normal", true, false, false, false, "mouse"), Regras.Settings.Defaults);
	}

	[Theory]
	[InlineData("\"textSize\": \"normal\"", "\"textSize\": \"gigante\"", "settings.defaults.textSize")]
	[InlineData("\"controls\": \"mouse\"", "\"controls\": \"joystick\"", "settings.defaults.controls")]
	[InlineData("\"scale\": 1.25", "\"scale\": 5", "settings.textSizes[1].scale")]
	[InlineData("\"scale\": 1.25", "\"scale\": 0.1", "settings.textSizes[1].scale")]
	[InlineData("\"voice\": true", "\"voice\": null", "settings.defaults.voice")]
	public void Configuracao_ruim_e_recusada_dizendo_qual_campo(string original, string novo, string campo)
	{
		var resultado = GameRules.Parse(TestJson.Rules.Replace(original, novo));

		Assert.False(resultado.IsOk);
		Assert.Contains(campo, resultado.Error);
	}

	[Theory]
	[InlineData("settings")]
	[InlineData("lobby")]
	public void Secao_ausente_e_recusada(string secao)
	{
		var semSecao = TestJson.Rules.Replace($"\"{secao}\":", $"\"{secao}Renomeada\":");

		var resultado = GameRules.Parse(semSecao);

		Assert.False(resultado.IsOk);
		Assert.Contains(secao, resultado.Error);
	}
}
