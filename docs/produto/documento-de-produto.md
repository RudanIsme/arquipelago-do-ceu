# Arquipélago do Céu: documento de produto

## Visão e objetivo

Um roguelite educativo em português em que a criança explora dungeons no céu e vence desafios usando o que aprende de matemática e português.

| Decisão | Escolha |
| --- | --- |
| Plataforma inicial | PC |
| Engine | Godot 4 (C#) |

## Público e BNCC

O público é de 6 a 10 anos. O MVP cobre do 1º ao 3º ano, e cada nível de dungeon corresponde a um ano escolar. Uma criança de 6 anos começa no nível 1; uma criança mais velha pode começar no nível do seu ano, escolhido pelo responsável ou pelo professor.

**Clínicas de fonoaudiologia e psicopedagogia também são público.** Na clínica o jogo é ferramenta de apoio à terapia, nunca de diagnóstico: o terapeuta ajusta as tolerâncias de voz e de traçado para cada criança e acompanha a evolução.

**O que é a BNCC:** a Base Nacional Comum Curricular é o documento oficial do MEC que define o que todo aluno brasileiro deve aprender em cada ano. Cada habilidade tem um código, por exemplo EF01MA06 (1º ano, matemática: fatos básicos de adição). Marcar cada missão com o código BNCC correspondente ajuda o professor a saber onde encaixá-lo na aula.

**A BNCC é o piso, não o teto.** Cada missão tem um campo opcional de código BNCC. Além disso, cada nível termina com **desafios de antecipação** opcionais, com conteúdo do ano seguinte (o nível do 3º ano antecipa o 4º). Eles nunca são obrigatórios, não bloqueiam progresso e não contam como erro: são para a criança que quer ir além e se preparar para o próximo ano. Problemas de olimpíadas de matemática para crianças servem de inspiração.

## Gameplay

A criança explora dungeons no céu, uma por disciplina, com personagens fixos, e o combate acontece pelas próprias mecânicas de aprendizado. O tema completo ainda está aberto; a dungeon no céu é o ponto de partida.

### Tema: um mundo que aceita qualquer disciplina

Matemática e Português não têm ambientação própria, mas História, Ciências e Geografia têm. Por isso o tema precisa ser um guarda-chuva em que cada disciplina ganha a sua ambientação, sem mudar a estrutura do jogo.

**Proposta: o Arquipélago do Céu.** Uma névoa do esquecimento cobriu as ilhas do céu e apagou o que elas sabiam: criaturas esqueceram seus nomes, máquinas perderam seus números. O lobby é uma ilha neutra, e cada disciplina é um arquipélago próprio que a criança restaura. Uma disciplina nova é só um novo arquipélago que surge da névoa. O combate não é violento: a criança dissolve a névoa e liberta as criaturas.

| Disciplina | Ambientação | Criaturas |
| --- | --- | --- |
| Matemática | Ilhas-oficina: engrenagens, cristais, balanças, relógios | Autômatos com escudos numéricos |
| Português | Ilhas-biblioteca: livros voadores, letras soltas, pergaminhos | Criaturas feitas de letras apagadas, sem nome |
| Ciências (depois) | Ilhas-jardim e laboratório: ecossistemas, clima | Animais e plantas a investigar e classificar |
| História (depois) | Ilhas-memória: cada andar é uma época | Pessoas de cada época que perderam a memória |
| Geografia (depois) | Ilhas-mapa: relevo, rios, cidades | Viajantes perdidos que precisam de direção |

**Regra para a construção:** a estrutura (andares, salas por função, marcos, desafios épicos) é igual para todas as disciplinas. O tema é um pacote de dados que veste essa estrutura, e um pacote pode ter sub-temas, como uma época histórica por andar. Assim História entra com um pacote novo e mecânicas novas, sem refazer o gerador.

**Estilo visual: 3D isométrico detalhado.** O MVP usa assets livres estilizados como provisórios, e a arte detalhada entra em fases, começando pelo lobby e pelas criaturas.

### Personagens

Personagens são fixos, cada um bom em uma área, e as habilidades crescem conforme a criança aprende conceitos novos.

| Personagem (nome provisório) | Área | Exemplo de progressão |
| --- | --- | --- |
| Matemático | Matemática | Com as 4 operações compõe números para desarmar inimigos; ao aprender frações, também divide inimigos ou pontes em partes |
| Escriba | Português | Com sílabas forma palavras que abrem portas; ao aprender frases, ordena mensagens que acalmam criaturas |

Regra: uma habilidade bloqueada nunca impede a criança de praticar o conceito. A área de treino do lobby sempre oferece o conceito que destrava a habilidade.

### Combate pelas mecânicas de aprendizado

O combate acontece em tempo real, no próprio cenário, sem trocar de tela. As criaturas se movem devagar e avisam antes de atacar, e a criança se move e desvia. Para atacar, ela chega perto e aperta o botão de ataque: o tempo desacelera enquanto ela monta a resposta com as peças da bolsa, e volta ao normal quando o ataque sai. O jogo tem ritmo de ação, mas a parte de pensar nunca é corrida.

- **Energia:** acertar recupera; errar, ou ser atingida por uma bolha de névoa, tira. Quando a energia acaba, a expedição termina (veja Energia e derrota).
- **A ação não fica mais difícil com o ano escolar.** Só o conteúdo fica. Ataques de criatura são sempre lentos, avisados e fáceis de desviar.
- **Modo calmo:** opção que para o tempo por completo durante a montagem e faz as criaturas só reagirem a erros. Serve para crianças menores ou com dificuldade motora, e o jogo sugere ligar se a criança perde muita energia por desvio.

**O ataque da criança é o próprio conhecimento.** A conta montada pelo Matemático vira um raio de energia que dissolve a névoa da criatura; o nome escrito pelo Escriba voa em letras luminosas até ela. O acerto exato é um golpe perfeito, com animação especial.

**O ataque da criatura nunca é violento.** As criaturas lançam bolhas de névoa lentas, com aviso visual e sonoro antes. Se a criança erra uma resposta, a criatura reage na hora com uma travessura: embaralha as peças da bolsa ou esconde uma peça por alguns segundos. Nada de golpes, sangue ou personagem caído: quando a energia acaba, o personagem volta flutuando ao lobby.

Depois de um erro o desafio recomeça com outros números ou outra palavra, para a criança não acertar por eliminação. Dois erros seguidos liberam uma dica.

#### Matemático: a bolsa de números

A bolsa funciona como o baralho de um roguelite de cartas: é o que a criança monta e melhora ao longo da expedição.

1. A bolsa começa com números de 1 a 5 e o sinal +. A criança tem 5 peças na mão; as usadas num ataque são repostas pela bolsa.
2. A criatura mostra um escudo com um número, por exemplo 12.
3. A criança monta uma conta com as peças nos encaixes do cajado (5 + 4 + 3). O resultado tira do escudo.
4. Acertar o valor exato é um golpe perfeito: derrota a criatura de uma vez e dá um bônus. Passar do valor não vale; o jogo mostra a conta e ela tenta outra combinação, o que treina estimativa.
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

**Quando entram:** voz e traçado ficam definidos agora e entram logo depois do MVP, junto com tablets Android. No MVP o Escriba usa sílabas por clique e teclado.

#### Energia e derrota

Um roguelite precisa de risco, mas o risco não pode virar punição por não saber. Regras propostas:

- A energia começa cheia em cada expedição (ex.: 5 pontos). Acerto recupera 1, golpe perfeito recupera 2, erro tira 1, e ser atingida por uma bolha de névoa também tira 1.
- Pedir dica nunca custa energia, e errar desafios de antecipação também não.
- Antes de confirmar, a criança vê a jogada montada e pode desfazer, para que um clique errado não conte como erro de conceito.
- Energia zerada encerra a expedição: a criança perde itens e melhorias temporárias, e mantém andares salvos, habilidades e tudo o que aprendeu.
- Duas derrotas seguidas no mesmo conceito descem a dificuldade adaptativa e oferecem treino daquele conceito no lobby, sem bloquear nada.
- A mensagem de derrota fala da aventura, nunca da criança: "A névoa ficou forte demais desta vez. Vamos recarregar a energia?"

### Estrutura do mundo

Cada disciplina tem sua própria dungeon; no MVP são duas, Matemática e Português. Cada dungeon tem níveis, cada nível é uma sequência de andares gerados e termina num desafio épico. Para passar um marco de dificuldade a criança precisa de um item da outra dungeon, e para subir de nível nas duas ela enfrenta um desafio mestre que mistura as disciplinas.

- **Andar é a unidade gerada.** A geração procedural monta um andar inteiro de uma vez (todas as salas e conexões), não sala por sala.
- **Cada andar tem um ponto de fast travel**, que também salva o progresso. O jogo também salva sozinho a cada sala e ao pausar.
- **O item exigido vem sempre do mesmo nível da outra dungeon**, nunca de um nível acima. Assim a criança nunca fica travada esperando algo impossível.
- **Desafio épico e mestre só usam conceitos já praticados** naquele nível; são uma revisão divertida, não uma prova surpresa.
- **Escopo do MVP:** 3 níveis em cada dungeon, um por ano escolar (1º, 2º e 3º), com desafio mestre entre eles e desafios opcionais de antecipação no fim de cada nível.

### Expedição e progressão

- **Duração:** 20 minutos ou mais, com pausa a qualquer momento, já que uma criança de 6 anos raramente fica 20 minutos seguidos.
- **Falha:** quando a energia acaba, volta ao lobby, perde só recursos temporários e pode recomeçar do último ponto de fast travel.
- **Narrativa:** mínima no MVP, só o contexto das missões.
- **Multiplayer e social:** nenhum. Sem ranking entre crianças; a motivação vem de recordes pessoais e metas.

## Conteúdo educacional

O MVP cobre matemática e português do 1º ao 3º ano: cerca de 60 missões base revisadas por um educador (10 por ano e disciplina), mais o lote diário gerado por IA. Ciências fica para depois.

- **Como chegar a \~60 missões:** 6 a 8 mecânicas reutilizáveis (compor números, comparar quantidades, completar sequências, formar palavras, ordenar frases), cada uma com parâmetros diferentes. Uma missão nova sobre uma mecânica existente é só um arquivo de dados.
- **Leitura:** texto com voz sintética (TTS). Toda instrução pode ser ouvida, porque uma criança de 6 anos ainda está aprendendo a ler. O Godot 4 tem TTS embutido que usa as vozes do sistema.
- **Dificuldade adaptativa:** no MVP é uma regra simples e transparente, por exemplo: três acertos sem dica sobem um nível, duas tentativas com dica descem um nível. Nada de modelo estatístico ainda. Responsáveis e educadores conseguem entender e ajustar a regra.
- **Revisão:** nenhuma missão vai para a criança sem revisão pedagógica, inclusive as variantes geradas por IA.

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
- **Portal da Memória:** no início de cada expedição, uma sala opcional com até 3 conceitos para revisar. Resolver dá energia extra para a expedição.
- **Mistura de conceitos:** cada andar mistura conceitos novos e antigos, e o desafio mestre é uma revisão misturada das duas dungeons.
- **Álbum de criaturas (flash cards):** cada criatura libertada vira uma carta na Biblioteca do lobby, com a criatura e o desafio na frente e a explicação no verso. A criança revisa quando quiser, e as cartas prontas para revisão brilham.
- **Sem punição por ausência:** não existe sequência diária nem perda por não jogar. Quando a criança volta, as revisões estão esperando.

## Escopo: MVP e depois

Tudo o que foi escolhido para o MVP entra, mas na forma mais simples que ainda prova a ideia. O que sobra vai para a fase 2. A IA entra como geração diária no servidor, não na hora em que a criança joga.

| Recurso | No MVP | Depois |
| --- | --- | --- |
| Lobby | Uma ilha pequena com portal, NPC, treino e fast travel | Biblioteca de descobertas, coleção, decoração |
| Dungeon | Matemática e Português, 3 níveis cada (1º ao 3º ano), andares gerados inteiros, desafios mestres | Novos biomas e tipos de sala |
| Fast travel | Um ponto por andar, que também salva | Mapa do mundo completo |
| Personagens | Matemático e Escriba, 2 a 3 habilidades cada | Mais personagens, Ciências |
| Conteúdo | \~60 missões base sobre 6 a 8 mecânicas, 1º ao 3º ano, com desafios de antecipação e repetição espaçada | 4º e 5º ano, Ciências, História, minijogo 2D |
| Dificuldade adaptativa | Regra simples por acertos e dicas | Modelo por conceito, ajustável pelo educador |
| Voz | TTS do sistema | Narração gravada; reconhecimento de fala com tolerância por nível |
| Conta | Opcional, só para sincronizar entre aparelhos | Conta gerenciada por responsável ou escola |
| Telemetria | Detalhada por criança, só com consentimento do responsável | Relatórios agregados para pesquisa |
| IA | Geração diária no servidor com validação automática e amostra com revisão pedagógica; o jogo baixa quando está online | Mais disciplinas e anos escolares no lote diário |
| Painéis | Nenhum | Responsáveis e professores |
| Plataforma | PC (Windows) | Linux, tablets Android (toque, traçado e voz) |

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

- [ ] Aprovar o tema Arquipélago do Céu e os nomes dos personagens
- [ ] Quais habilidades da BNCC entram primeiro em cada ano
- [ ] Quando entram voz e traçado de letras (padrão: logo após o MVP, junto com tablets)
