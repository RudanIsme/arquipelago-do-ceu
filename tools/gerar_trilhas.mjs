// Gera game/data/trilhas.json (números, sinais e letras, na ordem de escrita); cada traço são pontos em 0..1 dentro do quadrado do glifo.
// Uso: node tools/gerar_trilhas.mjs
import { writeFileSync } from 'node:fs';

const ARC_STEP = 12;

// Elipse em graus de tela: 0 é a direita, 90 é embaixo, -90 é em cima. Ângulo crescente gira no sentido horário.
function arc(cx, cy, rx, ry, from, to) {
	const steps = Math.max(2, Math.ceil(Math.abs(to - from) / ARC_STEP));
	return Array.from({ length: steps + 1 }, (_, i) => {
		const t = ((from + ((to - from) * i) / steps) * Math.PI) / 180;
		return [cx + rx * Math.cos(t), cy + ry * Math.sin(t)];
	});
}

// Curva suave (quadrática) de a até b, puxada para c.
function curve(a, c, b, steps = 8) {
	return Array.from({ length: steps + 1 }, (_, i) => {
		const t = i / steps;
		const u = 1 - t;
		return [u * u * a[0] + 2 * u * t * c[0] + t * t * b[0], u * u * a[1] + 2 * u * t * c[1] + t * t * b[1]];
	});
}

const join = (...parts) => parts.flat(1);
const line = (...points) => points;

const glyphs = {
	'0': [arc(0.5, 0.5, 0.25, 0.42, -90, -450)],
	'1': [line([0.35, 0.28], [0.52, 0.1], [0.52, 0.9])],
	'2': [join(arc(0.5, 0.32, 0.25, 0.22, 200, 395), line([0.22, 0.9], [0.8, 0.9]))],
	'3': [join(arc(0.5, 0.3, 0.24, 0.2, 200, 450), arc(0.5, 0.7, 0.27, 0.2, 270, 520))],
	'4': [line([0.62, 0.1], [0.2, 0.65], [0.82, 0.65]), line([0.62, 0.1], [0.62, 0.9])],
	'5': [join(line([0.75, 0.1], [0.3, 0.1], [0.27, 0.46]), arc(0.5, 0.66, 0.27, 0.24, -135, 140))],
	'6': [join(curve([0.7, 0.12], [0.22, 0.3], [0.25, 0.66]), arc(0.5, 0.66, 0.25, 0.24, 180, -180))],
	'7': [line([0.22, 0.12], [0.78, 0.12], [0.42, 0.9])],
	'8': [arc(0.5, 0.29, 0.21, 0.19, -90, -450), arc(0.5, 0.7, 0.25, 0.21, -90, -450)],
	'9': [join(arc(0.5, 0.34, 0.25, 0.24, 0, -360), line([0.75, 0.34], [0.7, 0.7], [0.45, 0.9]))],
	'+': [line([0.5, 0.22], [0.5, 0.78]), line([0.22, 0.5], [0.78, 0.5])],
	'-': [line([0.2, 0.5], [0.8, 0.5])],

	A: [line([0.5, 0.1], [0.2, 0.9]), line([0.5, 0.1], [0.8, 0.9]), line([0.32, 0.58], [0.68, 0.58])],
	B: [line([0.28, 0.1], [0.28, 0.9]), join(line([0.28, 0.1], [0.5, 0.1]), arc(0.5, 0.3, 0.22, 0.2, -90, 90), line([0.28, 0.5])), join(line([0.28, 0.5], [0.55, 0.5]), arc(0.55, 0.7, 0.25, 0.2, -90, 90), line([0.28, 0.9]))],
	C: [arc(0.55, 0.5, 0.3, 0.4, -40, -320)],
	D: [line([0.27, 0.1], [0.27, 0.9]), join(line([0.27, 0.1], [0.45, 0.1]), arc(0.45, 0.5, 0.33, 0.4, -90, 90), line([0.27, 0.9]))],
	E: [line([0.28, 0.1], [0.28, 0.9]), line([0.28, 0.1], [0.78, 0.1]), line([0.28, 0.5], [0.7, 0.5]), line([0.28, 0.9], [0.78, 0.9])],
	F: [line([0.28, 0.1], [0.28, 0.9]), line([0.28, 0.1], [0.78, 0.1]), line([0.28, 0.5], [0.7, 0.5])],
	G: [join(arc(0.52, 0.5, 0.3, 0.4, -40, -360), line([0.55, 0.5]))],
	H: [line([0.27, 0.1], [0.27, 0.9]), line([0.73, 0.1], [0.73, 0.9]), line([0.27, 0.5], [0.73, 0.5])],
	I: [line([0.5, 0.1], [0.5, 0.9])],
	J: [join(line([0.65, 0.1], [0.65, 0.65]), arc(0.45, 0.65, 0.2, 0.25, 0, 180))],
	K: [line([0.28, 0.1], [0.28, 0.9]), line([0.75, 0.1], [0.28, 0.55]), line([0.42, 0.43], [0.77, 0.9])],
	L: [line([0.28, 0.1], [0.28, 0.9], [0.78, 0.9])],
	M: [line([0.22, 0.1], [0.22, 0.9]), line([0.22, 0.1], [0.5, 0.62]), line([0.5, 0.62], [0.78, 0.1]), line([0.78, 0.1], [0.78, 0.9])],
	N: [line([0.25, 0.1], [0.25, 0.9]), line([0.25, 0.1], [0.75, 0.9]), line([0.75, 0.9], [0.75, 0.1])],
	O: [arc(0.5, 0.5, 0.3, 0.4, -90, -450)],
	P: [line([0.28, 0.1], [0.28, 0.9]), join(line([0.28, 0.1], [0.5, 0.1]), arc(0.5, 0.3, 0.26, 0.2, -90, 90), line([0.28, 0.5]))],
	Q: [arc(0.5, 0.5, 0.3, 0.4, -90, -450), line([0.55, 0.65], [0.8, 0.92])],
	R: [line([0.28, 0.1], [0.28, 0.9]), join(line([0.28, 0.1], [0.5, 0.1]), arc(0.5, 0.3, 0.26, 0.2, -90, 90), line([0.28, 0.5])), line([0.45, 0.5], [0.75, 0.9])],
	S: [join(arc(0.5, 0.3, 0.27, 0.2, -20, -270), arc(0.5, 0.7, 0.27, 0.2, -90, 160))],
	T: [line([0.2, 0.1], [0.8, 0.1]), line([0.5, 0.1], [0.5, 0.9])],
	U: [join(line([0.25, 0.1], [0.25, 0.62]), arc(0.5, 0.62, 0.25, 0.28, 180, 0), line([0.75, 0.1]))],
	V: [line([0.2, 0.1], [0.5, 0.9], [0.8, 0.1])],
	W: [line([0.15, 0.1], [0.32, 0.9], [0.5, 0.35], [0.68, 0.9], [0.85, 0.1])],
	X: [line([0.22, 0.1], [0.78, 0.9]), line([0.78, 0.1], [0.22, 0.9])],
	Y: [line([0.22, 0.1], [0.5, 0.5]), line([0.78, 0.1], [0.5, 0.5], [0.5, 0.9])],
	Z: [line([0.22, 0.1], [0.78, 0.1], [0.22, 0.9], [0.78, 0.9])],
};

const round = (n) => Math.round(Math.min(1, Math.max(0, n)) * 1000) / 1000;
const stroke = (points) => `[${points.map(([x, y]) => `[${round(x)},${round(y)}]`).join(',')}]`;
const entries = Object.entries(glyphs).map(([glyph, strokes]) => `    { "glyph": "${glyph}", "strokes": [\n      ${strokes.map(stroke).join(',\n      ')}\n    ] }`);

writeFileSync('game/data/trilhas.json', `{\n  "version": 1,\n  "glyphs": [\n${entries.join(',\n')}\n  ]\n}\n`);
console.log(`${entries.length} trilhas gravadas em game/data/trilhas.json`);
