# Arquipélago do Céu: documento de produto

## Visão e objetivo

Um roguelite educativo em português em que a criança explora dungeons no céu e vence desafios usando o que aprende de matemática e português.

| Decisão | Escolha |
| --- | --- |
| Plataforma inicial | PC |
| Engine | Godot 4 (C#) |

## Público e BNCC

O público é de 6 a 10 anos. O MVP cobre do 1º ao 3º ano, e cada Nível de Dungeon corresponde a um ano escolar. **Todo Perfil começa no Nível 1**, sem escolha de ano na criação (não sabemos o quanto a criança já domina). Quem quiser pode tentar o Atalho de Nível, descrito em Estrutura do mundo.

**Clínicas de fonoaudiologia e psicopedagogia também são público.** Na clínica o jogo é ferramenta de apoio à terapia, nunca de diagnóstico: o terapeuta ajusta as tolerâncias de voz e de traçado para cada criança e acompanha a evolução.

**O que é a BNCC:** a Base Nacional Comum Curricular é o documento oficial do MEC que define o que todo aluno brasileiro deve aprender em cada ano. Cada habilidade tem um código, por exemplo EF01MA06 (1º ano, matemática: fatos básicos de adição). Marcar cada missão com o código BNCC correspondente ajuda o professor a saber onde encaixá-lo na aula.

**A BNCC é o piso, não o teto.** Cada missão tem um campo opcional de código BNCC. Além disso, cada nível termina com **desafios de antecipação** opcionais, com conteúdo do ano seguinte (o nível do 3º ano antecipa o 4º). Eles nunca são obrigatórios, não bloqueiam progresso e não contam como erro: são para a criança que quer ir além e se preparar para o próximo ano. Problemas de olimpíadas de matemática para crianças servem de inspiração.

## Gameplay

A criança sobe dungeons no céu, uma por disciplina, e o combate acontece pelas próprias mecânicas de aprendizado. O Personagem é um só, personalizável, e assume o Papel da Dungeon. O tema completo ainda está aberto; a dungeon no céu é o ponto de partida.

### Tema: um mundo que aceita qualquer disciplina

Matemática e Português não têm ambientação própria, mas História, Ciências e Geografia têm. Por isso o tema precisa ser um guarda-chuva em que cada disciplina ganha a sua ambientação, sem mudar a estrutura do jogo.

**Proposta: o Arquipélago do Céu.** Uma névoa do esquecimento cobriu as ilhas do céu e apagou o que elas sabiam: criaturas esqueceram seus nomes, máquinas perderam seus números. O Lobby é uma ilha neutra, cercada pelas torres das Dungeons, uma por disciplina, que a criança restaura. Uma disciplina nova é só uma nova torre que surge da névoa. O combate não é violento: a criança dissolve a névoa e liberta as criaturas. Nome do jogo e nomes dos Papéis são provisórios e configuráveis em dados.

| Disciplina | Ambientação | Criaturas |
| --- | --- | --- |
| Matemática | Torre-oficina: engrenagens, cristais, balanças, relógios | Autômatos com escudos numéricos |
| Português | Torre-biblioteca: livros voadores, letras soltas, pergaminhos | Criaturas feitas de letras apagadas, sem nome |
| Ciências (depois) | Torre-jardim e laboratório: ecossistemas, clima | Animais e plantas a investigar e classificar |
| História (depois) | Torre-memória: cada andar é uma época | Pessoas de cada época que perderam a memória |
| Geografia (depois) | Torre-mapa: relevo, rios, cidades | Viajantes perdidos que precisam de direção |

**Regra para a construção:** a estrutura (andares, salas por função, marcos, desafios épicos) é igual para todas as disciplinas. O tema é um pacote de dados que veste essa estrutura, e um pacote pode ter sub-temas, como uma época histórica por andar. Assim História entra com um pacote novo e mecânicas novas, sem refazer o gerador.

**Estilo visual: 3D isométrico detalhado.** O MVP usa assets livres estilizados como provisórios, e a arte detalhada entra em fases, começando pelo Lobby e pelas criaturas.

### Personagem e Papéis

O Personagem é um só, do Perfil, e é personalizável: cores de roupa e de características, outras características e acessórios. O corpo é um só, sem gênero marcado; a aparência vem de partes combináveis (tom de pele, olhos, sobrancelhas, boca, cabelo e acessórios), listadas no guia de estilo visual. O que o faz Matemático ou Escriba é o **Papel** da Dungeon: roupas, ferramenta e habilidades. As habilidades do Papel crescem conforme a criança aprende conceitos novos nessa Dungeon.

| Papel (nome provisório) | Área | Exemplo de progressão |
| --- | --- | --- |
| Matemático | Matemática | Com as 4 operações compõe números para desarmar inimigos; ao aprender frações, também divide inimigos ou pontes em partes |
| Escriba | Português | Com sílabas forma palavras que abrem portas; ao aprender frases, ordena mensagens que acalmam criaturas |

**Opção em aberto: trocar de Papel dentro da Dungeon.** No MVP cada torre tem um só Papel. Se mais tarde houver torres híbridas (Andares com Matemática e Português juntos), o HUD de combate pode ganhar um botão **Trocar** no painel do Papel, para a criança alternar entre Matemático e Escriba (no teclado, a tecla Q). Trocar não cura nem muda a Energia, e cada Andar só aceita o Papel que combina com a Sala. Enquanto a torre tiver um só Papel, o botão fica escondido. Não está decidido; depende de existirem torres híbridas.

**Cosméticos:** roupas, cores, acessórios e variações visuais da ferramenta (como o tipo de arma). Não mudam o Papel nem dão vantagem. *Exceção em aberto: o experimento de equipamentos (PR #97) dá bônus pequenos de velocidade, Energia e alcance; precisa ser decidido se vira regra ou volta a ser só visual (ver `docs/arte-e-ux/quatro-caminhos-e-equipamentos.md`).* Só se ganham por aprendizado e exploração nas Dungeons (libertar criatura, dominar conceito, concluir Nível), nunca por compra, sorteio ou sequência diária.

Regra: uma habilidade bloqueada nunca impede a criança de praticar o conceito. Na Área de treino do Lobby ela pratica o mesmo combate das Dungeons contra o Boneco de treino, com uma Energia própria; se a Energia zera, a criança só sai da Área de treino, sem perder nada. **O treino no Lobby não conta para o progresso**: habilidades, caixas de memória e dificuldade adaptativa só mudam com o que acontece nas Dungeons.

### Combate pelas mecânicas de aprendizado

O combate acontece em tempo real, no próprio cenário, sem trocar de tela. As criaturas se movem devagar e avisam antes de atacar, e a criança se move e desvia. Para atacar, ela escolhe a criatura (clique, toque ou tecla F) a uma distância máxima definida em dados, e a UI de combate aparece sem interromper o jogo: o Personagem continua andando. O tempo desacelera só enquanto ela monta a resposta com as peças da bolsa, e volta ao normal quando o ataque sai. Se ela se afasta demais, o alvo se perde. O jogo tem ritmo de ação, mas a parte de pensar nunca é corrida.

- **Energia:** acertar recupera; errar, ou ser atingida por uma bolha de névoa, tira. Quando a energia acaba, a expedição termina (veja Energia e derrota).
- **A ação não fica mais difícil com o ano escolar.** Só o conteúdo fica. Ataques de criatura são sempre lentos, avisados e fáceis de desviar.
- **Modo calmo:** opção que para o tempo por completo durante a montagem e faz as criaturas só reagirem a erros. Serve para crianças menores ou com dificuldade motora, e o jogo sugere ligar se a criança perde muita energia por desvio. *Proposta, a fechar quando o combate existir: no Modo calmo as bolhas de névoa não tiram energia, para que qualquer criança conclua o jogo sem depender de reflexo.*

**O ataque da criança é o próprio conhecimento.** A conta montada pelo Matemático vira um raio de energia que dissolve a névoa da criatura; o nome escrito pelo Escriba voa em letras luminosas até ela. O acerto exato é um golpe perfeito, com animação especial.

**O ataque da criatura nunca é violento.** As criaturas lançam bolhas de névoa lentas, com aviso visual e sonoro antes. Se a criança erra uma resposta, a criatura reage na hora com uma travessura: embaralha as peças da bolsa ou esconde uma peça por alguns segundos. Nada de golpes, sangue ou personagem caído: quando a energia acaba, o personagem volta flutuando ao Lobby.

Depois de um erro o desafio recomeça com outros números ou outra palavra, para a criança não acertar por eliminação. Dois erros seguidos liberam uma dica.

#### Matemático: a bolsa de números

A bolsa funciona como o baralho de um roguelite de cartas: é o que a criança monta e melhora ao longo da expedição.

1. A bolsa começa com números de 1 a 5 e o sinal +. A criança tem 5 peças na mão; as usadas num ataque são repostas pela bolsa.
2. A criatura mostra um escudo com um número, por exemplo 12.
3. A criança monta uma conta na calculadora do cajado, tecla a tecla: números da mão e o sinal (5 + 4 + 3). Uma conta precisa de um sinal e de pelo menos dois números, e o jogo não mostra o resultado: quem soma é a criança. Cada número que ela escolhe aparece pontilhado e ela o desenha para colocá-lo na conta. O resultado tira do escudo.
4. Acertar o valor exato é um golpe perfeito: derrota a criatura de uma vez e dá um bônus. Passar do valor não vale: o jogo mostra a conta e conta como erro (tira 1 de Energia, o Boneco faz uma travessura e o Desafio recomeça com outros números), o que treina estimativa. As peças da mão voltam inteiras. É o único jeito de errar na calculadora.
5. Entre andares, a criança escolhe melhorias: adicionar, trocar ou remover peças da bolsa.

| Ano | Peças e operações |
| --- | --- |
| 1º | Números até 10, adição |
| 2º | Dezenas como peça (10, 20), subtração |
| 3º | Multiplicação e divisão simples, peça especial "dobro" |
| Depois | Frações: dividir o escudo em partes |

Criaturas têm traços que mudam o raciocínio: "só aceita números pares", "o escudo cresce com o tempo", "só cai com subtração".

#### Escriba: o nome das criaturas

As criaturas esqueceram o próprio nome, e dizer o nome certo as liberta. O jogo mostra a criatura e, se preciso, fala o nome pela voz sintética.

| Ano | Como a criança luta |
| --- | --- |
| 1º | Bolsa de sílabas: monta o nome (GA-TO); cada sílaba certa é um golpe e o nome completo liberta |
| 2º | Escreve a palavra inteira no teclado físico ou na tela, com letras sugeridas se travar |
| 3º | Completa ou ordena uma frase-feitiço sobre a criatura; ortografia (ss, rr, ç) |
| Depois do MVP | Fala a sílaba ou o nome no microfone, com reconhecimento de voz no próprio PC |

Para acessibilidade: fonte para dislexia, peças grandes e nenhum tempo obrigatório.

No Escriba, cada sílaba certa é um golpe e o nome completo liberta a criatura (recupera 1 de Energia). Sílaba errada é erro: tira 1 de Energia, o Boneco faz uma travessura e o Desafio recomeça com outro nome. Sílaba que ainda falta não é erro.

#### Voz

A criança pode falar a sílaba ou o nome da criatura no microfone. O reconhecimento roda no próprio aparelho, e a voz nunca sai dele. Criança pequena fala errado ou do seu jeito, então a tolerância muda com o nível:

| Nível | O que o jogo confere |
| --- | --- |
| Iniciante (1º ano) | Só a quantidade de sílabas, o ritmo da palavra |
| Intermediário (2º ano) | Os sons principais da palavra, aceitando trocas comuns da idade |
| Avançado (3º ano) | Cada sílaba pronunciada corretamente |

Errar na fala nunca tira energia. O jogo repete a palavra, mostra as sílabas e deixa tentar de novo ou responder tocando. O nível acompanha a criança, não só o ano escolar: o responsável ou o terapeuta pode ajustar a tolerância de cada uma.

#### Traçar letras

O Escriba também escreve a letra ou a sílaba desenhando com o dedo, o que treina coordenação motora fina. No tablet é com o dedo; no PC funciona com mouse ou caneta, com tolerância maior ao traço; quem usa teclado digita. Cada mecânica tem uma versão para toque, mouse e teclado, e a criança nunca fica presa por não ter um tipo de entrada.

**Quando entram:** a voz do reconhecedor entra logo depois do MVP. O desenho pontilhado já vale no combate: número na bolsa do Matemático e sílaba na do Escriba, conferido em código contra o próprio pontilhado (sem reconhecer a letra). Quem não consegue desenhar nunca fica preso: há "Pular o desenho", e no teclado digitar o número ou a sílaba vale como escrever.

#### Energia e derrota

Um roguelite precisa de risco, mas o risco não pode virar punição por não saber. Regras propostas:

- A energia começa cheia em cada expedição. O máximo é 5 no MVP e é um valor em dados, que upgrades futuros podem aumentar. Acerto recupera 1, golpe perfeito recupera 2, erro tira 1, e ser atingida por uma bolha de névoa também tira 1.
- Pedir dica nunca custa energia, e errar desafios de antecipação também não.
- Antes de confirmar, a criança vê a jogada montada e pode desfazer, para que um clique errado não conte como erro de conceito.
- Energia zerada encerra a expedição: a criança perde itens e melhorias temporárias, e mantém andares salvos, habilidades e tudo o que aprendeu.
- Duas derrotas seguidas no mesmo conceito descem a dificuldade adaptativa e oferecem treino daquele conceito no Lobby, sem bloquear nada.
- A mensagem de derrota fala da aventura, nunca da criança: "A névoa ficou forte demais desta vez. Vamos recarregar a energia?"

### Estrutura do mundo

Cada disciplina tem sua própria Dungeon, uma torre; no MVP são duas, Matemática e Português. Cada Dungeon tem Níveis, cada Nível é uma sequência de Andares gerados, feitos de Salas, e termina num Desafio épico. Para passar um Marco a criança precisa de um Item de troca da outra Dungeon, e para subir de Nível nas duas ela enfrenta um Desafio mestre que mistura as disciplinas.

**Atalho de Nível.** Como não sabemos o quanto a criança já domina, na entrada da Dungeon ela pode tentar passar um Nível inteiro vencendo direto o Desafio épico, com Conceitos sorteados de todo o Nível. Cada Perfil tem **uma única tentativa por Nível em cada Dungeon**: acertando, o Nível conta como concluído; errando, joga o Nível normalmente e não tenta de novo. Erro no Atalho não tira energia. Acertar o Atalho também entrega o Item de troca daquele Nível, para o Marco da outra torre nunca ficar sem saída (ADR 0002).

- **Andar é a unidade gerada.** A geração procedural monta um andar inteiro de uma vez (todas as salas e conexões), não sala por sala. O Andar é um grafo livre de Salas; as portas trancadas são Puzzles que abrem quando a criança resolve o Desafio, sem chaves (ADR 0003).
- **Plano do Nível.** Quantos Andares tem um Nível (3 a 4 no MVP) e em qual ficam a Sala do Item, o Marco e o Desafio épico é sorteado por seed. Um Andar tem de 6 a 9 Salas, e os limites ficam em dados.
- **Descanso:** Sala opcional que devolve um pouco de Energia, uma vez só (2 no MVP, em dados).
- **Cada andar tem um Fast travel**, numa Sala que a criança precisa encontrar, e que também salva o progresso. Sem tê-lo achado, ela retoma da entrada do Andar. O jogo também salva sozinho a cada sala e ao pausar.
- **O item exigido vem sempre do mesmo nível da outra dungeon**, nunca de um nível acima. Ele fica numa Sala do Item, de um Andar anterior ao Marco da própria dungeon, e a criança o guarda entre Expedições (não se perde quando a Energia zera). Assim a criança nunca fica travada esperando algo impossível (ADR 0002).
- **Nível liberado e Nível concluído.** O Perfil tem um só Nível liberado, nas duas torres, que sobe quando a criança vence o Desafio mestre. Cada Dungeon guarda, por Nível, se o Desafio épico foi concluído. O Desafio mestre acontece no Lobby, num portal próprio, e abre quando os dois épicos do Nível estão concluídos.
- **Desafio épico e mestre só usam conceitos já praticados** naquele nível; são uma revisão divertida, não uma prova surpresa. A exceção é o Atalho de Nível, que sorteia Conceitos de todo o Nível e por isso é opcional e sem punição.
- **Escopo do MVP:** 3 níveis em cada dungeon, um por ano escolar (1º, 2º e 3º), com desafio mestre entre eles e desafios opcionais de antecipação no fim de cada nível.

### Expedição e progressão

- **Duração:** 20 minutos ou mais, com pausa a qualquer momento, já que uma criança de 6 anos raramente fica 20 minutos seguidos.
- **Fim da Expedição:** termina quando a criança decide voltar ou quando a energia acaba. Ao concluir um Desafio épico, ela escolhe entre continuar e voltar ao Lobby. Com energia zerada, volta ao Lobby, perde só recursos temporários (o Item de troca fica) e pode recomeçar do último Fast travel encontrado, ou da entrada do Andar.
- **Narrativa:** mínima no MVP, só o contexto das missões.
- **Multiplayer e social:** nenhum. Sem ranking entre crianças; a motivação vem de recordes pessoais e metas.

## Conteúdo educacional

O MVP cobre matemática e português do 1º ao 3º ano: cerca de 60 missões base revisadas por um educador (10 por ano e disciplina), mais o lote diário gerado por IA. Ciências fica para depois.

- **Como chegar a \~60 missões:** 6 a 8 mecânicas reutilizáveis (compor números, comparar quantidades, completar sequências, formar palavras, ordenar frases), cada uma com parâmetros diferentes. Uma missão nova sobre uma mecânica existente é só um arquivo de dados.
- **Leitura:** texto com voz sintética (TTS). Toda instrução pode ser ouvida, porque uma criança de 6 anos ainda está aprendendo a ler. O Godot 4 tem TTS embutido que usa as vozes do sistema.
- **Dificuldade adaptativa:** no MVP é uma regra simples e transparente, por exemplo: três acertos sem dica sobem um nível, duas tentativas com dica descem um nível. Nada de modelo estatístico ainda. Responsáveis e educadores conseguem entender e ajustar a regra.
- **Revisão:** nenhuma missão vai para a criança sem revisão pedagógica, inclusive as variantes geradas por IA.
- **Progressão validada por profissionais:** a ordem das habilidades da BNCC, a granularidade dos Conceitos e a progressão de dificuldade só são definitivas depois do **consenso de profissionais de pedagogia, psicologia e fonoaudiologia**. Até lá, vale a [ordem recomendada provisória](../pedagogia/ordem-recomendada-mvp.md), e a progressão fica em dados, para mudar sem alterar o código. A referência das habilidades está em [docs/referencia/bncc/](../referencia/bncc/README.md).

### Exemplos de missão para o MVP

| Mecânica | Disciplina | Exemplo no céu |
| --- | --- | --- |
| Compor números | Matemática | Montar 7 com peças para desfazer uma nuvem de tempestade |
| Comparar quantidades | Matemática | Escolher a ilha com mais cristais para pousar |
| Sequências | Matemática | Completar 2, 4, 6, ? para religar uma ponte |
| Formar palavras | Português | Juntar sílabas para escrever a senha de um portão |
| Ordenar frases | Português | Pôr um bilhete rasgado na ordem certa para achar o caminho |

### Repetição e memória

O jogo traz de volta o que a criança já aprendeu no momento em que ela está começando a esquecer. É a repetição espaçada dos flash cards, só que dentro da aventura.

- **Caixas de memória por conceito (sistema Leitner):** cada conceito fica numa caixa de 1 a 5. Acertou numa revisão, sobe de caixa e volta mais tarde (ex.: 1, 2, 4, 8 e 16 dias). Errou, volta para a caixa 1. A conta é por conceito, não por pergunta decorada.
- **Revisão é sempre desafio, nunca releitura:** o conceito volta como criatura, porta ou ponte, com outros números e outra mecânica, para a criança puxar da memória em vez de reconhecer a resposta.
- **Portal da Memória:** no início de cada Expedição, uma sala opcional com até 3 conceitos para revisar. Resolver dá uma **Esquiva**: perdoa o primeiro erro da Expedição sem tirar energia, e não ultrapassa o teto.
- **Mistura de conceitos:** cada andar mistura conceitos novos e antigos, e o desafio mestre é uma revisão misturada das duas dungeons.
- **Álbum:** cada criatura libertada vira uma carta no Álbum do Lobby, com a criatura e o desafio na frente e a explicação no verso, ao lado dos Cosméticos. No Álbum a criança só revê e relê, sem efeito: as revisões que movem as caixas de memória acontecem só nas Dungeons.
- **Sem punição por ausência:** não existe sequência diária nem perda por não jogar. Quando a criança volta, as revisões estão esperando.

## Escopo: MVP e depois

Tudo o que foi escolhido para o MVP entra, mas na forma mais simples que ainda prova a ideia. O que sobra vai para a fase 2. O backend (contas, sincronização, telemetria e geração diária de missões com IA) é um projeto separado, fora deste repositório; aqui entra só o lado do jogo, que nunca chama IA durante a partida.

| Recurso | No MVP | Depois |
| --- | --- | --- |
| Lobby | Ilha cercada por cinco torres (Matemática e Português abertas, em greybox; Ciências, História e Geografia "em breve"), Perfil e Personagem editáveis, NPC Guia, Área de treino, Álbum de criaturas e Cosméticos, portal de fast travel (sem pontos ainda), configurações de jogo, acessibilidade e controles | Decoração, mais torres |
| Dungeon | Matemática e Português, 3 níveis cada (1º ao 3º ano), andares gerados inteiros, desafios mestres | Novos biomas e tipos de sala |
| Fast travel | Um ponto por andar, que também salva | Mapa do mundo completo |
| Personagens | Matemático e Escriba, 2 a 3 habilidades cada | Mais personagens, Ciências |
| Conteúdo | \~60 missões base sobre 6 a 8 mecânicas, 1º ao 3º ano, com desafios de antecipação e repetição espaçada | 4º e 5º ano, Ciências, História, minijogo 2D |
| Dificuldade adaptativa | Regra simples por acertos e dicas | Modelo por conceito, ajustável pelo educador |
| Voz | TTS do sistema | Narração gravada; reconhecimento de fala com tolerância por nível |
| Conta | Opcional, só para sincronizar entre aparelhos | Conta gerenciada por responsável ou escola |
| Telemetria | Detalhada por criança, só com consentimento do responsável | Relatórios agregados para pesquisa |
| IA | Geração diária no backend (outro repositório) com validação e revisão pedagógica; o jogo só baixa e valida o JSON quando está online | Mais disciplinas e anos escolares no lote diário |
| Painéis | Nenhum | Responsáveis e professores |
| Plataforma | PC (Windows) | Linux, tablets Android (toque, traçado e voz) |

### Segundo marco: Dungeons jogáveis (greybox)

Depois do Lobby, as duas torres ficaram jogáveis, sem arte nem tema definitivos (ver ADRs 0002 e 0003 e o glossário):

- A criança entra pela porta da torre, na beira da ilha. O Plano do Nível sorteia de 3 a 4 Andares, e cada Andar é um grafo de 6 a 9 Salas gerado por seed; se o validador reprovar, entra um Andar feito à mão.
- Salas: entrada, combate, Puzzle (porta trancada, sem criatura e sem bolhas), Descanso (devolve 2 de Energia, uma vez), Fast travel, Marco, Desafio épico e a saída (escada). Combate, Puzzle, Marco e épico precisam ser vencidos para seguir a uma Sala nova.
- A Energia da Expedição segue de uma luta para a outra; zerar volta ao Lobby sem perder o Item de troca nem os Andares descobertos. O jogo usa as Missões do ano do Nível, com dificuldade que sobe com o Andar. Matemática do 3º ano ainda não tem Missões próprias e usa as do 2º.
- O Fast travel é achado pela criança, salva o progresso e aparece no portal do Lobby. Ao recomeçar depois de zerar a Energia (ou pela porta da torre), a criança volta ao último Fast travel que achou, em qualquer Andar; sem nenhum, à entrada do Andar mais alto que visitou. Os Fast travels de outros Andares aparecem no mapa, mas ainda não servem para viajar entre Andares.
- A escada vale nos dois sentidos: sobe-se pela saída e desce-se pela entrada, e cada Andar guarda as Salas visitadas e vencidas (ADR 0004). Só o Marco sem o Item e o Desafio épico travam o avanço; explorar tudo é opcional. Os Descansos usados voltam a cada Expedição nova.
- O mapa do Andar mostra o que a criança explorou: as Salas visitadas, as vizinhas sob névoa ("?", sem dizer o tipo, nem na porta) e um cadeado nas que ainda pedem uma vitória; a dois passos ou mais, nem aparece. O botão "Ouvir o mapa" diz o estado em voz alta. O que foi explorado fica no save e volta depois de a Energia zerar.
- Cada Andar tem uma barra "4 de 7" (combate, Puzzle, Marco e épico contam vencidos; Descanso e Fast travel, visitados; a Sala do Item é um combate e conta vencida; entrada e saída não contam). Com todas resolvidas, o Andar ganha um selo e fica registrado como completo, uma só vez, e rende um Cosmético pequeno do catálogo (`game/data/cosmeticos.json`), que aparece no Álbum. A barra cheia nunca bloqueia nada, e o treino no Lobby nunca dá Cosmético. Por enquanto o Cosmético é só um nome e uma descrição no Álbum, sem arte nem efeito no Personagem.
- O Item de troca fica numa Sala de um Andar anterior ao Marco, e o Marco só abre com o Item da outra torre, do mesmo Nível.
- O Desafio épico conclui o Nível naquela torre, e a criança escolhe continuar ou voltar. O Atalho de Nível, oferecido na porta da torre, leva direto ao épico com 3 Desafios sorteados do Nível; errar encerra a tentativa sem tirar Energia, e só há uma tentativa por Nível e torre. Acertar conclui o Nível e entrega o Item.
- O Desafio mestre acontece no Lobby, num portal roxo que só abre com os dois épicos do Nível concluídos. São 4 Desafios alternando Matemática e Português contra o Guardião do Nível, e vencê-los sobe o Nível liberado nas duas torres.
- Ao iniciar a Expedição a criança escolhe **Progressão** (o Plano do Nível) ou **Livre**. Na Livre os Andares seguem sem fim, no Nível já liberado, sem Marco, Item de troca nem Desafio épico; as Missões variam (o mesmo problema não se repete na Expedição e os últimos vistos são evitados) e a Livre não avança o Plano, o épico nem o Desafio mestre. Zerar a Energia volta ao Lobby como sempre.
- Cada Papel tem uma **Maestria**: sobe quando a criança vence um mesmo Conceito na Dungeon o número de vezes definido em dados (`dungeon.conceptSolves`), sem XP e sem perda; o treino não conta.
- A criança ganha **moedas** por criatura acalmada, Sala do Item e Andar completo (`dungeon.coinsCreature`, `coinsItem`, `coinsFloor`); o saldo fica no Perfil, não tem teto e não cai. Treino do Lobby não rende moedas.
- O HUD mostra só o que se usa agora: na Dungeon, a placa do centro (Nível, Andar e objetivo, o Conceito do Andar, nomeado em `game/data/conceitos.json`), o mapa e a Energia; a bolsa de números e o pedido só aparecem em combate e no treino. O painel "Controles / bônus" saiu da tela de jogo (os controles estão nas Configurações).
- Fora do marco, e ainda sem código: repetição espaçada, dificuldade adaptativa, Portal da Memória, Álbum com criaturas, aparência dos Cosméticos no Personagem e o cliente de rede.

### Primeiro marco: Lobby funcional

O primeiro marco entregável foi o Lobby funcional, ainda sem Dungeons jogáveis. Prova a base técnica (Godot C#, cena 3D isométrica, entrada, salvamento, CI e build Windows), o contrato de conteúdo e o combate real, jogado contra o Boneco de treino.

- Criar e editar o Perfil e o Personagem (apelido, cores, características, acessórios). Todo Perfil começa no Nível 1.
- Cena 3D caminhável, em greybox seguindo `mockups/lobby.png`; os assets CC0 entram depois, em ticket próprio.
- Cinco ilhas com torres ao redor: Matemática e Português com placa "em construção", Ciências, História e Geografia com placa "em breve".
- NPC Guia, que fala por voz sintética.
- Área de treino com Boneco de treino: **o mesmo combate real das Dungeons** (botão de ataque, tempo que desacelera, golpe perfeito, bolhas de névoa avisadas e travessuras no erro, dicas, Modo calmo), contra um Boneco que também ataca. Usa **tudo o que já existir**: as Mecânicas de Matemática (compor número, com a bolsa e o cajado do Matemático) e de Português (formar palavras com sílabas, com o Escriba), com Desafios sorteados conforme a dificuldade escolhida, lidos de Missões em JSON validadas pelo schema.
- **A criança só treina os Níveis a que o Perfil já tem acesso** (no começo, o Nível 1), em cada Papel. O treino nunca libera um Nível.
- A Área de treino tem **Energia própria**, com o mesmo máximo das Expedições (valor em dados). Zerar a Energia só tira a criança da Área de treino, sem perder nada e sem tela de derrota. O treino não conta para o progresso: nenhum Conceito, caixa de memória, habilidade, Cosmético ou dificuldade adaptativa muda.
- Álbum de criaturas e de Cosméticos (vazio por enquanto).
- Portal de fast travel: abre uma seleção que avisa que ainda não há pontos.
- Configurações de jogo, de acessibilidade (tamanho de texto, voz, redução de animações, fonte para dislexia, Modo calmo) e de controles (mouse, teclado e toque).
- Salvamento local versionado, CI (schema, testes, build Windows) e todo valor de regra (nomes, energia máxima, limiares adaptativos, intervalos de revisão) em dados.

## Dados e motivação

### Dados e privacidade

A LGPD (art. 14) exige consentimento específico de um responsável para tratar dados de crianças. Como a telemetria do MVP é detalhada por criança, o jogo pede esse consentimento antes de enviar qualquer dado, e funciona normalmente sem ele.

- Nenhum nome real, foto ou dado de escola no MVP: a criança tem um apelido e um identificador aleatório.
- Nenhum dado da criança vai para serviços de IA de terceiros.
- O responsável pode pedir para apagar tudo.

**CPF não é necessário, nem do responsável nem da criança.** A LGPD pede consentimento do responsável, não um documento, e manda coletar o mínimo de dados; CPF seria mais um dado sensível para proteger sem ganho real. Para confirmar que é um adulto: e-mail confirmado, consentimento explícito e uma pergunta de controle parental. Uma conta de responsável tem vários perfis de criança, cada um só com apelido.

As contas também seguem o ECA Digital (Lei 15.211/2025), que traz regras para serviços digitais usados por crianças.

### Sem compras

Nada é cobrado dentro do jogo: sem conteúdo pago, anúncios, moedas ou caixas de recompensa.

### Ranking

Decisão: sem ranking entre crianças. Ele expõe quem tem dificuldade e faz o ponto virar o objetivo no lugar de aprender. A motivação vem da comparação da criança com ela mesma e de metas.

Alternativas que mantêm a motivação:

- **Recorde pessoal:** "você resolveu 3 a mais que na semana passada", "seu andar mais fundo".
- **Metas pessoais:** objetivos curtos por sessão ou semana, sem penalidade por não cumprir.
- **Meta coletiva da turma (depois do MVP):** todos somam para desbloquear algo juntos.
- **Pontos por esforço e exploração**, não por acerto ou velocidade.

### Decisões em aberto

- [x] Tema Arquipélago do Céu e nomes Matemático e Escriba aprovados como provisórios; nome do jogo e dos Papéis ficam em dados e podem mudar
- [ ] Quais habilidades da BNCC entram primeiro em cada ano: depende do consenso de pedagogia, psicologia e fonoaudiologia (há uma ordem recomendada provisória, usada no MVP)
- [ ] Quando entram voz e traçado de letras (padrão: logo após o MVP, junto com tablets)
- [ ] Trocar de Papel dentro da Dungeon, se surgirem torres híbridas (ver "Personagem e Papéis")
