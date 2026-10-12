# BNCC: Matemática, Língua Portuguesa e Computação, 1º ao 3º ano

Referência de consulta para o jogo, **só com transcrição da BNCC** (sem opinião). Uma pasta por ano e um arquivo por matéria:

| Ano | Matemática | Língua Portuguesa | Computação (complemento 2022) |
| --- | --- | --- | --- |
| 1º | [matematica.md](1-ano/matematica.md) | [lingua-portuguesa.md](1-ano/lingua-portuguesa.md) | [computacao.md](1-ano/computacao.md) |
| 2º | [matematica.md](2-ano/matematica.md) | [lingua-portuguesa.md](2-ano/lingua-portuguesa.md) | [computacao.md](2-ano/computacao.md) |
| 3º | [matematica.md](3-ano/matematica.md) | [lingua-portuguesa.md](3-ano/lingua-portuguesa.md) | [computacao.md](3-ano/computacao.md) |

A ordem recomendada e o mapeamento para as mecânicas **não estão aqui**: são uma recomendação provisória, que depende de validação profissional, em [docs/pedagogia/ordem-recomendada-mvp.md](../../pedagogia/ordem-recomendada-mvp.md).

## Versão consultada e atualização (leia antes de usar)

- O texto vem do PDF oficial **"BNCC – Educação Infantil e Ensino Fundamental"**, versão final do MEC (arquivo `BNCC_EI_EF_110518_versaofinal_site.pdf`, de 2018). Esse é o documento-base; **"2018" é a data do documento, não garantia de que seja a versão vigente em 2026.**
- **Atualização mais recente confirmada em fonte primária: 2022, o complemento Computação** (acesso em 2026-10-12). A página "Histórico da BNCC" do MEC (fonte 2) registra: "A BNCC Computação foi oficialmente introduzida por meio do Parecer CNE/CEB nº 2/2022 em 17 de fevereiro de 2022", e a Resolução CNE/CEB nº 1/2022, de 4 de outubro de 2022, reforçou essas normas. O anexo oficial (fonte 3) foi baixado e transcrito nos arquivos `computacao.md`.
  - **O que mudou:** a BNCC ganhou o complemento **Computação** para toda a Educação Básica (Educação Infantil, EF 1º ao 9º e Ensino Médio), com eixos Pensamento computacional, Mundo digital e Cultura digital. Para o 1º ao 3º ano: 7 + 6 + 9 habilidades específicas (`EF01CO`, `EF02CO`, `EF03CO`) e 9 compartilhadas do 1º ao 5º ano (`EF15CO01` a `EF15CO09`).
  - **O que não mudou (verificado):** o anexo de 2022 tem 2.785 linhas de texto extraído e **nenhum código de Matemática ou Língua Portuguesa** (busca por `EFnnMA`/`EFnnLP` retornou 0). Ele é aditivo (complemento), então não traz texto novo para MA/LP. Os arquivos `matematica.md` e `lingua-portuguesa.md` **não foram alterados**.
  - **Limite dessa conclusão:** não encontrei no site do MEC (fonte 2; a página inicial do site da BNCC continua apontando para o PDF de 2018) nenhum documento da BNCC consolidado mais recente que o de 2018, nem uma versão com MA/LP revisados. Isso é ausência de evidência no que consegui abrir, não prova; não comparei o PDF de 2018 com nenhuma versão posterior porque não achei nenhuma.
  - **Resolução CNE/CEB nº 1/2022 (texto do ato) NÃO verificada em fonte primária:** o PDF em `www.gov.br/mec/pt-br/cne/pdf/resolucoes-do-cne/ceb/2022/rceb001_22.pdf` devolveu uma página anti-robô (captcha) a `curl` e ao `WebFetch`; não contornei. `portal.mec.gov.br` e `normativasconselhos.mec.gov.br` não responderam (sem conexão) e o link do DOU testado deu 404. O que sei do conteúdo do ato (normas de Computação como complemento da BNCC; currículos devem considerar as tabelas anexas; implementação em até 1 ano pelos sistemas; vigência em 1/11/2022) veio de **trechos de resultados de busca**, não de leitura do documento; trate como indicativo. A data de publicação no DOU (6/10/2022 vs. 5/10/2022) está **inconsistente entre fontes e não foi verificada**.
  - **Atos de 2024/2025 citados por documentos municipais: NÃO confirmados em fonte primária.** Pelos resultados de busca (fontes secundárias, não abertas por mim): a Resolução CNE/CEB nº 2/2024 (13/11/2024) existe e trata das Diretrizes Curriculares do **Ensino Médio**, ou seja, fora do escopo do EF anos iniciais; uma "Resolução CNE/CEB nº 1/2024 (17/10/2024)" sobre Computação como área do conhecimento aparece só em documento municipal; "Resolução CNE/CP nº 22/2024" aparece só em ata estadual e conflita com a anterior (provável erro de citação; a BNCC original é a Resolução CNE/CP nº 2, de 22/12/2017); "CNE/CEB nº 2/2025" não apareceu em nenhuma fonte. **Nada disso foi aberto no CNE/DOU**; se importar, um humano deve conferir no CNE (gov.br/mec/pt-br/cne) e no DOU.
- Currículos de estados e municípios têm adaptações próprias; não foram pesquisados.

## Como ler

- **Transcrição (fonte primária):** códigos e textos das habilidades foram extraídos por máquina (`pdftotext`) do PDF oficial ; Matemática foi conferida entre duas extrações do mesmo PDF (layout vs. fluxo); LP foi conferida por contagem e sequência de códigos (ver "Limitações"). Não houve paráfrase nem código completado de memória. Os textos mantêm a redação da BNCC (incluindo "estruturas silábicas", "ordens" etc.).
- **Atenção sobre os códigos de Língua Portuguesa:** na BNCC, as habilidades de LP dos anos iniciais **não têm código só por ano**. Há três famílias que valem para os anos do projeto:
  - `EF01LP`, `EF02LP`, `EF03LP`: específicas de um ano;
  - `EF12LP`: valem para o **1º e o 2º ano**;
  - `EF15LP`: valem do **1º ao 5º ano**;
  - `EF35LP`: valem do **3º ao 5º ano**.
  O campo `bncc` das Missões deve aceitar esses prefixos (ex.: `EF12LP03`, `EF35LP13`), e não apenas `EF0xLP`.
- **Coluna de Matemática "Unidade temática":** o PDF apresenta a tabela de objetos de conhecimento por unidade e, separadamente, a lista de habilidades (sem rótulo de unidade por habilidade). A unidade de cada habilidade foi **atribuída pela ordem dos objetos de conhecimento** na tabela do próprio PDF (ver "Limitações"). Marcadas com `(*)`: Sistema monetário, cuja posição entre "Grandezas e medidas" e "Probabilidade e estatística" não ficou inequívoca na extração de texto; atribuí a "Grandezas e medidas" (estrutura usual da BNCC; **não confirmado visualmente** no PDF).
- **Colunas de Língua Portuguesa:** o PDF separa a tabela (práticas de linguagem / objetos de conhecimento por campo de atuação) da lista de habilidades. As colunas "Campo(s) de atuação" e "Prática(s) de linguagem" listam o que consta na **tabela da mesma página** da habilidade; quando uma página mistura mais de um campo ou prática, aparecem todos e a correspondência linha a linha **não foi verificada**. As habilidades de Análise linguística/semiótica (Alfabetização e Ortografização) estão em páginas de uma única prática e, portanto, são inequívocas. Eixos de LP: Leitura/escuta, Escrita (produção de textos), Oralidade e Análise linguística/semiótica.

## Contagem conferida (confere com as tabelas dos arquivos desta pasta)

| Área | 1º ano | 2º ano | 3º ano |
|---|---|---|---|
| Matemática (códigos EFnnMA) | 22 (EF01MA01 a EF01MA22) | 23 (EF02MA01 a EF02MA23) | 28 (EF03MA01 a EF03MA28) |
| LP – específicas do ano | 26 (EF01LP01 a EF01LP26) | 29 (EF02LP01 a EF02LP29) | 27 (EF03LP01 a EF03LP27) |
| LP – compartilhadas aplicáveis | EF12LP (19) + EF15LP (19) = 38 | EF12LP (19) + EF15LP (19) = 38 | EF15LP (19) + EF35LP (31) = 50 |
| LP – total aplicável ao ano | 64 | 67 | 77 |
| Computação – específicas do ano (códigos EFnnCO) | 7 (EF01CO01 a EF01CO07) | 6 (EF02CO01 a EF02CO06) | 9 (EF03CO01 a EF03CO09) |
| Computação – compartilhadas aplicáveis | EF15CO (9) | EF15CO (9) | EF15CO (9) |
| Computação – total aplicável ao ano | 16 | 15 | 18 |

Conferência: a sequência numérica de cada família foi checada sem lacunas no PDF (EF01LP01-26, EF02LP01-29, EF03LP01-27, EF12LP01-19, EF15LP01-19, EF35LP01-31; MA: 01-22, 01-23, 01-28). Total de códigos únicos de LP listados: 151. Computação: 22 códigos específicos (1º-3º) + 9 `EF15CO` = 31 códigos únicos, sequências EF01CO01-07, EF02CO01-06, EF03CO01-09 e EF15CO01-09 sem lacunas (conferidas no texto extraído do anexo).

## Fontes

| # | Fonte (primária) | URL | Data de acesso |
|---|---|---|---|
| 1 | BNCC, Educação Infantil e Ensino Fundamental, versão final (MEC), arquivo `BNCC_EI_EF_110518_versaofinal_site.pdf`. Matemática: tabelas e habilidades a partir da p. 278 do documento; Língua Portuguesa 1º ao 5º ano: a partir da p. 94 | `http://basenacionalcomum.mec.gov.br/images/BNCC_EI_EF_110518_versaofinal_site.pdf` (redireciona para `https://cdn.mec.gov.br/basenacionalcomum.mec.gov.br/images/BNCC_EI_EF_110518_versaofinal_site.pdf`) | 2026-10-12 |
| 2 | MEC, "Histórico da BNCC" (linha do tempo; registra o Parecer CNE/CEB nº 2/2022 e a Resolução CNE/CEB nº 1/2022 e linka o anexo) | `https://basenacionalcomum.mec.gov.br/historico/` | 2026-10-12 |
| 3 | Anexo ao Parecer CNE/CEB nº 2/2022, BNCC Computação (arquivo `anexo_parecer_cneceb_n_2_2022_bncc_computacao.pdf`, 1.426.590 bytes, SHA-256 `60aab0b192acf867bc582dc06678cd8f3bc597e8f1b65256fc7bf04190bb793d`). Computação 1º ano p. 12-15, 2º ano p. 16-17, 3º ano p. 18-23 (numeração do documento); `EF15CO` na tabela "Por etapa – 1º ao 5º ano", p. 34-37 | `https://basenacionalcomum.mec.gov.br/images/historico/anexo_parecer_cneceb_n_2_2022_bncc_computacao.pdf` | 2026-10-12 |

Procedimento: arquivo baixado com `curl` (HTTP 200, 3.058.960 bytes) para um diretório temporário, convertido em texto com `pdftotext` (duas extrações: layout e fluxo); nada do conteúdo foi executado. O PDF acusa "xref table danificada", mas o `pdftotext` o reconstruiu e o texto saiu íntegro (sem caracteres de substituição).


Computação (fonte 3): baixada com `curl` (HTTP 200, `application/pdf`) para diretório temporário novo, convertida com `pdftotext -enc UTF-8` (modos fluxo e layout; as linhas das habilidades vêm do modo fluxo e eixo/objeto do layout); nada foi executado. Cada habilidade foi extraída por padrão de código, sem completar nada de memória; contagem e sequência conferidas. Fontes secundárias (buscas) foram usadas só para apontar onde procurar e para os itens marcados "não verificado" acima.
Nenhuma fonte secundária foi usada para códigos ou textos. A busca na web só devolveu portais de planos de aula e um rascunho de consulta pública hospedado por prefeitura; ambos foram descartados.

## Limitações

- **Unidade temática (Matemática):** atribuída pela ordem dos objetos de conhecimento nas tabelas do PDF; a lista de habilidades não traz rótulo de unidade. A posição de "Sistema monetário" (marcada com `(*)`) é a única ambígua. Não renderizei as páginas como imagem para confirmar (sem renderizador de PDF no ambiente).
- **Campo de atuação e prática de linguagem (LP):** são os da tabela da mesma página; nas páginas que misturam campos ou práticas, a correspondência linha a linha **não está verificada**. Confira no PDF antes de usar em lógica do jogo.
- **Extração de texto:** o PDF usa colunas por ano (1º e 2º lado a lado). A Matemática foi conferida entre as duas extrações (diferenças só em número de página, removido). Para LP usei a extração em fluxo, porque a de layout mistura colunas; foi conferida por contagem e sequência de códigos e por leitura de amostra, sem segunda extração completa.
- **Computação, objeto de conhecimento `(~)`:** na tabela do anexo, alguns objetos aparecem como uma célula única entre linhas de habilidades (EF01CO02/03 "Conceituação de Algoritmos"; EF01CO04/05 e EF03CO04/05 "Codificação da informação"; EF03CO07/08 "Uso de tecnologias computacionais"). Atribuí o objeto a todas as habilidades adjacentes pela posição no layout; **não confirmado visualmente**. O eixo de cada habilidade vem do mesmo layout. O anexo também traz "explicação da habilidade" e "exemplos", que não foram transcritos.
- **Computação, escopo:** só 1º ao 3º ano mais `EF15CO`. Educação Infantil, 4º ao 9º e Ensino Médio do anexo não foram transcritos.
