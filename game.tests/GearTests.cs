using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Equipment;
using ArquipelagoDoCeu.Saves;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public sealed class GearTests : IDisposable
{
	readonly string folder = Path.Combine(Path.GetTempPath(), "arquipelago-gear-" + Guid.NewGuid().ToString("N"));
	string PathName => Path.Combine(folder, "save.json");
	SaveData Example => new(1, [new ProfileSave("p", "Luna", 1), new ProfileSave("q", "Davi", 1)], new SettingsSave(Voice: false));

	static readonly GearCatalog Catalog = GearCatalog.Parse(File.ReadAllText(Path.Combine(MissionFiles.RepositoryRoot(), "game", "data", "equipamentos.json"))).Value!;

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
		Assert.Equal("scout", equipped.Value!.Armor);
		Assert.Null(equipped.Value.Helmet);
		Assert.Equal(1.12f, Catalog.Bonuses(equipped.Value).SpeedMultiplier, 3);
	}

	[Fact]
	public void Maestria_insuficiente_bloqueia_e_diz_quanta_falta()
	{
		var blocked = Catalog.Equip(new Gear(), "guardian", mastery: 0);

		Assert.False(blocked.IsOk);
		Assert.Contains("Maestria", blocked.Error);
		Assert.Contains(Catalog.Find("guardian")!.MinMastery.ToString(), blocked.Error);
	}

	[Fact]
	public void Equipamento_nao_evolui_um_substitui_o_outro_no_mesmo_espaco()
	{
		var first = Catalog.Equip(new Gear(), "scout", 9).Value!;

		var swapped = Catalog.Equip(first, "guardian", 9).Value!;

		Assert.Equal("guardian", swapped.Armor);
		Assert.Equal(1f, Catalog.Bonuses(swapped).SpeedMultiplier);
		Assert.Equal(2, Catalog.Bonuses(swapped).ExtraEnergy);
	}

	[Fact]
	public void Os_tres_espacos_somam_mas_nunca_passam_dos_limites_do_experimento()
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

		Assert.Equal("amulet", gear.Accessory);
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

		var bare = Catalog.Unequip(gear, GearSlot.Armor);

		Assert.Null(bare.Armor);
		Assert.Equal("open", bare.Helmet);
	}

	[Fact]
	public void Trocar_equipamento_nunca_cura_a_Energia_ferida()
	{
		var energy = new Energy(5);
		energy.Lose(3);
		var guardian = Catalog.Equip(new Gear(), "guardian", 9).Value!;

		energy.SetMaximum(5 + Catalog.Bonuses(guardian).ExtraEnergy);
		energy.SetMaximum(5 + Catalog.Bonuses(Catalog.Unequip(guardian, GearSlot.Armor)).ExtraEnergy);
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
		Assert.Equal("scout", reopened.Get("p", "matematica").Armor);
		Assert.Null(reopened.Get("p", "matematica").Helmet);
		Assert.Equal("open", reopened.Get("p", "portugues").Helmet);
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
	public void Item_guardado_que_saiu_do_catalogo_e_ignorado_ao_calcular()
	{
		Directory.CreateDirectory(folder);
		File.WriteAllText(PathName, """{"version":1,"profiles":[{"id":"p","nickname":"Luna","level":1,"gear":{"matematica":{"armor":"sumiu","helmet":"open"}}}]}""");
		var store = new SaveStore(PathName);
		var service = new GearService(new SaveSession(store, store.Load().Value!.Data), Catalog);

		var bonus = service.Bonuses("p", "matematica");

		Assert.Equal(1, bonus.ExtraEnergy);
		Assert.Equal(1f, bonus.SpeedMultiplier);
	}

	// Os cinco itens do experimento do PR #97 mantêm os mesmos atributos: o catálogo em dados é a fonte, sem divergir do que já havia.
	[Fact]
	public void Os_itens_do_experimento_do_97_continuam_com_os_mesmos_atributos()
	{
		foreach (var armor in new[] { "original", "scout", "guardian", "scholar" })
			foreach (var helmet in new[] { "none", "open", "crystal" })
			{
				var old = EquipmentCatalog.Bonuses(new EquipmentLoadout(armor, helmet));
				var gear = new Gear(Helmet: helmet == "none" ? null : helmet, Armor: armor == "original" ? null : armor);
				var now = Catalog.Bonuses(gear);

				Assert.Equal(old.ExtraEnergy, now.ExtraEnergy);
				Assert.Equal(old.SpeedMultiplier, now.SpeedMultiplier, 3);
				Assert.Equal(old.InteractionReachBonus, now.InteractionReachBonus, 3);
			}
	}

	static string Json(params string[] items) =>
		"{\"limits\":{\"energy\":2,\"speed\":0.12,\"reach\":0.5},\"items\":[" + string.Join(",", items) + "]}";

	static string Item(string id, string slot, double value) =>
		"{\"id\":\"" + id + "\",\"name\":\"N\",\"description\":\"\",\"slot\":\"" + slot + "\",\"attribute\":\"energy\",\"value\":" + value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"minMastery\":0}";

	[Fact]
	public void Catalogo_recusa_json_quebrado_item_repetido_ou_valor_fora_do_limite()
	{
		Assert.False(GearCatalog.Parse("{").IsOk);
		Assert.False(GearCatalog.Parse(Json()).IsOk);
		Assert.False(GearCatalog.Parse(Json(Item("a", "armor", 1), Item("a", "helmet", 1))).IsOk);
		Assert.False(GearCatalog.Parse(Json(Item("a", "armor", 3))).IsOk);
		Assert.False(GearCatalog.Parse(Json(Item("a", "capa", 1))).IsOk);
		Assert.True(GearCatalog.Parse(Json(Item("a", "armor", 2))).IsOk);
	}
}
