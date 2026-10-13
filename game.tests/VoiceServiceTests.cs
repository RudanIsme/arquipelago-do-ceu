using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Saves;
using ArquipelagoDoCeu.Settings;
using ArquipelagoDoCeu.Voice;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public sealed class VoiceServiceTests : IDisposable
{
	readonly string pasta = Path.Combine(Path.GetTempPath(), "arquipelago-voz-" + Guid.NewGuid().ToString("N"));
	readonly GameRules regras = GameRules.Parse(TestJson.Rules).Value!;
	readonly FakeEngine motor = new();

	static readonly SystemVoice Maria = new("maria", "Maria", "pt-BR");
	static readonly SystemVoice Joao = new("joao", "João", "pt_BR");
	static readonly SystemVoice Portugal = new("rui", "Rui", "pt-PT");
	static readonly SystemVoice Ingles = new("sam", "Sam", "en-US");

	(VoiceService Voz, SettingsService Config) Abrir(params SystemVoice[] vozes)
	{
		motor.Installed = vozes;
		var store = new SaveStore(Path.Combine(pasta, "save.json"));
		var config = new SettingsService(new SaveSession(store, store.Load().Value!.Data), regras);
		return (new VoiceService(motor, config), config);
	}

	[Fact]
	public void Fala_a_frase_com_a_voz_pt_BR_e_a_velocidade_das_configuracoes()
	{
		var (voz, _) = Abrir(Ingles, Portugal, Maria);

		Assert.True(voz.Say("Olá!"));

		Assert.Equal([new Fala("Olá!", "maria", 1.0f, false)], motor.Spoken);
	}

	[Fact]
	public void So_voz_pt_BR_conta_e_o_separador_pode_ser_sublinhado()
	{
		var (voz, _) = Abrir(Ingles, Portugal, Joao);

		Assert.Equal(["joao"], voz.PortugueseVoices().Select(v => v.Id));
	}

	[Fact]
	public void Nova_fala_interrompe_a_anterior_quando_pedido()
	{
		var (voz, _) = Abrir(Maria);

		voz.Say("Primeira");
		voz.Say("Segunda", interrupt: true);

		Assert.Equal([false, true], motor.Spoken.Select(f => f.Interrupt));
	}

	[Fact]
	public void Sem_voz_pt_BR_nao_fala_e_o_aviso_aparece()
	{
		var (voz, _) = Abrir(Ingles);

		Assert.False(voz.Say("Olá!"));
		Assert.Empty(motor.Spoken);
		Assert.NotNull(voz.MissingVoiceNotice);
	}

	[Fact]
	public void Com_voz_pt_BR_nao_ha_aviso()
	{
		var (voz, _) = Abrir(Maria);

		Assert.Null(voz.MissingVoiceNotice);
	}

	[Fact]
	public void Voz_desligada_silencia_tudo_corta_a_fala_em_andamento_e_nao_avisa()
	{
		var (voz, config) = Abrir(Maria);
		voz.Say("Olá!");

		config.Change(s => s with { Voice = false });

		Assert.Equal(1, motor.Stops);
		Assert.False(voz.Say("Outra"));
		Assert.Single(motor.Spoken);
		Assert.Null(voz.MissingVoiceNotice);
	}

	[Fact]
	public void Usa_a_voz_e_a_velocidade_escolhidas()
	{
		var (voz, config) = Abrir(Maria, Joao);
		config.Change(s => s with { VoiceId = "joao", VoiceSpeed = "devagar" });

		voz.Say("Olá!");

		Assert.Equal(new Fala("Olá!", "joao", 0.8f, false), motor.Spoken.Single());
	}

	[Fact]
	public void Voz_escolhida_que_sumiu_do_aparelho_cai_na_primeira_pt_BR()
	{
		var (voz, config) = Abrir(Maria);
		config.Change(s => s with { VoiceId = "removida" });

		voz.Say("Olá!");

		Assert.Equal("maria", motor.Spoken.Single().VoiceId);
	}

	[Fact]
	public void Texto_vazio_nao_fala()
	{
		var (voz, _) = Abrir(Maria);

		Assert.False(voz.Say("  "));
	}

	public void Dispose()
	{
		if (Directory.Exists(pasta)) Directory.Delete(pasta, true);
	}

	record Fala(string Text, string VoiceId, float Rate, bool Interrupt);

	sealed class FakeEngine : ISpeechEngine
	{
		public IReadOnlyList<SystemVoice> Installed { get; set; } = [];
		public List<Fala> Spoken { get; } = [];
		public int Stops { get; private set; }

		public IReadOnlyList<SystemVoice> Voices() => Installed;
		public void Speak(string text, string voiceId, float rate, bool interrupt) => Spoken.Add(new Fala(text, voiceId, rate, interrupt));
		public void Stop() => Stops++;
	}
}
