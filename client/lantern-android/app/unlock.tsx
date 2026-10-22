import { useRouter } from 'expo-router';

import { useRegistrationCheck } from '../src/auth';
import { UnlockScreen } from '../src/unlock';

export default function Unlock() {
  const router = useRouter();
  const { recheck } = useRegistrationCheck();
  return (
    <UnlockScreen
      onOpened={() => router.dismissTo('/')}
      onStartedAgain={() => {
        recheck();
        router.dismissTo('/');
      }}
      onBack={() => router.back()}
    />
  );
}
