import { useEffect, useState } from 'react';
import { BackHandler } from 'react-native';

import { AboutYouScreen } from './AboutYouScreen';
import { BoardScreen } from './BoardScreen';
import { NOTICE_VERSION } from './notice';
import { NoticeView } from './NoticeView';
import { PassphraseNextView } from './PassphraseNextView';
import { RegistrationDraftProvider } from './RegistrationDraft';

type Step = 'about' | 'notice' | 'board' | 'passphrase';

// The steps share one draft, so going back never loses what the Parent typed.
export function RegistrationFlow() {
  const [step, setStep] = useState<Step>('about');
  const backToAbout = () => setStep('about');
  const backToBoard = () => setStep('board');
  const goBack = step === 'passphrase' ? backToBoard : backToAbout;

  useEffect(() => {
    if (step === 'about') return;
    const press = BackHandler.addEventListener('hardwareBackPress', () => {
      goBack();
      return true;
    });
    return () => press.remove();
  }, [step, goBack]);

  const steps: Record<Step, React.ReactNode> = {
    about: (
      <AboutYouScreen onOpenNotice={() => setStep('notice')} onContinue={() => setStep('board')} />
    ),
    notice: <NoticeView version={NOTICE_VERSION} onBack={backToAbout} />,
    board: <BoardScreen onContinue={() => setStep('passphrase')} onBack={backToAbout} />,
    passphrase: <PassphraseNextView onBack={backToBoard} />,
  };

  return <RegistrationDraftProvider>{steps[step]}</RegistrationDraftProvider>;
}
