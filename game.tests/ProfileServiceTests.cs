using ArquipelagoDoCeu.Profiles;
using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.Saves;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public sealed class ProfileServiceTests : IDisposable
{
	readonly string pasta = Path.Combine(Path.GetTempPath(), "arquipelago-perfis-" + Guid.NewGuid().ToString("N"));
	readonly GameRules regras = GameRules.Parse(TestJson.Rules).Value!;
	int contador;

	string Caminho => Path.Combine(pasta, "save.json");

	// Abrir de novo lê o arquivo, como o jogo faz ao ser reaberto.
	ProfileService Abrir()
	{
		var store = new SaveStore(Caminho);
		return new ProfileService(new SaveSession(store, store.Load().Value!.Data), regras, () => $"id-{++contador}");
	}

	// --- #11: criar

	[Fact]
	public void Criar_um_perfil_comeca_no_Nivel_1_e_volta_ao_reabrir()
	{
		var criado = Abrir().Create("Luna");

		Assert.True(criado.IsOk, criado.Error);
		Assert.Equal(new ProfileSave("id-1", "Luna", 1), criado.Value);
		Assert.Equal([new ProfileSave("id-1", "Luna", 1)], Abrir().Profiles);
	}

	[Theory]
	[InlineData("", "Escolha um apelido")]
	[InlineData("   ", "Escolha um apelido")]
	[InlineData("Apelido grande demais", "12")]
	[InlineData("Lu@na", "letras, números e espaços")]
	[InlineData("Luna😀", "letras, números e espaços")]
	[InlineData("a_b", "letras, números e espaços")]
	public void Apelido_ruim_e_recusado_com_mensagem_amigavel(string apelido, string trechoDaMensagem)
	{
		var servico = Abrir();

		var resultado = servico.Create(apelido);

		Assert.False(resultado.IsOk);
		Assert.Contains(trechoDaMensagem, resultado.Error);
		Assert.Empty(servico.Profiles);
	}

	[Theory]
	[InlineData("João", "João")]
	[InlineData("  Luna  ", "Luna")]
	[InlineData("Ana   Clara", "Ana Clara")]
	[InlineData("Leo2", "Leo2")]
	public void Apelido_com_acento_e_numero_e_aceito_e_os_espacos_sao_arrumados(string digitado, string guardado)
	{
		Assert.Equal(guardado, Abrir().Create(digitado).Value!.Nickname);
	}

	[Fact]
	public void Apelido_repetido_e_recusado_sem_diferenciar_maiusculas()
	{
		var servico = Abrir();
		servico.Create("Luna");

		var segundo = servico.Create("luna");

		Assert.False(segundo.IsOk);
		Assert.Contains("Já existe", segundo.Error);
	}

	[Fact]
	public void O_limite_de_perfis_vem_dos_dados()
	{
		var servico = Abrir();
		for (var i = 1; i <= regras.Profile.MaxProfiles; i++) Assert.True(servico.Create($"Crianca{i}").IsOk);

		var excedente = servico.Create("Mais um");

		Assert.False(excedente.IsOk);
		Assert.Contains(regras.Profile.MaxProfiles.ToString(System.Globalization.CultureInfo.InvariantCulture), excedente.Error);
	}

	[Fact]
	public void Os_perfis_ficam_na_ordem_em_que_foram_criados()
	{
		var servico = Abrir();
		servico.Create("Luna");
		servico.Create("Theo");

		Assert.Equal(["Luna", "Theo"], Abrir().Profiles.Select(p => p.Nickname));
	}

	[Fact]
	public void Falha_ao_gravar_devolve_erro_e_nao_muda_a_lista()
	{
		var servico = Abrir();
		servico.Create("Luna");
		// Uma pasta com o nome do temporário impede a gravação, sem depender de permissões.
		Directory.CreateDirectory(Caminho + ".tmp");

		var resultado = servico.Create("Theo");

		Assert.False(resultado.IsOk);
		Assert.Equal(["Luna"], servico.Profiles.Select(p => p.Nickname));
	}

	// --- #12: editar e escolher

	[Fact]
	public void Renomear_persiste_e_mantem_o_resto()
	{
		var servico = Abrir();
		var id = servico.Create("Luna").Value!.Id;

		var renomeado = servico.Rename(id, "Luninha");

		Assert.True(renomeado.IsOk, renomeado.Error);
		var depois = Abrir().Find(id).Value!;
		Assert.Equal("Luninha", depois.Nickname);
		Assert.Equal(1, depois.Level);
	}

	[Fact]
	public void Renomear_para_o_proprio_apelido_com_outra_caixa_e_permitido()
	{
		var servico = Abrir();
		var id = servico.Create("luna").Value!.Id;

		Assert.True(servico.Rename(id, "Luna").IsOk);
	}

	[Fact]
	public void Renomear_para_o_apelido_de_outro_perfil_e_recusado()
	{
		var servico = Abrir();
		servico.Create("Luna");
		var theo = servico.Create("Theo").Value!;

		var resultado = servico.Rename(theo.Id, "Luna");

		Assert.False(resultado.IsOk);
		Assert.Contains("Já existe", resultado.Error);
	}

	[Fact]
	public void Renomear_com_apelido_ruim_ou_perfil_inexistente_devolve_erro()
	{
		var servico = Abrir();
		var id = servico.Create("Luna").Value!.Id;

		Assert.False(servico.Rename(id, "").IsOk);
		Assert.False(servico.Rename("nao-existe", "Theo").IsOk);
		Assert.Equal("Luna", Abrir().Find(id).Value!.Nickname);
	}

	[Fact]
	public void Com_varios_perfis_cada_um_e_achado_pelo_id()
	{
		var servico = Abrir();
		servico.Create("Luna");
		var theo = servico.Create("Theo").Value!;

		Assert.Equal("Theo", Abrir().Find(theo.Id).Value!.Nickname);
		Assert.False(Abrir().Find("nao-existe").IsOk);
	}

	// --- #13 e #14: aparência

	[Fact]
	public void Sem_escolha_a_aparencia_usa_a_primeira_opcao_de_cada_categoria()
	{
		var servico = Abrir();
		var luna = servico.Create("Luna").Value!;

		var escolhidas = servico.AppearanceOf(luna);

		Assert.Equal(["skinTone=marfim", "clothesColor=bege"], escolhidas.Select(e => $"{e.Category.Id}={e.Option.Id}"));
	}

	[Fact]
	public void Escolher_uma_opcao_persiste_e_volta_ao_reabrir()
	{
		var servico = Abrir();
		var id = servico.Create("Luna").Value!.Id;

		Assert.True(servico.Choose(id, "skinTone", "cafe").IsOk);
		Assert.True(servico.Choose(id, "clothesColor", "azul").IsOk);

		var reaberto = Abrir();
		var escolhidas = reaberto.AppearanceOf(reaberto.Find(id).Value!);
		Assert.Equal(["skinTone=cafe", "clothesColor=azul"], escolhidas.Select(e => $"{e.Category.Id}={e.Option.Id}"));
	}

	[Fact]
	public void Escolher_uma_opcao_nao_mexe_nas_outras_categorias()
	{
		var servico = Abrir();
		var id = servico.Create("Luna").Value!.Id;
		servico.Choose(id, "skinTone", "cafe");

		servico.Choose(id, "clothesColor", "azul");

		Assert.Equal("cafe", servico.AppearanceOf(servico.Find(id).Value!).First(e => e.Category.Id == "skinTone").Option.Id);
	}

	[Theory]
	[InlineData("naoExiste", "azul")]
	[InlineData("clothesColor", "naoExiste")]
	[InlineData("accessory", "chapeu")]
	public void Categoria_ou_opcao_que_nao_existe_e_recusada(string categoria, string opcao)
	{
		var servico = Abrir();
		var id = servico.Create("Luna").Value!.Id;

		var resultado = servico.Choose(id, categoria, opcao);

		Assert.False(resultado.IsOk);
		Assert.Empty(servico.Find(id).Value!.Appearance ?? new Dictionary<string, string>());
	}

	[Fact]
	public void Categorias_sem_opcoes_nao_aparecem_na_aparencia_resolvida()
	{
		var servico = Abrir();
		var luna = servico.Create("Luna").Value!;

		Assert.DoesNotContain(servico.AppearanceOf(luna), e => e.Category.Id == "accessory");
	}

	[Fact]
	public void Opcao_guardada_que_saiu_dos_dados_volta_para_a_primeira()
	{
		var servico = Abrir();
		var id = servico.Create("Luna").Value!.Id;
		servico.Choose(id, "skinTone", "cafe");
		// Uma versão futura dos dados pode remover "cafe"; o save antigo não pode quebrar o jogo.
		var catalogoMenor = regras with
		{
			Appearance = new AppearanceCatalog([new AppearanceCategory("skinTone", "Tom de pele", [new AppearanceOption("marfim", "Marfim", "#F7D9C4")])]),
		};
		var store = new SaveStore(Caminho);
		var reaberto = new ProfileService(new SaveSession(store, store.Load().Value!.Data), catalogoMenor, () => "x");

		Assert.Equal("marfim", reaberto.AppearanceOf(reaberto.Find(id).Value!).Single().Option.Id);
	}

	public void Dispose()
	{
		if (Directory.Exists(pasta)) Directory.Delete(pasta, recursive: true);
	}

	[Fact]
	public void A_tela_sabe_se_ainda_pode_criar_e_o_tamanho_maximo_do_apelido()
	{
		var servico = Abrir();
		Assert.True(servico.CanCreate);
		Assert.Equal(regras.Profile.MaxNicknameLength, servico.MaxNicknameLength);
		Assert.Same(regras.Appearance, servico.Catalog);

		for (var i = 1; i <= regras.Profile.MaxProfiles; i++) servico.Create($"Crianca{i}");

		Assert.False(servico.CanCreate);
	}
}
