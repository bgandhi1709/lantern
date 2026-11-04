import { Text } from 'react-native';

import { useStrings } from '../shared/i18n';
import { TextField } from '../shared/ui/TextField';
import { WarningCard } from '../shared/ui/WarningCard';
import { ChildChoices } from './ChildChoices';
import { DeletionPage } from './DeletionPage';
import { EmailNote } from './EmailNote';
import { SendCodeButtons } from './SendCodeButtons';
import type { HomeChild } from '../home';
import type { DeleteFailure } from './deleteFailure';
import { OnlyChildNotice } from './OnlyChildNotice';
import { childrenOf } from './useDeletableFamily';
import type { FamilyState } from './useDeletableFamily';
import { ScreenHeading } from '../shared/ui/ScreenHeading';
import { ErrorText } from '../shared/ui/ErrorText';

const NAME_MAX_LENGTH = 50;

type Props = {
  family: FamilyState;
  picked: HomeChild | null;
  typedName: string;
  canSend: boolean;
  sending: boolean;
  failure: DeleteFailure | null;
  email: string;
  onPick: (childId: string) => void;
  onTypeName: (text: string) => void;
  onSend: () => void;
  onCancel: () => void;
  onBack: () => void;
  onDeleteFamily: () => void;
};

export function DeleteChildConfirmView(props: Props) {
  const strings = useStrings().deleteChild;
  const code = useStrings().deleteCode;
  const { family, picked } = props;
  const children = childrenOf(family);

  const failureMessage =
    family.status === 'failed' ? strings.loadFailed : props.failure && code.failures[props.failure];

  const footer = (
    <SendCodeButtons
      canSend={props.canSend}
      sending={props.sending}
      cancelLabel={code.cancel}
      onSend={props.onSend}
      onCancel={props.onCancel}
    />
  );

  return (
    <DeletionPage onBack={props.onBack} footer={footer}>
      <ScreenHeading heading={strings.heading} intro={strings.intro} />
      {family.status === 'loading' ? (
        <Text className="lantern-intro-text">{strings.loading}</Text>
      ) : null}
      {family.status === 'ready' && children.length < 2 ? (
        <OnlyChildNotice onDeleteFamily={props.onDeleteFamily} />
      ) : null}
      {children.length >= 2 ? (
        <ChildChoices options={children} pickedId={picked?.id ?? null} onPick={props.onPick} />
      ) : null}
      {picked === null ? null : (
        <>
          <WarningCard
            heading={strings.warningHeading(picked.name)}
            text={strings.warning(picked.name)}
          />
          <TextField
            label={strings.nameLabel(picked.name)}
            hint={strings.nameHint}
            value={props.typedName}
            onChangeText={props.onTypeName}
            maxLength={NAME_MAX_LENGTH}
          />
          <EmailNote text={code.emailNote(props.email)} />
        </>
      )}
      {failureMessage ? <ErrorText>{failureMessage}</ErrorText> : null}
    </DeletionPage>
  );
}
