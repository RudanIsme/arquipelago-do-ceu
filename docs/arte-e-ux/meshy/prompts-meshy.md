# Prompts Meshy: Personagem modular e criaturas

Os prompts seguem o que está no repositório: um Personagem só, personalizável, que veste o kit do Papel da Dungeon. Eles partem do estilo que você já gerou no Meshy. Os prompts estão em inglês porque o Meshy responde melhor assim.

## Imagens de referência

As imagens em `referencias/` foram aprovadas como referência de estilo. Use-as no Meshy (geração de imagem e Image to 3D), uma folha por chamada.

| Imagem | Serve para |
| --- | --- |
| [interior-torre-dos-numeros.png](referencias/interior-torre-dos-numeros.png) | objetos e paredes da Torre dos Números |
| [interior-torre-das-letras.png](referencias/interior-torre-das-letras.png) | objetos e paredes da Torre das Letras |
| [tiles-torre-dos-numeros.png](referencias/tiles-torre-dos-numeros.png) | tiles das Salas da Torre dos Números |
| [tiles-torre-das-letras.png](referencias/tiles-torre-das-letras.png) | tiles das Salas da Torre das Letras |
| [corpo-base.png](referencias/corpo-base.png) | corpo base do Personagem |
| [personagem-matematico.png](referencias/personagem-matematico.png) | estilo do Personagem; não copie a calculadora nem as fórmulas avançadas |

Fluxo: gere cada folha como imagem, salve como `folhas/NN-nome.png`, revise e só então passe para o 3D. Prompts têm limite de 800 caracteres.

## Estilo travado

Cole sempre o mesmo final de prompt e o mesmo negative prompt. É isso que mantém todas as peças no estilo do menino que você gerou: pintado à mão, chibi, cabeça grande e olhos castanhos grandes.

**Final de estilo (vai no fim de todo prompt):**

```text
stylized 3D game asset, hand-painted textures, storybook cartoon style, chibi proportions (head about 1/3 of body height), soft warm colors, clean readable silhouette, consistent with a cute 8-year-old adventurer character, plain light gray background, front view, centered, game-ready
```

**Negative prompt (todos):**

```text
realistic, photorealistic, anime, horror, scary, teeth, blood, skull, real weapons, sharp blades, guns, text, logo, watermark, extra limbs, extra fingers, asymmetrical face, noisy geometry, blurry texture, dark gloomy colors
```

Para roupa e acessório solto, troque `front view, centered` por `isolated item, no character, front three-quarter view, centered`.

## Como gerar as peças modulares

O segredo é gerar tudo a partir de um único corpo base. As peças entram por cima dele: as rígidas presas a um osso, e as roupas usando o mesmo esqueleto.

1. Gere o corpo base em A-pose (próxima seção) e aprove-o antes de qualquer peça.
2. Gere cada peça como imagem isolada e depois use **Image to 3D**, sempre com o corpo base como referência de proporção.
3. Roupa (jaqueta, capa, robe): no Blender, encaixe no corpo base e transfira os pesos do corpo (Data Transfer > Vertex Groups). Assim ela usa o mesmo esqueleto.
4. Peça rígida (óculos, chapéu, bolsa, livro, ferramenta): não precisa de rig. Exporte com a origem no ponto de encaixe.
5. No Godot, use `BoneAttachment3D` no osso certo para as peças rígidas e `Skeleton3D` compartilhado para as roupas.

| Encaixe | Osso | Peças |
| --- | --- | --- |
| Cabeça | head | cabelo, chapéu, óculos de aviador, óculos redondos |
| Tronco | spine / chest | jaqueta, robe, capa, cachecol (com pesos) |
| Quadril | hips | bolsa de números, cinto de penas |
| Mão direita | hand.R | ferramenta do Papel |
| Mão esquerda | hand.L | livro, item secundário |
| Costas | spine.002 | mochila, asas de cosmético |

Na troca de cor do Personagem, pinte roupas e cabelo em tons neutros claros na textura e aplique a cor no Godot, pelo material. Assim um modelo só vira várias cores.

## Corpo base e características

O corpo base é o do seu último menino de camiseta. Faça mais uma versão de menina com as mesmas proporções e o mesmo esqueleto.

**Corpo base (menino):**

```text
base body for a modular character, cute 8-year-old boy, plain light beige t-shirt and simple shorts, plain socks, barefoot-style simple shoes, no hair (bald smooth head for hair attachments), big brown expressive eyes, small nose, gentle smile, A-pose with arms 45 degrees down, symmetrical
```

**Corpo base (menina):** o mesmo prompt trocando `boy` por `girl` e `no hair` por `no hair, slightly softer face`. Ela usa o mesmo esqueleto.

**Tons de pele:** gere a textura do corpo em tom médio e troque no Godot entre pelo menos 6 tons, do claro ao escuro. Evite 6 modelos diferentes.

**Cabelos (peça da cabeça):**

| Cabelo | Prompt (antes do final de estilo) |
| --- | --- |
| Bagunçado curto | `messy short wavy hair piece, isolated hair mesh only` |
| Cacheado volumoso | `big curly afro hair piece, soft round volume, isolated hair mesh only` |
| Rabo de cavalo | `high ponytail hair piece with side bangs, isolated hair mesh only` |
| Tranças | `two braids hair piece with short bangs, isolated hair mesh only` |
| Liso com franja | `straight bob cut hair with bangs, isolated hair mesh only` |
| Raspado | `very short buzz cut hair piece, isolated hair mesh only` |

**Rosto:** mantenha um rosto só por corpo base e varie só por textura (sardas, bochechas, cor dos olhos). Rosto trocado como malha quebra o rig das expressões.

## Kit do Papel Matemático

O kit é a roupa da Torre-oficina (engrenagens, cristais, relógios), em teal e dourado. A jaqueta que você gerou já serve.

| Peça | Prompt (antes do final de estilo) |
| --- | --- |
| Jaqueta (pronta) | `teal explorer jacket with gold trim, small gold gear and triangle embroidery, open front, hollow inside, garment only` |
| Bermuda | `brown cargo shorts with small brass buttons, garment only` |
| Tênis | `pair of chunky orange adventure sneakers with cream soles, item only` |
| Óculos de aviador | `brass aviator goggles with light blue lenses and leather strap, item only` |
| Bolsa de números | `small brown leather satchel with a flap, three glowing golden number tiles peeking out, brass buckle, item only` |

**Ferramenta (variações cosméticas):** todas lançam o orbe de número; muda só a aparência.

| Variação | Prompt |
| --- | --- |
| Luva de engrenagem (inicial) | `chunky brass gauntlet glove with a small spinning gear on the back and a glowing socket in the palm, item only` |
| Bastão-ábaco | `short wooden staff with a small abacus of colorful beads near the top and a glowing crystal tip, item only` |
| Estilingue de cristal | `friendly wooden slingshot with a soft glowing crystal pouch, item only` |
| Compasso mágico | `oversized brass drawing compass used as a magic wand, glowing star at the tip, item only` |

## Kit do Papel Escriba

O kit é a roupa da Torre-biblioteca (livros voadores, letras soltas, pergaminhos), em roxo e dourado, com papel creme. Gere sobre o mesmo corpo base para o encaixe bater com o do Matemático.

| Peça | Prompt (antes do final de estilo) |
| --- | --- |
| Túnica curta | `short purple scholar tunic reaching mid-thigh, gold trim, small embroidered letters A and B on the hem, belt with a tiny ink pot, hollow inside, garment only` |
| Capa | `short cream cape with purple lining and a gold quill-shaped clasp, garment only` |
| Calça | `loose dark blue trousers tucked into boots, garment only` |
| Botas | `pair of soft brown leather boots with rolled cuffs, item only` |
| Chapéu | `floppy purple scholar beret with a long feather quill tucked in the band, item only` |
| Bolsa de sílabas | `small cream cloth pouch with purple stitching, a few glowing wooden letter tiles peeking out, item only` |

**Ferramenta (variações cosméticas):** todas lançam a sílaba ou letra; muda só a aparência.

| Variação | Prompt |
| --- | --- |
| Livro aberto (inicial) | `small open spellbook with cream pages, glowing golden letters floating above the pages, purple leather cover, item only` |
| Pena-varinha | `oversized white feather quill used as a magic wand, golden nib with a soft glow, item only` |
| Cajado de letras | `short wooden staff topped with a big carved wooden letter A wrapped in a glowing ribbon, item only` |
| Pergaminho mágico | `rolled parchment scroll with gold end caps, half unrolled, glowing letters drifting out, item only` |

O Matemático e o Escriba trocam só o kit; corpo, cabelo, cores e acessórios do Personagem continuam os mesmos nas duas Dungeons.

## Acessórios cosméticos

Acessório é peça pequena que vale nos dois Papéis e se ganha por aprendizado e exploração. Gere neutro (creme ou cinza claro) quando a peça puder ser tingida no Godot; deixe a cor pronta só quando o desenho depender dela.

| Encaixe | Acessório | Prompt (antes do final de estilo) |
| --- | --- | --- |
| head | Óculos redondos (pronto) | `round brass reading glasses with thin frame, item only` |
| head | Gorro de lã | `cozy knitted beanie with a big pom-pom, item only` |
| head | Chapéu de explorador | `small safari explorer hat with a leather band, item only` |
| head | Coroa de folhas | `small crown of leaves and tiny flowers, item only` |
| spine.002 | Cachecol (pronto) | `long knitted yellow scarf with fringe, wrapped around a neck, hollow center, item only` |
| spine.002 | Lenço de bolinhas | `small polka dot neckerchief tied in front, hollow center, item only` |
| spine (costas) | Mochila de tartaruga | `small cute backpack shaped like a turtle shell, item only` |
| spine (costas) | Asinhas de papel | `pair of small folded paper wings, item only` |
| hips | Lanterna de cinto | `tiny brass lantern hanging from a belt clip, warm glow, item only` |
| ombro | Mascote coruja | `tiny round owl companion sitting, big eyes, item only` |
| ombro | Mascote gato de engrenagem | `tiny round cat made of brass and soft cloth with a small gear tail, item only` |

Para acessórios novos, mantenha a regra: peça pequena, silhueta clara, nada de arma, nada que cubra o rosto inteiro.

## Criaturas

A direção de arte pede um modelo base redondo por Dungeon, com variações. As criaturas não têm dentes, armas nem ossos; são travessas, não más. O número do escudo e a palavra vêm do jogo, então o modelo sai com a placa **em branco**. Troque o final de estilo por `cute round creature, stylized 3D game asset, hand-painted textures, storybook cartoon style, soft colors, big friendly eyes, no mouth teeth, plain light gray background, front three-quarter view, centered, game-ready`.

**Matemática: autômatos da Torre dos Números**

| Criatura | Prompt |
| --- | --- |
| Base | `small round brass automaton robot, egg-shaped body, short stubby legs, tiny arms holding a round blank shield plate with an empty frame for a number, small gear on top of the head, one glowing round eye` |
| Variação pesada | `chubby round automaton with a big blank square shield plate, clock face on its belly, short legs` |
| Variação voadora | `small round automaton with propeller on top, blank shield plate hanging from its body, floating` |
| Variação cristal | `round automaton with teal crystal shoulder pads and a blank hexagonal shield plate` |

**Português: criaturas de letras apagadas**

| Criatura | Prompt |
| --- | --- |
| Base | `soft round ghost-like creature made of crumpled cream paper, faded gray letters floating around its body, sleepy big eyes, small paper arms, blank paper tag on its chest` |
| Variação livro | `small round creature that is a closed book with tiny legs, faded letters drifting from the pages, big eyes on the cover` |
| Variação pergaminho | `round creature wrapped in a loose parchment scroll, faded smudged letters on it, curious face` |
| Variação tinta | `round soft ink blob creature in light purple with a few faded letters inside, big shy eyes` |

Quando a criança acerta, a criatura é libertada: as letras ganham cor e o autômato abre um sorriso. Isso se faz com troca de textura ou blend shape no Godot, não com outro modelo.

**Boneco de treino (Lobby)**

| Peça | Prompt |
| --- | --- |
| Boneco | `cute round training dummy made of stitched burlap and straw, wooden post base, big button eyes, a stitched smile, blank wooden sign hanging from its neck, colorful patches` |

## Ajustes no que já foi gerado

| O que você gerou | O que fazer |
| --- | --- |
| Corpo base de camiseta | Use como corpo base de todos. Gere de novo em A-pose e com a camiseta lisa, para ela virar a camada de baixo das roupas. |
| Menino de jaqueta teal, cachecol e óculos de aviador | É a referência de estilo. Use no Image to 3D das próximas peças para manter traço e proporção. Separe jaqueta, cachecol e óculos em peças próprias. |
| Variação com óculos redondos | Vira acessório cosmético (encaixe head). |
| Óculos redondos de latão | Prontos como acessório. |
| Bolsa de couro com calculadora | Troque a calculadora pela bolsa de números (prompt no kit do Matemático). Calculadora faz a conta pela criança, e quem calcula é ela. |
| Jaqueta teal com fórmulas | Pode ficar como Cosmético do Matemático, mas tire fórmulas avançadas: use só números, + e − e formas, que é o conteúdo do 1º ao 3º ano. |

As cores (jaqueta, cachecol, cabelo) podem ser trocadas no Godot pelo material, então não precisa gerar uma versão por cor.

## Folhas de assets (todas as imagens antes do 3D)

O formato que funcionou foi a folha: uma imagem com as peças soltas em grade. Cada folha abaixo é um prompt só, montado assim: **início + itens da folha + final**.

- **Início:** `game asset sheet, collection of separate pieces laid out in a neat grid with empty space between each piece, nothing touching:`
- **Final (cenário):** `stylized 3D, chunky rounded shapes, soft bevels, hand-painted textures, storybook cartoon style, clean readable silhouettes, isometric three-quarter top view, plain light gray background, game-ready`
- **Final (Personagem e criaturas):** troque `isometric three-quarter top view` por `front view, consistent with a cute 8-year-old chibi adventurer`.
- **Negativo de todas:** `realistic, photorealistic, horror, skull, bones, teeth, weapons, text, words, logo, brand, emblem, watermark, calculator, advanced math formulas, noisy geometry, blurry texture`

| # | Folha | Situação | Itens da folha |
| --- | --- | --- | --- |
| 1 | Interior da Torre dos Números | Pronta |  |
| 2 | Interior da Torre das Letras | Pronta |  |
| 3 | Tiles da Torre dos Números | Pronta |  |
| 4 | Tiles da Torre das Letras | Pronta |  |
| 5 | Chão da ilha do Lobby | Prompt já enviado | grama, bordas de penhasco, caminho, água, ponte, árvores, pedras |
| 6 | Construções do Lobby | Falta | `entrance facade of a cozy stone workshop tower with teal roof and big clock, entrance facade of a cozy library tower with purple roof and open book sign, small round-door cottage for the guide, wooden lectern holding a big photo album, wooden bench, campfire with logs, small mailbox, flower planter` |
| 7 | Céu ao redor | Falta | `small floating grass island with one tree, medium floating rock island, tiny floating rock chunks, soft puffy cloud, long flat cloud, small waterfall falling off a cliff edge, distant tower silhouette, hot air balloon` |
| 8 | Cabelos do Personagem | Falta | `six separate cartoon hair pieces, no head: short messy, curly afro, long straight with bangs, two buns, braid, buzz cut` |

| # | Folha | Itens da folha |
| --- | --- | --- |
| 9 | Kit do Matemático | `teal explorer jacket with gold trim and small embroidered gears, plus signs and simple numbers 1 2 3, brown cargo shorts, chunky orange sneakers, brass aviator goggles, knitted yellow scarf, brown leather satchel with glowing golden number tiles peeking out` |
| 10 | Ferramentas do Matemático | `brass gauntlet glove with small gear and glowing palm, short wooden staff with colorful abacus beads and crystal tip, friendly wooden slingshot with glowing crystal pouch, oversized brass drawing compass with glowing star tip` |
| 11 | Kit do Escriba | `short purple scholar tunic with gold trim, cream cape with purple lining and quill clasp, loose dark blue trousers, soft brown leather boots, floppy purple beret with feather quill, cream cloth pouch with glowing wooden letter tiles` |
| 12 | Ferramentas do Escriba | `small open spellbook with glowing letters floating above, oversized white feather quill wand with golden nib, short wooden staff topped with big carved letter A and ribbon, half unrolled parchment scroll with glowing letters` |
| 13 | Acessórios | `round brass glasses, knitted beanie with pom-pom, small safari hat, crown of leaves and flowers, polka dot neckerchief, turtle shell backpack, folded paper wings, tiny brass belt lantern, tiny round owl companion, tiny brass cat with gear tail` |
| 14 | Criaturas da Torre dos Números | `small round brass automaton with blank round shield plate and gear on head, chubby automaton with blank square shield and clock belly, small flying automaton with propeller and blank shield, round automaton with teal crystal shoulders and blank hexagonal shield, all with big friendly eyes` |
| 15 | Criaturas da Torre das Letras | `soft round paper creature with faded gray letters floating around and blank paper tag, small closed book creature with tiny legs, round creature wrapped in parchment scroll, soft light purple ink blob creature, all with big shy friendly eyes` |
| 16 | Boneco e Guia | `cute round burlap training dummy with button eyes, stitched smile, wooden post and blank sign, kind round elderly guide character with big white beard, lantern and cozy patched robe` |
| 17 | Itens do jogo | `glowing golden number orb, glowing wooden letter tile, golden gear medallion, sealed scroll with wax seal, fast travel crystal, brass balance scale, small wall clock, cozy floor cushion with teapot` |

A aparência do Guia ainda não foi decidida nas docs; o da folha 16 é só uma sugestão.

**Correções no que você já gerou:** a bolsa veio com calculadora e com o chaveiro "Ravenswatch", que é o nome de outro jogo, então refaça pela folha 9. A jaqueta e o Personagem completo vieram com fórmulas como a²+b² e raiz quadrada, que passam do 1º ao 3º ano; a folha 9 troca por engrenagens, sinal de mais e números simples. O corpo base de camiseta está bom para o Image to 3D.

## Prompt único: tudo numa folha

Um prompt só que gera o Personagem, os módulos do Personagem, as criaturas, os tiles e os assets do mundo numa única folha, organizada em fileiras.

```
Game asset sheet, separate pieces in neat rows, nothing touching. 1: chibi 8-year-old kid base body, gray tee and shorts, A-pose; 4 hair pieces. 2: teal jacket with gold gears, brown shorts, orange sneakers, brass goggles, yellow scarf, satchel with glowing number tiles. 3: purple tunic, cream cape, beret with quill, boots, open spellbook. 4: round glasses, beanie, owl pet. 5: round brass robot with blank shield, paper creature with faded letters, burlap training dummy. 6: hexagonal stone floor tile, cracked tile, stairs, glowing portal tile, brick wall with torch, wall corner, arched door, pillar. 7: table, chest, crystal, bookshelf, barrel. 8: grass tile, grass cliff edge with roots, path tile, tree, bush, rock, fence. Stylized 3D, hand-painted, storybook cartoon, gray background
```

792 caracteres, dentro do limite de 800 do Meshy.

**Negativo:** `realistic, horror, teeth, weapons, text, logo, calculator, math formulas, diorama, pieces touching, blurry`

Com tanta coisa numa imagem, cada peça sai pequena e com menos detalhe. Se alguma peça vier ruim, gere só a fileira dela de novo usando o mesmo texto daquela linha.
