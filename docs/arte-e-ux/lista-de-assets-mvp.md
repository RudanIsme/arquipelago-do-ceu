# Lista de assets para testar a ideia

Escopo **mínimo** para validar o jogo: andar no Lobby, treinar contra o Boneco e ver uma criatura de cada torre. **Sem customização por enquanto:** nada de cabelos, partes do rosto, roupas por Papel nem criaturas extras. Os limites de triângulos estão no [guia de estilo visual](guia-de-estilo-visual.md), seção 8. Custos medidos em créditos do Meshy: modelo `latest` = 30, `meshy-6-lite` = 15, remesh e rig = 5.

## Modelos gerados (todos em `docs/arte-e-ux/meshy/modelos/`, fora do Git)

| Grupo | Asset | Triângulos | Limite | Modelo |
| --- | --- | --- | --- | --- |
| Personagem | Corpo base, com rig de 24 ossos, andar e correr | 3.671 | 3.500 | latest + remesh + rig |
| Lobby | Boneco de treino | 2.529 | 2.500 | latest |
| | Guia | 3.132 | 3.000 | latest |
| | Portal de fast travel | 1.469 | 1.500 | lite |
| | Atril do Álbum | 796 | 800 | lite |
| | Grupo de árvores | 420 | 600 | lite |
| Torres | Torre dos Números | 5.851 | 6.000 | latest |
| | Torre das Letras | 5.849 | 6.000 | latest |
| Criaturas | Coruja mecânica (Números) | 2.580 | 2.500 | latest |
| | Pato (Letras) | 2.615 | 2.500 | latest |

Os arquivos têm 1 textura de 2048 px (o do Personagem, 3), mas só a cor base será usada, a 1024 px no import do Godot. Os que passam um pouco do limite (até 5%) foram aceitos para o teste.

## Sem modelo do Meshy

- **Chão hexagonal:** um prisma hexagonal simples no Godot, com texturas pintadas a partir da folha `30-chao-hexagonal-vivo`.
- **Água, nuvens, névoa e efeitos:** shaders e partículas do Godot.
- **Animação de criaturas e torres:** no Godot, sem esqueleto (seção 9 do guia).
- **Interface:** Control nodes com a fonte do HUD.

## Fica para depois

Customização do Personagem (cabelos, partes do rosto, roupas e ferramentas dos Papéis, que exigem Blender), criaturas além de uma por torre, as outras três torres, interiores das salas, balão e ilhas do céu.
