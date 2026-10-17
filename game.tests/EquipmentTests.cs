using ArquipelagoDoCeu.Equipment;
using ArquipelagoDoCeu.Saves;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public sealed class EquipmentTests : IDisposable
{
	readonly string folder = Path.Combine(Path.GetTempPath(), "arquipelago-equipment-" + Guid.NewGuid().ToString("N"));
	string PathName => Path.Combine(folder, "save.json");

	[Fact]
	public void Save_antigo_sem_campos_do_Inventario_continua_valido_e_nao_e_regravado_ao_ler()
	{
		Directory.CreateDirectory(folder);
		const string json = """{"version":1,"profiles":[{"id":"p","nickname":"Luna","level":2}]}""";
		File.WriteAllText(PathName, json);

		var loaded = new SaveStore(PathName).Load();

		Assert.True(loaded.IsOk);
		Assert.Equal(SaveNotice.None, loaded.Value!.Notice);
		Assert.Null(loaded.Value.Data.Profiles[0].Worn);
		Assert.Null(loaded.Value.Data.Profiles[0].Seen);
		Assert.Equal(json, File.ReadAllText(PathName));
	}

	[Fact]
	public void Save_do_experimento_com_journey_ainda_abre_e_o_resto_do_Perfil_vale()
	{
		Directory.CreateDirectory(folder);
		const string json = """{"version":1,"profiles":[{"id":"p","nickname":"Luna","level":2,"journey":{"variant":"crystal","equipment":{"armor":"scout"}}}]}""";
		File.WriteAllText(PathName, json);

		var loaded = new SaveStore(PathName).Load();

		Assert.True(loaded.IsOk);
		Assert.Equal("Luna", loaded.Value!.Data.Profiles[0].Nickname);
		Assert.Equal(2, loaded.Value.Data.Profiles[0].Level);
	}

	[Fact]
	public void O_conjunto_vestido_vira_a_aparencia_do_Personagem()
	{
		var gear = new Gear().With("armor", "guardian").With("helmet", "crystal");

		var look = EquipmentLooks.For(gear);

		Assert.Equal("guardian", look.Armor);
		Assert.Equal("crystal", look.Helmet);
	}

	[Fact]
	public void Peca_sem_modelo_3D_aparece_como_a_aparencia_original()
	{
		var gear = new Gear().With("armor", "peca-sem-modelo").With("helmet", "amuleto").With("accessory", "amulet");

		var look = EquipmentLooks.For(gear);

		Assert.Equal("original", look.Armor);
		Assert.Equal("none", look.Helmet);
	}

	[Fact]
	public void Os_Cosmeticos_vestidos_acompanham_a_aparencia_sem_mudar_o_equipamento()
	{
		var gear = new Gear().With("armor", "scout");
		CosmeticLook[] looks = [new("bow", "#112233")];

		var look = EquipmentLooks.For(gear, looks);

		Assert.Equal(looks, look.Cosmetics);
		Assert.Equal("scout", look.Armor);
	}

	public void Dispose()
	{
		if (Directory.Exists(folder)) Directory.Delete(folder, true);
	}
}
