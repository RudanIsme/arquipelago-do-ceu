# Folha de pedidos do Meshy (#199)

O que falta modelar além do que já está em `game/assets/modelos/`. A fonte dos dados é [`tools/meshy/pedidos.json`](../../../tools/meshy/pedidos.json); esta tabela sai de `node tools/meshy/gerar.mjs --lista --md`.

**Gerado em 18/10/2026**, depois de o usuário aprovar a lista e o custo (o script recusa rodar sem `--aprovado=<total>`). O totem de Fast travel saiu da lista porque o `portal.glb` já existe.

## Custo

Tabela de preços do Meshy (docs.meshy.ai/en/api/pricing, conferida em 18/10/2026): prévia da malha 20 créditos e textura 2K 10 créditos, ou seja, **30 créditos por modelo**. A malha já sai reduzida ao alvo de triângulos (`should_remesh` + `target_polycount`), sem pagar o remesh à parte. Nenhum modelo leva rig nem animação: o movimento das criaturas é feito no Godot, como diz o guia de estilo.

- Prioridade A: 14 modelos; B: 6; total de 20 modelos, **600 créditos** pelo preço de tabela.
- Saldo antes: 717; depois da primeira rodada: 127.
- Os 3 que saíram fora do pedido foram refeitos em 18/10/2026 (90 créditos; saldo final 37). O pergaminho ficou bom; a parede baixa (bloco quadrado vazado) e a pilha de engrenagens (anéis) são só aceitáveis e podem ser refeitas se incomodarem.

## Como entram no jogo

1. Gerar com o script, numa pasta **fora do repositório**.
2. Otimizar com `tools/otimizar-modelos` (só cor base, 1024 px) e conferir os triângulos contra o limite.
3. Copiar para `game/assets/modelos/` com o nome da coluna Arquivo, sem mudar código (o jogo carrega pelo nome, como o Lobby).
4. Estilo, referências e negativo: os mesmos de [prompts-meshy.md](prompts-meshy.md). A chave fica em `MESHY_API_KEY`, nunca no repositório.

## Lista

| Prior. | Arquivo | Grupo | Triângulos | Tamanho | Clipes | Créditos | Prompt |
| --- | --- | --- | --- | --- | --- | --- | --- |
| A | `criaturas/automato-base.glb` | Criatura (Números) | 2500 | 1 hexágono | nenhum no modelo (movimento feito no Godot) | 30 | cute small round brass automaton robot, egg-shaped body, short stubby legs, tiny arms holding a round blank shield plate with an empty frame, small gear on top of the head, one big glowing friendly round eye, no mouth |
| A | `criaturas/automato-pesado.glb` | Criatura (Números) | 2500 | 1 hexágono | nenhum no modelo (movimento feito no Godot) | 30 | cute chubby round brass automaton with a big blank square shield plate, a clock face on its belly, short legs, one big glowing friendly round eye, no mouth |
| A | `criaturas/automato-voador.glb` | Criatura (Números) | 2500 | 1 hexágono | nenhum no modelo (movimento feito no Godot) | 30 | cute small round brass automaton floating in the air with a propeller on top, blank round shield plate hanging from its body, one big glowing friendly round eye, no mouth |
| A | `criaturas/papel-fantasma.glb` | Criatura (Letras) | 2500 | 1 hexágono | nenhum no modelo (movimento feito no Godot) | 30 | cute soft round ghost-like creature made of crumpled cream paper, faded gray letters floating around its body, sleepy big eyes, small paper arms, blank paper tag on its chest, no mouth |
| A | `criaturas/livro-com-pernas.glb` | Criatura (Letras) | 2500 | 1 hexágono | nenhum no modelo (movimento feito no Godot) | 30 | cute small round creature that is a closed purple book with tiny legs, faded letters drifting from the pages, big friendly eyes on the cover, blank bookmark tag, no mouth |
| A | `criaturas/pergaminho.glb` | Criatura (Letras) | 2500 | 1 hexágono | nenhum no modelo (movimento feito no Godot) | 30 | cute round creature wrapped in a loose parchment scroll, faded smudged letters on it, curious big eyes, blank label, no mouth |
| B | `criaturas/automato-cristal.glb` | Criatura (Números) | 2500 | 1 hexágono | nenhum no modelo (movimento feito no Godot) | 30 | cute round brass automaton with teal crystal shoulder pads and a blank hexagonal shield plate, one big glowing friendly round eye, no mouth |
| B | `criaturas/tinta-roxa.glb` | Criatura (Letras) | 2500 | 1 hexágono | nenhum no modelo (movimento feito no Godot) | 30 | cute round soft ink blob creature in light purple with a few faded letters inside, big shy eyes, no mouth |
| A | `cenario/porta-arco.glb` | Cenário | 1000 | 1 hexágono de largura, 2 de altura | nenhum no modelo (movimento feito no Godot) | 30 | game door frame: two thick stone pillars and a lintel forming an open arch, a plain open passage between them, small carved gear on top, no door leaf, front view |
| A | `cenario/escada-pedra.glb` | Cenário | 1200 | 1 hexágono | nenhum no modelo (movimento feito no Godot) | 30 | short stone staircase of five wide steps on a hexagonal base, chunky rounded steps, a small glowing rune on the top step |
| A | `cenario/fonte-descanso.glb` | Cenário | 1000 | 1 hexágono | nenhum no modelo (movimento feito no Godot) | 30 | small cozy stone fountain with calm glowing blue water and a little cushion beside it, round base, welcoming |
| A | `cenario/parede-baixa.glb` | Cenário | 200 | 1 hexágono de comprimento, meio de altura | nenhum no modelo (movimento feito no Godot) | 30 | low castle wall segment with a battlement notch on top, chunky rounded stone, straight, short, tileable on both ends |
| A | `cenario/pilar-torre.glb` | Cenário | 250 | meio hexágono de largura, 1,5 de altura | nenhum no modelo (movimento feito no Godot) | 30 | short chunky stone pillar with a small rounded cap, slightly weathered, hand-painted, front view |
| A | `obstaculos/caixote-madeira.glb` | Obstáculo | 400 | 1 hexágono | nenhum no modelo (movimento feito no Godot) | 30 | chunky wooden crate with brass corners, a small gear stamp on the side, slightly rounded |
| A | `obstaculos/pilha-de-livros.glb` | Obstáculo | 500 | 1 hexágono | nenhum no modelo (movimento feito no Godot) | 30 | tall tilted stack of colorful closed books with a few loose pages, chunky rounded, cozy library prop |
| A | `obstaculos/pilha-de-engrenagens.glb` | Obstáculo | 500 | 1 hexágono | nenhum no modelo (movimento feito no Godot) | 30 | heap of big brass gears and cogs piled together, chunky rounded, workshop prop |
| B | `itens/orbe-de-numero.glb` | Item | 300 | um quarto de hexágono | nenhum no modelo (movimento feito no Godot) | 30 | glowing golden orb with a soft halo, blank face, chunky rounded |
| B | `itens/medalhao-engrenagem.glb` | Item | 300 | um quarto de hexágono | nenhum no modelo (movimento feito no Godot) | 30 | golden gear medallion with a short chain, chunky rounded, blank center |
| B | `itens/placa-de-letra.glb` | Item | 300 | um quarto de hexágono | nenhum no modelo (movimento feito no Godot) | 30 | glowing wooden letter tile, blank face, rounded edges, small golden border |
| B | `cenario/pedestal-mestre.glb` | Cenário | 1500 | 2 hexágonos | nenhum no modelo (movimento feito no Godot) | 30 | round stone pedestal platform with three wide steps and a glowing ring on top, for a master challenge hall, chunky rounded |
