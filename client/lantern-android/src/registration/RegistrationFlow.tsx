import { useEffect, useState } from 'react';
import { BackHandler } from 'react-native';

import { AboutYouScreen } from './AboutYouScreen';
import { BoardScreen } from './BoardScreen';
import { PLACEHOLDER_NOTICE_VERSION } from './notice';
import { NoticeView } from './NoticeView';
import { FirstChildScreen } from './FirstChildScreen';
import { PassphraseScreen } from './PassphraseScreen';
import { RecoveryCodeScreen } from './RecoveryCodeScreen';
import { RegistrationDraftProvider } from './RegistrationDraft';

type Step = 'about' | 'notice' | 'board' | 'passphrase' | 'recovery' | 'firstChild';

// The steps share one draft, so going back never loses what the Parent typed.
export function RegistrationFlow() {
  const [step, setStep] = useState<Step>('about');
  const backToAbout = () => setStep('about');
  const backToBoard = () => setStep('board');
  const backToRecovery = () => setStep('recovery');

  useEffect(() => {
    // The First child step handles its own Back, because it must ignore it while Finish runs.
    if (step === 'about' || step === 'firstChild') return;
    const press = BackHandler.addEventListener('hardwareBackPress', () => {
      // The Recovery code is shown once, so Android's back button does nothing there.
      if (step === 'recovery') return true;
      if (step === 'passphrase') backToBoard();
      else backToAbout();
      return true;
    });
    return () => press.remove();
  }, [step]);

  const steps: Record<Step, React.ReactNode> = {
    about: (
      <AboutYouScreen onOpenNotice={() => setStep('notice')} onContinue={() => setStep('board')} />
    ),
    notice: <NoticeView version={PLACEHOLDER_NOTICE_VERSION} onBack={backToAbout} />,
    board: <BoardScreen onContinue={() => setStep('passphrase')} onBack={backToAbout} />,
    passphrase: <PassphraseScreen onCreated={() => setStep('recovery')} onBack={backToBoard} />,
    recovery: <RecoveryCodeScreen onContinue={() => setStep('firstChild')} />,
    firstChild: <FirstChildScreen onBack={backToRecovery} />,
  };

  return <RegistrationDraftProvider>{steps[step]}</RegistrationDraftProvider>;
}
