using ArquipelagoDoCeu.Rules;
using ArquipelagoDoCeu.World;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

// A ilha principal ampliada e as ilhas das torres e do Mercador, ligadas por pontes a pé (#154, #155).
public class LobbyIslandsTests
{
	static readonly LobbyRules Rules =
		GameRules.Parse(File.ReadAllText(Path.Combine(MissionFiles.RepositoryRoot(), "game", "data", "rules.json"))).Value!.Lobby;

	static readonly LobbyMap Map = LobbyMap.Build(Rules).Value!;

	[Fact]
	public void A_ilha_principal_e_bem_maior_que_a_de_antes()
	{
		Assert.True(Rules.IslandRadius >= 7);
		Assert.True(Map.Main.Count > 91);
	}

	[Fact]
	public void Cada_torre_e_o_Mercador_ficam_em_ilha_propria()
	{
		Assert.Equal(["matematica", "portugues", "mercador"], Rules.Islands.Select(i => i.Id));
		Assert.All(Rules.Islands, i => Assert.True(Map.Islands[i.Id].Count >= 7));
	}

	[Fact]
	public void Da_ilha_principal_se_chega_a_pe_a_cada_ilha_pela_ponte()
	{
		var grid = Map.Grid(Rules);

		foreach (var island in Rules.Islands)
		{
			var gate = Rules.Objects.First(o => o.Id == (island.Id == "mercador" ? "mercador" : "torre-" + island.Id));
			var near = HexCell(gate.Q, gate.R).Neighbors().First(grid.IsWalkable);
			Assert.NotNull(grid.FindPath(new HexCell(0, 0), near));
			Assert.NotEmpty(Map.Bridges[island.Id]);
			Assert.All(Map.Bridges[island.Id], c => Assert.True(grid.IsWalkable(c)));
		}
	}

	[Fact]
	public void A_ponte_liga_a_borda_da_ilha_principal_ao_chao_da_outra_ilha()
	{
		foreach (var island in Rules.Islands)
		{
			var bridge = Map.Bridges[island.Id];
			Assert.Contains(bridge, c => c.Neighbors().Any(Map.Main.Contains));
			Assert.Contains(bridge, c => c.Neighbors().Any(Map.Islands[island.Id].Contains));
			Assert.DoesNotContain(bridge, c => Map.Main.Contains(c) || Map.Islands[island.Id].Contains(c));
		}
	}

	[Fact]
	public void Os_lugares_e_objetos_de_antes_continuam_no_chao_da_ilha_principal()
	{
		var grid = Map.Grid(Rules);

		foreach (var item in Rules.Objects.Where(o => o.Id is "album" or "portal" or "guia" or "boneco" or "mestre"))
			Assert.Contains(grid.Cells, c => c == HexCell(item.Q, item.R));
	}

	[Fact]
	public void As_ilhas_nao_se_tocam_e_nenhum_objeto_cai_na_agua()
	{
		var all = Rules.Islands.Select(i => Map.Islands[i.Id]).Prepend(Map.Main).ToList();
		for (var a = 0; a < all.Count; a++)
			for (var b = a + 1; b < all.Count; b++)
				Assert.Empty(all[a].Intersect(all[b]));

		var grid = Map.Grid(Rules);
		foreach (var item in Rules.Objects) Assert.Contains(grid.Cells, c => c == HexCell(item.Q, item.R));
	}

	static HexCell HexCell(int q, int r) => new(q, r);
}
