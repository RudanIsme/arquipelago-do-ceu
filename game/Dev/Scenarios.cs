using System.IO;
using System.Threading.Tasks;
using Godot;

namespace ArquipelagoDoCeu;

static class Scenarios
{
	public static async Task Run(DevDriver d, string name)
	{
		switch (name)
		{
			case "criar":
				await d.Shot("01-criar");
				await d.Press("Criar");
				await d.Shot("02-apelido-vazio");
				await d.Type("Lu@na");
				await d.Press("Criar");
				await d.Shot("03-apelido-invalido");
				await d.Type("Luna");
				await d.Press("Criar");
				await d.Shot("04-lobby");
				break;
			case "escolher":
				await d.Shot("01-lista");
				await d.Press("Editar");
				await d.Shot("02-editar");
				await d.Type("Luninha");
				await d.Press("Salvar");
				await d.Shot("03-lista-depois");
				await d.Press("Theo");
				await d.Shot("04-lobby");
				break;
			case "personagem":
				await d.Shot("01-lobby");
				await d.Press("Personagem");
				await d.Wait(0.8);
				await d.Shot("02-editor-padrao");
				await d.Press("Café");
				await d.Press("Azul");
				await d.Wait(0.5);
				await d.Shot("03-editor-cafe-azul");
				await d.Press("Pronto");
				await d.Shot("04-fechado");
				break;
			case "movimento":
				await d.Shot("01-parado");
				await d.Hold(Controls.Right, 0.8);
				await d.Shot("02-andou-para-a-direita");
				await d.Hold(Controls.Left, 2.6);
				await d.Shot("03-contra-a-arvore");
				await d.Hold(Controls.Up, 4.0);
				await d.Shot("04-contra-a-borda");
				break;
			case "torre":
				await DungeonScenarios.Walkthrough(d);
				break;
			case "torre-letras":
				await DungeonScenarios.Letters(d);
				break;
			case "mestre":
				await DungeonScenarios.Master(d);
				break;
			case "torre-epico":
				await DungeonScenarios.Epic(d);
				break;
			case "torre-atalho":
				await DungeonScenarios.Shortcut(d, fail: false);
				break;
			case "torre-atalho-erro":
				await DungeonScenarios.Shortcut(d, fail: true);
				break;
			case "torre-itens":
				await DungeonScenarios.Items(d);
				break;
			case "torre-fast-travel":
				await DungeonScenarios.FastTravel(d);
				break;
			case "torre-salas":
				await DungeonScenarios.Rooms(d);
				break;
			case "cosmeticos":
				await DungeonScenarios.Cosmetics(d);
				break;
			case "mercador":
				await DungeonScenarios.Merchant(d);
				break;
			case "combate-niveis":
				await DungeonScenarios.CombatLevels(d);
				break;
			case "combate-dinheiro":
				await DungeonScenarios.CombatMoney(d);
				break;
			case "combate":
				await DungeonScenarios.Combat(d);
				break;
			case "kit-medidas":
				foreach (var asset in new[] { "hex-base-v02", "margem-reta", "margem-convexa", "margem-concava", "ponte-piso", "ponte-guarda-corpo", "ponte-cabeceira", "rocha-inferior-avaliacao", "grama-tres-pontas-v01" })
				{
					var node = GD.Load<PackedScene>($"res://assets/modelos/lote02/{asset}.glb").Instantiate<Node3D>();
					d.AddChild(node);
					var box = default(Aabb);
					var first = true;
					foreach (var mesh in System.Linq.Enumerable.Cast<MeshInstance3D>(node.FindChildren("*", "MeshInstance3D", true, false)))
					{
						var b = mesh.GlobalTransform * mesh.GetAabb();
						box = first ? b : box.Merge(b);
						first = false;
					}
					GD.Print($"KIT {asset}: pos={box.Position} tamanho={box.Size}");
					node.QueueFree();
				}
				break;
			case "modais-tempo":
				await DungeonScenarios.ModalTimes(d);
				break;
			case "inventario":
				await DungeonScenarios.Inventory(d);
				break;
			case "torre-equipamento":
				await DungeonScenarios.Equipment(d);
				break;
			case "torre-profunda":
				await DungeonScenarios.DeepFloors(d);
				break;
			case "torre-andares":
				await DungeonScenarios.Floors(d);
				break;
			case "torre-explorado":
				await DungeonScenarios.Explored(d);
				break;
			case "torre-derrota":
				await DungeonScenarios.Defeat(d);
				break;
			case "trilhas":
				await TrailShots.Run(d);
				break;
			case "andar":
				await FloorShots.Run(d);
				break;
			case "lobby":
				await d.Shot("01-visao-geral");
				break;
			case "album":
				await d.GoToObject("album");
				await d.Shot("01-perto-do-album");
				await d.Press("Abrir o Álbum");
				await d.Shot("02-album-criaturas");
				await d.Press("Cosméticos");
				await d.Shot("03-album-cosmeticos");
				await d.Press("Fechar");
				await d.Shot("04-de-volta-ao-lobby");
				break;
			case "portal":
				await d.GoToObject("portal");
				await d.Shot("01-perto-do-portal");
				await d.Press("Usar o portal");
				await d.Shot("02-portal");
				await d.Press("Fechar");
				await d.Shot("03-de-volta-ao-lobby");
				break;
			case "tecla":
				await d.GoToObject("album");
				await d.Tap(Controls.Interact);
				await d.Shot("01-abriu-com-a-tecla");
				await d.PressKey(Key.Escape);
				await d.Shot("02-fechou-com-esc");
				break;
			case "config":
				await d.Press("Configurações");
				await d.Shot("01-jogo");
				await d.Press("Acessibilidade");
				await d.Shot("02-acessibilidade");
				await d.Press("Muito grande");
				await d.Shot("03-texto-muito-grande");
				await d.Press("Fonte para dislexia: Desligado");
				await d.Shot("04-fonte-dislexia");
				await d.Press("Normal");
				await d.Press("Redução de animações: Desligado");
				await d.Press("Modo calmo: Desligado");
				await d.Shot("05-opcoes-ligadas");
				await d.Press("Controles");
				await d.Press("Teclado");
				await d.Shot("06-controles-teclado");
				await d.Press("Fechar");
				await d.Shot("07-lobby-com-dica-de-teclado");
				break;
			case "animacao":
				GD.Print($"Parado: andando={d.Player!.IsWalkingAnimation}");
				Input.ActionPress(Controls.Right);
				await d.Wait(0.5);
				GD.Print($"Andando: andando={d.Player.IsWalkingAnimation}");
				Input.ActionRelease(Controls.Right);
				await d.Wait(0.4);
				GD.Print($"Parou: andando={d.Player.IsWalkingAnimation}");
				break;
			case "boneco":
			case "boneco-dislexia":
				if (name == "boneco-dislexia")
				{
					await d.Press("Configurações");
					await d.Press("Acessibilidade");
					await d.Press("Muito grande");
					await d.Press("Fonte para dislexia: Desligado");
					await d.Press("Fechar");
				}
				await d.GoNearDummy();
				await d.ClickCreature();
				await d.Shot("01-escolha-do-treino");
				await d.Press("Começar");
				GD.Print($"Tempo com o alvo escolhido: {Engine.TimeScale}");
				await d.Shot("02-alvo-escolhido");
				await d.Press("Dica");
				await d.Shot("03-dica-com-conta-de-exemplo");
				await d.DrawText("3+5");
				await d.Shot("04-depois-de-desenhar");
				break;
			case "erro":
				await d.GoNearDummy();
				await d.ClickCreature();
				await d.Press("Começar");
				await d.Shot("01-comeco");
				await d.OvershootWithKeys();
				await d.Shot("02-passou-do-valor-travessura");
				await d.Wait(5.0);
				await d.Shot("03-peca-voltou");
				await d.OvershootWithKeys();
				await d.Shot("04-segundo-erro-com-dica");
				await d.Press("Dica");
				await d.Shot("05-pediu-a-dica");
				break;
			case "energia":
				await d.GoNearDummy();
				await d.ClickCreature();
				await d.Press("Começar");
				for (var i = 0; i < 5; i++)
				{
					await d.OvershootWithKeys();
					await d.Shot($"erro-{i + 1}");
				}
				await d.Wait(0.5);
				await d.Shot("saiu-do-treino");
				break;
			case "bolha":
				await d.GoNearDummy();
				await d.ClickCreature();
				await d.Press("Começar");
				await d.PressKey(Key.Escape);
				await d.Wait(7.7);
				await d.Shot("01-aviso");
				await d.Wait(1.7);
				await d.Shot("02-bolha-no-ar");
				await d.Wait(6.5);
				await d.Shot("03-depois-da-bolha");
				break;
			case "calmo":
				await d.Press("Configurações");
				await d.Press("Acessibilidade");
				await d.Press("Modo calmo: Desligado");
				await d.Press("Fechar");
				await d.GoNearDummy();
				await d.ClickCreature();
				await d.Press("Começar");
				await d.PressKey(Key.Escape);
				await d.Wait(9.5);
				await d.Shot("01-sem-bolha-no-modo-calmo");
				break;
			case "nome":
				await d.GoNearDummy();
				await d.ClickCreature();
				await d.Press("Escriba");
				await d.Shot("01-escolha-do-treino");
				await d.Press("Começar");
				await d.Shot("02-montagem-do-nome");
				await d.PlayName();
				await d.Shot("03-nome-certo");
				await d.Wait(3.5);
				await d.Shot("04-novo-nome");
				break;
			case "nome-erro":
				await d.GoNearDummy();
				await d.ClickCreature();
				await d.Press("Escriba");
				await d.Press("Começar");
				await d.PlayName(wrong: true);
				await d.Shot("01-silaba-errada-travessura");
				await d.Wait(5.0);
				await d.Shot("02-peca-voltou");
				break;
			case "treino-nivel":
				await d.GoNearDummy();
				await d.ClickCreature();
				await d.Shot("01-so-o-nivel-1");
				await d.Press("5 · Muito difícil");
				await d.Press("Começar");
				await d.Shot("02-sem-desafios-nessa-dificuldade");
				break;
			case "nivel2-matematica":
				await d.GoNearDummy();
				await d.ClickCreature();
				await d.Press("Nível 2");
				await d.Shot("01-escolha-nivel-2");
				await d.Press("Começar");
				await d.Shot("02-dezenas-na-mao");
				await d.PlayMath();
				await d.Shot("03-conta-escrita");
				break;
			case "nivel2-escrita":
				await d.GoNearDummy();
				await d.ClickCreature();
				await d.Press("Escriba");
				await d.Press("Nível 2");
				await d.Press("Começar");
				await d.Shot("01-teclado-na-tela");
				await d.Press("Dica");
				await d.Shot("02-pontilhado-na-dica");
				await d.PlayWord();
				await d.Shot("03-palavra-certa");
				break;
			case "nivel3-frase":
				await d.GoNearDummy();
				await d.ClickCreature();
				await d.Press("Escriba");
				await d.Press("Nível 3");
				await d.Press("Começar");
				await d.Shot("01-frase");
				await d.PlayName();
				await d.Shot("02-frase-certa");
				break;
			case "camera":
				await d.Shot("01-zoom-padrao");
				for (var i = 0; i < 3; i++) await d.PressKey(Key.Pageup);
				await d.Wait(1.2);
				await d.Shot("02-zoom-perto");
				await d.ClickHex(-4, 3);
				await d.Wait(4.5);
				await d.Shot("03-camera-seguiu-o-personagem");
				for (var i = 0; i < 8; i++) await d.PressKey(Key.Pagedown);
				await d.Wait(1.2);
				await d.Shot("04-zoom-longe");
				await d.PressKey(Key.Home);
				await d.Wait(1.2);
				await d.Shot("05-zoom-reiniciado");
				break;
			case "efeitos":
				await d.Shot("01-com-efeitos");
				await d.Press("Configurações");
				await d.Press("Acessibilidade");
				await d.Press("Efeitos visuais reduzidos: Desligado");
				await d.Press("Fechar");
				await d.Shot("02-efeitos-reduzidos");
				break;
			case "e2e-3":
				await DungeonScenarios.E2eLevelFirst(d);
				break;
			case "e2e-4":
				await DungeonScenarios.E2eLevelReopen(d);
				break;
			case "e2e-1":
				await E2E.First(d);
				break;
			case "e2e-2":
				await E2E.Reopen(d);
				break;
			case "mouse":
				await d.ClickHex(3, -4);
				await d.Wait(0.9);
				await d.Shot("01-a-caminho");
				await d.Wait(2.5);
				await d.Shot("02-chegou");
				await d.ClickHex(-3, 1);
				await d.Wait(4.5);
				await d.Shot("03-clique-na-arvore");
				break;
			default:
				GD.PushError($"Piloto: cenário '{name}' não existe.");
				break;
		}
	}
}
