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
Item de aparência do personagem do Perfil (roupas, cores, acessórios, variações visuais da ferramenta), sem efeito no jogo. Só se ganha por aprendizado e exploração nas Dungeons, nunca por compra ou sorteio.
_Avoid_: Skin

**Dungeon**:
O espaço de uma disciplina inteira, em forma de torre. Tem três Níveis, um por ano escolar.
_Avoid_: Arquipélago, torre (como termo de regra)

**Nível**:
Uma etapa da Dungeon que corresponde a um ano escolar (1º ao 3º no MVP). É uma sequência de Andares e termina num Desafio épico.
_Avoid_: Fase, ano (para se referir à etapa)

**Andar**:
A unidade que o jogo gera de uma vez. Tem um ponto de fast travel, que também salva o progresso.
_Avoid_: Fase, mapa

**Sala**:
Cada espaço de um Andar, com uma função: combate, puzzle, descanso, fast travel, Marco ou Desafio épico.
_Avoid_: Ilha (ilha é só o Lobby e o cenário), room

**Marco**:
Sala de um Andar que só se passa com um Item de troca.
_Avoid_: Portão, checkpoint (checkpoint é o fast travel)

**Item de troca**:
Item de uma Dungeon, sempre do mesmo Nível, exigido para passar um Marco da outra Dungeon.
_Avoid_: Chave

### Progressão

**Expedição**:
Uma tentativa contínua, desde a saída do Lobby até a volta. Termina quando a criança decide voltar ou quando a Energia acaba; ao concluir um Desafio épico, ela escolhe entre continuar e voltar ao Lobby. Perde-se o que é temporário; Andares salvos e habilidades ficam.
_Avoid_: Run, partida, sessão

**Desafio épico**:
O desafio que fecha um Nível, dentro de uma só Dungeon; concluí-lo é finalizar a Dungeon naquele Nível. Usa só conceitos já praticados naquele Nível.
_Avoid_: Chefe

**Atalho de Nível**:
Tentativa, feita na entrada da Dungeon, de passar um Nível inteiro vencendo direto o Desafio épico, com Conceitos sorteados de todo o Nível. Cada Perfil tem uma só tentativa por Nível em cada Dungeon: acertando, o Nível conta como concluído; errando, joga o Nível normalmente e não tenta de novo.
_Avoid_: Pulo, teste de nível

**Desafio mestre**:
O desafio que mistura as duas disciplinas e permite subir de Nível nas duas Dungeons.
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

**Matemático** (nome provisório):
O Papel da Dungeon de Matemática, que monta contas com uma bolsa de números.

**Escriba** (nome provisório):
O Papel da Dungeon de Português, que forma nomes e frases com sílabas e letras.
