import { useEffect } from 'react';
import { BackHandler } from 'react-native';

import { useStrings } from '../shared/i18n';
import { getParent } from '../shared/session';
import { DeleteCodeView } from './DeleteCodeView';
import { DeleteFamilyConfirmView } from './DeleteFamilyConfirmView';
import { useDeleteFamily } from './useDeleteFamily';

type Props = { onDeleted: () => void; onBack: () => void };

export function DeleteFamilyScreen({ onDeleted, onBack }: Props) {
  const strings = useStrings().deleteFamily;
  const deletion = useDeleteFamily(onDeleted);
  const email = getParent().email;
  const inCode = deletion.step === 'code';

  const goBack = () => {
    if (deletion.isBusy()) return;
    if (inCode) deletion.backToName();
    else onBack();
  };

  useEffect(() => {
    const press = BackHandler.addEventListener('hardwareBackPress', () => {
      goBack();
      return true;
    });
    return () => press.remove();
  });

  if (inCode) {
    return (
      <DeleteCodeView
        intro={strings.codeIntro(email)}
        deleteLabel={strings.delete}
        cancelLabel={strings.keep}
        lastStep={strings.lastStep}
        code={deletion.code}
        verified={deletion.verified}
        work={deletion.work}
        failure={deletion.failure}
        resent={deletion.resent}
        onTypeCode={deletion.typeCode}
        onResend={() => deletion.emailCode(true)}
        onDelete={deletion.remove}
        onCancel={() => (deletion.isBusy() ? undefined : onBack())}
        onBack={goBack}
      />
    );
  }

  return (
    <DeleteFamilyConfirmView
      family={deletion.family}
      typed={deletion.typed}
      canSend={deletion.wordMatches}
      sending={deletion.work === 'requesting'}
      failure={deletion.failure}
      email={email}
      onType={deletion.typeWord}
      onSend={() => deletion.emailCode(false)}
      onCancel={() => (deletion.isBusy() ? undefined : onBack())}
      onBack={goBack}
    />
  );
}
