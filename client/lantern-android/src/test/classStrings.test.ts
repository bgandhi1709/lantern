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

function closingBacktick(source: string, from: number): number {
  for (let i = from; i < source.length; i++) {
    if (source[i] === '`') return i;
    if (source.startsWith('${', i)) i = closingBrace(source, i + 2);
  }
  return source.length;
}

function closingBrace(source: string, from: number): number {
  let depth = 1;
  for (let i = from; i < source.length; i++) {
    if (source[i] === '`') i = closingBacktick(source, i + 1);
    else if (source[i] === '{') depth++;
    else if (source[i] === '}' && --depth === 0) return i;
  }
  return source.length;
}

function templateSegments(body: string): string[] {
  const holes: string[] = [];
  let text = '';
  for (let i = 0; i < body.length; i++) {
    if (!body.startsWith('${', i)) {
      text += body[i];
      continue;
    }
    const end = closingBrace(body, i + 2);
    holes.push(body.slice(i + 2, end));
    text += ' ';
    i = end;
  }
  return [text, ...holes.flatMap(holeSegments)];
}

function holeSegments(hole: string): string[] {
  const segments: string[] = [];
  for (let i = 0; i < hole.length; i++) {
    if (hole[i] === '`') {
      const end = closingBacktick(hole, i + 1);
      segments.push(...templateSegments(hole.slice(i + 1, end)));
      i = end;
    } else if (hole[i] === "'") {
      const end = hole.indexOf("'", i + 1);
      segments.push(hole.slice(i + 1, end));
      i = end;
    }
  }
  return segments;
}

function classSegments(source: string): string[] {
  const plain = [...source.matchAll(/\w*[cC]lassName=(?:"([^"]*)"|\{'([^']*)'\})/g)].map(
    (span) => span[1] ?? span[2],
  );
  const templates = [...source.matchAll(/\w*[cC]lassName=\{`/g)].flatMap((span) => {
    const from = (span.index ?? 0) + span[0].length;
    return templateSegments(source.slice(from, closingBacktick(source, from)));
  });
  return [...plain, ...templates];
}

function coveredBy(segment: string, composites: Composites): string[] {
  const tokens = new Set(segment.split(/\s+/));
  return Object.entries(composites)
    .filter(([, parts]) => parts.length >= MIN_TOKENS && parts.every((part) => tokens.has(part)))
    .map(([name]) => name);
}

const roles: Composites = {
  'lantern-hint': ['font-inter-medium', 'text-small', 'text-muted'],
  'lantern-card-padded': ['rounded-card', 'border', 'border-line', 'bg-card', 'p-4'],
};

const PREFIX = 'lantern-';

function unknownNames(segment: string, composites: Composites): string[] {
  return segment.split(/\s+/).filter((token) => token.startsWith(PREFIX) && !(token in composites));
}

describe('coveredBy', () => {
  it('names the composite a class string spells out in full', () => {
    expect(coveredBy('mt-1 font-inter-medium text-small text-muted', roles)).toEqual([
      'lantern-hint',
    ]);
  });

  it('ignores a string that spells out only part of one', () => {
    expect(coveredBy('font-inter-medium text-small text-ink', roles)).toEqual([]);
  });

  it('ignores a composite of fewer than three classes', () => {
    expect(
      coveredBy('border-primary bg-tile', {
        'lantern-pick-row-selected': ['border-primary', 'bg-tile'],
      }),
    ).toEqual([]);
  });
});

describe('unknownNames', () => {
  it('names a lantern- class that global.css does not define', () => {
    expect(unknownNames('mt-1 lantern-hint lantern-hnt', { 'lantern-hint': [] })).toEqual([
      'lantern-hnt',
    ]);
  });

  it('ignores classes that do not start with the prefix', () => {
    expect(unknownNames('type-lead mt-1', {})).toEqual([]);
  });
});

describe('compositesIn', () => {
  it('expands a composite built from another', () => {
    const css =
      '@utility lantern-card {\n  @apply rounded-card border;\n}\n@utility lantern-card-padded {\n  @apply lantern-card p-4;\n}';
    expect(compositesIn(css)['lantern-card-padded']).toEqual(['rounded-card', 'border', 'p-4']);
  });
});

describe('classSegments', () => {
  it('reads plain, template and conditional class strings', () => {
    const source = [
      '<View className="a b" />',
      "<View className={`c d ${on ? 'e f' : 'g'}`} />",
      '<ScrollView contentContainerClassName="h i" />',
    ].join('\n');
    expect(classSegments(source).sort()).toEqual(['a b', 'c d  ', 'e f', 'g', 'h i']);
  });

  it('reads a template nested inside a template', () => {
    const source = "<TextInput className={`a ${n ? 'b c' : `d ${i ? 'e' : 'f'}`}`} />";
    expect(classSegments(source).sort()).toEqual(['a  ', 'b c', 'd  ', 'e', 'f']);
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

  it('uses only lantern- classes that global.css defines', () => {
    const composites = compositesIn(fs.readFileSync(path.join(root, 'global.css'), 'utf8'));
    const offences = ['src', 'app']
      .flatMap((dir) => filesUnder(path.join(root, dir)))
      .filter((file) => file.endsWith('.tsx') && !file.endsWith('.test.tsx'))
      .flatMap((file) =>
        classSegments(fs.readFileSync(file, 'utf8')).flatMap((segment) =>
          unknownNames(segment, composites).map(
            (name) => `${path.relative(root, file)}: ${name} is not in global.css`,
          ),
        ),
      );
    expect(offences).toEqual([]);
  });
});
