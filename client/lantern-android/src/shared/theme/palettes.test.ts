import { COLOUR_SCHEMES } from './appearance';
import { appearanceVariables } from './appearanceVariables';
import { paletteFor, scaledPx } from './palettes';

const channel = (hex: string, at: number) => {
  const value = parseInt(hex.slice(at, at + 2), 16) / 255;
  return value <= 0.03928 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
};
const luminance = (hex: string) =>
  0.2126 * channel(hex, 1) + 0.7152 * channel(hex, 3) + 0.0722 * channel(hex, 5);
const contrast = (a: string, b: string) => {
  const [light, dark] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return ((light ?? 0) + 0.05) / ((dark ?? 0) + 0.05);
};

const MINIMUM_TEXT_CONTRAST = 4.5;
const combos = COLOUR_SCHEMES.flatMap((scheme) => [
  [scheme, false] as const,
  [scheme, true] as const,
]);

describe.each(combos)('the %s colours, dark %s', (scheme, dark) => {
  const palette = paletteFor(scheme, dark);

  it('keeps white button text readable on the accent', () => {
    expect(contrast(palette['on-primary'], palette.primary)).toBeGreaterThanOrEqual(
      MINIMUM_TEXT_CONTRAST,
    );
  });

  it.each(['ground', 'card'] as const)('keeps text readable on the %s', (surface) => {
    for (const text of ['ink', 'muted', 'accent', 'primary-deep', 'danger'] as const) {
      expect(contrast(palette[text], palette[surface])).toBeGreaterThanOrEqual(
        MINIMUM_TEXT_CONTRAST,
      );
    }
  });
});

describe('appearance variables', () => {
  const teal = { theme: 'light', textSize: 'default', colourScheme: 'teal' } as const;

  it('use the chosen theme', () => {
    expect(appearanceVariables({ ...teal, theme: 'dark' })['--app-ground']).toBe('#0e1716');
    expect(appearanceVariables({ ...teal, theme: 'light' })['--app-ground']).toBe('#f2faf9');
  });

  it('carry the chosen colour scheme', () => {
    expect(appearanceVariables({ ...teal, colourScheme: 'blue' })['--app-primary']).toBe('#2563eb');
  });

  it('scale the text in three steps', () => {
    expect(scaledPx('body', 'default')).toBe(16);
    expect(scaledPx('body', 'large')).toBe(18.4);
    expect(scaledPx('body', 'largest')).toBe(20.8);
    expect(appearanceVariables({ ...teal, textSize: 'largest' })['--app-text-body']).toBe('20.8px');
  });

  it('leaves the phone text size alone while the two together stay within the limit', () => {
    expect(appearanceVariables(teal, 1.15)['--app-text-body']).toBe('16px');
    expect(appearanceVariables({ ...teal, textSize: 'large' }, 1.15)['--app-text-body']).toBe(
      '18.4px',
    );
  });

  it('holds the phone and in-app text sizes together to 1.4 times the designed size', () => {
    const largestOnLargePhone = appearanceVariables({ ...teal, textSize: 'largest' }, 1.15);
    const defaultOnHugePhone = appearanceVariables(teal, 2);

    expect(largestOnLargePhone['--app-text-body']).toBe('19.5px');
    expect(defaultOnHugePhone['--app-text-body']).toBe('11.2px');
  });
});
