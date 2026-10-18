import { dayPartAt, firstNameOf } from './greeting';

describe('the greeting', () => {
  it.each([
    [0, 'morning'],
    [11, 'morning'],
    [12, 'afternoon'],
    [16, 'afternoon'],
    [17, 'evening'],
    [23, 'evening'],
  ] as const)('at %i o’clock it is the %s', (hour, expected) => {
    expect(dayPartAt(hour)).toBe(expected);
  });

  it('greets the Parent by first name', () => {
    expect(firstNameOf('Asha Verma')).toBe('Asha');
    expect(firstNameOf('  asha@example.com ')).toBe('asha@example.com');
  });
});
