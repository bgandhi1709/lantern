import { useRouter } from 'expo-router';

import { UnlockScreen } from '../src/unlock';

export default function Unlock() {
  const router = useRouter();
  return <UnlockScreen onOpened={() => router.dismissTo('/')} onBack={() => router.back()} />;
}
