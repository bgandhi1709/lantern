import { useRouter } from 'expo-router';

import { useRegistrationCheck } from '../src/auth';
import { StartAgainScreen } from '../src/unlock';

export default function StartAgain() {
  const router = useRouter();
  const { recheck } = useRegistrationCheck();
  return (
    <StartAgainScreen
      onDeleted={() => {
        recheck();
        router.dismissTo('/');
      }}
      onKeep={() => router.back()}
    />
  );
}
