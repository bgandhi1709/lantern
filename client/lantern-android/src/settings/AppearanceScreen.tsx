import { useEffect, useState } from 'react';

import { tapped, useTrackScreen } from '../shared/analytics';
import { getParent } from '../shared/session';
import { readFamilyKey } from '../shared/storage';
import { useAppearance } from '../shared/theme';
import { AppearanceView } from './AppearanceView';

type Props = { onBack: () => void; onUnlock: () => void; onStartAgain: () => void };

export function AppearanceScreen({ onBack, onUnlock, onStartAgain }: Props) {
  useTrackScreen('appearance');
  const { appearance, setTheme, setTextSize, setColourScheme } = useAppearance();
  const [locked, setLocked] = useState(false);

  useEffect(() => {
    readFamilyKey(getParent().uid).then((key) => setLocked(key === null));
  }, []);

  return (
    <AppearanceView
      theme={appearance.theme}
      textSize={appearance.textSize}
      colourScheme={appearance.colourScheme}
      onTheme={tapped('change-appearance', setTheme)}
      onTextSize={tapped('change-appearance', setTextSize)}
      onColourScheme={tapped('change-appearance', setColourScheme)}
      onBack={onBack}
      onUnlock={locked ? tapped('unlock', onUnlock) : undefined}
      onStartAgain={locked ? tapped('start-again', onStartAgain) : undefined}
    />
  );
}
