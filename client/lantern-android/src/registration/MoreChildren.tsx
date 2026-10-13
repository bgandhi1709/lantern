import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { PlusIcon } from '../shared/ui/icons';
import { OutlineButton } from '../shared/ui/OutlineButton';
import type { Board } from './board';
import { MAX_CHILDREN, isChildComplete, isChildTouched } from './child';
import type { ChildDraft } from './child';
import { ChildCard } from './ChildCard';
import type { ChildCardStatus } from './ChildCard';
import { ChildFields } from './ChildFields';

type Props = {
  board: Board;
  // The first Child, then the extras; the extras are what this section shows.
  drafts: ChildDraft[];
  years: string[];
  now: Date;
  openIndex: number | null;
  onChange: (index: number, changes: Partial<ChildDraft>) => void;
  onToggle: (index: number) => void;
  onAdd: () => void;
  onRemove: (index: number) => void;
};

export function MoreChildren({
  board,
  drafts,
  years,
  now,
  openIndex,
  onChange,
  onToggle,
  onAdd,
  onRemove,
}: Props) {
  const strings = useStrings().registration.firstChild.more;

  const statusOf = (draft: ChildDraft): ChildCardStatus => {
    if (isChildComplete(draft, board, now)) return 'ready';
    return isChildTouched(draft) ? 'needsMore' : 'optional';
  };

  return (
    <View className="mt-4 gap-3">
      <View className="gap-0.5">
        <Text accessibilityRole="header" className="font-inter-bold text-lead text-ink">
          {strings.heading}
        </Text>
        <Text className="font-inter-medium text-small text-muted">{strings.intro}</Text>
      </View>
      {drafts.slice(1).map((draft, offset) => {
        const index = offset + 1;
        return (
          <ChildCard
            key={index}
            number={index + 1}
            name={draft.name}
            classLevel={draft.classLevel}
            status={statusOf(draft)}
            open={openIndex === index}
            onToggle={() => onToggle(index)}
            onRemove={() => onRemove(index)}
          >
            <ChildFields
              idPrefix={`child-${index + 1}`}
              child={draft}
              board={board}
              years={years}
              classAsList
              onChange={(changes) => onChange(index, changes)}
            />
          </ChildCard>
        );
      })}
      {drafts.length < MAX_CHILDREN ? (
        <OutlineButton label={strings.add} onPress={onAdd} icon={<PlusIcon />} />
      ) : null}
    </View>
  );
}
