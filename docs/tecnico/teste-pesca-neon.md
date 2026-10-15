# Testar a entrega de personagens, Lobby e pesca neon

Esta entrega reúne o estado visual aprovado, equipamentos, HUD Livro, portal/NPC e o modo de teste de pesca creme/dourado. Não é a integração definitiva da pesca à progressão da campanha.

## Preparar um checkout limpo

Ambiente verificado no Windows: Godot **4.7.2 .NET** (`4.7.2.stable.mono.official.ed1daf0bf`) e .NET SDK **8.0.422**. Instale a edição .NET do Godot e ajuste `GODOT` para o executável de console da sua instalação. Não há dependência do caminho da máquina do autor.

Na raiz do checkout, execute no PowerShell:

```powershell
dotnet test game.tests
dotnet build game/ArquipelagoDoCeu.csproj
& $env:GODOT --headless --path game --editor --import
$qa = Join-Path $env:TEMP ('arquipelago-qa-' + [guid]::NewGuid())
New-Item -ItemType Directory $qa | Out-Null
& $env:GODOT --path game -- "--save=$qa/save.json"
```

A primeira importação leva alguns minutos: modelos, animações de capa e texturas estão no Git, com os respectivos `.import` e `.uid`. O cache `.godot` é recriado localmente. Não é necessário Git LFS para esta entrega. O build copia automaticamente `content/` para o projeto Godot.

## Roteiro manual

1. Use F9 ou **Pescar com puxada** para entrar no modo de teste.
2. Clique **B**, **O**, **A**, aguardando cada ciclo completo. Os atalhos correspondentes são 2, 3, 1.
3. Observe idle contínuo, captura com a mão esquerda, golpe de espada, recuperação e retorno ao idle. As três variantes usam os ataques aprovados `Sword_Regular_A`, `Sword_Regular_B` e `Sword_Regular_C`; A/B incluem suas recuperações.
4. Clique **Conferir**. Segure e arraste no guia do **B luminoso**; pode soltar entre traços. Um clique parado não conclui o desenho.
5. Use Esc durante o desenho, volte a Conferir e confira que o traço reinicia. Outro Esc retorna ao jogo; F9 permite reentrar.
6. Confira também Esc durante captura/golpe, ausência de fila ao clicar repetidamente, restauração de câmera/UI/pausa e as opções de reduzir brilho/movimento e letras/sílabas.
7. No Lobby, confira personagem/capa, portal/NPC, HUD Livro e equipamentos. O save temporário mantém seu progresso pessoal separado.

## QA automatizado no motor

Com `$qa` criado acima:

```powershell
& $env:GODOT --path game -- --pull-qa "--pull-out=$qa" "--save=$qa/qa-save.json"
& $env:GODOT --headless --path game -- --driver=e2e-1 "--save=$qa/e2e-save.json"
& $env:GODOT --headless --path game -- --driver=e2e-2 "--save=$qa/e2e-save.json"
```

O primeiro comando precisa de janela/renderização e grava PNGs. O sucesso é indicado por `LETTERS_QA_ALL_PASS` e saída 0. Ele exercita F9, cliques nas letras/Conferir, bloqueio de entradas durante o ciclo, arrasto, cancelamento, reentrada, modo de sílabas e resoluções 1280/1920. Os dois últimos comandos verificam o fluxo de jogo e persistência em processos separados.

## Assets e limites

- Personagens Spectrum, capa, modelos do Lobby/torres/landmarks, portal e texturas estão sob `game/assets/` e `game/Lobby/`.
- O personagem de pesca, os três ataques e as texturas de efeitos estão em `game/Dev/PullTest/`. Variantes anteriores presentes no projeto foram preservadas para não perder o trabalho acumulado.
- A capa do QTA usa animação pré-calculada aprovada. Esta entrega não adiciona uma nova simulação de tecido nem refaz mão, rosto ou ataques.
- BOA/B é um modo de teste; não representa conteúdo completo de campanha. A demonstração anterior não tem áudio e não faz parte dos arquivos publicados.
- Saves pessoais, vídeos brutos, fontes externas de Blender, backups e caches não são necessários para executar e não entram no PR.

Na importação inicial foram observadas mensagens `UVs are required to generate tangents` em malhas sem UV. A importação concluiu e o QA em janela passou; as malhas aprovadas não foram alteradas para silenciar essas mensagens.
