import { useEffect, useState } from 'react';

import { tapped } from '../shared/analytics';
import { getParent } from '../shared/session';
import { readFamilyKey } from '../shared/storage';
import { useAppearance } from '../shared/theme';
import { SettingsView } from './SettingsView';
import { useSettingsOutline } from './useSettingsOutline';

type Props = {
  onBack: () => void;
  onUnlock: () => void;
  onStartAgain: () => void;
  onDeleteChild: () => void;
  onDeleteFamily: () => void;
};

export function SettingsScreen({
  onBack,
  onUnlock,
  onStartAgain,
  onDeleteChild,
  onDeleteFamily,
}: Props) {
  const { appearance, setTheme, setTextSize, setColourScheme } = useAppearance();
  const outline = useSettingsOutline();
  const [locked, setLocked] = useState<boolean | null>(null);

  useEffect(() => {
    readFamilyKey(getParent().uid).then((key) => setLocked(key === null));
  }, []);

  return (
    <SettingsView
      theme={appearance.theme}
      textSize={appearance.textSize}
      colourScheme={appearance.colourScheme}
      onTheme={tapped('change-appearance', setTheme)}
      onTextSize={tapped('change-appearance', setTextSize)}
      onColourScheme={tapped('change-appearance', setColourScheme)}
      outline={outline}
      onBack={onBack}
      onUnlock={locked === true ? tapped('unlock', onUnlock) : undefined}
      onStartAgain={locked === true ? tapped('start-again', onStartAgain) : undefined}
      onDeleteChild={locked === false ? tapped('delete-child', onDeleteChild) : undefined}
      onDeleteFamily={locked === false ? tapped('delete-family', onDeleteFamily) : undefined}
    />
  );
}
