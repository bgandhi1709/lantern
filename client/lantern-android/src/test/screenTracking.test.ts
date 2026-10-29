/** @jest-environment node */
import fs from 'node:fs';
import path from 'node:path';

const src = path.resolve(__dirname, '..');

// These show while nobody has agreed to the notice, so they collect nothing (D68).
const collectNothing = ['auth/StartScreen.tsx', 'auth/OpeningScreen.tsx'];

const filesUnder = (dir: string): string[] =>
  fs
    .readdirSync(dir, { withFileTypes: true })
    .flatMap((entry) =>
      entry.isDirectory() ? filesUnder(path.join(dir, entry.name)) : [path.join(dir, entry.name)],
    );

const screens = filesUnder(src)
  .filter((file) => file.endsWith('Screen.tsx'))
  .map((file) => path.relative(src, file))
  .filter((file) => !collectNothing.includes(file));

describe('every screen container', () => {
  it('reports that it was viewed', () => {
    const silent = screens.filter(
      (file) =>
        !/use(TrackScreen|RegistrationStep)\(/.test(fs.readFileSync(path.join(src, file), 'utf8')),
    );

    expect(silent).toEqual([]);
  });
});
