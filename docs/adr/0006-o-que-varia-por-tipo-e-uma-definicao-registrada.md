# O que varia por tipo é uma definição registrada, e só o que tem comportamento vira estratégia

Espaço de equipamento, efeito de equipamento, tipo de Sala, gatilho de recompensa, Mecânica e torre são **definições registradas por id**. Quando a definição só descreve (nome, letra, forma, limite, valor), ela vive em dados. Quando tem comportamento (somar um atributo, decidir se uma Sala pede vitória, dizer quando uma recompensa sai), ela é uma **estratégia**: uma classe pequena atrás de uma interface, registrada por id num único lugar. O resto do código lê a definição e nunca faz `switch` nem `enum` por tipo. Um tipo novo é uma linha de dados, ou uma classe mais uma linha no registro, e nada mais.

Descartamos a herança (uma classe base `Equipamento` com `Capacete` e `Armadura` por baixo) porque as peças só diferem no espaço em que entram e nos números, e isso é dado. Descartamos também mais `enum`s, que hoje obrigam um espaço ou atributo novo a mexer em cerca de seis arquivos, e plug-ins carregados por reflexão ou de fora do jogo: o registro é explícito, testável, e o jogo nunca executa código que não é dele (CLAUDE.md). Id desconhecido é validado na borda e vira `Result`, nunca exceção.

Continua valendo o "pragmático" do ADR 0001: interface só num ponto de extensão real, sem estrutura "para o futuro". A definição de um tipo só nasce quando existe o segundo tipo ou um pedido concreto; antes disso, o código simples basta. O custo é uma camada de indireção e a validação dos dados na carga. Mudar essa regra muda como todo tipo novo entra no jogo e o formato dos dados e do save; por isso o registro. Os tickets #163 a #168 aplicam a decisão em cada ponto.

## Guia: um tipo novo de Sala

1. Acrescente um `RoomType` em `core/Generation/RoomTypes.cs` (id, nome, letra, cor, forma e os campos de comportamento que ele precisar: `NeedsVictory`, `Counts`, `Free`, `Line`...) e registre-o em `RoomTypes.Default`.
2. Se o gerador deve sorteá-lo entre as Salas livres, marque `Free: true`. Ele passa a poder guardar o Item de troca e o validador o aceita sem mudança.
3. Se tem algo para ver no mundo 3D além do nome e da cor, o desenho é um acréscimo em `DungeonWorld`.
4. Teste: `RoomTypeTests` mostra um tipo entrando só com a definição.

Não existe mais `enum` de Sala nem `switch` por tipo. Mudar a ordem de `Free` muda as seeds (a ordem do registro entra no sorteio).
