# Arquipélago do Céu: documento técnico

## Visão geral

O jogo é um aplicativo Godot que funciona inteiro offline; o servidor em Go só entra para quem cria conta ou consente com telemetria. O conteúdo educativo nasce fora do jogo, é gerado todo dia no servidor, passa por filtros e por revisão pedagógica e chega ao jogo pela API.

Se o servidor cair, a criança continua jogando; só a sincronização e a telemetria esperam.

## Cliente do jogo

O jogo é feito em Godot 4 (versão .NET) com C#, roda 100% offline e só usa a rede para sincronizar, enviar telemetria e baixar missões novas. O Godot suporta C# oficialmente. O backend fica em Go, separado.

O Godot com C# não exporta para navegador. Como navegador não é alvo, isso não pesa.

### Módulos

| Módulo | Responsabilidade |
| --- | --- |
| Core | Estado do jogo, cenas, salvamento local |
| World | Gerador de andares inteiros por dungeon, validador, fast travel por andar, troca de itens entre dungeons |
| Rooms | Cenas de ilha feitas à mão, com pontos de encaixe para pontes e desafios |
| Missions | Carrega missões em JSON e executa as mecânicas (compor, comparar, sequência, sílabas, frases) |
| Characters | Matemático e Escriba, habilidades destravadas por conceito |
| Adaptive | Regra de dificuldade por conceito e caixas de repetição espaçada (Leitner), calculadas no próprio jogo, sem precisar do backend |
| Accessibility | TTS, legendas, tamanho de texto, remoção de tempo, redução de animações |
| Net | Login opcional, sincronização e fila de telemetria que espera conexão |

### Geração procedural

- Cada andar é gerado inteiro, de uma vez, a partir de `(seed do mundo, dungeon, nível, número do andar, versão do gerador)`. A mesma entrada sempre gera o mesmo mapa.
- Usar um gerador de números aleatórios próprio (PCG32, cerca de 30 linhas) em vez de `System.Random`, para que a sequência não mude entre versões do .NET ou do Godot.
- O validador confere, antes de mostrar o andar, que toda ilha obrigatória é alcançável e toda porta tem chave. Se falhar, carrega um andar feito à mão.
- O gerador roda em testes automatizados com milhares de seeds.

**Tema separado da estrutura.** O gerador só conhece funções de sala: combate, puzzle, descanso, fast travel, marco, desafio épico. Um pacote de tema em dados (cenas de sala, objetos, criaturas, música, paleta, vocabulário da narrativa) veste essas salas. Uma disciplina nova, como História, é um pacote de tema mais mecânicas novas, sem mexer no gerador. Um pacote pode ter sub-temas, como uma época histórica por andar.

**Nível da dungeon = ano escolar.** A seed do andar inclui o nível, e o distribuidor de desafios só escolhe missões daquele ano, mais as de antecipação (`stretch`) no fim do nível.

### Salvamento

Arquivo JSON local em `user://`, com número de versão do formato. Guarda a seed, a versão do gerador, andares descobertos, puzzles resolvidos, pontos de fast travel, itens de troca, habilidades, progresso por conceito, caixa de memória e data da próxima revisão, e energia da expedição em andamento. Grava em arquivo temporário e renomeia, para não corromper se o PC desligar. Salva sozinho a cada sala e ao pausar, e também no ponto de fast travel de cada andar.

### Voz e câmera

- TTS pela API embutida do Godot (`DisplayServer.TtsSpeak`), escolhendo uma voz pt-BR do sistema.
- Câmera 3D ortográfica em ângulo isométrico; estilo alvo 3D isométrico detalhado, com assets CC0 estilizados como provisórios no MVP. Para manter o desempenho com arte detalhada: iluminação pré-calculada (lightmaps), LOD e um orçamento de polígonos por sala definido na prova técnica.

### Combate

O combate roda em tempo real com escala de tempo: ao abrir a montagem da resposta, `Engine.TimeScale` cai (ex.: 0,15), ou vai a zero no modo calmo, e volta ao normal quando o ataque sai. A IA das criaturas é simples: patrulhar, avisar, lançar bolha lenta e reagir a erro. A bolsa do Matemático e a de sílabas do Escriba são a mesma estrutura de dados (um baralho de peças com sorteio pela seed), só com peças diferentes. Quem valida a jogada é código: o avaliador de expressões no Matemático e a comparação com o nome esperado no Escriba.

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

Dois campos a mais no schema: `year` (ano escolar, 1 a 3 no MVP) e `stretch` (`true` nos desafios de antecipação, que usam conteúdo do ano seguinte e nunca são obrigatórios).

### Geração diária com IA

A geração tem duas partes que não se misturam. A geração procedural, em código, monta o andar: salas, conexões e onde fica cada desafio. A IA só escreve o conteúdo que preenche cada desafio (enunciado, dicas, explicação, contexto), sempre a partir de um template cuja mecânica e parâmetros são definidos por código. A IA nunca inventa mecânica nem calcula resposta.

Todo dia um job no servidor gera um lote pequeno de missões e as coloca no banco. O jogo baixa as publicadas quando está online e guarda em cache; sem internet, usa o que já tem mais as missões escritas à mão que vêm no build. O jogo também carrega pacotes de missões em JSON de uma pasta local.

1. O job escolhe conceitos com pouco conteúdo, respeitando uma cota por conceito.
2. O código sorteia os parâmetros (ex.: alvo 7, peças de 1 a 5) e calcula a resposta.
3. A IA escreve enunciado, dicas e explicação para esses parâmetros, em JSON no schema da missão.
4. Filtros automáticos (tabela abaixo). Reprovou em qualquer um, é descartada.
5. As aprovadas ficam em quarentena, visíveis só para contas de teste, até um educador revisar uma amostra do lote.
6. Publicadas ficam sob observação: se as crianças erram ou abandonam muito mais que o normal, a missão volta sozinha para a quarentena.

| Problema | Filtro |
| --- | --- |
| Resposta errada | Resposta calculada por código; em Português, palavras vêm de uma lista curada. O texto precisa citar exatamente os parâmetros sorteados |
| Ambiguidade | Um segundo modelo resolve a missão sem ver a resposta; se errar ou achar outra resposta válida, descarta |
| Repetição | Hash do texto normalizado e similaridade com as missões do mesmo conceito; muito parecida, descarta |
| Linguagem inadequada | Vocabulário permitido por ano escolar, tamanho máximo de frase e filtro de conteúdo |
| Falso positivo que passou | Amostra com revisão pedagógica, botão "achei estranho" no jogo e retirada automática por estatística |
| Volume grande demais para revisar | Lote diário pequeno e cota por conceito |

## Backend e infraestrutura

Um único serviço em Go com PostgreSQL, rodando por Docker Compose. O jogo funciona sem ele; o backend só serve conta opcional, sincronização, telemetria consentida e atualização de conteúdo.

### Stack

| Peça | Escolha | Por quê |
| --- | --- | --- |
| API | Go, `net/http` da biblioteca padrão | Um binário, sem framework para manter |
| Banco | PostgreSQL 16 | Como no documento original |
| Acesso ao banco | `pgx` + `sqlc` | SQL escrito à mão, código Go gerado e tipado |
| Migrações | `goose` | Arquivos SQL versionados no repositório |
| HTTPS | Caddy | Certificado automático, configuração de poucas linhas |
| Deploy | Docker Compose (Caddy + API + Postgres) | Sobe tudo no VPS com um comando |
| Backup | `pg_dump` diário, cópia fora do VPS | Perder o VPS não pode perder o progresso das crianças |

Um VPS com 1 vCPU e 2 GB de RAM sobra para o MVP.

### Contas

A conta é do responsável (e-mail e senha com hash argon2id), e cada criança é um perfil dentro dela, com apelido. A criança nunca digita e-mail, e nenhum CPF é pedido. O jogo cria um perfil local sem conta e permite ligar a uma conta depois.

### Endpoints do MVP

| Método e rota | Função |
| --- | --- |
| `POST /v1/accounts` | Criar conta de responsável |
| `POST /v1/sessions` | Login, devolve token |
| `GET /v1/profiles` | Perfis de criança da conta |
| `GET /v1/profiles/{id}/save` | Baixar o save mais recente |
| `PUT /v1/profiles/{id}/save` | Enviar save com número de revisão |
| `POST /v1/telemetry` | Lote de eventos, só se houver consentimento |
| `GET /v1/content/manifest` | Missões publicadas desde a última sincronização |
| `DELETE /v1/profiles/{id}` | Apagar perfil e todos os dados |

**Job diário de missões:** roda dentro do mesmo binário Go com um agendador simples, sem fila nem serviço extra. Grava na tabela `missions` com status (quarentena, publicada, retirada). A chave da IA fica só em variável de ambiente no VPS.

**Conflito de sincronização:** o save leva um número de revisão. Se o servidor tem uma revisão mais nova que a do jogo, o jogo mostra os dois progressos e o responsável escolhe qual manter.

## Dados, privacidade e telemetria

A telemetria é detalhada por criança, mas só é enviada depois que um responsável consente na tela de conta, como pede o art. 14 da LGPD. Sem consentimento, os eventos nem são gravados na fila.

### Tabelas principais

| Tabela | Conteúdo | Dado pessoal |
| --- | --- | --- |
| `accounts` | E-mail e hash de senha do responsável | Sim |
| `consents` | Tipo, versão do texto, data, conta | Sim |
| `profiles` | Apelido da criança e identificador aleatório | Mínimo |
| `saves` | Save em JSONB e número de revisão | Não |
| `telemetry_events` | Perfil, tipo, missão, conceito, acerto, dicas usadas, duração, data | Ligado ao perfil |

### Eventos de telemetria

`mission_started`, `mission_completed`, `hint_used`, `attempt`, `difficulty_changed`, `expedition_ended`, `session_paused`. São eles que mostram onde a criança trava e se a dificuldade adaptativa está funcionando.

### Regras

- Telemetria fica separada da conta: os eventos guardam só o identificador do perfil, nunca o e-mail.
- Retenção de 12 meses para eventos (proposta); apagar o perfil apaga tudo na hora.
- Nenhum dado da criança sai do servidor, nem para serviços de IA.
- Senhas, chaves de API e dados de produção ficam só em variáveis de ambiente no servidor, nunca no código.

## Estrutura do código e CI

```text
/game        projeto Godot (C#)
/game.tests  testes do gerador, do validador e das mecânicas
/content     missões base em JSON e o schema
/docs        documentos do jogo e contrato da API
```

O backend (API, migrações, geração diária com IA, deploy) fica separado do jogo; os dois só conversam por HTTP, e o contrato da API fica em `docs`.

### CI a cada pull request

- Validar todas as missões contra o schema.
- Testes do gerador com milhares de seeds (toda região válida e determinística).
- Build de exportação Windows do Godot, publicado como artefato.

Arte e áudio de terceiros só de fontes CC0 ou compatíveis, com `CREDITS.md` listando a origem.
