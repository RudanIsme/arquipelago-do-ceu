import { NodeIO } from '@gltf-transform/core';
import { prune, textureCompress } from '@gltf-transform/functions';
import sharp from 'sharp';
import fs from 'node:fs';
import path from 'node:path';

const [, , entrada, saida] = process.argv;
const io = new NodeIO();
fs.mkdirSync(saida, { recursive: true });

function triangulos(doc) {
  let n = 0;
  for (const mesh of doc.getRoot().listMeshes())
    for (const prim of mesh.listPrimitives()) {
      const idx = prim.getIndices();
      n += idx ? idx.getCount() / 3 : prim.getAttribute('POSITION').getCount() / 3;
    }
  return Math.round(n);
}

for (const arq of fs.readdirSync(entrada).filter((f) => f.endsWith('.glb')).sort()) {
  const doc = await io.read(path.join(entrada, arq));
  const antes = triangulos(doc);
  const tamAntes = fs.statSync(path.join(entrada, arq)).size;

  // O estilo é pintado à mão: só a cor base é usada. Sem normal, metálico, rugosidade nem emissão.
  for (const mat of doc.getRoot().listMaterials()) {
    mat.setNormalTexture(null);
    mat.setMetallicRoughnessTexture(null);
    mat.setOcclusionTexture(null);
    mat.setEmissiveTexture(null);
    mat.setMetallicFactor(0);
    mat.setRoughnessFactor(1);
  }
  await doc.transform(
    prune(),
    textureCompress({ encoder: sharp, targetFormat: 'jpeg', resize: [1024, 1024], quality: 85 }),
  );
  const depois = triangulos(doc);
  await io.write(path.join(saida, arq), doc);
  const tamDepois = fs.statSync(path.join(saida, arq)).size;
  const texturas = doc.getRoot().listTextures().map((t) => t.getSize().join('x')).join(', ');
  const ossos = doc.getRoot().listSkins().reduce((s, k) => s + k.listJoints().length, 0);
  const animacoes = doc.getRoot().listAnimations().length;
  console.log(
    `${arq.padEnd(26)} tri ${antes}->${depois}${antes === depois ? '' : '  !!! MUDOU'} | ${(tamAntes / 1048576).toFixed(1)} MB -> ${(tamDepois / 1024).toFixed(0)} KB | tex ${texturas} | ossos ${ossos} | animações ${animacoes}`,
  );
}
