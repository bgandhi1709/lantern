import { useStrings } from '../shared/i18n';
import { FamilyRow } from './FamilyRow';
import { SettingsGroup } from './SettingsGroup';
import { FAMILY_IDS } from './settingsSearch';
import type { FamilyId } from './settingsSearch';
import type { Outline } from './useSettingsOutline';

export type FamilyActions = Partial<Record<FamilyId, () => void>>;

type Props = { outline: Outline; actions: FamilyActions };

export function FamilyManagement({ outline, actions }: Props) {
  const strings = useStrings();
  const rows: Record<FamilyId, { title: string; text: string; danger: boolean }> = {
    unlock: { ...strings.appearance.unlockRow, danger: false },
    deleteChild: { ...strings.settings.deleteChildRow, danger: true },
    deleteFamily: { ...strings.settings.deleteFamilyRow, danger: true },
    startAgain: { ...strings.appearance.startAgainRow, danger: false },
  };
  const shown = FAMILY_IDS.filter((id) => actions[id] && outline.matches(id));

  if (shown.length === 0) return null;

  return (
    <SettingsGroup
      title={strings.settings.groups.family}
      open={outline.isGroupOpen('family')}
      onToggle={() => outline.toggleGroup('family')}
    >
      {shown.map((id) => (
        <FamilyRow key={id} {...rows[id]} onPress={() => actions[id]?.()} />
      ))}
    </SettingsGroup>
  );
}
