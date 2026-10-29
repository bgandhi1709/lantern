import { useEffect, useState } from 'react';
import { BackHandler } from 'react-native';

import { setCollectionEnabled } from '../shared/analytics';

import { AboutYouScreen } from './AboutYouScreen';
import { BoardScreen } from './BoardScreen';
import { PLACEHOLDER_NOTICE_VERSION } from './notice';
import { NoticeScreen } from './NoticeScreen';
import { FirstChildScreen } from './FirstChildScreen';
import { RecoveryCodeScreen } from './RecoveryCodeScreen';
import { RegistrationDraftProvider, useRegistrationDraft } from './RegistrationDraft';

type Step = 'about' | 'notice' | 'board' | 'recovery' | 'firstChild';

// Usage numbers are collected only while the notice is ticked.
function ConsentSync() {
  const { draft } = useRegistrationDraft();
  useEffect(() => {
    setCollectionEnabled(draft.noticeAccepted);
  }, [draft.noticeAccepted]);
  return null;
}

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
      if (step === 'recovery') backToBoard();
      else backToAbout();
      return true;
    });
    return () => press.remove();
  }, [step]);

  const steps: Record<Step, React.ReactNode> = {
    about: (
      <AboutYouScreen onOpenNotice={() => setStep('notice')} onContinue={() => setStep('board')} />
    ),
    notice: <NoticeScreen version={PLACEHOLDER_NOTICE_VERSION} onBack={backToAbout} />,
    board: <BoardScreen onContinue={() => setStep('recovery')} onBack={backToAbout} />,
    recovery: <RecoveryCodeScreen onContinue={() => setStep('firstChild')} onBack={backToBoard} />,
    firstChild: <FirstChildScreen onBack={backToRecovery} />,
  };

  return (
    <RegistrationDraftProvider>
      <ConsentSync />
      {steps[step]}
    </RegistrationDraftProvider>
  );
}
