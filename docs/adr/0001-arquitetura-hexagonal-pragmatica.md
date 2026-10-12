# Arquitetura hexagonal com DDD pragmático

As regras do jogo (Energia, Conceito, Missão, avaliador, PCG32, Leitner) ficam em C# puro, sem referência ao Godot, e falam com o mundo por portas (interfaces) que adaptadores implementam: Godot, disco, voz, rede. Escolhemos isso porque o jogo precisa rodar offline com rede, voz e save que falham, e porque as regras precisam de teste rápido sem abrir o motor. A alternativa, regras dentro dos nós do Godot, é mais curta no começo, mas só testa com o editor aberto e mistura regra com cena.

"Pragmático" significa o mínimo de cerimônia: poucos arquivos, interface só numa fronteira real (disco, voz, rede, relógio, aleatoriedade) e tipos de domínio só para os termos do `GLOSSARY.md`. O detalhe e as regras de dependência estão em `docs/tecnico/documento-tecnico.md`, seção "Arquitetura do código".
