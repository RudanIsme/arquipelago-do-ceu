# Lista de assets do MVP

O que precisa existir para o MVP (duas torres, o Lobby, dois Papéis e 12 criaturas), separado por como cada coisa é feita. **Proposta**, a confirmar. Os limites de triângulos estão no [guia de estilo visual](guia-de-estilo-visual.md), seção 8. Custos em créditos do Meshy, medidos: modelo `latest` = 30, `meshy-6-lite` = 15 (visual faceteado, bom para fundo), remesh = 5.

## 1. Gerar no Meshy (Image to 3D)

| Grupo | Assets | Modelo | Créditos |
| --- | --- | --- | --- |
| Personagem | Corpo base com **rosto liso** (inclui gerar a imagem nova) | latest | 33 |
| | 4 cabelos (curto bagunçado, afro, longo ondulado, maria-chiquinha) | lite | 60 |
| | 2 ferramentas (cajado de ábaco, cajado do Escriba) | lite | 30 |
| NPCs | Guia (3.000 triângulos) e Boneco de treino (amigável, nova imagem) | latest | 63 |
| Lobby | Portal da Memória, atril do Álbum, canteiro em construção, 2 árvores, 1 pedra | lite | 90 |
| | Torre das Letras | latest | 30 |
| Criaturas de Números | 5 novas (coruja com ábaco, pássaro de engrenagens, pardal com hélice, coruja-relógio, coruja grande); a coruja básica já está pronta | latest | 150 |
| Criaturas de Letras | 6 (pato, bode, sapo, gato, mula, bota) | latest | 180 |
| **Total** | **27 modelos** | | **cerca de 636** |

Saldo em 2026-10-13: 880. A torre dos Números e a coruja básica já estão gerados.

## 2. Sem modelo do Meshy

- **Chão hexagonal:** um prisma hexagonal de poucos triângulos, feito no Godot, com texturas pintadas a partir da folha `30-chao-hexagonal-vivo`.
- **Água, nuvens, névoa, efeitos:** shaders e partículas do Godot.
- **Rosto do Personagem:** olhos por IA (folha `31a`), sobrancelhas e bocas em vetor.
- **Interface:** Control nodes com a fonte do HUD.

## 3. Fica para depois do MVP

Roupas completas dos Papéis ajustadas ao corpo (exigem Blender), as outras três torres, criaturas além das 12, objetos de interior das salas e o balão e as ilhas do céu.

## 4. Pendências de decisão

- Confirmar a lista e os limites de triângulos.
- Como vestir o Personagem no MVP: peças modulares ajustadas no Blender, ou modelos completos por Papel.
