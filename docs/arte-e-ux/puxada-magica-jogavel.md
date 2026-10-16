# Puxada mágica jogável

> Registro histórico das iterações. Para o estado aprovado atual (BOA/B, espada e traçado neon), requisitos e testes, consulte [Teste da entrega](../tecnico/teste-pesca-neon.md). As seções abaixo descrevem também versões anteriores.


Modo opcional solicitado para selecionar sílabas com ação cinematográfica. F9 (ou o botão Pescar com puxada) abre uma prática sem progresso; o botão de mesmo nome no painel de sílabas abre a montagem atual usando o mesmo NameBattle.

Clique e solte em uma sílaba, toque nela, ou use 1–9. Cada seleção executa Precisa (2,4 s), Diagonal (3 s), Carregada (3,6 s), em ciclo. Espaço escolhe a primeira disponível. A sílaba entra na palavra no fim da puxada, junto ao clarão. Seleções, desfazer e conferência ficam bloqueados durante o golpe; não há fila de cliques nem prazo para responder. Backspace desfaz; Enter confere. Esc/F9 ou Voltar ao jogo restauram câmera, controles, interface e escala de tempo. Sair antes do instante da captura cancela a sílaba pendente; capturas já concluídas permanecem no painel original.

O modo pausa o restante do jogo durante a encenação, como exceção opcional solicitada ao fluxo normal do Escriba. Fora dele, Livro, desenho, locomoção, capa e regras de conferência permanecem no fluxo existente. Conferir e voltar entrega a resposta ao NamePanel e ao treino existentes; não calcula acertos no adaptador visual. F9 usa uma missão existente de três sílabas e não grava progresso. Recomeçar palavra reinicia essa missão de prática.

A cena usa warrior-face-refined-v2.glb exclusivamente em Dev/PullTest. Inclui parado e três puxadas; NÃO substitui bibliotecas de andar/correr. Modelo natural-v2 anterior preservado. Efeitos acompanham o eixo real do antebraço. Reduzir movimento fixa a câmera e desacelera o golpe; Reduzir brilho suprime clarão e luz local.

Validação em 15/10/2026: build sem erros/avisos; 382 testes existentes passaram; roteiro Godot --pull-qa passou com eventos de mouse, três ações, cliques rápidos sem fila, duplicação rejeitada, formação de palavra, Esc durante golpe, cancelamento antes da captura, captura no fluxo compartilhado, conferência de volta ao painel, restauração e reentrada, resoluções 1280x720 e 1920x1080. Evidências e backups na pasta task-4/qta-backup e task-4/qta-qa.

O teste utiliza save separado. Não inclui novo traçado neon jogável nem recompensas; preserva o traçado já existente no painel.

## HUD inspirado no concept — 15/10/2026

O modo de pesca usa agora a paleta creme #f8edcf, contorno dourado #c59750, texto castanho #463a35 e laço ciano #79f8f0 do vídeo Pescar-palavras-concept.mp4. O cabeçalho e as sílabas reutilizam a mesma fonte de interface do concept (ThemeDB.FallbackFont, títulos 27px, sílabas 26px em base 1280x720). As sílabas são botões distribuídos ao redor do personagem, com posições estáveis para facilitar clique/toque. A faixa inferior mostra cada encaixe da palavra. Opções recolhe brilho/movimento/recomeçar; Voltar permanece acessível. O alvo e as instruções são texto discreto, sem painéis de depuração.

Diferenças intencionais: faixa inferior começa em y=600, em vez de y=570 do concept, para não cobrir o personagem na câmera funcional; botões Conferir, Desfazer, Opções e Voltar são necessários à interação real. Número de sílabas e encaixes vem da missão, não é fixo em BORBOLETA. Sílabas ficam estáveis (sem flutuação contínua) e não distinguem resposta correta de distrator pela cor. Sem novo traçado neon, nova mecânica ou alteração de rosto, câmera e golpes.
