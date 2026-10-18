using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.World;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

// O desenho do Lobby em zonas (#193): tudo relativo ao raio da ilha, com espaço livre para andar e sem canto vazio.
public class LobbyPlanTests
{
	static readonly LobbyRules Rules =
		GameRules.Parse(File.ReadAllText(Path.Combine(MissionFiles.RepositoryRoot(), "game", "data", "rules.json"))).Value!.Lobby;

	static readonly LobbyMap Map = LobbyMap.Build(Rules).Value!;

	static LobbyPlan Plan => LobbyPlan.Build(Rules, Map).Value!;

	[Fact]
	public void O_desenho_dos_dados_cabe_na_ilha()
	{
		var built = LobbyPlan.Build(Rules, Map);

		Assert.True(built.IsOk, built.Error);
		Assert.NotEmpty(built.Value!.Lake);
		Assert.Equal(5, built.Value.Zones.Count);
	}

	[Fact]
	public void Cada_zona_tem_o_lugar_do_proprio_objeto_e_fica_em_terra()
	{
		foreach (var (id, cell) in Plan.Zones)
		{
			var item = Rules.Objects.Single(o => o.Id == id);
			Assert.Equal(cell, new HexCell(item.Q, item.R));
			Assert.Contains(cell, Map.Main);
			Assert.DoesNotContain(cell, Plan.Lake);
		}
	}

	[Fact]
	public void Do_anel_se_chega_a_pe_a_cada_objeto_e_a_cada_ponte_sem_cruzar_a_lagoa()
	{
		var blocked = Rules.Objects.Select(o => new HexCell(o.Q, o.R)).Concat(Plan.Lake).Concat(Plan.Trees);
		var grid = new HexGrid(Map.AllCells, blocked);
		var start = Plan.Path.First();

		foreach (var item in Rules.Objects)
		{
			var near = new HexCell(item.Q, item.R).Neighbors().First(grid.IsWalkable);
			Assert.NotNull(grid.FindPath(start, near));
		}
	}

	[Fact]
	public void Nenhum_canto_grande_da_ilha_fica_vazio()
	{
		var features = Plan.Lake.Concat(Plan.Path).Concat(Plan.Trees).Concat(Plan.Zones.Values).ToList();

		var empty = Map.Main.Where(c => features.All(f => f.DistanceTo(c) > 3)).ToList();

		Assert.True(empty.Count == 0, $"{empty.Count} hexágonos longe de tudo, a partir de {empty.FirstOrDefault()}");
	}

	[Fact]
	public void Sobra_espaco_livre_para_andar_e_testar()
	{
		var taken = Plan.Lake.Concat(Plan.Trees).Concat(Rules.Objects.Select(o => new HexCell(o.Q, o.R))).ToHashSet();

		var free = Map.Main.Count(c => !taken.Contains(c));

		Assert.True(free >= Map.Main.Count * 0.8, $"só {free} de {Map.Main.Count} hexágonos livres");
	}

	[Theory]
	[InlineData(8)]
	[InlineData(10)]
	[InlineData(12)]
	public void Mudar_o_raio_da_ilha_nao_quebra_o_desenho(int radius)
	{
		var bigger = Rules with { IslandRadius = radius, Islands = [] };
		var map = LobbyMap.Build(bigger).Value!;

		var built = LobbyPlan.Build(bigger, map);

		Assert.True(built.IsOk, built.Error);
		Assert.All(built.Value!.Zones.Values, c => Assert.Contains(c, map.Main));
	}

	[Fact]
	public void Sem_desenho_nos_dados_o_Lobby_fica_so_de_grama()
	{
		var plain = Rules with { Layout = null };

		var built = LobbyPlan.Build(plain, Map);

		Assert.True(built.IsOk);
		Assert.Empty(built.Value!.Lake);
	}
}
