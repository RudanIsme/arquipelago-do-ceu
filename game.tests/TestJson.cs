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
		    { "id": "accessory", "name": "Acessório", "options": [] } ] },
		  "settings": {
		    "textSizes": [
		      { "id": "normal", "name": "Normal", "scale": 1.0 },
		      { "id": "grande", "name": "Grande", "scale": 1.25 } ],
		    "controls": [
		      { "id": "mouse", "name": "Mouse" },
		      { "id": "teclado", "name": "Teclado" },
		      { "id": "toque", "name": "Toque" } ],
		    "voiceSpeeds": [
		      { "id": "devagar", "name": "Devagar", "rate": 0.8 },
		      { "id": "normal", "name": "Normal", "rate": 1.0 } ],
		    "defaults": { "textSize": "normal", "voice": true, "reduceMotion": false, "dyslexiaFont": false, "calmMode": false, "controls": "mouse", "voiceSpeed": "normal" } },
		  "lobby": {
		    "interactionReach": 2.0,
		    "places": [
		      { "id": "matematica", "name": "Torre dos Números", "sign": "Em construção", "model": "torre-numeros", "x": -10, "z": -6 },
		      { "id": "ciencias", "name": "Ilha das Ciências", "sign": "Em breve", "x": 10, "z": 6 } ],
		    "objects": [
		      { "id": "album", "name": "Álbum", "prompt": "Abrir o Álbum", "model": "atril", "q": -2, "r": -2 },
		      { "id": "portal", "name": "Portal", "prompt": "Usar o portal", "model": "portal", "q": -3, "r": 3 },
		      { "id": "guia", "name": "Guia", "prompt": "Falar com o Guia", "model": "guia", "q": 1, "r": 1, "lines": ["Olá!", "Vamos explorar?"] } ] } }
		""";
}
