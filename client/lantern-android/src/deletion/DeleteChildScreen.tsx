import { useStrings } from '../shared/i18n';
import { getParent } from '../shared/session';
import { DeleteCodeView } from './DeleteCodeView';
import { DeleteChildConfirmView } from './DeleteChildConfirmView';
import { useDeleteChild } from './useDeleteChild';
import { useDeletionBack } from './useDeletionBack';

type Props = { onDeleted: () => void; onBack: () => void; onDeleteFamily: () => void };

export function DeleteChildScreen({ onDeleted, onBack, onDeleteFamily }: Props) {
  const strings = useStrings().deleteChild;
  const cancelLabel = useStrings().deleteCode.cancel;
  const deletion = useDeleteChild(onDeleted);
  const email = getParent().email;
  const inCode = deletion.step === 'code';

  const { goBack, cancel } = useDeletionBack(deletion, onBack);

  if (inCode) {
    return (
      <DeleteCodeView
        intro={strings.codeIntro(email)}
        deleteLabel={strings.delete}
        cancelLabel={cancelLabel}
        code={deletion.code}
        verified={deletion.verified}
        work={deletion.work}
        failure={deletion.failure}
        resent={deletion.resent}
        onTypeCode={deletion.typeCode}
        onResend={() => deletion.emailCode(true)}
        onDelete={deletion.remove}
        onCancel={cancel}
        onBack={goBack}
      />
    );
  }

  return (
    <DeleteChildConfirmView
      family={deletion.family}
      picked={deletion.picked}
      typedName={deletion.typedName}
      canSend={deletion.nameMatches}
      sending={deletion.work === 'requesting'}
      failure={deletion.failure}
      email={email}
      onPick={deletion.pick}
      onTypeName={deletion.typeName}
      onSend={() => deletion.emailCode(false)}
      onCancel={cancel}
      onBack={goBack}
      onDeleteFamily={onDeleteFamily}
    />
  );
}
