import { useEffect, useState } from 'react';
import { BackHandler } from 'react-native';

import { setCollectionEnabled, trackRegistrationStep, useTrackScreen } from '../shared/analytics';
import type { RegistrationStep, ScreenName } from '../shared/analytics';

import { AboutYouScreen } from './AboutYouScreen';
import { BoardScreen } from './BoardScreen';
import { PLACEHOLDER_NOTICE_VERSION } from './notice';
import { NoticeView } from './NoticeView';
import { FirstChildScreen } from './FirstChildScreen';
import { RecoveryCodeScreen } from './RecoveryCodeScreen';
import { RegistrationDraftProvider, useRegistrationDraft } from './RegistrationDraft';

type Step = 'about' | 'notice' | 'board' | 'recovery' | 'firstChild';

const SCREEN_OF: Record<Step, ScreenName> = {
  about: 'about-you',
  notice: 'privacy-notice',
  board: 'board',
  recovery: 'recovery-code',
  firstChild: 'first-child',
};

// The privacy notice is a page of About you, not a step of its own.
const STEP_OF: Partial<Record<Step, RegistrationStep>> = {
  about: 'about-you',
  board: 'board',
  recovery: 'recovery-code',
  firstChild: 'first-child',
};

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
  useTrackScreen(SCREEN_OF[step]);
  useEffect(() => {
    const reached = STEP_OF[step];
    if (reached) trackRegistrationStep(reached, 'reached');
  }, [step]);
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
    notice: <NoticeView version={PLACEHOLDER_NOTICE_VERSION} onBack={backToAbout} />,
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
