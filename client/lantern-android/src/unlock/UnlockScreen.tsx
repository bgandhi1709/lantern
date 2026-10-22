import { useState } from 'react';

import { StartAgainView } from './StartAgainView';
import { UnlockView } from './UnlockView';
import { useStartAgain } from './useStartAgain';
import { useUnlock } from './useUnlock';

type Props = { onOpened: () => void; onStartedAgain: () => void; onBack: () => void };

export function UnlockScreen({ onOpened, onStartedAgain, onBack }: Props) {
  const [confirming, setConfirming] = useState(false);
  const unlock = useUnlock(onOpened);
  const again = useStartAgain(onStartedAgain);

  if (confirming) {
    return (
      <StartAgainView
        deleting={again.deleting}
        failed={again.failed}
        onDelete={again.remove}
        onKeep={() => (again.busy.current ? undefined : setConfirming(false))}
      />
    );
  }
  return (
    <UnlockView
      phase={unlock.phase}
      kind={unlock.kind}
      value={unlock.value}
      opening={unlock.opening}
      wrong={unlock.wrong}
      somethingWentWrong={unlock.somethingWentWrong}
      secondsLeft={unlock.secondsLeft}
      onKind={unlock.pickKind}
      onChange={unlock.change}
      onOpen={unlock.open}
      onLoadAgain={unlock.loadAgain}
      onStartAgain={() => setConfirming(true)}
      onBack={() => (unlock.busy.current ? undefined : onBack())}
    />
  );
}
