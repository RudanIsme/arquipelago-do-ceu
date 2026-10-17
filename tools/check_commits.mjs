// Commits Convencionais (feat, fix, refactor...), em português, como no commitlint dos outros projetos.
// Uso: node tools/check_commits.mjs [base]   (padrão origin/main; confere os commits de base..HEAD, sem os de merge)
import { execFileSync } from 'node:child_process';

const base = process.argv[2] ?? 'origin/main';
const padrao = /^(feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)(\(.+\))?!?: .+/;

const assuntos = execFileSync('git', ['log', '--no-merges', '--format=%s', `${base}..HEAD`], { encoding: 'utf8' })
  .split('\n')
  .filter(Boolean);
const fora = assuntos.filter((assunto) => !padrao.test(assunto));

if (fora.length > 0) {
  console.error(fora.map((assunto) => `fora do padrão: ${assunto}`).join('\n'));
  console.error('\nUse tipo(escopo): descrição, com tipo em feat, fix, docs, style, refactor, perf, test, build, ci, chore ou revert.');
  process.exit(1);
}
console.log(`${assuntos.length} commit(s) no padrão.`);
