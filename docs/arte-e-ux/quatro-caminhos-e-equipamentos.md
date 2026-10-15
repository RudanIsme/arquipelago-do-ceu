# Quatro caminhos de interface e equipamento

Experimento local de 14/10/2026. As quatro opções são jogáveis e selecionáveis no alto da tela: **Essencial**, **Livro**, **Aventura** e **Cristais**. **Base** restaura a interface anterior e oculta os novos adornos; o botão **Quatro interfaces** permite voltar. Trocar interface preserva o equipamento do Perfil.

| Caminho | Organização | Preset explícito |
|---|---|---|
| Essencial | HUD compacto contextual; oficina por abas | Explorador, sem capacete |
| Livro | Pergaminho; equipamentos e cores em duas páginas | Estudioso, elmo aberto |
| Aventura | Navegação vertical; inventário lateral | Guardião, elmo aberto |
| Cristais | Comandos inferiores; bancada em bússola | Estudioso, diadema |

**Equipar** abre uma prévia temporária. Armaduras, capacetes e cores alteram as peças visíveis; somente **Aplicar** grava no Perfil. **Cancelar** ou Escape restauram a aparência anterior e não mudam bônus ou save. Cada preset exige um clique próprio, separado do seletor de interface. O traje original e a opção sem capacete removem os acessórios adicionais.

## Bônus e persistência

Este experimento autorizado introduz bônus funcionais de equipamento, além dos cosméticos previstos anteriormente. A Energia base continua definida pelos dados (5); durante o treino o conjunto pode acrescentar até 2 ao máximo. Não muda desafios, dificuldade, progresso ou conteúdo educacional.

- Explorador: velocidade de caminhada e corrida ×1,12.
- Guardião: +2 de Energia máxima; elmo aberto: +1, limitado a +2 no conjunto.
- Estudioso: +0,35 unidade de alcance de interação; diadema: +0,15, limitado a +0,5.
- Traje original e nenhum capacete: sem bônus.

Os valores derivam da base e de um único conjunto normalizado. Reaplicar ou trocar a interface não acumula efeitos. Aumentar o máximo de Energia não cura a sessão em andamento; reduzir o máximo apenas limita a Energia atual. Um novo treino começa cheio, como antes. O alcance afeta interações com Guia, álbum e portal, não o alcance de ataque ao Boneco.

`ProfileSave.Journey` é opcional para compatibilidade com saves antigos. Guarda variante e conjunto, incluindo cores HTML. Campos inválidos retornam aos valores seguros. A gravação usa a transação existente do SaveSession; erro de escrita preserva o estado anterior.

## Controles

WASD/setas movem, Shift corre, clique caminha, E interage e F escolhe o Boneco. Roda do mouse, PageUp/PageDown, Home e gesto de pinça mantêm o zoom existente. F8 mantém a comparação de capa. O diagnóstico textual da capa só aparece com `--cape-diagnostics`. No diálogo, Enter revela a fala e, depois de completa, avança; ouvir novamente e sair continuam disponíveis.

## Arte e limites

Os acessórios são peças procedurais presas à cabeça, coluna e braços, com acabamento simplificado. As cores alteram os acessórios novos; rosto, texturas, tecido e capa originais não recebem tintura global. Não são uma substituição completa da armadura original do Spectrum.

Cristais, pequenos livros e estandartes ocupam quatro células de árvores já bloqueadas. A revisão v02 acrescenta quatro canteiros com 12 pedras arredondadas, 20 volumes de folhagem e oito flores (40 pétalas). A decoração completa tem 60 meshes, 4.800 triângulos e seis materiais compartilhados; esses valores corrigem a estimativa inicial. Somente o grupo do caminho ativo e os canteiros aparecem: 4.272/4.464/4.368/4.368 triângulos respectivamente. Sem novas colisões ou luzes. Reduzir movimento interrompe o balanço; reduzir efeitos remove emissão. Guia reconstruído v05, portal pintado com partículas, vegetação e caminhos anteriores foram preservados.

Nas novas interfaces, rótulos 3D usam 55% do tamanho anterior para liberar a visão do cenário; Base restaura o tamanho original sem acumulação. As oficinas rolam quando necessário e mantêm Aplicar/Cancelar acessíveis. A revisão v02 também adapta configurações, perfis, preparação e painéis de treino aos quatro temas, usando componentes comuns e preservando a lógica. O treino ativo oculta o HUD de navegação, mantém a Energia no canto e o painel de resposta à direita. A rolagem substitui o encolhimento dos controles. As legendas das ilhas futuras ficam ocultas nas novas interfaces; o Boneco exibe uma indicação contextual ou somente o escudo. Base restaura os elementos legados.

## Preservação e verificação

Backup anterior ao trabalho: `D:\arquipelago-do-ceu-assets\backups\antes-quatro-caminhos-20261014-011048`. São 659 arquivos, 351.141.481 bytes, incluindo o save real e um cache Vulkan da pasta de usuário, hashes SHA-256 conferidos e bundle Git verificado. O hash do save real permaneceu `b58edffe1443c528881c8149474963d53819437ca2ccf38abcbacf116f15dee3` após os testes. Nada foi enviado, commitado ou comprado. Spectrum é fonte somente leitura.

Build .NET sem erros/avisos e 382 testes passaram, incluindo normalização, saves antigos, reload, dez ciclos de variantes, rollback de escrita e mudanças de Energia sem cura. QA visual usa processos fora da tela e saves descartáveis; evidências finais são registradas junto à entrega local.

A rodada final V5 passou 225 verificações nas três resoluções, após compensar o stretch do Godot para manter fonte mínima de cerca de 16 pixels físicos e botões de 44 pixels. Rodadas integrais anteriores validaram dez ciclos de troca por resolução. Capturas finais: `quatro-caminhos-20261014-v01/validacao/integration-v5-RESOLUCAO`. O piloto verifica mouse na abertura da oficina e teclado no cancelamento/diálogo; outras ações usam os sinais reais dos botões. Energia e alcance não foram medidos em combate pelo piloto gráfico; Energia tem testes de domínio e o alcance está ligado ao InteractionFinder no Main.

## Capa aprovada do Spectrum

A fonte `E:\Spectrum\art\motion-library\knight\cape-body-v2\final\knight-cape-body.blend` tem aprovação explícita registrada em `publication.json`, de 14/10/2026. SHA-256 verificado e preservado: `36383e1b77c92003cedcac6ffa5735dddbf24517f6db38a105ec65f144c74923`.

A transferência final está em `D:\arquipelago-do-ceu-assets\spectrum-cape-body-v2-transfer\ready-v03`. As tentativas anteriores permanecem guardadas e não são usadas. Três GLBs novos (parado, andando, correndo) preservam a espessura de 5 mm e 118 estados de morph usados pelos movimentos: 105.204 triângulos, 10 materiais, 24 ossos, cerca de 36,45 MB por arquivo. Reimportação geométrica: erro inferior a 9×10⁻⁸ m; destinos de animação resolvidos no modelo base. Os arquivos antigos permanecem intactos.

A capa pré-calculada aprovada é o padrão. F8 compara com o solver experimental anterior, ocultando a outra malha; não há duas capas visíveis simultaneamente. Reduzir movimento usa a capa pré-calculada parada. A aprovação da fonte não equivale a aprovação artística dos novos acessórios ou de toda combinação possível entre movimentos; as capturas locais permitem essa revisão. O tamanho dos GLBs ainda merece otimização futura.

A dobra forte da capa na corrida foi comparada visualmente: quadros 11/21 do Blender original correspondem aos ticks 10/20 no Godot e apresentam a mesma silhueta. Ela pertence à animação aprovada, não à transferência. Dez ciclos de transição (240 quadros) mantiveram soma dos morphs em 1, com erro máximo de 5,59×10⁻⁸. F8 e redução de movimento confirmaram uma única capa visível.


## Revisão visual v02: o que é novo e o que foi preservado

| Elemento | Trabalho nesta noite | Movimento |
|---|---|---|
| Cristais decorativos | Dois no Essencial; quatro principais e quatro satélites em Cristais | Flutuação dos principais; satélites estáticos |
| Livros decorativos | Quatro no caminho Livro | Eram estáticos no primeiro marco; agora oito páginas inteiras oscilam suavemente em dobradiças na lombada |
| Estandartes | Quatro no caminho Aventura | Balanço suave, com emblema acompanhando a bandeira |
| Canteiros | Quatro novos; 12 pedras, 20 volumes de folhagem, oito flores | Folhagem e flores ao vento; pedras estáticas |
| Guia reconstruído v05 | Já existia e foi preservado | Respiração, olhar, alongamento e bocejo existentes |
| Portal pintado e partículas | Já existiam e foram preservados | Vórtice/pulso/partículas existentes |
| Grama de três pontas, árvores, terreno, ponte e torres | Já existiam e foram preservados | Nenhuma rotação arbitrária de engrenagens fundidas |

As referências locais inspecionadas foram `05-chao-ilha-lobby.jpg` e `30-chao-hexagonal-vivo.jpg`. Os canteiros são uma interpretação procedural da paleta e dos volumes arredondados; não representam implantação de toda a folha de concept. Três aparecem claramente na vista ampla; o quarto fica parcialmente atrás do portal. Detalhes têm capturas aproximadas. Não há novos colliders, luzes ou partículas.

Os canteiros ficam nos hexágonos bloqueados (-4,0), (-2,3), (1,-4), (4,-3), com limites conservadores incluindo vento |x|≤0,32 e |z|≤0,80. Base oculta toda a decoração nova. Reduzir movimento retorna os elementos à pose de repouso; reduzir efeitos desliga a emissão opcional dos cristais.

As imagens reais de 1280×720 e 1920×1080 cobrem HUD, configurações, acessibilidade, perfis, oficina, diálogo, preparação e treino ativo nos quatro temas. A revisão corrigiu sobreposição do seletor com o treino, fontes/botões encolhidos, faces invertidas e conversão de cor dos canteiros. Os painéis ativos mantêm botões de 44 pixels e texto de pelo menos 16 pixels nas resoluções verificadas. Traçado de letras ou texto ampliado podem exigir rolagem.

Backup adicional antes desta revisão: `D:\arquipelago-do-ceu-assets\backups\antes-quatro-caminhos-20261014-013642`: 700 arquivos, 526.039.094 bytes, SHA-256 de cada cópia e bundle Git verificados. O primeiro marco e seus previews continuam em `quatro-caminhos-20261014-v01`.

### Identificação exata da capa em uso

- Aprovação: `E:\Spectrum\art\motion-library\knight\cape-body-v2\publication.json`, declaração “aprovado” de 14/10/2026.
- Fonte: `E:\Spectrum\art\motion-library\knight\cape-body-v2\final\knight-cape-body.blend`.
- Transferência: `D:\arquipelago-do-ceu-assets\spectrum-cape-body-v2-transfer\ready-v03`.
- Runtime: `D:\arquipelago-do-ceu\game\assets\modelos\spectrum-cape-body-v2\guerreiro-{parado,andando,correndo}.glb`.
- QA: `reimport-validation.json`, `godot-validation.json` e `source-godot-comparison.json` em `ready-v03`; `equipment_integration_validation.json` na validação da entrega v01.
- Fonte SHA-256: `36383e1b77c92003cedcac6ffa5735dddbf24517f6db38a105ec65f144c74923`.

Sem gastos, instalações, uploads externos, commit ou push. Saves reais não foram usados pelos pilotos; as capturas usam perfis descartáveis. Os acessórios continuam sendo aproximações procedurais, e as ilhas futuras continuam com construções provisórias.
