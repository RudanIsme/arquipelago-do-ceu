using ArquipelagoDoCeu.Equipment;
using ArquipelagoDoCeu.Saves;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public sealed class EquipmentTests : IDisposable
{
    readonly string folder = Path.Combine(Path.GetTempPath(), "arquipelago-equipment-" + Guid.NewGuid().ToString("N"));
    string PathName => Path.Combine(folder, "save.json");
    SaveData Example => new(1, [new ProfileSave("p", "Luna", 2, new Dictionary<string, string> { ["hair"] = "blue" })], new SettingsSave(Voice: false));

    [Fact]
    public void Save_antigo_sem_journey_continua_valido_e_nao_e_regravado_ao_consultar()
    {
        Directory.CreateDirectory(folder);
        const string json = """{"version":1,"profiles":[{"id":"p","nickname":"Luna","level":2}]}""";
        File.WriteAllText(PathName, json);
        var store = new SaveStore(PathName);
        var loaded = store.Load();
        Assert.True(loaded.IsOk);
        Assert.Equal(SaveNotice.None, loaded.Value!.Notice);
        Assert.Null(loaded.Value.Data.Profiles[0].Journey);
        var service = new JourneyService(new SaveSession(store, loaded.Value.Data));
        Assert.Equal(new EquipmentLoadout(), service.Get("p").Equipment);
        Assert.Equal(json, File.ReadAllText(PathName));
    }

    [Fact]
    public void Equipamento_cores_e_variante_persistem_sem_apagar_perfil_ou_settings()
    {
        var store = new SaveStore(PathName);
        var session = new SaveSession(store, Example);
        var value = new JourneyPreferences("crystal", EquipmentCatalog.Preset("crystal") with { ClothColor = "#abcdef" });
        Assert.True(new JourneyService(session).Set("p", value).IsOk);
        var loaded = store.Load().Value!.Data;
        Assert.Equal(JourneyPreferences.Normalize(value), new JourneyService(new SaveSession(store, loaded)).Get("p"));
        Assert.Equal("blue", loaded.Profiles[0].Appearance!["hair"]);
        Assert.Equal(2, loaded.Profiles[0].Level);
        Assert.False(loaded.Settings!.Voice);
    }

    [Fact]
    public void Dados_invalidos_sao_normalizados_de_forma_estavel()
    {
        var invalid = new JourneyPreferences("missing", new EquipmentLoadout("bad", null!, "#abcd", "javascript:test", "#abcdef"));
        var result = JourneyPreferences.Normalize(invalid);
        Assert.Equal("book", result.Variant);
        Assert.Equal(new EquipmentLoadout(AccentColor: "#ABCDEF"), result.Equipment);
        Assert.Equal(result, JourneyPreferences.Normalize(result));
        Assert.Equal(new EquipmentLoadout(), EquipmentCatalog.Normalize(null));
    }

    [Fact]
    public void Dez_trocas_de_interface_A_D_A_preservam_conjunto_e_bonus()
    {
        var service = new JourneyService(new SaveSession(new SaveStore(PathName), Example));
        var equipment = EquipmentCatalog.Preset("adventure");
        Assert.True(service.Set("p", new("minimal", equipment)).IsOk);
        var bonus = EquipmentCatalog.Bonuses(equipment);
        for (var i = 0; i < 10; i++)
        {
            foreach (var variant in new[] { "minimal", "book", "adventure", "crystal", "minimal" })
            {
                Assert.True(service.Set("p", service.Get("p") with { Variant = variant }).IsOk);
                Assert.Equal(equipment, service.Get("p").Equipment);
                Assert.Equal(bonus, EquipmentCatalog.Bonuses(service.Get("p").Equipment));
            }
        }
    }

    [Fact]
    public void Todos_conjuntos_respeitam_limites_e_original_nao_tem_bonus()
    {
        foreach (var armor in EquipmentCatalog.Armors)
        foreach (var helmet in EquipmentCatalog.Helmets)
        {
            var bonus = EquipmentCatalog.Bonuses(new(armor.Id, helmet.Id));
            Assert.InRange(bonus.SpeedMultiplier, 1, 1.12f);
            Assert.InRange(bonus.ExtraEnergy, 0, 2);
            Assert.InRange(bonus.InteractionReachBonus, 0, .5f);
        }
        Assert.Equal(new EquipmentBonuses(), EquipmentCatalog.Bonuses(EquipmentCatalog.Preset("base")));
        Assert.Equal(4, new[] { "minimal", "book", "adventure", "crystal" }.Select(EquipmentCatalog.Preset).Distinct().Count());
        Assert.Equal(2, EquipmentCatalog.Bonuses(new("guardian", "open")).ExtraEnergy);
        Assert.Equal(.5f, EquipmentCatalog.Bonuses(new("scholar", "crystal")).InteractionReachBonus);
    }

    [Fact]
    public void Perfil_inexistente_nao_cria_save()
    {
        var service = new JourneyService(new SaveSession(new SaveStore(PathName), Example));
        Assert.False(service.Set("missing", new()).IsOk);
        Assert.False(File.Exists(PathName));
    }

    [Fact]
    public void Falha_de_gravacao_preserva_estado_em_memoria()
    {
        Directory.CreateDirectory(PathName);
        var session = new SaveSession(new SaveStore(PathName), Example);
        var result = new JourneyService(session).Set("p", new("book", EquipmentCatalog.Preset("book")));
        Assert.False(result.IsOk);
        Assert.Null(session.Data.Profiles[0].Journey);
    }

    public void Dispose() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
}
