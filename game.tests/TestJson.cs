namespace ArquipelagoDoCeu.Tests;

// JSON de regras válido, compartilhado pelos testes. Cada teste que precisa de um valor ruim troca um trecho dele.
static class TestJson
{
	public const string Rules = """
		{ "gameName": "Arquipélago do Céu",
		  "roles": { "mathematics": "Matemático", "portuguese": "Escriba" },
		  "maxEnergy": 5,
		  "adaptive": { "streakToLevelUp": 3, "mistakesToLevelDown": 2, "hintsToLevelDown": 2 },
		  "leitnerIntervalsDays": [1, 2, 4, 8, 16],
		  "profile": { "maxNicknameLength": 12, "maxProfiles": 6 },
		  "appearance": { "categories": [
		    { "id": "skinTone", "name": "Tom de pele", "options": [
		      { "id": "marfim", "name": "Marfim", "color": "#F7D9C4" },
		      { "id": "cafe", "name": "Café", "color": "#8D5A3A" } ] },
		    { "id": "clothesColor", "name": "Cor da roupa", "options": [
		      { "id": "bege", "name": "Bege", "color": "#C9BBA3" },
		      { "id": "azul", "name": "Azul", "color": "#2E7DE8" } ] },
		    { "id": "accessory", "name": "Acessório", "options": [] } ] } }
		""";
}
