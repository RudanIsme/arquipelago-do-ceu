# Arquipélago do Céu

Roguelite educativo em que a criança sobe dungeons, uma por disciplina, e vence desafios usando matemática e português. O nome do jogo e os nomes dos personagens são provisórios e vivem em dados, para trocar sem mexer em regras.

## Language

### Mundo

**Lobby**:
O lugar neutro de onde a criança parte e para onde volta ao fim de uma Expedição. É uma ilha cercada pelas torres das Dungeons, com o NPC guia, a Área de treino, o Álbum, o portal de fast travel e as configurações.
_Avoid_: Ilha-base, hub

**Guia**:
O NPC do Lobby que orienta a criança e fala por voz sintética.
_Avoid_: Tutorial

**Área de treino**:
Espaço do Lobby onde a criança pratica Conceitos num Boneco de treino, no mesmo combate das Dungeons. Tem Energia própria: se zera, a criança só sai da Área de treino, sem perder nada. Só treina os Níveis a que o Perfil já tem acesso. Não conta para o progresso (só o que acontece nas Dungeons conta).
_Avoid_: Sala de treinamento (Sala é de Andar)

**Boneco de treino**:
Criatura de treino da Área de treino. Luta como uma criatura das Dungeons, com bolhas e travessuras, e propõe Desafios sorteados conforme a dificuldade escolhida.
_Avoid_: Dummy

**Travessura**:
A reação do Boneco quando a criança erra: embaralha as peças da bolsa ou esconde uma por alguns segundos (valor em dados). Nunca é violenta.
_Avoid_: Castigo, punição, ataque

**Bolha de névoa**:
O ataque lento do Boneco, sempre avisado antes por uma faixa no chão, texto e som. A criança desvia andando; ser atingida tira 1 de Energia. Com o Modo calmo ligado ela não aparece.
_Avoid_: Tiro, projétil

**Álbum**:
A coleção do Perfil, com as criaturas libertadas (cartas de revisão) e os Cosméticos.
_Avoid_: Biblioteca de descobertas

**Cosmético**:
Item de aparência do personagem do Perfil (roupas, cores, acessórios, variações visuais da ferramenta), sem efeito no jogo. Só se ganha por aprendizado e exploração nas Dungeons, nunca por compra ou sorteio nem pelo treino no Lobby. Completar um Andar rende um Cosmético pequeno, uma só vez.
_Avoid_: Skin

**Equipamento**:
Capacete, Armadura ou Acessório do Personagem, um por espaço e por Papel, escolhido no Inventário. Dá atributos (velocidade, Energia máxima, alcance de interação) e pede uma Maestria mínima do Papel; um substitui o outro, sem evoluir. É a exceção declarada a "Cosmético não dá vantagem". Vive em `core/Equipment`.
_Avoid_: Skin, item (Item de troca é de Dungeon)

**Inventário**:
O modal onde a criança vê o Personagem equipado em caixinhas, a biblioteca de Equipamentos e Itens (os vestidos primeiro) e compara só o que muda antes de equipar. Tem a aba Cosméticos, sem atributos. Abre pelo Dock, no Lobby e na Dungeon fora de combate.

**Dock**:
O canto inferior esquerdo do HUD, com o botão do Inventário (e o selo "novo", sempre em texto) e o nome do Papel.

**Pesca** (modo de teste, F9):
Prática opcional em que a criança "puxa" sílabas com uma encenação (BOA/B) e confere desenhando a letra com traço neon. Não grava progresso e não integra a campanha; o Escriba do jogo continua sendo a bolsa de sílabas.
_Avoid_: Minijogo (é outra coisa, prevista para depois do MVP)

**Dungeon**:
O espaço de uma disciplina inteira, em forma de torre. Tem três Níveis, um por ano escolar.
_Avoid_: Arquipélago, torre (como termo de regra)

**Nível**:
Uma etapa da Dungeon que corresponde a um ano escolar (1º ao 3º no MVP). É uma sequência de Andares e termina num Desafio épico.
_Avoid_: Fase, ano (para se referir à etapa)

**Plano do Nível**:
A divisão de um Nível em Andares: quantos são e em qual deles ficam a Sala do Item, o Marco e o Desafio épico. Cada Andar é gerado a partir do Plano do seu Nível.
_Avoid_: Roteiro, mapa do Nível

**Andar**:
A unidade que o jogo gera de uma vez: um grafo de Salas ligadas por portas, que são Puzzles. Tem um Fast travel, que também salva o progresso. A escada vale nos dois sentidos e cada Andar guarda as Salas visitadas e vencidas (ADR 0004). No mapa, a Sala vizinha ainda não visitada aparece sob névoa, sem dizer o que é, e a que ainda pede uma vitória tem um cadeado.
_Avoid_: Fase, mapa

**Sala**:
Cada espaço de um Andar, com uma função: combate, Puzzle, Descanso, Fast travel, Marco ou Desafio épico. Combate, Puzzle, Marco e Desafio épico precisam ser vencidos para seguir a uma Sala nova; voltar a uma Sala já visitada é sempre permitido.
_Avoid_: Ilha (ilha é só o Lobby e o cenário), room

**Andar completo**:
Um Andar com todas as Salas resolvidas: Combate, Puzzle, Marco e Desafio épico vencidos, e Descanso, Fast travel e Sala do Item (que é um combate) já visitados ou vencidos. Entrada e saída não contam. É mostrado como "4 de 7" no mapa, ganha um selo e é registrado uma única vez; a barra cheia nunca bloqueia nada, e o Nível se conclui só com o Desafio épico.
_Avoid_: Andar 100%

**Descanso**:
Sala opcional em que a criança recupera um pouco de Energia, uma vez só (a quantidade vem de dados). Não tem Desafio e não recupera tudo.
_Avoid_: Cura, fogueira

**Fast travel**:
Sala de um Andar que a criança precisa encontrar; ao chegar nela o progresso é salvo e ela pode voltar ao Lobby e retomar dali. Ao recomeçar depois de zerar a Energia, a criança volta ao último Fast travel que achou, em qualquer Andar; sem nenhum, à entrada do Andar mais alto que visitou.
_Avoid_: Checkpoint, save

**Sala do Item**:
Sala de combate ou de Puzzle cujo prêmio é o Item de troca do Nível.
_Avoid_: Baú

**Puzzle**:
Sala em que a Missão é a mesma do combate, mas o alvo é uma porta ou uma ponte, sem criatura e sem Bolha de névoa. A porta abre quando o Desafio é resolvido; não existe chave. Erro tira 1 de Energia e o Desafio recomeça com outros números.
_Avoid_: Quebra-cabeça

**Guardião do Nível**:
O Boneco no Desafio mestre: a criatura que a criança enfrenta, com Desafios de Matemática e de Português do Nível.
_Avoid_: Chefe

**Marco**:
Sala de um Andar que só se passa com um Item de troca.
_Avoid_: Portão, checkpoint (checkpoint é o fast travel)

**Nível liberado**:
O Nível até o qual o Perfil pode jogar e treinar, nas duas Dungeons. Só sobe quando a criança vence o Desafio mestre. É diferente de Nível concluído, que é de uma Dungeon e conta o Desafio épico vencido, por jogo ou por Atalho de Nível.
_Avoid_: Nível atual, nível do Perfil

**Item de troca**:
Item de uma Dungeon, sempre do mesmo Nível, exigido para passar um Marco da outra Dungeon. Fica numa Sala de um Andar anterior ao Marco da própria Dungeon, então as duas torres nunca esperam uma pela outra. Fica com a criança entre Expedições: não se perde quando a Energia zera.
_Avoid_: Chave

### Progressão

**Moeda**:
O saldo do Perfil, só do jogo e nunca dinheiro de verdade. Ganha-se por criatura acalmada, Sala do Item e Andar completo (os valores vêm de dados), e não se perde, nem ao zerar a Energia. Não há teto, e velocidade e sequência não rendem nada. O treino do Lobby não dá moedas.
_Avoid_: Dinheiro, ouro

**Progressão**:
O modo de Expedição que segue o Plano do Nível, com o Marco, o Item de troca e o Desafio épico. É o que conclui o Nível.

**Expedição livre**:
O outro modo de Expedição: os Andares seguem sem fim, a partir da mesma seed, no Nível que a criança já liberou, sem Marco, Item de troca nem Desafio épico. O conteúdo varia (o mesmo problema não se repete na Expedição) e rende moedas, mas não avança o Plano do Nível, o Desafio épico nem o Desafio mestre. A criança sai quando quiser, sem punição.
_Avoid_: Modo infinito

**Expedição**:
Uma tentativa contínua, desde a saída do Lobby até a volta. Termina quando a criança decide voltar ou quando a Energia acaba; ao concluir um Desafio épico, ela escolhe entre continuar e voltar ao Lobby. Perde-se o que é temporário; Andares salvos e habilidades ficam.
_Avoid_: Run, partida, sessão

**Desafio épico**:
O desafio que fecha um Nível, dentro de uma só Dungeon; concluí-lo é finalizar a Dungeon naquele Nível. Usa só conceitos já praticados naquele Nível.
_Avoid_: Chefe

**Atalho de Nível**:
Tentativa, feita na entrada da Dungeon, de passar um Nível inteiro vencendo direto o Desafio épico, com Conceitos sorteados de todo o Nível. Cada Perfil tem uma só tentativa por Nível em cada Dungeon: acertando, o Nível conta como concluído e entrega o Item de troca daquele Nível; errando, joga o Nível normalmente e não tenta de novo.
_Avoid_: Pulo, teste de nível

**Desafio mestre**:
O desafio que mistura as duas disciplinas e permite subir de Nível nas duas Dungeons. Acontece no Lobby, num portal próprio, e só abre quando os dois Desafios épicos do Nível estão concluídos, inclusive pelo Atalho de Nível.
_Avoid_: Prova

**Energia**:
O que a criança gasta ao errar e recupera ao acertar numa Expedição. Começa no máximo, que vale 5 no MVP e pode aumentar com upgrades futuros.
_Avoid_: Vida, HP

**Esquiva**:
Recompensa do Portal da Memória que perdoa o primeiro erro de uma Expedição, sem tirar Energia e sem ultrapassar o teto.
_Avoid_: Escudo (escudo é o número da criatura), segunda chance

### Conteúdo

**Conceito**:
Unidade de aprendizado que tem caixa de repetição espaçada, por exemplo "adição até 10".
_Avoid_: Tópico, habilidade (habilidade é do personagem)

**Mecânica**:
A regra do jogo que uma Missão escolhe, por exemplo compor um número ou formar uma palavra.
_Avoid_: Tipo de questão

**Missão**:
O dado que descreve um conteúdo: enunciado, parâmetros, dicas e explicação, ligado a um Conceito e a uma Mecânica.
_Avoid_: Questão, exercício, pergunta

**Desafio**:
Uma Missão posta num lugar do jogo, como uma criatura, uma porta ou uma ponte, que a criança enfrenta.
_Avoid_: Fase, exercício

### Pessoas

**Perfil**:
A identidade de uma criança no jogo: apelido e Personagem personalizável, criada localmente e sem conta.
_Avoid_: Jogador, usuário, conta (conta é do Responsável)

**Responsável**:
O adulto que consente com o uso de dados, pode ligar Perfis a uma conta e ajusta o jogo para a criança.
_Avoid_: Pai, pais

**Educador**:
Quem revisa o conteúdo pedagógico e acompanha a turma. O professor é um Educador.
_Avoid_: Professor (como termo de regra)

**Terapeuta**:
Profissional de fonoaudiologia ou psicopedagogia que ajusta as tolerâncias de voz e de traçado de um Perfil.
_Avoid_: Clínico

### Personagem

**Personagem**:
O avatar único e personalizável do Perfil (cores, características, acessórios). Assume o Papel da Dungeon em que está.
_Avoid_: Herói, avatar

**Papel**:
O que a Dungeon faz o Personagem ser: roupas, ferramenta e habilidades próprias. As habilidades crescem conforme a criança aprende Conceitos.
_Avoid_: Classe

**Definição**:
A descrição, em dados, de um tipo que o jogo reconhece pelo id (um espaço de equipamento, um tipo de Sala, uma torre). Adicionar um tipo novo é adicionar uma definição. O que tiver comportamento próprio vem junto de uma estratégia registrada com o mesmo id.
_Avoid_: Classe de tipo, enum

**Efeito**:
O que uma peça de equipamento muda no Personagem (por exemplo Energia máxima, velocidade ou alcance de interação), somado aos atributos base até o limite do conjunto.
_Avoid_: Bônus solto, buff

**Gatilho**:
O acontecimento do jogo que dá uma recompensa, como completar um Andar, dominar um Conceito ou acalmar uma criatura. Cada recompensa (moedas, Cosmético, equipamento) está ligada a um gatilho.
_Avoid_: Evento (evento é do código)

**Equipamento**:
Peça que o Personagem veste num dos três espaços (Capacete, Armadura e Acessório), por Papel. Cada peça dá um atributo (Energia máxima, velocidade ou alcance de interação) e pede uma Maestria mínima do Papel; quem não tem a Maestria não a veste. O Personagem soma os atributos base com os do equipamento, até os limites do conjunto. Vale em qualquer combate, inclusive no treino. Não evolui (uma peça substitui a outra), e vestir ou trocar nunca recupera Energia. Não é Cosmético: Cosmético só muda a aparência.
_Avoid_: Item (Item é o Item de troca), Cosmético

**Maestria**:
O número de um Papel, que sobe cada vez que a criança domina um Conceito na Dungeon daquele Papel (acumulando as vitórias que os dados pedem). Não há XP separado, não cai nunca e ficar sem jogar não tira nada. O treino do Lobby não conta, e velocidade e número de tentativas não entram.
_Avoid_: Nível do Papel, XP

**Matemático** (nome provisório):
O Papel da Dungeon de Matemática, que monta contas com uma bolsa de números.

**Escriba** (nome provisório):
O Papel da Dungeon de Português, que forma nomes e frases com sílabas e letras.
