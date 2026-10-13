# Guia de estilo visual

Padrão que toda arte do jogo segue, gerada no Meshy ou feita à mão. Complementa [direcao-de-arte-e-ux.md](direcao-de-arte-e-ux.md) (câmera, luz, HUD) com o que faltava para as peças combinarem entre si: cores, formas, regras por tipo de asset e animação das torres. Os valores marcados como **proposta** ainda precisam de aprovação.

## 1. Paleta

Cor viva, nunca pastel. O erro mais comum das primeiras folhas foi o chão sair pastel e o resto saturado, o que faz as peças parecerem de jogos diferentes.

| Papel | Cor | Hex (proposta) |
| --- | --- | --- |
| Lobby, principal | Azul | `#2E7DE8` |
| Lobby, apoio | Amarelo | `#FFC83D` |
| Torre dos Números, principal | Roxo | `#7A44D1` |
| Torre dos Números, apoio | Dourado | `#F2B632` |
| Torre dos Números, pedra | Pedra quente | `#C9A27E` |
| Torre das Letras, principal | Verde | `#35B96A` |
| Torre das Letras, apoio | Azul-céu | `#4FB9F0` |
| Torre das Letras, papel | Pergaminho | `#F3E3BE` |
| Grama | Verde vivo | `#62B83A` (sombra `#3F9A3A`) |
| Terra do caminho | Marrom quente | `#C98A48` |
| Água | Azul | `#34A6E8` |
| Rocha | Cinza quente | `#9A8F8A` |
| Madeira | Marrom | `#8A5A36` |
| Fundo das folhas de geração | Cinza claro | `#E6E8EB` |

**Regra de saturação:** as superfícies principais ficam com saturação de 55% a 80%. Só nuvens, papel, pedra e neutros podem ficar abaixo de 35%. Dourado e azul de brilho continuam reservados ao que a criança pode usar (números, sílabas, energia), como na direção de arte.

## 2. Formas e materiais

- **Volumes grossos e arredondados**, com chanfros suaves. Nada de aresta afiada, ponta de lança ou dente.
- **Textura pintada à mão**, com variação leve de tom dentro de cada superfície. Sem contorno preto, sem foto-realismo.
- **Silhueta legível** a 128 px. Se duas criaturas se confundem na silhueta, uma delas muda de forma, não só de cor.
- **Proporção chibi** em personagens e criaturas: cabeça grande, olhos grandes e simpáticos.
- **Sem texto, logotipo, marca ou etiqueta** em nenhum modelo. Número, palavra e nome vêm do jogo (balão de pedido, UI e fonte do HUD). Texto gerado por IA sai errado e pode ensinar letra ou conta errada.

## 3. Chão hexagonal (decidido)

O chão do Lobby e das salas é de **hexágonos**, como nos mockups. O padrão de **forma** é a folha `18-chao-hexagonal-lobby-b` (peças lisas e simples, que se encaixam numa grade) e o de **cor e vida** é a `30-chao-hexagonal-vivo`, que é a mesma folha repintada com as cores da seção 1.

- Um prisma hexagonal por tile, com topo e uma lateral de terra ou rocha visível.
- Variantes: grama lisa, grama com flores, caminho (reto, curva, T, ponta), água, borda de penhasco com rocha embaixo, ponte, poço de pedra.
- Orientação (vértice para cima ou para o lado) e tamanho em metros ficam para a prova técnica do gerador de Andares.

## 4. Personagem: um corpo, muitas combinações

Há **um corpo base só**, neutro, sem gênero marcado. A aparência vem de partes combináveis, e o catálogo é único (sem "masculino" e "feminino").

| Parte | Como entra | Mínimo no MVP (proposta) |
| --- | --- | --- |
| Tom de pele | Cor no material | 6 tons, do claro ao escuro |
| Olhos | Atlas de texturas no rosto | 6 formatos (redondo, amendoado, grande com brilho, sonolento, arregalado, com cílios) e cor da íris |
| Sobrancelhas | Atlas de texturas | 6 estilos (fina, grossa, arqueada, reta, inclinada, nenhuma) |
| Boca | Atlas de texturas | 3 sorrisos |
| Sardas e bochechas | Atlas de texturas | liga e desliga |
| Cabelo | Peça presa ao osso da cabeça, tingida por material | 12 penteados e 10 cores |
| Roupa e acessórios | Pelo Papel e pelos Cosméticos | os kits do Matemático e do Escriba |

O rosto **não troca de malha**, só de textura, para não quebrar as expressões. O cabelo é gerado em tom neutro e ganha cor no Godot.

**O corpo base tem de sair com o rosto liso**, sem olhos, sobrancelhas nem boca pintados, porque essas partes entram por cima, como decalque. No primeiro teste de Image to 3D o rosto veio com olhos e sobrancelhas na textura, o que impede a troca. Olhos podem ser gerados por IA (a folha `31a-olhos` serve); **sobrancelhas e bocas são formas simples e devem ser desenhadas em vetor**, com a paleta do guia, porque a IA gerou bocas com dentes e sobrancelhas que pareciam bigode.

## 5. Criaturas

- **Reconhecíveis à primeira vista.** Se a criança não vê o que é, não tem como saber o que escrever (Letras) ou como lutar (Números).
- **Sem etiqueta, letra, número ou texto** no modelo nem em volta. Letras soltas em volta de uma criatura de Letras podem sugerir uma grafia errada. O pedido aparece no balão acima da criatura.
- **Olhos grandes, boca sem dentes**, nunca armas ou ossos (regra da direção de arte). Os chifrinhos e as bochechas rosadas da direção de arte ficam como detalhe opcional.

**Torre dos Números: animais e aves mecânicos** em latão, roxo e dourado, com algo que lembre matemática (ábaco, compasso, régua, relógio sem algarismos, engrenagens). Cada um tem um **escudo liso na frente**, de 30% a 40% da largura do tronco, sem nada na frente do rosto, na mesma posição em todas as espécies, para o jogo escrever o número em cima. Criaturas de Nível mais alto são maiores e têm mais peças, mas a ação não fica mais difícil.

**Torre das Letras: coisas do dia a dia que ganharam vida**, em papel e pergaminho com acentos verde e azul-céu. Cada criatura é **a própria palavra-alvo** (pato, bode, sapo, gato, fada, mula, bota, bola, mala, nave, uva, ovo são exemplos da ordem recomendada provisória). As palavras reais vêm das Missões.

## 6. Torres: elementos animados

Cada torre tem uma identidade própria que se vê de longe e se move devagar. **Proposta**, para aprovar.

| | Torre dos Números | Torre das Letras |
| --- | --- | --- |
| Cores | Roxo e dourado, pedra quente | Verde e azul-céu, pergaminho |
| Anéis orbitando | 2 a 3 anéis de medalhões dourados com os algarismos de 0 a 9, em sentidos opostos | 2 a 3 espirais subindo, de peças de madeira com letras e sílabas |
| Expressões | Placas flutuantes que mostram uma conta e a resolvem em sequência (por exemplo, 2 + 3 = 5) | Peças que se juntam em sílabas e palavras curtas (por exemplo, BA + TA) |
| Mecanismo | Engrenagens girando em sincronia, ponteiros de relógio, cristal do topo pulsando | Livros que abrem e fecham, páginas virando, pena escrevendo um rastro de luz no ar |
| Reação ao progresso | Os medalhões do Nível já vencido acendem em dourado | As peças do Nível já vencido acendem em azul-céu |

**Como se faz:** o Meshy gera só a torre e as **peças em branco** (medalhão, peça de madeira, engrenagem, livro, pena). **O algarismo, a letra, a conta e a sílaba são desenhados pelo jogo**, com a fonte do HUD, a partir dos dados do conteúdo. Assim nunca aparece uma conta errada ou uma letra deformada, e a fonte para dislexia vale também aqui. As peças são instâncias de um mesmo modelo, no máximo 40 por torre.

**Regras de movimento (proposta):**

- Lento: uma volta de anel leva de 30 a 60 segundos. Nada pisca mais de 3 vezes por segundo.
- Nada depende de timer nem interfere no jogo; é só cenário vivo.
- A opção **Redução de animações** deixa os anéis parados e as expressões fixas; o Modo calmo reduz as partículas.
- Os números e as letras em órbita seguem o Nível: no Nível 1, só o que o 1º ano usa.

## 7. Como gerar uma nova peça (Meshy)

1. Escolha as cores da seção 1 e escreva-as por extenso no prompt (por exemplo, "vivid purple and gold").
2. Use este fechamento: `stylized 3D game asset, vivid saturated colors, chunky rounded shapes, soft bevels, hand-painted textures, storybook cartoon style, clean readable silhouette, plain light gray background, game-ready`.
3. Termine com: `no text, no letters, no numbers, no labels, no logo, no watermark, no teeth, no weapons`. A API de imagem não tem campo de negative prompt, então o negativo vai no próprio texto.
4. Imagem de referência ajuda no estilo, mas **o modelo copia as peças dela**. Para variar o conteúdo, gere sem referência de conteúdo, só com texto.
5. Confira a folha contra o checklist abaixo antes de passar para o 3D. Refazer uma folha custa 3 créditos.

**Checklist de aprovação:** cores da paleta e saturação viva; silhueta legível; sem texto, letra, número ou etiqueta; sem dente, arma ou osso; chanfros e arredondados; combina com as outras folhas aprovadas.

## 8. Orçamento de polígonos e texturas

A otimização é requisito: o jogo roda em PC e em tablets Android de gama média. Os limites abaixo são **proposta** e devem ser confirmados na prova técnica, medindo em tablet real. Valem para **triângulos por modelo**, depois da redução.

| Asset | Limite |
| --- | --- |
| Corpo base com roupa básica | 3.500 |
| Cabelo (por peça) | 600 |
| Roupa do Papel (conjunto) | 1.000 |
| Acessório ou ferramenta | 400 |
| Criatura | 2.500 (chefe: 5.000) |
| Guia e outros NPCs | 3.000 |
| Tile hexagonal | 100, instanciado em lote (um mesmo modelo repetido) |
| Árvore, pedra e prop pequeno | 300 a 600 |
| Prop grande (casa, ponte) | 1.500 |
| Torre | 6.000 |
| Peça que orbita a torre (medalhão, peça de madeira) | 150, no máximo 40 por torre, instanciadas |
| Tudo o que aparece numa sala ou no Lobby | 100.000 a 150.000 (a medir em tablet) |

**Texturas:** um material por modelo, **só cor base** (sem normal, metálico nem rugosidade, porque o estilo é pintado à mão). 1024 pixels para personagem e criatura, 512 para prop e atlas compartilhado de 1024 para os tiles, com compressão de GPU no import do Godot.

**Como chegar lá no Meshy** (medido no corpo base):

- O Image to 3D com `target_polycount` 12.000 gerou **12.461 triângulos** e uma textura de 2048 pixels (2,7 MB). Muito para um personagem chibi.
- O *remesh* da Meshy (5 créditos) levou o mesmo modelo para **3.671 triângulos** com o visual praticamente idêntico, **mas** devolveu 3 texturas PNG (2048, 2048 e 4096 pixels, 8 MB no total). Só a cor base é usada; o resto se descarta.
- Portanto: gerar com `should_remesh` e `target_polycount` já no limite da tabela, e **medir os triângulos do arquivo** antes de aceitar. Uma verificação automática de triângulos dos modelos na CI é recomendada quando houver modelos no repositório.

## 9. Animação, física e efeitos

Tudo aqui é **proposta** para o MVP. A regra geral é a mesma da seção 8: o que o motor faz de graça não se gera em modelo.

**Personagem (humanoide).** O Meshy tem rig automático, mas só para humanoides, e devolve animações básicas de andar e correr. Para as demais, a opção é a biblioteca do Mixamo, reaproveitada no Godot pelo mapa de ossos humanoide. Clipes do MVP: parado, andar, interagir, atacar (lançar o orbe ou conjurar sílabas), acerto, golpe perfeito, erro (tropeço leve, sem queda) e voltar flutuando ao Lobby. O repositório [Motion2MixamoRig](https://github.com/MaxLiu22/Motion2MixamoRig), que copia o movimento de um vídeo para um rig do Mixamo, **não entra no MVP**: é experimental, baixa cerca de 5 GB de pesos, é lento em CPU, exige vídeo de uma pessoa, e o modelo corporal SMPL-X é de uso não comercial por padrão. Fica como opção se um dia precisarmos de um movimento próprio.

**Criaturas e torres (não humanoides).** O Meshy não faz rig delas. Elas se animam **no Godot, sem esqueleto**: pairar e balançar, esticar e achatar, inclinar, pulsar o escudo, balançar por shader de vértice. Clipes: parada, mover devagar, avisar antes de atacar, lançar bolha, travessura, ser libertada (sorri e se dissolve). Os anéis e as peças das torres giram por script.

**Física.** O MVP não usa `RigidBody3D` em nenhuma regra de jogo. O Personagem e as criaturas são corpos cinemáticos que andam na grade hexagonal, e as bolhas de névoa são áreas lentas com aviso. Física torna o tempo do jogo menos previsível e pesa em tablet. Só objetos decorativos opcionais podem usá-la, e o Modo calmo os desliga.

**Efeitos.** Feitos com partículas e shaders do Godot, sem modelo do Meshy: raio de energia da conta ou do nome, névoa que se dissolve, brilho dourado do acerto, rajada do golpe perfeito, bolha com anel de aviso, tremida leve do erro (nunca vermelho forte), brilho do portal e pulso do cristal. Limite proposto: até 200 partículas na tela, e o Modo calmo reduz as partículas.
