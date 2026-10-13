using ArquipelagoDoCeu.Profiles;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Saves;
using ArquipelagoDoCeu.Settings;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public sealed class SettingsServiceTests : IDisposable
{
	readonly string pasta = Path.Combine(Path.GetTempPath(), "arquipelago-config-" + Guid.NewGuid().ToString("N"));
	readonly GameRules regras = GameRules.Parse(TestJson.Rules).Value!;

	string Caminho => Path.Combine(pasta, "save.json");

	// Abrir de novo lê o arquivo, como o jogo faz ao ser reaberto.
	SaveSession Sessao()
	{
		var store = new SaveStore(Caminho);
		return new SaveSession(store, store.Load().Value!.Data);
	}

	SettingsService Abrir() => new(Sessao(), regras);

	[Fact]
	public void Sem_nada_salvo_valem_os_padroes_dos_dados()
	{
		var atual = Abrir().Current;

		Assert.Equal(new GameSettings("normal", 1.0f, true, false, false, false, "mouse", null, "normal", 1.0f), atual);
	}

	[Fact]
	public void Cada_valor_escolhido_persiste_e_volta_ao_reabrir()
	{
		var servico = Abrir();

		Assert.True(servico.Change(s => s with { TextSize = "grande", ReduceMotion = true, DyslexiaFont = true, CalmMode = true, Voice = false, Controls = "toque" }).IsOk);

		var reaberto = Abrir().Current;
		Assert.Equal(new GameSettings("grande", 1.25f, false, true, true, true, "toque", null, "normal", 1.0f), reaberto);
	}

	[Fact]
	public void Mudar_uma_opcao_nao_mexe_nas_outras()
	{
		var servico = Abrir();
		servico.Change(s => s with { CalmMode = true });

		Abrir().Change(s => s with { TextSize = "grande" });

		Assert.True(Abrir().Current.CalmMode);
		Assert.Equal("grande", Abrir().Current.TextSize);
	}

	[Fact]
	public void Tamanho_de_texto_ou_controle_que_nao_existe_e_recusado_e_nada_muda()
	{
		var servico = Abrir();

		var ruim = servico.Change(s => s with { TextSize = "gigante" });
		var ruim2 = servico.Change(s => s with { Controls = "joystick" });

		Assert.False(ruim.IsOk);
		Assert.False(ruim2.IsOk);
		Assert.Equal("normal", Abrir().Current.TextSize);
		Assert.Equal("mouse", servico.Current.Controls);
	}

	[Fact]
	public void Voz_e_velocidade_escolhidas_persistem()
	{
		Abrir().Change(s => s with { VoiceId = "maria", VoiceSpeed = "devagar" });

		var reaberto = Abrir().Current;

		Assert.Equal("maria", reaberto.VoiceId);
		Assert.Equal(("devagar", 0.8f), (reaberto.VoiceSpeed, reaberto.VoiceRate));
	}

	[Fact]
	public void Velocidade_de_voz_que_nao_existe_e_recusada()
	{
		var servico = Abrir();

		Assert.False(servico.Change(s => s with { VoiceSpeed = "turbo" }).IsOk);
		Assert.Equal("normal", servico.Current.VoiceSpeed);
	}

	[Fact]
	public void Valor_guardado_que_saiu_dos_dados_volta_ao_padrao()
	{
		Abrir().Change(s => s with { TextSize = "grande" });
		var menor = regras with { Settings = regras.Settings with { TextSizes = [regras.Settings.TextSizes[0]] } };

		var reaberto = new SettingsService(Sessao(), menor);

		Assert.Equal("normal", reaberto.Current.TextSize);
		Assert.Equal(1.0f, reaberto.Current.TextScale);
	}

	[Fact]
	public void Falha_ao_gravar_devolve_erro_e_nao_muda_o_valor()
	{
		var servico = Abrir();
		servico.Change(s => s with { CalmMode = true });
		Directory.CreateDirectory(Caminho + ".tmp");

		var resultado = servico.Change(s => s with { CalmMode = false });

		Assert.False(resultado.IsOk);
		Assert.True(servico.Current.CalmMode);
	}

	[Fact]
	public void Avisa_quem_escuta_quando_uma_configuracao_muda()
	{
		var servico = Abrir();
		GameSettings? recebida = null;
		servico.Changed += s => recebida = s;

		servico.Change(s => s with { DyslexiaFont = true });

		Assert.True(recebida!.DyslexiaFont);
	}

	[Fact]
	public void Configuracoes_e_perfis_dividem_o_mesmo_arquivo_sem_se_apagar()
	{
		var sessao = Sessao();
		var perfis = new ProfileService(sessao, regras, () => "id-1");
		var config = new SettingsService(sessao, regras);

		perfis.Create("Luna");
		config.Change(s => s with { CalmMode = true });
		perfis.Create("Theo");

		var depois = Sessao();
		Assert.Equal(["Luna", "Theo"], depois.Data.Profiles.Select(p => p.Nickname));
		Assert.True(new SettingsService(depois, regras).Current.CalmMode);
	}

	[Fact]
	public void Save_antigo_sem_configuracoes_continua_valido()
	{
		Directory.CreateDirectory(pasta);
		File.WriteAllText(Caminho, """{ "version": 1, "profiles": [ { "id": "a", "nickname": "Luna", "level": 1 } ] }""");

		var servico = Abrir();

		Assert.Equal("normal", servico.Current.TextSize);
		Assert.Equal("Luna", Sessao().Data.Profiles.Single().Nickname);
	}

	public void Dispose()
	{
		if (Directory.Exists(pasta)) Directory.Delete(pasta, recursive: true);
	}
}
