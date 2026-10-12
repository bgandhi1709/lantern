import { formatRecoveryCode, makeRecoveryCode } from './recoveryCode';

describe('the Recovery code', () => {
  it('is four groups of four, dash-separated, with no look-alike characters', () => {
    const code = makeRecoveryCode();

    expect(code).toMatch(/^[A-HJ-NP-Z2-9]{4}(-[A-HJ-NP-Z2-9]{4}){3}$/);
  });

  it('is different every time', () => {
    const codes = new Set(Array.from({ length: 50 }, makeRecoveryCode));

    expect(codes.size).toBe(50);
  });

  it('reads the same however it is typed: lower case, spaces or no dashes', () => {
    expect(formatRecoveryCode(' k7qm 4tdx-92hb wepn ')).toBe('K7QM-4TDX-92HB-WEPN');
  });
});
