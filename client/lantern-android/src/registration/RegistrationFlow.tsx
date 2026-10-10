import { useEffect, useState } from 'react';
import { BackHandler } from 'react-native';

import { AboutYouScreen } from './AboutYouScreen';
import { BoardNextView } from './BoardNextView';
import { NOTICE_VERSION } from './notice';
import { NoticeView } from './NoticeView';
import { RegistrationDraftProvider } from './RegistrationDraft';

type Step = 'about' | 'notice' | 'board';

// The steps share one draft, so going back never loses what the Parent typed.
export function RegistrationFlow() {
  const [step, setStep] = useState<Step>('about');
  const backToAbout = () => setStep('about');

  useEffect(() => {
    if (step === 'about') return;
    const press = BackHandler.addEventListener('hardwareBackPress', () => {
      backToAbout();
      return true;
    });
    return () => press.remove();
  }, [step]);

  const steps: Record<Step, React.ReactNode> = {
    about: (
      <AboutYouScreen onOpenNotice={() => setStep('notice')} onContinue={() => setStep('board')} />
    ),
    notice: <NoticeView version={NOTICE_VERSION} onBack={backToAbout} />,
    board: <BoardNextView onBack={backToAbout} />,
  };

  return <RegistrationDraftProvider>{steps[step]}</RegistrationDraftProvider>;
}
