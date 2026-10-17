# O Andar é um grafo cujas portas são Puzzles, dentro de um Plano do Nível sorteado

O Andar é um grafo livre de Salas ligadas por portas, e cada porta trancada é um Puzzle: abre quando a criança resolve o Desafio, sem chave. Só o Marco exige um Item. Escolhemos isso em vez de chaves para a criança não carregar outro item que se perde quando a Energia zera, e para o validador só ter dois bloqueios a provar: todo Puzzle e o Fast travel são alcançáveis da entrada sem passar pelo Marco, e o Item vem antes do Marco.

Quantos Andares tem um Nível, e em qual ficam a Sala do Item, o Marco e o Desafio épico, é sorteado por seed num Plano do Nível, gerado de (mundo, dungeon, nível, versão do gerador). Cada Andar nasce do seu Plano, então a mesma entrada continua gerando o mesmo mapa. Os limites (3 a 4 Andares, 6 a 9 Salas) ficam em dados. A alternativa de ordem fixa (Item no 1º Andar, Marco no 2º, épico no 3º) seria mais fácil de testar, mas deixaria todos os Níveis iguais.

Trocar o grafo por uma fila de Salas, ou o Plano por ordem fixa, muda o gerador e o formato do save; por isso o registro.

## Andares profundos (#153)

Além do Plano do Nível há os Andares profundos: sem fim, gerados pela mesma seed (com números de seed acima dos do Plano, para o primeiro Andar profundo não repetir o Andar 1), comuns, sem Marco, Item de troca nem Desafio épico. A criança chega a eles depois do último Andar do Plano, ou por uma escada lateral opcional na entrada de qualquer Andar da Progressão. Eles rendem moedas e Maestria, não avançam o Nível, nunca bloqueiam o Marco nem são obrigatórios, e o Fast travel deles volta à Progressão. Substitui a escolha entre Progressão e Livre: entrar numa torre sempre começa a Progressão, e o validador continua provando só os dois bloqueios do Plano.
