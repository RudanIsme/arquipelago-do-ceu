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

O chão do Lobby e das salas é de **hexágonos**, como nos mockups. O padrão de geração é a folha `18-chao-hexagonal-lobby-b` (peças lisas e simples, que se encaixam numa grade), com as cores da seção 1.

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
