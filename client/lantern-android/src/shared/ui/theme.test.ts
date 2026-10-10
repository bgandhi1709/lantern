/** @jest-environment node */
import fs from 'node:fs';
import path from 'node:path';

import { colors } from './theme';

const css = fs.readFileSync(path.resolve(__dirname, '../../../global.css'), 'utf8');
const cssColor = (name: string) =>
  new RegExp(`--color-${name}:\\s*(#[0-9a-fA-F]{6})`).exec(css)?.[1]?.toLowerCase();

const cssNames: Record<keyof typeof colors, string> = {
  primary: 'primary',
  primaryDeep: 'primary-deep',
  text: 'ink',
  textSecondary: 'muted',
  ground: 'ground',
  card: 'card',
  border: 'line',
  iconTile: 'tile',
  onPrimary: 'on-primary',
  error: 'danger',
};

describe('theme.ts and global.css', () => {
  it.each(Object.entries(cssNames))('agree on the colour %s', (key, name) => {
    expect(cssColor(name)).toBe(colors[key as keyof typeof colors].toLowerCase());
  });
});
