// Gera as Missões da Mecânica "Pagar e dar troco" em content/missions/sistema-monetario (BNCC EF01MA19, EF02MA20 e EF03MA24).
// Uso: node tools/gerar_missoes_dinheiro.mjs
import { mkdirSync, writeFileSync } from 'node:fs';

const real = (cents) => `R$ ${Math.floor(cents / 100)},${String(cents % 100).padStart(2, '0')}`;

const years = [
	{ year: 1, bncc: 'EF01MA19', pieces: [50, 100, 200, 500], tasks: [['pagar', 100], ['pagar', 150], ['pagar', 250], ['pagar', 300]] },
	{ year: 2, bncc: 'EF02MA20', pieces: [5, 10, 25, 50, 100, 200], tasks: [['pagar', 175], ['pagar', 350], ['troco', 350, 500], ['troco', 480, 500]] },
	{ year: 3, bncc: 'EF03MA24', pieces: null, tasks: [['troco', 750, 1000], ['troco', 1250, 2000], ['troco', 3590, 5000], ['pagar', 2450]] },
];

const things = ['fruta', 'caderno', 'picolé', 'livro'];
mkdirSync('content/missions/sistema-monetario', { recursive: true });

for (const { year, bncc, pieces, tasks } of years) {
	tasks.forEach(([task, price, given], i) => {
		const id = `mat-din-a${year}-${String(i + 1).padStart(3, '0')}`;
		const thing = things[i % things.length];
		const params = { task, price, ...(given ? { given } : {}), ...(pieces ? { pieces } : {}) };
		const prompt = task === 'pagar'
			? `O ${thing} custa ${real(price)}. Monte esse valor com moedas e notas para pagar!`
			: `O ${thing} custa ${real(price)} e você deu uma nota de ${real(given)}. Monte o troco!`;
		const hints = task === 'pagar'
			? ['Comece pela peça maior que ainda cabe no valor.', 'Um real vale 100 centavos: duas moedas de 50 centavos fazem um real.', 'Peça a dica para ver a próxima peça.']
			: ['O troco é o que sobra da nota depois de pagar o preço.', `De ${real(price)} até ${real(given)} falta quanto?`, 'Peça a dica para ver a próxima peça.'];
		const explanation = task === 'pagar'
			? `Juntando as peças chegamos a ${real(price)}.`
			: `${real(given)} menos ${real(price)} é ${real(given - price)}: esse é o troco.`;
		const mission = {
			id, version: 1, discipline: 'matematica', concept: 'sistema-monetario', bncc: [bncc], year, difficulty: 5,
			mechanic: 'pagar_troco', params, prompt, hints, explanation,
			accessibility: { timerAllowed: false }, review: { status: 'pending' },
		};
		writeFileSync(`content/missions/sistema-monetario/${id}.json`, JSON.stringify(mission, null, 2) + '\n');
	});
}
console.log('12 Missões gravadas em content/missions/sistema-monetario');
