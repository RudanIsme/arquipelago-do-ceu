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

| Elemento | Regra |
| --- | --- |
| Câmera | Perspectiva com FOV de cerca de 30°, a uns 50° de inclinação. O jogador fica no terço central da tela. |
| Luz | Uma luz principal quente, preenchimento frio vindo do céu e luzes pontuais (tochas, cristais, orbes) com brilho. |
| Pós-processamento | Desfoque nas bordas, bloom leve só nos itens mágicos, oclusão de ambiente e ACES. |
| Proporção | Personagens chibi, com cabeça grande e de 1/3 a 1/2 da altura de uma parede. |
| Densidade | Salas cheias de objetos nas bordas e o centro livre para o combate. |
| Cor por ilha | Torre dos Números em roxo e dourado, com pedra quente. Ilha das Letras em verde e azul-céu. Ilha-base em azul e amarelo. |
| Névoa | Lilás-claro e translúcida. Ela cobre as criaturas que ainda não foram acalmadas. |

Os números, as sílabas e as energias brilham em dourado ou azul. Assim, o que se pode usar se destaca do cenário, que é mais neutro.

## Personagens, criaturas e névoa

Os heróis são o Matemático e o Escriba. No jogo final eles usam modelos próprios, gerados no Meshy e com o rig ajustado no Blender.

- **Matemático:** roupa com dourado e uma bolsa de números na cintura que brilha. A pose de ataque é lançar um orbe.
- **Escriba:** chapéu e capa roxos e um livro aberto. A pose de ataque é conjurar as sílabas no ar.
- **Criaturas da névoa:** corpo redondo e macio, olhos grandes, bochechas rosadas e chifrinhos. Nunca têm dentes, armas nem ossos. Cada ilha tem a sua cor (lilás, azul, rosa, amarelo).
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

**Ilha das Letras (combate do Escriba).** A criatura mostra um gato. O Escriba já conjurou GA e a criança escolhe TO. A tecla F abre o modo de falar o nome.

![Ilha das Letras](../../mockups/ilha-das-letras.png)

**Ilha-base.** O Portal da Memória mostra quantas cartas faltam revisar, o Álbum mostra 14 de 40 criaturas e a Expedição leva à próxima torre. O menu de baixo troca a bolsa por quatro destinos.

![Ilha-base](../../mockups/ilha-base.png)

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

1. Cenário: kits KayKit Dungeon e Medieval Hexagon (CC0), com cores ajustadas por ilha e registrados em CREDITS.md.
2. Heróis: Meshy, depois limpeza e rig no Blender, exportados em glTF para o Godot.
3. Criaturas: um modelo base redondo com variações de cor, chifre e orelha. Assim 40 criaturas saem de poucos modelos.
4. Godot: luz quente, SSAO, glow só nos itens mágicos e desfoque de profundidade pela câmera.
5. HUD: Control nodes com tema único (painéis, fonte e cores desta página).

O MVP inclui duas ilhas (Torre dos Números e Ilha das Letras), a ilha-base, 2 heróis e 12 criaturas. As outras ilhas e criaturas ficam para depois.

## Decisões em aberto

- [ ] Aprovar os nomes Torre dos Números, Ilha das Letras e Ilha-base
- [ ] Definir o visual final do Matemático e do Escriba 
- [ ] Escolher entre chão hexagonal ou quadrado nas ilhas abertas
- [ ] Decidir se o desfoque nas bordas fica ligado por padrão em PCs fracos
- [ ] Testar se os números dentro dos orbes são lidos a 1 metro da tela
