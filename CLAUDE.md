# CLAUDE.md

## O projeto

**Arquipélago do Céu** é um roguelite educativo em 3D isométrico para crianças de 6 a 10 anos, com matemática e português (alfabetização). O Lobby funcional está implementado (Perfil, Personagem, treino contra o Boneco nos 3 Níveis, voz, configurações); as duas torres (Dungeons) estão jogáveis em greybox, com Plano do Nível, Andares gerados por seed, Puzzle, Descanso, Fast travel, Item de troca e Marco, Desafio épico, Atalho de Nível e Desafio mestre. Faltam arte e tema das torres, repetição espaçada, dificuldade adaptativa e o cliente de rede. A documentação está em `docs/`.

Idioma: português do Brasil nos documentos, textos do jogo e commits. Identificadores de código podem ficar em inglês.

## Onde está cada coisa

- `docs/produto/documento-de-produto.md`: gameplay, mundo, combate, conteúdo educacional e escopo do MVP.
- `docs/tecnico/documento-tecnico.md`: módulos do cliente, geração procedural, salvamento, schema de missão, cliente de rede e dados.
- `docs/referencia/bncc/`: habilidades da BNCC (Matemática e Língua Portuguesa, 1º ao 3º ano), uma pasta por ano e um arquivo por matéria. A ordem de progressão **não está decidida**: exige consenso de profissionais de pedagogia, psicologia e fonoaudiologia (ordem recomendada provisória em `docs/pedagogia/ordem-recomendada-mvp.md`, usada no MVP).
- `docs/referencia/alfabetizacao/`: resumo do RENABE, do padrão de alfabetização do Inep (743 no Saeb, preliminar), do PNAIC e do Plano de Trabalho do MEC, com o que li e o que não li. Os PDFs ficam fora do repositório.
- `docs/referencia/legislacao/`: Lei 15.247/2025 (Compromisso Criança Alfabetizada) e decretos anteriores, colados pelo usuário e não conferidos com o DOU. A lei não impõe método; a PNA (2019) está revogada.
- `GLOSSARY.md`: vocabulário canônico do projeto (Lobby, Dungeon, Nível, Andar, Sala, Expedição, Papel...). Use esses termos e evite os listados em _Avoid_.
- `docs/arte-e-ux/direcao-de-arte-e-ux.md`: estilo visual, HUD (cores, fonte, tamanhos), acessibilidade e pipeline de arte.
- `docs/arte-e-ux/meshy/`: prompts do Meshy (Personagem modular, kits dos Papéis, criaturas, folhas de assets) e imagens de referência aprovadas.
- `docs/apresentacao/apresentacao.md`: resumo do jogo.
- `mockups/`: telas de referência da Torre dos Números, Torre das Letras e Lobby.

## Decisões principais

- **Engine**: Godot 4 (.NET) com C#. Plataforma do MVP: PC (Windows); depois Linux e tablets Android.
- **Offline primeiro**: o jogo funciona 100% sem rede. O backend (contas, sincronização, telemetria consentida, missões geradas) é um projeto **separado, em outro repositório**; este repositório só contém o jogo e o cliente da API (módulo Net, atrás de uma interface).
- **Estrutura**: duas Dungeons (torres) no MVP, Matemática e Português, 3 Níveis cada, Nível = ano escolar (1º ao 3º). O Andar é a unidade gerada: um grafo de Salas cujas portas são Puzzles (sem chaves), com um Fast travel; os Andares de um Nível vêm de um Plano do Nível sorteado por seed (ADR 0003). A escada vale nos dois sentidos e cada Andar guarda o que a criança explorou (ADR 0004); completar um Andar rende um Cosmético pequeno. Para passar um Marco é preciso um Item de troca do mesmo Nível da outra Dungeon, achado antes do Marco na própria Dungeon e guardado entre Expedições (ADR 0002). O Desafio mestre acontece no Lobby. Todo Perfil começa no Nível 1; o Atalho de Nível permite uma única tentativa por Nível de vencer direto o Desafio épico.
- **Geração procedural determinística**: seed = (mundo, dungeon, nível, andar, versão do gerador), PRNG próprio (PCG32), validador de conectividade e andar feito à mão como fallback. Tema separado da estrutura.
- **Combate**: tempo real com `Engine.TimeScale` reduzido ao montar a resposta; modo calmo para o tempo. Sem violência.
- **Energia**: máximo 5 no MVP (valor em dados, upgrades futuros podem aumentar). Erro custa 1; dica nunca custa. Zerar volta ao Lobby e perde só itens temporários. Ao concluir um Desafio épico, a criança escolhe continuar ou voltar.
- **Conteúdo**: missões em JSON versionadas com JSON Schema. O cliente nunca executa código vindo de fora; o JSON só escolhe uma mecânica existente e passa parâmetros.
- **IA**: só gera texto de missões, fora do jogo, no serviço separado. O jogo nunca chama IA, nunca inventa mecânica nem calcula resposta.
- **Voz**: TTS do sistema (`DisplayServer.TtsSpeak`) no MVP. Reconhecimento de fala local e traçado de letras entram depois.
- **Repetição espaçada**: caixas de Leitner por conceito, calculadas no próprio jogo.
- **Privacidade**: sem nome real; perfis por apelido; telemetria só com consentimento do responsável.
- **Sem ranking, anúncios ou compras**, e sem punição por ficar sem jogar.
- **Primeiro marco**: Lobby funcional (ver `docs/produto/documento-de-produto.md`). Nomes do jogo e dos Papéis são provisórios e ficam em dados.
- **Treino no Lobby é o combate real contra o Boneco, com Energia própria** (zerar só tira a criança da Área de treino), e usa todas as Mecânicas existentes; **não conta progresso**, só o que acontece nas Dungeons conta. Cosméticos só por aprendizado e exploração, ou comprados no Mercador com moedas do jogo (ADR 0007).
- **Equipamento dá atributos e isso é regra (decisão de 16/10/2026, a partir do experimento do PR #97)**: o Personagem tem os atributos base e soma os bônus do equipamento (Energia base + até 2, velocidade até 1,12×, alcance de interação até +0,5). É a exceção declarada a "Cosmético não dá vantagem": Cosmético continua só aparência, e equipamento é outra coisa. Equipar ou trocar nunca cura, e conteúdo educacional e dificuldade não mudam. O Personagem tem três espaços (Capacete, Armadura e Acessório) por Papel, cada equipamento tem um atributo e pede uma Maestria mínima do Papel (em `game/data/equipamentos.json`), e um substitui o outro, sem evoluir. Ver `docs/arte-e-ux/quatro-caminhos-e-equipamentos.md`; o #119 escreve as regras nas docs.
- **Maestria, moedas e Expedição livre**: a Maestria é um número por Papel que sobe por Conceito dominado (vitórias na Dungeon, sem XP e sem perda). Moedas só do jogo, ganhas por criatura acalmada, Sala do Item e Andar completo, sem teto e sem perda, nunca por velocidade nem sequência. Ao iniciar uma Expedição a criança escolhe Progressão (o Plano do Nível) ou Livre (Andares sem fim, sem Marco, Item nem Desafio épico, que não avança o Nível).

## Estrutura planejada do código

```text
game/         adaptadores Godot (C#) e raiz de composição
core/         domínio e aplicação em C# puro, sem Godot (nasce com o primeiro código de regra)
game.tests/   testes do gerador, do validador e das mecânicas
content/      missões base em JSON e o schema
docs/         documentação
```

## Ao trabalhar aqui

- Antes de propor uma mecânica ou sistema, confira se os documentos já decidiram. Se uma mudança contradiz uma decisão, aponte isso e atualize o documento correspondente.
- Toda mecânica funciona com toque, mouse e teclado; nenhuma informação essencial só por cor ou só por som; nada depende de velocidade ou precisão motora para avançar.

## Como escrever código

- **Hexagonal com DDD pragmático** (`docs/adr/0001-arquitetura-hexagonal-pragmatica.md`, detalhes em "Arquitetura do código" no documento técnico). Regras em C# puro, sem Godot; Godot, disco, voz e rede são adaptadores atrás de portas. As dependências só apontam para dentro.
- **Poucos arquivos** e nomes iguais aos do `GLOSSARY.md`. Interface só em fronteira real. Sem estrutura "para o futuro".
- **Resiliência:** dado externo é validado na borda; falha esperada vira resultado, não exceção; voz, rede e save nunca travam a criança.
- **Legível por quem está começando:** funções curtas, sem herança profunda, testes como exemplo.
- **Comentários só respondem "por quê"** (restrição, decisão não óbvia, armadilha). Nunca descreva o que o código já diz.

## Portões de qualidade

Rodam na CI (job `qualidade`, mais `segredos` e `arquivos`) e localmente com `tools\qualidade.cmd`. Nada entra na `main` sem eles verdes.

- **Formatação:** `dotnet format whitespace --verify-no-changes`, com o `.editorconfig` (tabs e LF).
- **Análise estática:** analisadores CA, Sonar e Roslynator no build, com aviso como erro. Regra desligada precisa de motivo na linha de cima, no `.editorconfig`; nunca desligue para passar.
- **Dependências:** `NuGetAudit` ligado; pacote vulnerável quebra o build.
- **Camadas e tamanho:** `ArquiteturaTests` barra `core` ver o Godot e arquivo de código acima de 400 linhas (os já grandes não podem crescer).
- **Comentário:** no máximo 2 linhas seguidas, só o "porquê" (`node tools/check_comments.mjs`).
- **Cobertura:** o `core` precisa de pelo menos 90% das linhas. O `game` fica fora porque depende do motor.
- **Segredos e arquivos:** gitleaks e trivy na CI.
- **Commits:** Conventional Commits em português (`feat(escopo): descrição`; tipos feat, fix, docs, style, refactor, perf, test, build, ci, chore, revert), checados por `node tools/check_commits.mjs`.

## Ambiente local (Windows)

Ferramentas instaladas sem administrador em `C:\dev\tools` e no PATH do usuário: .NET SDK 8, Godot 4.7.2 .NET e Node LTS. A variável `GODOT` aponta para o executável de console (use-a para exportar e rodar o jogo sem abrir o editor). `dotnet test game.tests` roda os testes de regra, que não abrem o Godot. Os roteiros `e2e-1`, `e2e-2` e `--pull-qa` precisam do motor (ver README e `docs/tecnico/teste-pesca-neon.md`). O Godot usa `net8.0` e `Godot.NET.Sdk/4.7.2`.

## Agent skills

### Issue tracker

Os tickets ficam no GitHub Issues de justgu1/arquipelago-do-ceu (via `gh`). Ver `docs/agents/issue-tracker.md`.

### Domain docs

Contexto único: um `GLOSSARY.md` e `docs/adr/` na raiz. Ver `docs/agents/domain.md`.
