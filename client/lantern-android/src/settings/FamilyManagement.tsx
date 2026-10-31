import { useStrings } from '../shared/i18n';
import { FamilyRow } from './FamilyRow';
import { SettingsGroup } from './SettingsGroup';
import type { Outline } from './useSettingsOutline';

type Props = {
  outline: Outline;
  onUnlock?: () => void;
  onStartAgain?: () => void;
  onDeleteChild?: () => void;
  onDeleteFamily?: () => void;
};

export function FamilyManagement({
  outline,
  onUnlock,
  onStartAgain,
  onDeleteChild,
  onDeleteFamily,
}: Props) {
  const strings = useStrings();
  const rows = [
    { id: 'unlock', onPress: onUnlock, danger: false, ...strings.appearance.unlockRow },
    { id: 'deleteChild', onPress: onDeleteChild, danger: true, ...strings.settings.deleteChildRow },
    {
      id: 'deleteFamily',
      onPress: onDeleteFamily,
      danger: true,
      ...strings.settings.deleteFamilyRow,
    },
    { id: 'startAgain', onPress: onStartAgain, danger: false, ...strings.appearance.startAgainRow },
  ] as const;
  const shown = rows.filter((row) => row.onPress && outline.matches(row.id));

  if (shown.length === 0) return null;

  return (
    <SettingsGroup
      title={strings.settings.groups.family}
      open={outline.isGroupOpen('family')}
      onToggle={() => outline.toggleGroup('family')}
    >
      {shown.map(({ id, onPress, ...row }) => (
        <FamilyRow key={id} {...row} onPress={() => onPress?.()} />
      ))}
    </SettingsGroup>
  );
}
