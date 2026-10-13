# Otimizar modelos 3D

Reduz os `.glb` gerados no Meshy ao que o jogo usa: **só a cor base, a 1024 px em JPEG**. Remove as texturas de normal, metálico, rugosidade e emissão, apaga o que ficou sem uso e **não mexe na malha**, no rig nem nas animações. Mostra os triângulos antes e depois, que devem ser iguais.

```text
cd tools/otimizar-modelos
npm install
node otimizar.mjs <pasta com os .glb> <pasta de saída>
```

Os limites de triângulos de cada tipo de asset estão em `docs/arte-e-ux/guia-de-estilo-visual.md`, seção 8.
