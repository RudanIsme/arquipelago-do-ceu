# CLAUDE.md

## O projeto

**Arquipélago do Céu** é um roguelite educativo em 3D isométrico para crianças de 6 a 10 anos, com matemática e português (alfabetização). O código do jogo ainda não começou; a documentação está em `docs/`.

Idioma: português do Brasil nos documentos, textos do jogo e commits. Identificadores de código podem ficar em inglês.

## Onde está cada coisa

- `docs/produto/documento-de-produto.md`: gameplay, mundo, combate, conteúdo educacional e escopo do MVP.
- `docs/tecnico/documento-tecnico.md`: módulos do cliente, geração procedural, salvamento, schema de missão, backend e dados.
- `docs/arte-e-ux/direcao-de-arte-e-ux.md`: estilo visual, HUD (cores, fonte, tamanhos), acessibilidade e pipeline de arte.
- `docs/apresentacao/apresentacao.md`: resumo do jogo.
- `mockups/`: telas de referência da Torre dos Números, Ilha das Letras e Ilha-base.

## Decisões principais

- **Engine**: Godot 4 (.NET) com C#. Plataforma do MVP: PC (Windows); depois Linux e tablets Android.
- **Offline primeiro**: o jogo funciona 100% sem rede. O backend (Go + PostgreSQL) só serve conta opcional, sincronização, telemetria consentida e missões novas.
- **Estrutura**: duas dungeons no MVP (Matemática e Português), 3 níveis cada, nível = ano escolar (1º ao 3º). O andar é a unidade gerada e tem ponto de fast travel. Avançar numa dungeon exige item do mesmo nível da outra.
- **Geração procedural determinística**: seed = (mundo, dungeon, nível, andar, versão do gerador), PRNG próprio (PCG32), validador de conectividade e andar feito à mão como fallback. Tema separado da estrutura.
- **Combate**: tempo real com `Engine.TimeScale` reduzido ao montar a resposta; modo calmo para o tempo. Sem violência.
- **Energia**: 5 segmentos. Erro custa 1; dica nunca custa. Zerar volta à ilha-base e perde só itens temporários.
- **Conteúdo**: missões em JSON versionadas com JSON Schema. O cliente nunca executa código vindo de fora; o JSON só escolhe uma mecânica existente e passa parâmetros.
- **IA**: só gera texto de missões no servidor, com filtros e quarentena. Nunca inventa mecânica nem calcula resposta.
- **Voz**: TTS do sistema (`DisplayServer.TtsSpeak`) no MVP. Reconhecimento de fala local e traçado de letras entram depois.
- **Repetição espaçada**: caixas de Leitner por conceito, calculadas no próprio jogo.
- **Privacidade**: sem nome real; perfis por apelido; telemetria só com consentimento do responsável.
- **Sem ranking, anúncios ou compras**, e sem punição por ficar sem jogar.

## Estrutura planejada do código

```text
game/         projeto Godot (C#)
game.tests/   testes do gerador, do validador e das mecânicas
content/      missões base em JSON e o schema
docs/         documentação
```

## Ao trabalhar aqui

- Antes de propor uma mecânica ou sistema, confira se os documentos já decidiram. Se uma mudança contradiz uma decisão, aponte isso e atualize o documento correspondente.
- Toda mecânica funciona com toque, mouse e teclado; nenhuma informação essencial só por cor ou só por som; nada depende de velocidade ou precisão motora para avançar.
