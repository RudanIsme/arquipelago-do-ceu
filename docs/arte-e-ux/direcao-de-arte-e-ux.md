# Arquipélago do Céu: direção de arte e UX

## Princípios de UX para crianças de 6 a 9 anos

A criança precisa entender cada tela sem ler. Por isso, todo texto também vira voz, ícone ou cor.

- **Um objetivo por vez:** só a criatura ativa mostra o pedido (8, ou a imagem do gato). As outras ficam na névoa, mais apagadas.
- **Alvos grandes:** botões com no mínimo 72 px no toque e 86 px nos espaços da bolsa. Nada importante fica nos cantos do toque.
- **Voz em tudo:** o botão de alto-falante repete o pedido. Quando a criança segura o dedo sobre um elemento, o jogo diz o nome dele.
- **Erro sem castigo:** errar tira um segmento de energia e a criatura faz um ataque leve e engraçado. Não há tela vermelha nem som de derrota.
- **Ritmo controlado:** a câmera lenta é ativada ao compor a resposta, e o modo calmo dispensa o tempo de vez.
- **Leitura do mapa como nas referências:** anel colorido sob o personagem ativo, retratos embaixo à esquerda, ações embaixo no centro e minimapa no canto superior direito.

## Direção de arte

O estilo é 3D estilizado em vista isométrica de cima, com luz quente, sombras suaves e desfoque nas bordas (efeito maquete). A referência de estilo é Ravenswatch.

As cores, formas e regras de cada tipo de asset estão em [guia-de-estilo-visual.md](guia-de-estilo-visual.md). Toda arte nova parte dele.

| Elemento | Regra |
| --- | --- |
| Câmera | Perspectiva com FOV de cerca de 30°, a uns 50° de inclinação. O jogador fica no terço central da tela. |
| Luz | Uma luz principal quente, preenchimento frio vindo do céu e luzes pontuais (tochas, cristais, orbes) com brilho. |
| Pós-processamento | Desfoque nas bordas, bloom leve só nos itens mágicos, oclusão de ambiente e ACES. |
| Proporção | Personagens chibi, com cabeça grande e de 1/3 a 1/2 da altura de uma parede. |
| Densidade | Salas cheias de objetos nas bordas e o centro livre para o combate. |
| Cor por torre | Torre dos Números em roxo e dourado, com pedra quente. Torre das Letras em verde e azul-céu. Lobby em azul e amarelo. |
| Névoa | Lilás-claro e translúcida. Ela cobre as criaturas que ainda não foram acalmadas. |

Os números, as sílabas e as energias brilham em dourado ou azul. Assim, o que se pode usar se destaca do cenário, que é mais neutro.

## Personagens, criaturas e névoa

O Personagem é um só, personalizável (cores, características, acessórios e variações da ferramenta), e assume o Papel de Matemático ou de Escriba conforme a Dungeon, com roupas e ferramenta próprias. No jogo final usa modelos próprios, gerados no Meshy e com o rig ajustado no Blender.

- **Papel Matemático:** roupa com dourado e uma bolsa de números na cintura que brilha. A pose de ataque é lançar um orbe.
- **Papel Escriba:** chapéu e capa roxos e um livro aberto. A pose de ataque é conjurar as sílabas no ar.
- **Criaturas da névoa:** reconhecíveis à primeira vista, com olhos grandes e simpáticos. Nunca têm dentes, armas, ossos nem texto, etiqueta ou letra no corpo. Na Torre dos Números são animais e aves mecânicos com um escudo liso na frente; na Torre das Letras são coisas do dia a dia, cada uma a própria palavra a escrever. Detalhes no guia de estilo.
- **Acalmar:** quando a criança acerta, a névoa se dissolve, a criatura sorri e entra no Álbum de criaturas.
- **Pedido:** fica num balão acima da criatura. É um número no Matemático e uma imagem (gato) no Escriba, sempre com voz.

## Interface

O HUD tem painéis escuros com borda dourada, como nas referências, mas com fonte arredondada e menos texto.

| Item | Especificação |
| --- | --- |
| Fonte | Baloo 2, peso 700 a 800. No mínimo 20 px no HUD e 26 px nos títulos. |
| Painéis | Fundo #1b1424 a #2a2036, borda #d9a64a de 3 px e cantos de 16 px. |
| Energia | Raio e 5 segmentos dourados (#ffd24a). Segmento vazio em #3b3046. Nunca usar corações. |
| Bolsa de números | 4 espaços de 86 px com orbe dourado e tecla (1 a 4). Selecionado tem brilho; usado fica cinza. |
| Sílabas | Peças retangulares no lugar dos orbes. A sílaba certa não tem destaque especial. |
| Progresso do andar | Bolinhas lilás no título, uma por criatura acalmada. |
| Personagem ativo | Anel colorido no chão (dourado para o Matemático, lilás para o Escriba) e retrato maior. |
| Feedback | Acerto: brilho dourado e som de sino. Erro: tremida leve e som macio. Nunca usar vermelho forte. |
| Som | Voz em todos os pedidos e música calma com instrumentos de madeira. |

## Telas principais

As três cenas abaixo foram renderizadas em 3D com modelos CC0 (KayKit) só para marcar o lugar. Os personagens finais entram depois.

**Torre dos Números (combate do Matemático).** A criatura pede 8 e o Matemático já lançou o 5. A criança escolhe o 3 na bolsa. Duas criaturas esperam na névoa ao fundo.

![Torre dos Números](../../mockups/torre-dos-numeros.png)

**Torre das Letras (combate do Escriba).** A criatura mostra um gato. O Escriba já conjurou GA e a criança escolhe TO. A tecla F abre o modo de falar o nome.

![Torre das Letras](../../mockups/torre-das-letras.png)

**Lobby.** O Portal da Memória mostra quantas cartas faltam revisar, o Álbum mostra 14 de 40 criaturas e a Expedição leva à próxima torre. O menu de baixo troca a bolsa por quatro destinos.

![Lobby](../../mockups/lobby.png)

Ainda faltam o Álbum aberto, a carta do Portal da Memória, a tela de energia zerada e a área do responsável.

## Acessibilidade, modo calmo e entrada

Todas as ações funcionam com toque, mouse ou teclado, e nenhuma depende de cor sozinha.

- **Toque:** tocar no orbe lança o número. Arrastar move o personagem. Segurar faz o jogo ler o elemento em voz alta.
- **Teclado:** WASD para andar, 1 a 4 para a bolsa, Espaço para esquivar, Q para trocar de herói e F para falar.
- **Modo calmo:** sem tempo, sem câmera lenta, menos partículas e criaturas que esperam paradas.
- **Contraste:** texto claro sobre painéis escuros com razão de pelo menos 4,5:1. Números e sílabas sempre com contorno.
- **Daltonismo:** os heróis se distinguem pelo anel e pelo ícone, não só pela cor.
- **Movimento:** há opções para desligar o desfoque e o tremor da câmera.

## Pipeline de arte e escopo do MVP

No MVP, a arte usa kits CC0 para o cenário e modelos próprios só para os dois heróis e as criaturas.

1. Cenário: kits KayKit Dungeon e Medieval Hexagon (CC0) como provisório, com cores ajustadas por torre e registrados em CREDITS.md. O cenário próprio é gerado no Meshy, no padrão do guia de estilo (chão hexagonal da folha `18-chao-hexagonal-lobby-b`, com as cores vivas da paleta).
2. Heróis: Meshy, depois limpeza e rig no Blender, exportados em glTF para o Godot.
3. Criaturas: um modelo base redondo com variações de cor, chifre e orelha. Assim 40 criaturas saem de poucos modelos.
4. Godot: luz quente, SSAO, glow só nos itens mágicos e desfoque de profundidade pela câmera.
5. HUD: Control nodes com tema único (painéis, fonte e cores desta página).

O MVP inclui duas torres (Torre dos Números e Torre das Letras), o Lobby, 2 Papéis (Matemático e Escriba) e 12 criaturas. As outras torres e criaturas ficam para depois.

## Decisões em aberto

- [ ] Aprovar os nomes Torre dos Números, Torre das Letras e Lobby
- [ ] Definir o visual final dos Papéis Matemático e Escriba e do editor de Personagem
- [x] Chão hexagonal nas salas abertas (decidido; padrão no guia de estilo)
- [ ] Aprovar a paleta e os elementos animados das torres do guia de estilo
- [ ] Decidir se o desfoque nas bordas fica ligado por padrão em PCs fracos
- [ ] Testar se os números dentro dos orbes são lidos a 1 metro da tela
