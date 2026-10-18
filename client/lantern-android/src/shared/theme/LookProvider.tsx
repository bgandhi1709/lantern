import { VariableContextProvider } from 'nativewind';
import { useMemo } from 'react';
import type { ReactNode } from 'react';
import { useColorScheme } from 'react-native';

import { colorsOf } from '../ui/theme';
import { isDark } from './appearance';
import type { Appearance } from './appearance';
import { AppearanceContext } from './appearanceContext';
import type { AppearanceValue } from './appearanceContext';
import { appearanceVariables } from './appearanceVariables';
import { paletteFor } from './palettes';

type Actions = Pick<AppearanceValue, 'setTheme' | 'setTextSize' | 'setColourScheme'>;

const nothing = () => undefined;
const NO_ACTIONS: Actions = { setTheme: nothing, setTextSize: nothing, setColourScheme: nothing };

type Props = { appearance: Appearance; actions?: Actions; children: ReactNode };

export function LookProvider({ appearance, actions = NO_ACTIONS, children }: Props) {
  const phoneIsDark = useColorScheme() === 'dark';

  const value = useMemo<AppearanceValue>(() => {
    const dark = isDark(appearance.theme, phoneIsDark);
    return {
      appearance,
      dark,
      colors: colorsOf(paletteFor(appearance.colourScheme, dark)),
      ...actions,
    };
  }, [appearance, phoneIsDark, actions]);

  const variables = useMemo(
    () => appearanceVariables(appearance, phoneIsDark),
    [appearance, phoneIsDark],
  );

  return (
    <AppearanceContext.Provider value={value}>
      <VariableContextProvider value={variables}>{children}</VariableContextProvider>
    </AppearanceContext.Provider>
  );
}
