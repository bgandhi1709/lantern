import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(__dirname, '../..');
const MIN_TOKENS = 3;

type Composites = Record<string, string[]>;

const filesUnder = (dir: string): string[] =>
  fs
    .readdirSync(dir, { withFileTypes: true })
    .flatMap((entry) =>
      entry.isDirectory() ? filesUnder(path.join(dir, entry.name)) : [path.join(dir, entry.name)],
    );

function compositesIn(css: string): Composites {
  const written: Composites = {};
  for (const match of css.matchAll(/@utility ([\w-]+) \{\s*@apply ([^;]+);/g)) {
    written[match[1]] = match[2].trim().split(/\s+/);
  }
  const expand = (tokens: string[]): string[] =>
    tokens.flatMap((token) => (written[token] ? expand(written[token]) : [token]));
  return Object.fromEntries(
    Object.entries(written).map(([name, tokens]) => [name, expand(tokens)]),
  );
}

function classSegments(source: string): string[] {
  const spans = source.matchAll(/\w*[cC]lassName=(?:"([^"]*)"|\{`([^`]*)`\}|\{'([^']*)'\})/g);
  return [...spans].flatMap((span) => {
    const value = span[1] ?? span[2] ?? span[3];
    const quoted = [...value.matchAll(/\$\{([^}]*)\}/g)].flatMap((hole) =>
      [...hole[1].matchAll(/'([^']*)'/g)].map((literal) => literal[1]),
    );
    return [value.replace(/\$\{[^}]*\}/g, ' '), ...quoted];
  });
}

function coveredBy(segment: string, composites: Composites): string[] {
  const tokens = new Set(segment.split(/\s+/));
  return Object.entries(composites)
    .filter(([, parts]) => parts.length >= MIN_TOKENS && parts.every((part) => tokens.has(part)))
    .map(([name]) => name);
}

const roles: Composites = {
  'type-small-muted': ['font-inter-medium', 'text-small', 'text-muted'],
  panel: ['rounded-card', 'border', 'border-line', 'bg-card', 'p-4'],
};

describe('coveredBy', () => {
  it('names the composite a class string spells out in full', () => {
    expect(coveredBy('mt-1 font-inter-medium text-small text-muted', roles)).toEqual([
      'type-small-muted',
    ]);
  });

  it('ignores a string that spells out only part of one', () => {
    expect(coveredBy('font-inter-medium text-small text-ink', roles)).toEqual([]);
  });

  it('ignores a composite of fewer than three classes', () => {
    expect(
      coveredBy('border-primary bg-tile', { 'choice-on': ['border-primary', 'bg-tile'] }),
    ).toEqual([]);
  });
});

describe('compositesIn', () => {
  it('expands a composite built from another', () => {
    const css =
      '@utility surface {\n  @apply rounded-card border;\n}\n@utility panel {\n  @apply surface p-4;\n}';
    expect(compositesIn(css).panel).toEqual(['rounded-card', 'border', 'p-4']);
  });
});

describe('classSegments', () => {
  it('reads plain, template and conditional class strings', () => {
    const source = [
      '<View className="a b" />',
      "<View className={`c d ${on ? 'e f' : 'g'}`} />",
      '<ScrollView contentContainerClassName="h i" />',
    ].join('\n');
    expect(classSegments(source)).toEqual(['a b', 'c d  ', 'e f', 'g', 'h i']);
  });
});

describe('the app', () => {
  it('spells out no class combination that global.css already names', () => {
    const composites = compositesIn(fs.readFileSync(path.join(root, 'global.css'), 'utf8'));
    const offences = ['src', 'app']
      .flatMap((dir) => filesUnder(path.join(root, dir)))
      .filter((file) => file.endsWith('.tsx') && !file.endsWith('.test.tsx'))
      .flatMap((file) =>
        classSegments(fs.readFileSync(file, 'utf8')).flatMap((segment) =>
          coveredBy(segment, composites).map(
            (name) => `${path.relative(root, file)}: use ${name} for "${segment.trim()}"`,
          ),
        ),
      );
    expect(offences).toEqual([]);
  });
});
