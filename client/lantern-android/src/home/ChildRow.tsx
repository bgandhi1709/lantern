import { useStrings } from '../shared/i18n';
import { ChoiceRow } from '../shared/ui/ChoiceRow';
import { ChildAvatar } from './ChildAvatar';
import { SelectedMark } from './homeIcons';
import type { SwitcherChild } from './useHome';

type Props = { child: SwitcherChild; selected: boolean; onPick: () => void };

export function ChildRow({ child, selected, onPick }: Props) {
  const home = useStrings().home;
  const status = child.stillBeingMade
    ? home.gettingReady(child.classLevel)
    : home.classLabel(child.classLevel);

  return (
    <ChoiceRow
      role="button"
      selected={selected}
      label={`${child.name}, ${status}`}
      onPress={onPick}
      leading={<ChildAvatar name={child.name} large />}
      title={child.name}
      subtitle={status}
      trailing={<SelectedMark />}
    />
  );
}
