using System.Text.Json;
using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Equipment;
using ArquipelagoDoCeu.Saves;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public sealed class GearTests : IDisposable
{
	readonly string folder = Path.Combine(Path.GetTempPath(), "arquipelago-gear-" + Guid.NewGuid().ToString("N"));
	string PathName => Path.Combine(folder, "save.json");

	static SaveData Example => new(1, [new ProfileSave("p", "Luna", 1), new ProfileSave("q", "Davi", 1)], new SettingsSave(Voice: false));

	static readonly GearCatalog Catalog = GearCatalog.Parse(File.ReadAllText(Path.Combine(MissionFiles.RepositoryRoot(), "game", "data", "equipamentos.json")), GearEffects.Default).Value!;

	public void Dispose()
	{
		if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
	}

	[Fact]
	public void Sem_equipamento_o_Personagem_so_tem_os_atributos_base()
	{
		Assert.Equal(new EquipmentBonuses(), Catalog.Bonuses(new Gear()));
	}

	[Fact]
	public void Equipar_com_Maestria_suficiente_guarda_no_espaco_certo()
	{
		var equipped = Catalog.Equip(new Gear(), "scout", mastery: 1);

		Assert.True(equipped.IsOk);
		Assert.Equal("scout", equipped.Value!.In("armor"));
		Assert.Null(equipped.Value.In("helmet"));
		Assert.Equal(1.12f, Catalog.Bonuses(equipped.Value).SpeedMultiplier, 3);
	}

	[Fact]
	public void Maestria_insuficiente_bloqueia_e_diz_quanta_falta()
	{
		var blocked = Catalog.Equip(new Gear(), "guardian", mastery: 0);

		Assert.False(blocked.IsOk);
		Assert.Contains("Maestria", blocked.Error);
		Assert.Contains(Catalog.Find("guardian")!.MinMastery.ToString(System.Globalization.CultureInfo.InvariantCulture), blocked.Error);
	}

	[Fact]
	public void Equipamento_nao_evolui_um_substitui_o_outro_no_mesmo_espaco()
	{
		var first = Catalog.Equip(new Gear(), "scout", 9).Value!;

		var swapped = Catalog.Equip(first, "guardian", 9).Value!;

		Assert.Equal("guardian", swapped.In("armor"));
		Assert.Equal(1f, Catalog.Bonuses(swapped).SpeedMultiplier);
		Assert.Equal(2, Catalog.Bonuses(swapped).ExtraEnergy);
	}

	[Fact]
	public void Os_espacos_somam_mas_nunca_passam_dos_limites_do_experimento()
	{
		var gear = new Gear();
		foreach (var id in new[] { "guardian", "open", "amulet", "scholar", "crystal", "scout", "lantern", "wings" })
			gear = Catalog.Equip(gear, id, 99).Value!;

		var bonus = Catalog.Bonuses(gear);

		Assert.InRange(bonus.ExtraEnergy, 0, 2);
		Assert.InRange(bonus.SpeedMultiplier, 1f, 1.12f);
		Assert.InRange(bonus.InteractionReachBonus, 0f, .5f);
	}

	[Fact]
	public void Acessorio_e_o_terceiro_espaco()
	{
		var gear = Catalog.Equip(new Gear(), "amulet", 99).Value!;

		Assert.Equal("amulet", gear.In("accessory"));
		Assert.Equal(1, Catalog.Bonuses(gear).ExtraEnergy);
	}

	[Fact]
	public void Item_inexistente_e_recusado()
	{
		Assert.False(Catalog.Equip(new Gear(), "nao-existe", 99).IsOk);
	}

	[Fact]
	public void Desequipar_esvazia_so_o_espaco_pedido()
	{
		var gear = Catalog.Equip(Catalog.Equip(new Gear(), "scout", 9).Value!, "open", 9).Value!;

		var bare = Catalog.Unequip(gear, "armor");

		Assert.Null(bare.In("armor"));
		Assert.Equal("open", bare.In("helmet"));
	}

	[Fact]
	public void O_texto_dos_atributos_vem_da_mesma_conta_que_vale_em_jogo()
	{
		var gear = Catalog.Equip(Catalog.Equip(new Gear(), "scout", 9).Value!, "open", 9).Value!;

		Assert.Equal("Velocidade +12% · Energia máxima +1 · Alcance +0", Catalog.Summary(gear));
	}

	[Fact]
	public void Trocar_equipamento_nunca_cura_a_Energia_ferida()
	{
		var energy = new Energy(5);
		energy.Lose(3);
		var guardian = Catalog.Equip(new Gear(), "guardian", 9).Value!;

		energy.SetMaximum(5 + Catalog.Bonuses(guardian).ExtraEnergy);
		energy.SetMaximum(5 + Catalog.Bonuses(Catalog.Unequip(guardian, "armor")).ExtraEnergy);
		energy.SetMaximum(5 + Catalog.Bonuses(guardian).ExtraEnergy);

		Assert.Equal(2, energy.Current);
	}

	[Fact]
	public void O_equipamento_e_por_Papel_e_sobrevive_a_gravar_e_reabrir()
	{
		var store = new SaveStore(PathName);
		var service = new GearService(new SaveSession(store, Example), Catalog);

		Assert.True(service.Equip("p", "matematica", "scout", mastery: 1).IsOk);
		Assert.True(service.Equip("p", "portugues", "open", mastery: 1).IsOk);

		var reopened = new GearService(new SaveSession(store, store.Load().Value!.Data), Catalog);
		Assert.Equal("scout", reopened.Get("p", "matematica").In("armor"));
		Assert.Null(reopened.Get("p", "matematica").In("helmet"));
		Assert.Equal("open", reopened.Get("p", "portugues").In("helmet"));
		Assert.Equal(new Gear(), reopened.Get("q", "matematica"));
		Assert.Equal(1, reopened.Bonuses("p", "portugues").ExtraEnergy);
	}

	[Fact]
	public void Equipar_sem_Maestria_nao_grava_nada()
	{
		Directory.CreateDirectory(folder);
		var store = new SaveStore(PathName);
		var service = new GearService(new SaveSession(store, Example), Catalog);

		var blocked = service.Equip("p", "matematica", "guardian", mastery: 0);

		Assert.False(blocked.IsOk);
		Assert.False(File.Exists(PathName));
	}

	[Fact]
	public void Save_antigo_sem_equipamento_abre_vazio_e_nao_e_regravado_ao_consultar()
	{
		Directory.CreateDirectory(folder);
		const string json = """{"version":1,"profiles":[{"id":"p","nickname":"Luna","level":1}]}""";
		File.WriteAllText(PathName, json);
		var store = new SaveStore(PathName);
		var service = new GearService(new SaveSession(store, store.Load().Value!.Data), Catalog);

		Assert.Equal(new Gear(), service.Get("p", "matematica"));
		Assert.Equal(json, File.ReadAllText(PathName));
	}

	[Fact]
	public void Save_antigo_com_tres_campos_fixos_abre_e_continua_com_o_mesmo_formato()
	{
		Directory.CreateDirectory(folder);
		const string json = """{"version":1,"profiles":[{"id":"p","nickname":"Luna","level":1,"gear":{"matematica":{"helmet":"open","armor":"scout","accessory":null}}}]}""";
		File.WriteAllText(PathName, json);
		var store = new SaveStore(PathName);
		var service = new GearService(new SaveSession(store, store.Load().Value!.Data), Catalog);

		var gear = service.Get("p", "matematica");

		Assert.Equal("scout", gear.In("armor"));
		Assert.Equal("open", gear.In("helmet"));
		Assert.Null(gear.In("accessory"));
		Assert.Equal(json, File.ReadAllText(PathName));
	}

	[Fact]
	public void O_equipamento_grava_como_objeto_simples_de_espaco_para_peca()
	{
		var gear = new Gear().With("armor", "scout").With("helmet", "open");

		var written = JsonSerializer.Serialize(gear);

		Assert.Equal("""{"armor":"scout","helmet":"open"}""", written);
		Assert.Equal(gear, JsonSerializer.Deserialize<Gear>(written));
	}

	[Fact]
	public void Item_guardado_que_saiu_do_catalogo_e_ignorado_ao_calcular()
	{
		Directory.CreateDirectory(folder);
		File.WriteAllText(PathName, """{"version":1,"profiles":[{"id":"p","nickname":"Luna","level":1,"gear":{"matematica":{"armor":"sumiu","helmet":"open","capa":"antiga"}}}]}""");
		var store = new SaveStore(PathName);
		var service = new GearService(new SaveSession(store, store.Load().Value!.Data), Catalog);

		var bonus = service.Bonuses("p", "matematica");

		Assert.Equal(1, bonus.ExtraEnergy);
		Assert.Equal(1f, bonus.SpeedMultiplier);
	}

	static string Json(string slots, string effects, params string[] items) =>
		"{\"slots\":[" + slots + "],\"effects\":[" + effects + "],\"items\":[" + string.Join(",", items) + "]}";

	const string Basic = "{\"id\":\"helmet\",\"name\":\"Capacete\"},{\"id\":\"armor\",\"name\":\"Armadura\"}";

	const string BasicEffects = "{\"id\":\"energy\",\"limit\":2},{\"id\":\"speed\",\"limit\":0.12}";

	static string Item(string id, string slot, string effect, double value, int mastery = 0) =>
		"{\"id\":\"" + id + "\",\"name\":\"N\",\"description\":\"\",\"slot\":\"" + slot + "\",\"effect\":\"" + effect + "\",\"value\":"
		+ value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"minMastery\":" + mastery + "}";

	[Fact]
	public void Um_espaco_novo_entra_so_com_dados()
	{
		var json = Json(Basic + ",{\"id\":\"boots\",\"name\":\"Botas\"}", BasicEffects, Item("trail", "boots", "speed", 0.05));
		var catalog = GearCatalog.Parse(json, GearEffects.Default).Value!;

		var gear = catalog.Equip(new Gear(), "trail", 0).Value!;

		Assert.Equal("trail", gear.In("boots"));
		Assert.Equal(1.05f, catalog.Bonuses(gear).SpeedMultiplier, 3);
		Assert.Contains(catalog.Slots, s => s.Id == "boots" && s.Name == "Botas");
	}

	sealed class LuckEffect : IGearEffect
	{
		public string Id => "luck";

		public EquipmentBonuses Apply(EquipmentBonuses bonuses, double total) =>
			bonuses with { Extras = new Dictionary<string, double>(bonuses.Extras ?? new Dictionary<string, double>()) { ["luck"] = total } };

		public string Describe(double total) => $"Sorte +{total}";
	}

	[Fact]
	public void Um_efeito_novo_entra_com_uma_classe_e_o_registro_sem_mexer_no_calculo()
	{
		var effects = GearEffects.Default.With(new LuckEffect());
		var json = Json(Basic, BasicEffects + ",{\"id\":\"luck\",\"limit\":3}", Item("clover", "helmet", "luck", 2), Item("more", "armor", "luck", 3));
		var catalog = GearCatalog.Parse(json, effects).Value!;

		var gear = catalog.Equip(catalog.Equip(new Gear(), "clover", 0).Value!, "more", 0).Value!;
		var bonus = catalog.Bonuses(gear);

		Assert.Equal(3, bonus.Extras!["luck"]);
		Assert.Contains("Sorte +3", catalog.Summary(gear));
	}

	[Fact]
	public void O_catalogo_recusa_espaco_ou_efeito_que_nao_existem_e_valor_acima_do_limite()
	{
		Assert.False(GearCatalog.Parse(Json(Basic, BasicEffects, Item("a", "capa", "speed", 0.01)), GearEffects.Default).IsOk);
		Assert.False(GearCatalog.Parse(Json(Basic, BasicEffects, Item("a", "armor", "sorte", 1)), GearEffects.Default).IsOk);
		Assert.False(GearCatalog.Parse(Json(Basic, BasicEffects, Item("a", "armor", "energy", 3)), GearEffects.Default).IsOk);
		Assert.False(GearCatalog.Parse(Json(Basic, "{\"id\":\"luck\",\"limit\":3}", Item("a", "armor", "luck", 1)), GearEffects.Default).IsOk);
		Assert.True(GearCatalog.Parse(Json(Basic, BasicEffects, Item("a", "armor", "energy", 2)), GearEffects.Default).IsOk);
	}

	[Fact]
	public void Catalogo_recusa_json_quebrado_item_repetido_e_espaco_repetido()
	{
		Assert.False(GearCatalog.Parse("{", GearEffects.Default).IsOk);
		Assert.False(GearCatalog.Parse(Json(Basic, BasicEffects), GearEffects.Default).IsOk);
		Assert.False(GearCatalog.Parse(Json(Basic, BasicEffects, Item("a", "armor", "energy", 1), Item("a", "helmet", "energy", 1)), GearEffects.Default).IsOk);
		Assert.False(GearCatalog.Parse(Json(Basic + ",{\"id\":\"armor\",\"name\":\"Outra\"}", BasicEffects, Item("a", "armor", "energy", 1)), GearEffects.Default).IsOk);
	}
}
