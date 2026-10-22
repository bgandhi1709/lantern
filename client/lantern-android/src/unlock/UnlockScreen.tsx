import { UnlockView } from './UnlockView';
import { useUnlock } from './useUnlock';

type Props = { onOpened: () => void; onBack: () => void };

export function UnlockScreen({ onOpened, onBack }: Props) {
  const unlock = useUnlock(onOpened);

  return (
    <UnlockView
      phase={unlock.phase}
      value={unlock.value}
      opening={unlock.opening}
      wrong={unlock.wrong}
      somethingWentWrong={unlock.somethingWentWrong}
      secondsLeft={unlock.secondsLeft}
      onChange={unlock.change}
      onOpen={unlock.open}
      onLoadAgain={unlock.loadAgain}
      onBack={() => (unlock.busy.current ? undefined : onBack())}
    />
  );
}
