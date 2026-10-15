import { View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { BookIcon, CalendarIcon, SchoolIcon, UserIcon } from '../shared/ui/icons';
import { Picker } from '../shared/ui/Picker';
import { TextField } from '../shared/ui/TextField';
import type { Board } from './board';
import { NAME_MAX_LENGTH, SCHOOL_MAX_LENGTH, classChoices } from './child';
import type { ChildDraft } from './child';
import { ClassChips } from './ClassChips';

type Props = {
  child: ChildDraft;
  board: Board;
  years: string[];
  // The first Child picks the Class from a row of buttons; an extra Child's card is shorter, so a list.
  classAsList?: boolean;
  // Names the fields for the Maestro flows, which find one by its id.
  idPrefix: string;
  onChange: (changes: Partial<ChildDraft>) => void;
};

export function ChildFields({
  child,
  board,
  years,
  classAsList = false,
  idPrefix,
  onChange,
}: Props) {
  const strings = useStrings().registration.firstChild;
  const levels = classChoices(board);
  const hint = board === 'ssc' ? strings.sscHint : undefined;

  return (
    <View className="gap-3.5">
      <TextField
        testID={`${idPrefix}-name`}
        label={strings.nameLabel}
        placeholder={strings.namePlaceholder}
        value={child.name}
        onChangeText={(name) => onChange({ name })}
        icon={<UserIcon />}
        maxLength={NAME_MAX_LENGTH}
      />
      {classAsList ? (
        <Picker
          testID={`${idPrefix}-class`}
          label={strings.classLabel}
          placeholder={strings.classPlaceholder}
          value={child.classLevel === null ? '' : String(child.classLevel)}
          options={levels.map((level) => ({
            value: String(level),
            label: strings.classOption(level),
          }))}
          onChange={(value) => onChange({ classLevel: value === '' ? null : Number(value) })}
          icon={<BookIcon />}
          hint={hint}
        />
      ) : (
        <ClassChips
          levels={levels}
          selected={child.classLevel}
          onSelect={(classLevel) => onChange({ classLevel })}
          hint={hint}
        />
      )}
      <Picker
        testID={`${idPrefix}-year`}
        label={strings.birthYearLabel}
        placeholder={strings.birthYearPlaceholder}
        value={child.birthYear}
        options={years.map((year) => ({ value: year, label: year }))}
        onChange={(birthYear) => onChange({ birthYear })}
        icon={<CalendarIcon />}
      />
      <TextField
        testID={`${idPrefix}-school`}
        label={strings.schoolLabel}
        placeholder={strings.schoolPlaceholder}
        value={child.school}
        onChangeText={(school) => onChange({ school })}
        icon={<SchoolIcon />}
        maxLength={SCHOOL_MAX_LENGTH}
      />
    </View>
  );
}
