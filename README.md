# Arquipélago do Céu

Roguelite educativo em 3D isométrico, feito em Godot, para crianças de 6 a 10 anos aprenderem matemática e português jogando.

## O jogo

Uma névoa do esquecimento cobriu as ilhas do céu: criaturas esqueceram seus nomes, máquinas perderam seus números. A criança parte do Lobby e sobe as torres das dungeons, geradas proceduralmente, uma por disciplina, para restaurar o que a névoa apagou.

- **Matemático**: carrega uma bolsa de números e operações. Cada criatura mostra um escudo com um número, e a criança monta a conta que chega nele.
- **Escriba**: as criaturas esqueceram o próprio nome. No 1º ano a criança junta sílabas, no 2º escreve a palavra, no 3º completa frases.
- **Combate em tempo real, sem violência**: ao atacar, o tempo desacelera para a criança pensar. O acerto dissolve a névoa e a criatura entra no Álbum de criaturas.
- **Aprendizado**: revisão espaçada, dificuldade adaptativa, desafios opcionais do ano seguinte e voz em todas as instruções.

## Estrutura

```text
docs/
  produto/       gameplay, mundo, conteúdo educacional e escopo do MVP
  tecnico/       arquitetura, Godot/C#, geração procedural, conteúdo e cliente de rede (o backend é outro repositório)
  arte-e-ux/     direção de arte, HUD e acessibilidade
  apresentacao/  resumo do jogo
mockups/         telas de referência em 3D
```

## Como trabalhamos

O desenvolvimento segue um fluxo único, com skills do Claude Code instaladas em `.claude/skills/`:

```text
(/wayfinder | /grill-with-docs)  ->  /to-tickets  ->  /implement
```

| Etapa | Quando usar | O que produz |
| --- | --- | --- |
| `/setup-matt-pocock-skills` | Uma vez por repositório | Define onde ficam os tickets (GitHub Issues ou Markdown local) e grava a configuração no `CLAUDE.md` |
| `/wayfinder` | O trabalho é grande (várias sessões) e o caminho ainda não está claro | Um mapa de tickets de decisão, resolvidos um a um até não sobrar nada a decidir. Só planeja, não escreve código |
| `/grill-with-docs` | O trabalho cabe numa sessão e o plano ainda está vago | Entrevista até o plano ficar claro e grava o vocabulário em `GLOSSARY.md` e as decisões difíceis em ADRs |
| `/to-tickets` | Já existe um plano ou spec | Tickets pequenos, cada um uma fatia completa e demonstrável, com as dependências entre eles |
| `/implement` | Há um ticket (ex.: `/implement #12`) ou uma spec | Implementa com testes primeiro (TDD), roda os testes, pede revisão e faz commit na branch atual |

Quando o fluxo apontar uma mudança que contradiz algo já decidido, o documento correspondente em `docs/` é atualizado. Guias detalhados de cada skill estão em `docs/referencia/skills/`.

## Ambiente

Para rodar o jogo: .NET SDK 8, Godot 4.7.2 (versão .NET) e Node LTS (validação do schema das missões). A variável `GODOT` deve apontar para o executável de console do Godot. O backend não faz parte deste repositório.

### Como rodar

O projeto Godot está em `game/`. No PowerShell, na raiz do repositório (a variável `GODOT` já aponta para o executável de console):

```powershell
# compilar e abrir o jogo de uma vez (argumentos extras vão para o jogo; o .cmd dispensa mudar a política de scripts do PowerShell)
.\tools\run.cmd

# ou, passo a passo:
# compilar o C#
dotnet build game/ArquipelagoDoCeu.csproj

# abrir o jogo (janela)
& $env:GODOT --path game

# rodar sem janela e sair depois de alguns quadros (teste rápido)
& $env:GODOT --headless --path game --quit-after 30
```

Para rodar todos os testes (não precisam do Godot aberto; a CI roda este mesmo comando a cada pull request):

```powershell
dotnet test game.tests
```

Para editar no Godot, abra a pasta `game/` pelo gerenciador de projetos. A cena inicial, `Main`, monta o Lobby, o Personagem do Perfil, as telas de Perfil, Personagem e configurações, o treino contra o Boneco, o Inventário (equipamentos e Cosméticos) e o HUD no tema Livro. As duas torres são Dungeons jogáveis em greybox: a criança entra pela porta da torre na beira da ilha (a Torre dos Números e a Torre das Letras), anda de Sala em Sala pelas portas (E ou o botão), luta, abre Puzzles, descansa, acha o Fast travel, ganha o Item de troca, passa o Marco, vence o Desafio épico e, com as duas torres concluídas, enfrenta o Desafio mestre no portal roxo do Lobby. Cada Andar guarda o que a criança explorou (a escada vale nos dois sentidos), o mapa mostra névoa e cadeado, e completar um Andar rende um Cosmético no Álbum. Para abrir o jogo com um comando: `.\tools\run.cmd`. Para rodar os portões de qualidade da CI (formatação, análise estática, testes com cobertura, comentários e commits): `tools\qualidade.cmd`.

Controles no Lobby: WASD ou setas (relativo à tela), Shift corre, roda do mouse, pinça ou Page Up e Page Down para o zoom (Home reinicia), F8 compara a capa, F9 abre a prática de pesca (modo de teste), clique do mouse ou toque no chão para ir até lá (o caminho contorna árvores e lagoa). Perto do Álbum ou do portal aparece um botão (ou a tecla E) para usar o objeto. Em Configurações dá para mudar tamanho do texto, voz, redução de animações, fonte para dislexia, Modo calmo e as dicas de controle; tudo é gravado no save.

### Piloto de desenvolvimento

O `DevDriver` percorre o jogo como uma criança faria (clica, digita, segura teclas) e grava capturas. Só liga com argumentos de usuário, depois do `--`; rode com janela, não com `--headless`, para as capturas saírem:

```powershell
& $env:GODOT --path game -- --driver=movimento --saida=C:\caminho\da\pasta
```

Cenários: `criar`, `escolher`, `personagem`, `movimento`, `mouse`, `lobby`, `album`, `portal`, `tecla`, `config`, `animacao` (imprime no console se a animação de andar está ligada), `camera`, `efeitos`, `boneco`, `boneco-dislexia`, `erro`, `energia`, `bolha`, `calmo`, `treino-nivel`, `nome`, `nome-erro`, `nivel2-matematica`, `nivel2-escrita`, `nivel3-frase`, `andar` (desenha o Plano de um Nível), `torre`, `torre-salas`, `torre-letras`, `torre-itens`, `torre-epico`, `torre-atalho`, `torre-atalho-erro`, `torre-fast-travel`, `torre-derrota` e `mestre` (as torres e o Desafio mestre). Em build de depuração, `--seed-do-mundo=N` fixa a seed dos Andares e `--sem-voz` desliga a voz. `--pull-qa` roda o QA da pesca (ver `docs/tecnico/teste-pesca-neon.md`). `--reduzir-animacoes` liga a redução de animações e `--nivel=N` libera os Níveis até N no treino. O piloto trabalha numa cópia do save do usuário, então não mexe nos seus Perfis.

### Teste de ponta a ponta do Nível 1

O roteiro `e2e-3` joga o Nível 1 inteiro, sem janela: o Atalho de Nível numa torre, a outra torre jogada (Descanso, Puzzle, Fast travel, Sala do Item, Marco, Desafio épico) e o Desafio mestre, que libera o Nível 2. O `e2e-4`, num segundo processo, reabre o save e confere o progresso. A CI roda os dois depois do e2e do treino.

```powershell
$save = "$env:TEMPe2e-nivel.json"; Remove-Item $save, "$save.snapshot" -ErrorAction SilentlyContinue
& $env:GODOT --headless --path game -- --driver=e2e-3 --save=$save --seed-do-mundo=1
& $env:GODOT --headless --path game -- --driver=e2e-4 --save=$save --seed-do-mundo=1
```

### Teste de ponta a ponta do treino

O roteiro `e2e-1` cria o Perfil, anda, treina nos dois Papéis e confere que o save não mudou; o `e2e-2`, num segundo processo, reabre o jogo e confere que o Perfil e o progresso são os mesmos. Uma verificação que falha faz o jogo sair com código 1. A CI roda os dois no executável de depuração exportado (job `e2e-windows`). Para rodar aqui, sem janela:

```powershell
$save = "$env:TEMP\e2e-save.json"; Remove-Item $save, "$save.snapshot" -ErrorAction SilentlyContinue
& $env:GODOT --headless --path game -- --driver=e2e-1 --save=$save
& $env:GODOT --headless --path game -- --driver=e2e-2 --save=$save
```

## Telas de referência

| Torre dos Números | Torre das Letras | Lobby |
| --- | --- | --- |
| ![Torre dos Números](mockups/torre-dos-numeros.png) | ![Torre das Letras](mockups/torre-das-letras.png) | ![Lobby](mockups/lobby.png) |

## Entrega de pesca e personagens

Veja [como testar o estado aprovado, F9, pesca BOA e desenho neon](docs/tecnico/teste-pesca-neon.md), incluindo preparação de checkout limpo e uso de save temporário.
