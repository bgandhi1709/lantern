import { useEffect, useState } from 'react';

import { tapped } from '../shared/analytics';
import { getParent } from '../shared/session';
import { readFamilyKey } from '../shared/storage';
import { useAppearance } from '../shared/theme';
import type { FamilyActions } from './FamilyManagement';
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

  const look = {
    theme: appearance.theme,
    textSize: appearance.textSize,
    colourScheme: appearance.colourScheme,
    onTheme: tapped('change-appearance', setTheme),
    onTextSize: tapped('change-appearance', setTextSize),
    onColourScheme: tapped('change-appearance', setColourScheme),
  };
  const family: FamilyActions =
    locked === true
      ? { unlock: tapped('unlock', onUnlock), startAgain: tapped('start-again', onStartAgain) }
      : locked === false
        ? {
            deleteChild: tapped('delete-child', onDeleteChild),
            deleteFamily: tapped('delete-family', onDeleteFamily),
          }
        : {};

  return <SettingsView look={look} family={family} outline={outline} onBack={onBack} />;
}
