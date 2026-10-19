import { VariableContextProvider } from 'nativewind';
import { useMemo } from 'react';
import type { ReactNode } from 'react';
import { useWindowDimensions } from 'react-native';

import { colorsOf } from '../ui/theme';
import { isDark } from './appearance';
import type { Appearance } from './appearance';
import { AppearanceContext } from './appearanceContext';
import type { AppearanceValue } from './appearanceContext';
import { appearanceVariables } from './appearanceVariables';
import { markColorsFor, paletteFor } from './palettes';

type Actions = Pick<AppearanceValue, 'setTheme' | 'setTextSize' | 'setColourScheme'>;

const nothing = () => undefined;
const NO_ACTIONS: Actions = { setTheme: nothing, setTextSize: nothing, setColourScheme: nothing };

type Props = { appearance: Appearance; actions?: Actions; children: ReactNode };

export function LookProvider({ appearance, actions = NO_ACTIONS, children }: Props) {
  const { fontScale } = useWindowDimensions();

  const value = useMemo<AppearanceValue>(() => {
    const dark = isDark(appearance.theme);
    return {
      appearance,
      dark,
      colors: colorsOf(paletteFor(appearance.colourScheme, dark)),
      markColors: markColorsFor(appearance.colourScheme, dark),
      ...actions,
    };
  }, [appearance, actions]);

  const variables = useMemo(
    () => appearanceVariables(appearance, fontScale),
    [appearance, fontScale],
  );

  return (
    <AppearanceContext.Provider value={value}>
      <VariableContextProvider value={variables}>{children}</VariableContextProvider>
    </AppearanceContext.Provider>
  );
}
