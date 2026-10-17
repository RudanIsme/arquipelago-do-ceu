# Arquipélago do Céu: documento técnico

## Visão geral

O jogo é um aplicativo Godot que funciona inteiro offline. O conteúdo educativo nasce fora do jogo (missões à mão no build e pacotes gerados e revisados por um serviço separado) e chega em JSON validado.

Sem rede, a criança continua jogando; só a sincronização e a telemetria esperam.

## Cliente do jogo

O jogo é feito em Godot 4 (versão .NET) com C#, roda 100% offline e só usa a rede para sincronizar, enviar telemetria e baixar missões novas. O Godot suporta C# oficialmente. O backend é um projeto separado, fora deste repositório.

O Godot com C# não exporta para navegador. Como navegador não é alvo, isso não pesa.

### Módulos

| Módulo | Responsabilidade |
| --- | --- |
| Core | Estado do jogo, cenas, salvamento local, Perfil |
| Lobby | Cena do Lobby, Guia, Área de treino (usa o módulo de combate), Álbum, portal de fast travel, configurações |
| World | Grade hexagonal, movimento e câmera (`core/World`) |
| Dungeon | `core/Generation` (Plano do Nível, gerador de Andares, validador, Andares de reserva), `core/Dungeon` (Expedição e escolha do combate de cada Sala) e `game/Dungeon` (mundo 3D das Salas, mapa, HUD e a Expedição no Godot) |
| Rooms | Cenas de sala feitas à mão, com pontos de encaixe para pontes e desafios |
| Missions | Carrega missões em JSON e executa as mecânicas (compor, comparar, sequência, sílabas, frases) |
| Characters | Personagem personalizável do Perfil (um corpo e um rig; rosto por atlas de texturas, pele e cabelo por material, cabelo e acessórios presos a ossos), Papéis (Matemático e Escriba), habilidades destravadas por conceito e Cosméticos |
| Equipment | `core/Equipment`: `GearCatalog` (equipamentos em dados), `GearLibrary` (biblioteca ordenada e comparação do que muda), `InventoryService` (Cosméticos vestidos, o que já foi visto e o guia no Perfil) e `EquipmentLooks` (a aparência do conjunto). No Godot: o Inventário (`game/Inventory`), o dock (`Dock`), o HUD do Lobby (`LobbyHud`), `EquipmentVisuals` (equipamento e Cosméticos procedurais), `DynamicCape` e a pesca de teste (`Main.PullTest`, F9, não grava progresso) |
| Adaptive | Regra de dificuldade por conceito e caixas de repetição espaçada (Leitner), calculadas no próprio jogo, sem precisar do backend |
| Accessibility | TTS, legendas, tamanho de texto, remoção de tempo, redução de animações |
| Net | Login opcional, sincronização e fila de telemetria que espera conexão |

### Arquitetura do código

Hexagonal com DDD pragmático (ver `docs/adr/0001-arquitetura-hexagonal-pragmatica.md`). Três camadas, e as dependências só apontam para dentro:

| Camada | O que tem | Pode depender de |
| --- | --- | --- |
| Domínio | Regras puras: Energia, Conceito, Missão, avaliadores, PCG32, Leitner | Nada |
| Aplicação | Casos de uso e as **portas** (interfaces): salvar, falar, rede, relógio, sorteio | Domínio |
| Adaptadores | Godot (cenas, nós, entrada), arquivo de save, voz do sistema, cliente de rede | Aplicação e Domínio |

Domínio e Aplicação ficam numa biblioteca C# **sem referência ao Godot**, em `core/`, para o compilador impedir o vazamento. `game/` guarda os adaptadores do Godot e a raiz de composição (`Main`), que monta tudo. A biblioteca `core/` nasce junto com o primeiro código de regra, e não antes. Os testes das duas camadas rodam sem abrir o Godot. Adaptadores que não precisam do Godot, como o arquivo de save (`System.IO` e JSON), também ficam em `core/`, para serem testados sem o motor; o Godot só informa o caminho (`user://`) ao criá-los.

Regras para manter o código simples:

- **DDD só onde ajuda.** O nome de cada tipo de domínio é o termo do `GLOSSARY.md` e não muda. Agregado só onde há invariante a proteger (Perfil, Energia da Expedição). Sem repositório genérico, sem eventos de domínio e sem camadas extras até serem necessários.
- **Poucos arquivos.** Um arquivo por conceito; tipos pequenos e do mesmo conceito ficam juntos. Interface só nas fronteiras da tabela acima, nunca "por garantia".
- **Falha esperada é valor, não exceção.** Dado externo (JSON de Missão, save, rede, voz) é validado na borda. Erro esperado volta como resultado explícito; exceção é só para bug. Nenhuma falha de voz, rede ou save pode travar a criança: o jogo cai num comportamento seguro e avisa.
- **Simples de manter.** Funções curtas, sem herança profunda, composição no lugar de herança, e testes que servem de exemplo de uso.
- **Piloto fora da produção.** Os roteiros de depuração (`game/Dev/`) não ganham métodos `Dev*` nas classes do jogo: a `Main` só entrega ao `DevDriver` um `PilotPort` (em build de depuração), que expõe o estado e força vitórias e derrotas pelas mesmas saídas internas da luta de verdade. Nenhum arquivo de código passa de 400 linhas (`ArquiteturaTests`).
- **Comentário responde "por quê".** Só para uma restrição, uma decisão não óbvia ou uma armadilha. Nunca para dizer o que o código já diz.

### Como adicionar uma coisa nova (ADR 0006)

O que varia por tipo é uma definição registrada por id: em dados quando só descreve, em estratégia quando tem comportamento. A coluna "hoje" diz o que é verdade agora; a última, quando o pivô de arquitetura (#160) faz do caminho ideal o único.

| Para adicionar | Hoje | Depois do pivô |
|---|---|---|
| Peça de equipamento | Uma entrada em `game/data/equipamentos.json` (espaço, efeito, valor, Maestria mínima). Sem visual 3D, a peça só dá o efeito (aparece com a aparência original) | Igual, com a aparência também em dados (#127) |
| Cosmético | Uma entrada em `game/data/cosmeticos.json` e uma recompensa em `game/data/recompensas.json` ligando-o a um gatilho (e a um Andar, se quiser); vários Cosméticos podem sair do mesmo gatilho | Igual |
| Missão | Um JSON em `content/missions/`, validado pelo schema | Igual |
| Conceito | Nome em `game/data/conceitos.json`; um teste cobra | Igual |
| Andar de reserva | Uma entrada em `game/data/andares-reserva.json` | Igual |
| Valor de regra | `game/data/rules.json` | Igual |
| Espaço de equipamento | Só dados: uma entrada em `slots` do mesmo arquivo (o Inventário mostra todos os espaços do catálogo) | Igual |
| Efeito de equipamento | Uma classe que implementa `IGearEffect` registrada em `GearEffects`, mais o limite em `effects` do arquivo; o cálculo dos bônus não muda | Igual |
| Tipo de Sala | Uma definição `RoomType` em `RoomTypes` (`core/Generation/RoomTypes.cs`): nome, letra, cor, forma, se pede vitória, como conta no progresso, falas e o que a Sala faz (descansa, abre com o Item, encerra a Expedição, pode guardar o Item). O gerador, o validador, o progresso, a voz, o `FloorMap` e o `DungeonWorld` só leem esses campos. Uma Sala nova que o gerador sorteie é só a definição (`Free`); o desenho 3D de algo exclusivo dela é um acréscimo em `DungeonWorld` | Igual |
| Gatilho de recompensa | Uma linha em `RewardTriggers` (`RepeatableTrigger` ou `OnceTrigger`, ou uma classe que implemente `IRewardTrigger`) e as recompensas dele em `recompensas.json`; quem dispara o gatilho (a Dungeon) chama `RewardService.Grant` | Igual |
| Mecânica | Código: `MissionLoader.KnownMechanics`, a luta em `core/Combat` e o painel em `game/Ui` | Igual, registrada por id |
| Torre, Papel ou Nível | Uma entrada em `game/data/torres.json` (id, nome, Papel, assunto, Mecânicas aceitas, quantos Níveis e com qual torre troca o Item); o último Nível do jogo é o da torre que mais tem. As Missões da torre nova entram em `content/missions/` com a disciplina dela. O objeto da torre no Lobby (`torre-<id>` em `rules.json`) também é um dado | Igual |

### Geração procedural

- Primeiro se gera o Plano do Nível, a partir de `(seed do mundo, dungeon, nível, versão do gerador)`: quantos Andares (3 a 4) e em qual ficam a Sala do Item, o Marco e o Desafio épico, sempre nessa ordem. Depois cada andar é gerado inteiro, de uma vez, a partir de `(seed do mundo, dungeon, nível, número do andar, versão do gerador)` e do seu trecho do Plano. A mesma entrada sempre gera o mesmo mapa (ADR 0003).
- Usar um gerador de números aleatórios próprio (PCG32, cerca de 30 linhas) em vez de `System.Random`, para que a sequência não mude entre versões do .NET ou do Godot.
- O andar é um grafo de 6 a 9 Salas. As portas trancadas são Puzzles, sem chaves. O validador confere, antes de mostrar o andar, que toda Sala obrigatória é alcançável a partir da entrada, que o Fast travel e todo Puzzle são alcançáveis sem passar pelo Marco, e que a Sala do Item vem antes do Marco no Plano. Se falhar, carrega um andar feito à mão.
- O gerador roda em testes automatizados com milhares de seeds.

**Tema separado da estrutura.** O gerador só conhece funções de sala: combate, puzzle, descanso, fast travel, marco, desafio épico. A Sala do Item não é uma função: é uma sala de combate ou puzzle marcada com o prêmio. Um pacote de tema em dados (cenas de sala, objetos, criaturas, música, paleta, vocabulário da narrativa) veste essas salas. Uma disciplina nova, como História, é um pacote de tema mais mecânicas novas, sem mexer no gerador. Um pacote pode ter sub-temas, como uma época histórica por andar.

**Valores de regra em dados.** Nome do jogo e dos Papéis, energia máxima (5 no MVP), limiares da dificuldade adaptativa e intervalos das caixas de Leitner ficam em arquivos de dados, não no código, para mudar sem recompilar regras.

**Nível da dungeon = ano escolar.** A seed do andar inclui o nível, e o distribuidor de desafios só escolhe missões daquele ano, mais as de antecipação (`stretch`) no fim do nível.

### Salvamento

Arquivo JSON local em `user://`, com número de versão do formato. Guarda a seed, a versão do gerador, o Nível liberado (o `Level` do Perfil), os Níveis com o Desafio épico concluído por Dungeon, andares descobertos, puzzles resolvidos, Fast travels encontrados, itens de troca (que não se perdem quando a Energia zera), habilidades, aparência do Personagem e Cosméticos ganhos, Atalhos de Nível já usados, progresso por conceito, caixa de memória e data da próxima revisão, e energia da expedição em andamento. Grava em arquivo temporário e renomeia, para não corromper se o PC desligar. Salva sozinho a cada sala e ao pausar, e também no ponto de fast travel de cada andar.

### Voz e câmera

- TTS pela API embutida do Godot (`DisplayServer.TtsSpeak`), escolhendo uma voz pt-BR do sistema. O jogo nunca interrompe uma fala em andamento: guarda a própria fila e só manda falar quando a anterior termina (ADR 0005).
- Câmera 3D ortográfica em ângulo isométrico; estilo alvo 3D isométrico detalhado, com assets CC0 estilizados como provisórios no MVP. Para manter o desempenho com arte detalhada: iluminação pré-calculada (lightmaps), LOD e um orçamento de polígonos por sala definido na prova técnica.

### Combate

O combate roda em tempo real com escala de tempo: ao abrir a montagem da resposta, `Engine.TimeScale` cai (ex.: 0,15), ou vai a zero no modo calmo, e volta ao normal quando o ataque sai. A IA das criaturas é simples: patrulhar, avisar, lançar bolha lenta e reagir a erro. A bolsa do Matemático e a de sílabas do Escriba são a mesma estrutura de dados (um baralho de peças com sorteio pela seed), só com peças diferentes. Quem valida a jogada é código: o avaliador de expressões no Matemático e a comparação com o nome esperado no Escriba.

**Bolsa, cajado e tempo (#43 a #54),** tudo em `core/Combat/`, sem Godot: `MathBag` (pilha, mão e descarte, embaralhados pelo PCG32) e `Staff` (o cajado: montar, desfazer e confirmar; só confirmar gasta peças), `ExpressionEvaluator` e `Shield` (abaixo tira do escudo, igual dissolve, acima não vale), `CombatTime` (a escala de tempo), `SyllableBag` e `NameChecker` (Escriba) e `TrainingBattle` (a luta com o Boneco, com o escudo vindo de uma Missão de compor número). A conta é montada como uma calculadora (número, sinal, número...), com os sinais em `combat.operators`, hoje só +. Uma conta precisa de um sinal e de pelo menos dois números, e o cajado nunca expõe o total antes da confirmação. `Tracing` confere o desenho do número ou da sílaba contra um pontilhado gerado da própria fonte do jogo (`combat.tracing`), sem reconhecer a letra. Mão, peças da bolsa, cópias de cada peça e a escala reduzida (`slowTimeScale`) ficam em `combat` no `rules.json`. O Godot só copia `CombatTime.Scale` para `Engine.TimeScale`. As Missões e o schema de `content/` são copiados para `game/content/` a cada build (pasta ignorada pelo git) e lidos por `MissionSource`.

**Treino (#55 a #73),** também em `core/Combat/`: `Energy` (máximo vindo de `maxEnergy`), `BubbleAttack` (a bolha do Boneco: espera, aviso, voo lento e acerto, em `Vector2`, sem Godot; a mira trava quando o aviso começa e a velocidade, o intervalo e o raio vêm de `combat.bubble`, nunca do Nível), a travessura em `MathBag.PlayPrank` e `NameBattle.PlayPrank` (embaralhar a bolsa ou esconder uma peça, que volta com `Reveal` depois de `combat.prankSeconds`), `NameBattle` (a luta do Escriba: cada sílaba certa é um golpe, o nome completo liberta) e `CombatSession` (o treino: Papel, Nível liberado e dificuldade; sorteio das Missões pela seed; Energia, erros seguidos e dicas). Em `combat`, `energy` guarda quanto cada acontecimento muda a Energia, `mistakesForHint` quantos erros seguidos oferecem a dica e `prankSeconds` quanto a peça escondida some. No Godot, `TrainingArea` liga a sessão ao Boneco (`TrainingDummy`), ao Personagem, às telas (`TrainingSetupScreen`, `StaffPanel`, `NamePanel`, `EnergyView`) e à voz.

**Níveis 2 e 3, câmera e luz (#74 a #94):** `Staff` aceita `-` (`Token.Minus`; a conta que dá zero ou menos não vale), `CombatRules.ForYear` troca a bolsa e os sinais pelo ano (`combat.years`: o 2º ano tem as dezenas 10 e 20 e a subtração; o 3º usa os do 2º até existir multiplicação), `WordBattle` (o Escriba do 2º ano escreve a palavra inteira, grafia exata, letra sugerida sem custo) e `NameBattle` passa a montar também frases (`ordenar_frases`) e a completar frase com a palavra de grafia certa (`completar_frase`). O `CombatSession` escolhe a luta pela Mecânica das Missões do ano. `CameraRig` (em `core/World`) guarda o zoom e o acompanhamento da câmera do Lobby (`lobby.camera` em dados); o `CameraController` do Godot copia o resultado para a câmera e liga a roda do mouse, a pinça e as teclas. A iluminação (luz quente, SSAO, glow só em itens com emissão, desfoque de profundidade leve) fica em `Lobby.AddEnvironment`; a opção `reduceEffects` desliga os efeitos. O SSAO só aparece no renderizador Forward+: o Mobile, que o jogo usa, o ignora.

**Dungeons (#101 a #111).** Tudo em `core/`, sem Godot, e testado sem o motor. `LevelPlan.Create` sorteia o Plano do Nível a partir de `(mundo, dungeon, nível, versão)` com `Seed.Mix` (FNV-1a próprio): de `dungeon.floorsMin` a `floorsMax` Andares, o Item num Andar anterior ao do Marco e o épico no último. `FloorGenerator.Generate` monta um Andar de `roomsMin` a `roomsMax` Salas: uma árvore a partir da entrada, mais um laço opcional, com o Marco como única passagem até a saída. `FloorValidator` recusa o Andar que quebra uma regra, e `FloorProvider` entrega o de `FallbackFloors` (`game/data/andares-reserva.json`). `Expedition` guarda a Sala, o que foi vencido e visitado em cada Andar (a escada vale nos dois sentidos, ADR 0004), a Energia da Expedição (a mesma `Energy` passada a cada `CombatSession`), o Descanso, e pode recomeçar numa entrada, num Fast travel ou direto no épico (o Atalho); ela também diz o que o mapa mostra (`SightOf`: visitada, névoa ou nada; `IsLocked`: o cadeado) e o progresso do Andar (`ProgressOf`, `IsFloorComplete`). `DungeonCombat` escolhe a dificuldade pelo Andar, e também os Desafios sorteados do Atalho e do Desafio mestre. Os serviços do progresso (`ProgressServices`: `LevelProgress`, `ExplorationService`, `RewardService`, `MasteryService` e `WalletService`, sobre um `ProgressStore` comum) gravam em `ProfileSave.Progress`, e os identificadores são tipos (`LevelKey`, `FloorKey`, `ConceptKey`) que são o único lugar onde o texto do save ("matematica:1:2") nasce e é lido os épicos concluídos, os Itens de troca, os Andares descobertos, os Fast travels e os Atalhos tentados, e sobe o `Level` do Perfil. Também guarda, em `Progress`, a exploração de cada Andar (`Explored`: chave `disciplina:nível:andar`, Salas visitadas e vencidas; os Descansos nunca entram), os Andares completos (`FloorsDone`) e os ids dos Cosméticos ganhos (`Cosmetics`); todos são opcionais, então um save antigo abre com tudo vazio, e o Fast travel que ele já tinha basta para o recomeço (as Salas do caminho até ele contam como vencidas). Sala que não existe mais no Andar gerado é ignorada ao carregar. Ainda em `Progress`: `Solved` (vitórias por Conceito), `Mastered` (Conceitos dominados; a Maestria do Papel é quantos há com a torre dele, e nunca cai) e `Coins` (saldo). `RecordSolved` e `AddCoins` só são chamados pelas `DungeonRewards` (teste de arquitetura), então o treino não conta progresso. As regras (`dungeon.conceptSolves`, `coinsCreature`, `coinsItem`, `coinsFloor`) ficam em `rules.json`.

**Equipamento (#126).** `GearCatalog` (`core/Equipment/Gear.cs`) lê `game/data/equipamentos.json` (os limites do conjunto e cada peça: espaço, atributo, valor e Maestria mínima), recusa JSON quebrado, id repetido e valor acima do limite, e implementa `Equip` (a Maestria decide), `Unequip` e `Bonuses` (soma limitada, devolve o `EquipmentBonuses` de sempre). `GearService` grava `ProfileSave.Gear` (um `Gear` por Papel; ausente vale vazio) e o `Main` usa os bônus do Papel de agora (a torre ou o treino mais recentes). Os ids das peças são os do experimento do PR #97. O `EquipmentCatalog` antigo e a oficina saíram com o Inventário (#127, #172): a aparência do conjunto vem de `EquipmentLooks.For`.

**Inventário (#123, #127, #128, #129).** `InventoryScreen` (um `OverlayScreen`) tem duas abas. `GearTab` mostra as caixinhas por espaço, a biblioteca (os vestidos primeiro, depois o mais novo, com cadeado e o número da Maestria) e a comparação, que vem de `GearLibrary.Compare` (só o que muda, com ▲, ▼ e a frase dita em voz). Apontar, tocar ou focar só faz a prévia; Equipar (ou Enter no equipamento focado) grava no `GearService`, e equipar nunca cura. `CosmeticsTab` veste e tira Cosméticos na hora (`InventoryService.Wear`), sem atributos. Cada Cosmético tem em `cosmeticos.json` uma aparência (`look`: peça e cor) que `EquipmentVisuals` desenha. A tela não conhece o `Main`: tudo chega por `InventoryHost`. Na Dungeon o dock abre o mesmo Inventário, mas só os Cosméticos mudam (equipamento mexe na Energia máxima, e isso se troca no Lobby); durante a luta o dock some e a tecla I não abre. Enquanto o #130 não concede equipamento, tudo o que está no catálogo é da criança. O selo "novo" (texto no botão do dock, na aba e no item) some quando a criança vê o item, e o guia de primeira vez abre uma vez (o "?" o revê); os dois ficam em `ProfileSave.Seen` e `ProfileSave.GuideSeen`, e os Cosméticos vestidos em `ProfileSave.Worn`, todos opcionais para saves antigos. O campo `journey` do experimento (interface e cores) não existe mais: um save antigo com ele abre normalmente e o campo é ignorado.

**Expedição livre (#135).** `LevelPlan.Free` é um Plano sem fim (`Endless`: todo Andar é comum, e `For(n)` gera o Plano do Andar n); a `Expedition` o aceita no lugar do Plano sorteado, então a geração por seed, o validador e o Andar de reserva são os de sempre. Na Livre a `DungeonRun` não grava exploração, Fast travel, Andar descoberto nem Andar completo. `MissionHistory` (`core/Combat`) guarda os problemas vistos na Expedição; as três lutas (`TrainingBattle`, `NameBattle`, `WordBattle`) preferem os ainda não vistos e, esgotados, evitam os últimos. `DungeonCombat.FreeSetup` procura por perto uma dificuldade que ainda tenha problema novo, e `DungeonCombat.ConceptOf` dá o Conceito do Andar (a placa do HUD), com o nome em `ConceptNames` (`game/data/conceitos.json`). `RewardService.Grant` paga, num só salvamento, as recompensas de `RewardCatalog` (`game/data/recompensas.json`) ligadas ao gatilho do acontecimento, uma vez só quando o gatilho é de uma vez só (Andar completo, Sala do Item, Conceito dominado, Desafio mestre); só `DungeonRewards` (da `DungeonRun`) e `MasterChallenge` o chamam, então o treino no Lobby nunca paga nada. O Andar completo fica em `FloorsDone`, o resto em `Occurrences`. No Godot, `DungeonRun` liga a `Expedition` ao `DungeonWorld` (a Sala em greybox, com portas e mapa em `FloorMap`), à interface e a um `TrainingArea` em modo Expedição (`BeginFight`/`EndFight`, `ICombatStage`), e delega a peças pequenas: `DungeonHud` (placa, mapa, card, Energia e recados), `DungeonNarration` (o que a Dungeon diz), `DungeonRewards` (moedas, Cosméticos e Maestria em texto) e `DungeonExploration` (o que a Expedição grava). `TrainingArea` também delega: `BubbleDirector` (a Bolha de névoa), `AnswerPanels` (o painel de cada Mecânica) e `AnswerReactions` (como o Boneco reage à resposta). Na `Main`, `RootUi` e `LegacyHud` montam a interface, `SaveOpener` abre o save e `GameExit` fecha o jogo; `MasterChallenge` conduz o Desafio mestre no Lobby. Os valores ficam em `dungeon` no `rules.json`. Para repetir um mundo no piloto, `--seed-do-mundo=N`.

**Combate no Lobby.** O Boneco de treino usa o mesmo módulo de combate das Dungeons, com um contexto de treino: Energia própria (máximo em dados, não entra no save da Expedição), saída da Área de treino ao zerar, e nenhuma escrita em progresso por Conceito, caixas de Leitner, habilidades nem Cosméticos. O contexto é injetado, e o combate não sabe se está no Lobby ou numa Dungeon. Um teste automatizado garante que um treino completo deixa o save de progresso idêntico.

### Fala (logo após o MVP)

O Godot captura o microfone (`AudioEffectCapture`), mas não reconhece fala. Isso exige um modelo local em português, como Vosk ou whisper.cpp, e o áudio nunca sai do aparelho. A comparação é fonética, não pelo texto exato, em três níveis de tolerância:

1. Contar sílabas pelos picos de energia das vogais, sem modelo de linguagem.
2. Comparar a sequência de fonemas com uma distância que aceita trocas comuns da idade (r por l, por exemplo).
3. Exigir cada sílaba correta.

O limiar de cada nível é configurável por criança, pelo responsável ou pelo terapeuta, e vai no save.

### Entrada: toque, mouse e teclado

Uma camada de entrada traduz toque, mouse e teclado em ações do jogo (mover, atacar, escolher peça, traçar), e cada mecânica declara o que faz em cada modo. O traçado de letras usa um reconhecedor de gestos por nuvem de pontos ($P), que compara o traço com modelos de cada letra, com tolerância maior no mouse que no toque. A exportação C# para Android é suportada no Godot 4, mas precisa ser testada cedo em tablet real.

## Conteúdo

Missões são arquivos JSON versionados no repositório, validados por um JSON Schema. As escritas à mão ficam no repositório e vêm no build; as geradas por IA ficam no banco e chegam pela API. O cliente nunca executa código vindo de fora: o JSON só escolhe uma mecânica que já existe no jogo e passa parâmetros.

```json
{
  "id": "mat-compor-007",
  "version": 1,
  "discipline": "matematica",
  "concept": "adicao-ate-10",
  "bncc": ["EF01MA06"],
  "difficulty": 2,
  "mechanic": "compor_numero",
  "params": { "target": 7, "pieces": [1, 2, 3, 4, 5], "maxPieces": 2 },
  "prompt": "Monte o número 7 para desfazer a nuvem!",
  "hints": ["Comece pela peça maior.", "4 e mais quanto dá 7?"],
  "explanation": "3 + 4 = 7. Juntamos 3 e 4 e chegamos a 7.",
  "accessibility": { "timerAllowed": false },
  "review": { "status": "approved", "by": "revisor" }
}
```

O schema está em `content/mission.schema.json` e as Missões escritas à mão em `content/missions/<conceito>/`, um arquivo por Missão. `difficulty` vai de 1 a 5. Campos desconhecidos são recusados, para pegar erro de digitação. Os `params` de `compor_numero` são `target`, `pieces` (as peças da bolsa) e `maxPieces` (quantas peças, no máximo, a criança junta). Os `params` de `formar_palavras` são `syllables` (as sílabas do nome, na ordem), `pool` (sílabas que podem aparecer como distratores), `distractors` (quantos distratores entram na bolsa) e `image` (nome do arquivo da imagem da criatura, sem extensão; a imagem ainda não existe e o Boneco mostra um ponto de interrogação). A unidade da peça (sílaba ou grafema) é dado da Missão. Os `params` de `escrever_palavra` são `word`, `image` e `distractors` (letras a mais no teclado da tela, opcional); os de `ordenar_frases`, `words` (na ordem certa), `pool`, `distractors` e `image`; os de `completar_frase`, `before`, `after`, `answer`, `options` e `image`. O teste `MissionContentTests` valida todo o conteúdo em `dotnet test`, e por isso a CI fica vermelha com a mensagem `arquivo: /campo: motivo` quando uma Missão é inválida.

Dois campos a mais no schema: `year` (ano escolar, 1 a 3 no MVP) e `stretch` (`true` nos desafios de antecipação, que usam conteúdo do ano seguinte e nunca são obrigatórios).

### Missões geradas fora do jogo

O backend (API em Go, banco, geração diária de missões com IA, contas e telemetria) é outro projeto, em outro repositório, e **não faz parte deste**. Aqui só existe o lado cliente: o jogo consome o mesmo JSON de missão, validado pelo mesmo schema, venha ele do build, de uma pasta local ou de um pacote baixado.

- O cliente só aceita missões que passem no JSON Schema de `content/` e cuja `mechanic` exista no jogo. Missão inválida é ignorada, nunca executada.
- Missões escritas à mão vêm no build e são a base garantida; pacotes extras ficam em cache em `user://` e entram quando disponíveis.
- A IA nunca inventa mecânica nem calcula resposta; isso é responsabilidade de quem gera o pacote e está documentado no repositório do backend.

## Camada de rede do cliente (módulo Net)

Tudo aqui é opcional: o jogo funciona 100% sem rede e sem conta.

- **Contrato**: o cliente fala com a API definida no repositório do backend. Este repositório guardará só uma cópia do contrato em `docs/tecnico/contrato-api.md` e a implementação do cliente atrás de uma interface (`IRemoteService`) com uma versão offline/fake para testes. Nada disso existe ainda: o módulo Net não foi começado.
- **Conta e perfis**: o jogo cria um perfil local por apelido, sem conta. Ligar a uma conta de responsável é opcional e posterior.
- **Sincronização**: o save leva número de revisão. Em conflito, o jogo mostra os dois progressos e o responsável escolhe qual manter.
- **Telemetria**: fila local de eventos (`mission_started`, `mission_completed`, `hint_used`, `attempt`, `difficulty_changed`, `expedition_ended`, `session_paused`), **só gravada depois do consentimento de um responsável** (LGPD, art. 14); sem consentimento, os eventos nem entram na fila. Os eventos levam o identificador do perfil, nunca e-mail ou nome real.
- **Segredos**: nenhuma chave ou senha de serviço fica no código nem no build do jogo.

## Estrutura do código e CI

```text
/game        adaptadores Godot (C#) e raiz de composição
/core        domínio e aplicação em C# puro, sem Godot
/game.tests  testes do gerador, do validador e das mecânicas
/content     missões base em JSON e o schema
/docs        documentos do jogo e cópia do contrato da API
```

### CI a cada pull request

- Validar todas as missões contra o schema.
- Testes do gerador com milhares de seeds (toda região válida e determinística).
- Build de exportação Windows do Godot, publicado como artefato.

Arte e áudio de terceiros só de fontes CC0 ou compatíveis, com `CREDITS.md` listando a origem.
