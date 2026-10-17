using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Equipment;
using ArquipelagoDoCeu.Saves;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public sealed class InventoryTests : IDisposable
{
	readonly string folder = Path.Combine(Path.GetTempPath(), "arquipelago-inventory-" + Guid.NewGuid().ToString("N"));

	static readonly GearCatalog Catalog = GearCatalog.Parse(File.ReadAllText(Path.Combine(MissionFiles.RepositoryRoot(), "game", "data", "equipamentos.json")), GearEffects.Default).Value!;

	static readonly CosmeticCatalog Cosmetics = CosmeticCatalog.Parse(File.ReadAllText(Path.Combine(MissionFiles.RepositoryRoot(), "game", "data", "cosmeticos.json"))).Value!;

	public void Dispose()
	{
		if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
	}

	SaveSession NewSession() => new(new SaveStore(Path.Combine(folder, "save.json")), new SaveData(1, [new ProfileSave("p", "Luna", 1)]));

	[Fact]
	public void A_comparacao_mostra_so_o_que_muda_com_valores_de_antes_e_depois()
	{
		var library = new GearLibrary(Catalog);
		var worn = new Gear().With("armor", "scout");

		var changes = library.Compare(worn, worn.With("armor", "guardian"));

		Assert.Contains(changes, c => c.Effect == "speed" && c.From == 0.12 && c.To == 0);
		Assert.Contains(changes, c => c.Effect == "energy" && c.From == 0 && c.To == 2);
		Assert.DoesNotContain(changes, c => c.Effect == "reach");
	}

	[Fact]
	public void Atributos_iguais_nao_aparecem_na_comparacao()
	{
		var library = new GearLibrary(Catalog);
		var worn = new Gear().With("armor", "scout");

		Assert.Empty(library.Compare(worn, worn));
	}

	[Fact]
	public void A_comparacao_diz_em_voz_se_sobe_ou_desce()
	{
		var library = new GearLibrary(Catalog);

		var up = library.Compare(new Gear(), new Gear().With("helmet", "open")).Single();
		var down = library.Compare(new Gear().With("helmet", "open"), new Gear()).Single();

		Assert.Equal("Energia sobe 1", up.Spoken);
		Assert.Equal("Energia desce 1", down.Spoken);
		Assert.Equal("▲", up.Arrow);
		Assert.Equal("▼", down.Arrow);
	}

	[Fact]
	public void O_limite_do_conjunto_vale_na_comparacao()
	{
		var library = new GearLibrary(Catalog);
		var full = new Gear().With("armor", "guardian");

		// A armadura já dá o máximo de Energia (+2): outro elmo de Energia não muda nada.
		Assert.DoesNotContain(library.Compare(full, full.With("helmet", "open")), c => c.Effect == "energy");
	}

	[Fact]
	public void A_biblioteca_mostra_o_equipado_primeiro_e_depois_o_mais_novo()
	{
		var library = new GearLibrary(Catalog);
		var worn = new Gear().With("armor", "scout");

		var entries = library.Entries(worn, mastery: 9);

		Assert.Equal("scout", entries[0].Item.Id);
		Assert.True(entries[0].Equipped);
		Assert.Equal(Catalog.Items.Count, entries.Count);
		var rest = entries.Skip(1).Select(e => e.Item.Id).ToList();
		Assert.Equal(Catalog.Items.Select(i => i.Id).Where(id => id != "scout").Reverse(), rest);
	}

	[Fact]
	public void O_equipamento_que_pede_mais_Maestria_aparece_bloqueado_com_o_numero()
	{
		var library = new GearLibrary(Catalog);

		var entries = library.Entries(new Gear(), mastery: 0);

		Assert.All(entries, e => Assert.Equal(e.Item.MinMastery > 0, e.Locked));
		Assert.Contains(entries, e => e.Locked);
	}

	[Fact]
	public void Cosmeticos_vestidos_so_valem_se_a_crianca_os_tem()
	{
		var service = new InventoryService(NewSession(), Cosmetics);

		Assert.False(service.Wear("p", "matematica-1-1", owned: []).IsOk);
		Assert.True(service.Wear("p", "matematica-1-1", owned: ["matematica-1-1"]).IsOk);
		Assert.Equal(["matematica-1-1"], service.Worn("p"));
	}

	[Fact]
	public void Tirar_um_Cosmetico_vestido_o_remove_e_vestir_de_novo_nao_duplica()
	{
		var service = new InventoryService(NewSession(), Cosmetics);
		string[] owned = ["matematica-1-1", "matematica-1-2"];

		service.Wear("p", "matematica-1-1", owned);
		service.Wear("p", "matematica-1-1", owned);
		service.Wear("p", "matematica-1-2", owned);
		service.Remove("p", "matematica-1-1");

		Assert.Equal(["matematica-1-2"], service.Worn("p"));
	}

	[Fact]
	public void Um_Cosmetico_que_saiu_do_catalogo_nao_aparece_como_vestido()
	{
		var session = NewSession();
		session.Commit(d => d with { Profiles = [d.Profiles[0] with { Worn = ["sumiu", "matematica-1-1"] }] });
		var service = new InventoryService(session, Cosmetics);

		Assert.Equal(["matematica-1-1"], service.Worn("p"));
	}

	[Fact]
	public void Vestir_Cosmetico_nao_mexe_no_equipamento_nem_nos_bonus()
	{
		var session = NewSession();
		var gear = new GearService(session, Catalog);
		gear.Equip("p", "matematica", "scout", 1);
		var before = gear.Bonuses("p", "matematica");

		new InventoryService(session, Cosmetics).Wear("p", "matematica-1-1", ["matematica-1-1"]);

		Assert.Equal(before, gear.Bonuses("p", "matematica"));
		Assert.Equal("scout", gear.Get("p", "matematica").In("armor"));
	}

	[Fact]
	public void Item_novo_some_quando_a_crianca_o_ve_e_isso_sobrevive_a_salvar_e_carregar()
	{
		var session = NewSession();
		var service = new InventoryService(session, Cosmetics);
		Assert.True(service.IsNew("p", "gear:open"));

		service.MarkSeen("p", "gear:open");

		var reloaded = new SaveStore(Path.Combine(folder, "save.json")).Load().Value!.Data;
		var again = new InventoryService(new SaveSession(new SaveStore(Path.Combine(folder, "save.json")), reloaded), Cosmetics);
		Assert.False(again.IsNew("p", "gear:open"));
		Assert.True(again.IsNew("p", "gear:crystal"));
	}

	[Fact]
	public void Conta_o_que_e_novo_entre_as_chaves_dadas()
	{
		var service = new InventoryService(NewSession(), Cosmetics);
		service.MarkSeen("p", "gear:open");

		Assert.Equal(2, service.NewCount("p", ["gear:open", "gear:crystal", "cosmetic:matematica-1-1"]));
	}

	[Fact]
	public void O_guia_de_primeira_vez_abre_uma_so_vez()
	{
		var service = new InventoryService(NewSession(), Cosmetics);
		Assert.False(service.GuideSeen("p"));

		service.MarkGuideSeen("p");

		Assert.True(service.GuideSeen("p"));
	}

	[Fact]
	public void Perfil_inexistente_vira_resultado_de_falha_e_nao_excecao()
	{
		var service = new InventoryService(NewSession(), Cosmetics);

		Assert.False(service.MarkSeen("nada", "gear:open").IsOk);
		Assert.False(service.Wear("nada", "matematica-1-1", ["matematica-1-1"]).IsOk);
		Assert.Empty(service.Worn("nada"));
	}

	[Fact]
	public void Cosmetico_com_aparencia_invalida_e_recusado_no_catalogo()
	{
		const string json = """{"cosmetics":[{"id":"a","name":"A","description":"","look":{"kind":"asa-delta","color":"#112233"}}]}""";
		const string badColor = """{"cosmetics":[{"id":"a","name":"A","description":"","look":{"kind":"hat","color":"azul"}}]}""";

		Assert.False(CosmeticCatalog.Parse(json).IsOk);
		Assert.False(CosmeticCatalog.Parse(badColor).IsOk);
	}

	[Fact]
	public void Todo_Cosmetico_do_jogo_tem_aparencia()
	{
		Assert.All(Cosmetics.All, c => Assert.NotNull(c.Look));
	}
}
