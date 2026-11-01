import { useEffect } from 'react';
import { BackHandler } from 'react-native';

import { getParent } from '../shared/session';
import { DeleteChildCodeView } from './DeleteChildCodeView';
import { DeleteChildConfirmView } from './DeleteChildConfirmView';
import { useDeleteChild } from './useDeleteChild';

type Props = { onDeleted: () => void; onBack: () => void; onDeleteFamily: () => void };

export function DeleteChildScreen({ onDeleted, onBack, onDeleteFamily }: Props) {
  const deletion = useDeleteChild(onDeleted);
  const email = getParent().email;
  const inCode = deletion.step === 'code';

  const goBack = () => {
    if (deletion.isBusy()) return;
    if (inCode) deletion.backToName();
    else onBack();
  };

  const cancel = () => (deletion.isBusy() ? undefined : onBack());

  useEffect(() => {
    const press = BackHandler.addEventListener('hardwareBackPress', () => {
      goBack();
      return true;
    });
    return () => press.remove();
  });

  if (inCode) {
    return (
      <DeleteChildCodeView
        email={email}
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
