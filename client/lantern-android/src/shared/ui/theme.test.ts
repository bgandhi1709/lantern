/** @jest-environment node */
import fs from 'node:fs';
import path from 'node:path';

import { COLOUR_TOKENS, SCALED_TEXT_PX, paletteFor } from '../theme/palettes';

const css = fs.readFileSync(path.resolve(__dirname, '../../../global.css'), 'utf8');
const fallback = (property: string, name: string) =>
  new RegExp(`--${property}-${name}:\\s*var\\(--app-${name},\\s*([^)]+)\\)`).exec(css)?.[1]?.trim();

describe('palettes.ts and global.css', () => {
  it.each(COLOUR_TOKENS)('agree on the default colour %s', (token) => {
    expect(fallback('color', token)).toBe(paletteFor('teal', false)[token]);
  });

  it.each(Object.entries(SCALED_TEXT_PX))('agree on the default text size %s', (name, px) => {
    expect(new RegExp(`--text-${name}:\\s*var\\(--app-text-${name},\\s*${px}px\\)`).test(css)).toBe(
      true,
    );
  });
});
