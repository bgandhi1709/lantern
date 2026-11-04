import { useStrings } from '../shared/i18n';
import { ErrorText } from '../shared/ui/ErrorText';
import { ScreenHeading } from '../shared/ui/ScreenHeading';
import { TextField } from '../shared/ui/TextField';
import { WarningCard } from '../shared/ui/WarningCard';
import type { DeleteFailure } from './deleteFailure';
import { DeletionPage } from './DeletionPage';
import { EmailNote } from './EmailNote';
import { SendCodeButtons } from './SendCodeButtons';
import { childrenOf } from './useDeletableFamily';
import type { FamilyState } from './useDeletableFamily';

const CONFIRM_MAX_LENGTH = 20;

type Props = {
  family: FamilyState;
  typed: string;
  canSend: boolean;
  sending: boolean;
  failure: DeleteFailure | null;
  email: string;
  onType: (text: string) => void;
  onSend: () => void;
  onCancel: () => void;
  onBack: () => void;
};

export function DeleteFamilyConfirmView(props: Props) {
  const strings = useStrings();
  const family = strings.deleteFamily;
  const names = childrenOf(props.family).map((child) => child.name);
  const warning = names.length > 0 ? family.warningWithNames(family.names(names)) : family.warning;
  const footer = (
    <SendCodeButtons
      canSend={props.canSend}
      sending={props.sending}
      cancelLabel={family.keep}
      onSend={props.onSend}
      onCancel={props.onCancel}
    />
  );

  return (
    <DeletionPage onBack={props.onBack} footer={footer}>
      <ScreenHeading heading={family.heading} intro={family.intro} />
      <WarningCard heading={family.warningHeading} text={warning} />
      <TextField
        label={family.typeLabel(family.confirmWord)}
        hint={family.typeHint}
        value={props.typed}
        onChangeText={props.onType}
        maxLength={CONFIRM_MAX_LENGTH}
      />
      <EmailNote text={strings.deleteCode.emailNote(props.email)} />
      {props.failure ? <ErrorText>{strings.deleteCode.failures[props.failure]}</ErrorText> : null}
    </DeletionPage>
  );
}
