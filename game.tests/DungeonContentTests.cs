using ArquipelagoDoCeu.Combat;
using ArquipelagoDoCeu.Dungeon;
using ArquipelagoDoCeu.Missions;
using Xunit;

namespace ArquipelagoDoCeu.Tests;

public class DungeonContentTests
{
	static readonly IReadOnlyList<Mission> Missions =
		MissionLoader.Create(File.ReadAllText(MissionFiles.SchemaPath)).Value!.Load(MissionFiles.All()).Missions;

	static readonly ConceptNames Names = ConceptNames.Parse(File.ReadAllText(Path.Combine(MissionFiles.RepositoryRoot(), "game", "data", "conceitos.json"))).Value!;

	[Fact]
	public void Todo_Conceito_das_Missoes_tem_nome_nos_dados()
	{
		var missing = Missions.Select(m => m.Concept).Distinct().Where(c => !Names.Ids.Contains(c)).ToList();

		Assert.Empty(missing);
	}

	[Fact]
	public void O_nome_vem_dos_dados_e_na_falta_dele_o_id_vira_texto_legivel()
	{
		Assert.Equal("Adição até 10", Names.NameOf("adicao-ate-10"));
		Assert.Equal("Conceito novo aqui", Names.NameOf("conceito-novo-aqui"));
	}

	[Fact]
	public void Arquivo_de_nomes_quebrado_ou_vazio_e_recusado()
	{
		Assert.False(ConceptNames.Parse("{").IsOk);
		Assert.False(ConceptNames.Parse("""{"concepts":{}}""").IsOk);
		Assert.False(ConceptNames.Parse("""{"concepts":{"a":" "}}""").IsOk);
	}

	[Fact]
	public void O_Conceito_do_Andar_e_o_que_mais_aparece_na_dificuldade_dele()
	{
		var setup = DungeonCombat.Setup(Missions, "matematica", 1, 1).Value!;

		var concept = DungeonCombat.ConceptOf(Missions, setup);

		Assert.NotNull(concept);
		Assert.Contains(Missions, m => m.Concept == concept && m.Discipline == setup.Discipline && m.Year == setup.Year && m.Difficulty == setup.Difficulty);
	}

	[Fact]
	public void Todo_Andar_de_toda_torre_e_Nivel_tem_Conceito()
	{
		foreach (var discipline in new[] { "matematica", "portugues" })
			for (var level = 1; level <= 3; level++)
				for (var floor = 1; floor <= 6; floor++)
				{
					var setup = DungeonCombat.Setup(Missions, discipline, level, floor).Value!;
					Assert.NotNull(DungeonCombat.ConceptOf(Missions, setup));
				}
	}

	[Fact]
	public void A_dificuldade_da_Livre_procura_por_perto_uma_com_problema_novo()
	{
		var history = new MissionHistory();
		var first = DungeonCombat.FreeSetup(Missions, history, "matematica", 1, 5).Value!;
		foreach (var m in Missions.Where(m => m.Discipline == first.Discipline && m.Year == first.Year && m.Difficulty == first.Difficulty)) history.Add(m.Id);

		var next = DungeonCombat.FreeSetup(Missions, history, "matematica", 1, 5).Value!;

		Assert.NotEqual(first.Difficulty, next.Difficulty);
		Assert.Equal(first.Year, next.Year);
	}
}
