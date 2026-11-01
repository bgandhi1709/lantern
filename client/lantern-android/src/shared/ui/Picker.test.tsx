import { render, screen } from '@testing-library/react-native';
import * as ReactNative from 'react-native';

import { clearAll, writeValue } from '../storage';
import { ThemeProvider, paletteFor } from '../theme';
import { Picker } from './Picker';

jest.mock('@react-native-picker/picker', () => {
  const { View } = jest.requireActual<typeof ReactNative>('react-native');
  const Item = (props: object) => <View testID="picker-item" {...props} />;
  const NativePicker = ({ children }: { children: React.ReactNode }) => <View>{children}</View>;
  NativePicker.Item = Item;
  return { Picker: NativePicker };
});

const options = ['2021', '2020'].map((year) => ({ value: year, label: year }));

const openPicker = () =>
  render(
    <ThemeProvider>
      <Picker
        label="Birth year"
        placeholder="Choose"
        value="2021"
        options={options}
        onChange={jest.fn()}
      />
    </ThemeProvider>,
  );

const itemProps = async () => {
  await screen.findByText('Birth year');
  return screen.getAllByTestId('picker-item').map((item) => item.props);
};

beforeEach(async () => {
  await clearAll();
  jest.spyOn(ReactNative, 'useColorScheme').mockReturnValue('light');
});

describe.each([
  ['dark', true],
  ['light', false],
] as const)('Picker in the %s theme', (theme, dark) => {
  beforeEach(() => writeValue('theme', theme));

  it('shows every year in the readable text colour of the theme, not the phone’s default black', async () => {
    await openPicker();
    const palette = paletteFor('teal', dark);

    expect((await itemProps()).map((item) => [item.value, item.color])).toEqual([
      ['', palette.muted],
      ['2021', palette.ink],
      ['2020', palette.ink],
    ]);
  });

  it('gives the dropdown list the card colour of the theme, so text never sits on the phone’s white popup', async () => {
    await openPicker();
    const card = paletteFor('teal', dark).card;

    expect((await itemProps()).map((item) => item.style.backgroundColor)).toEqual([
      card,
      card,
      card,
    ]);
  });
});
