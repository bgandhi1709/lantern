import { useRouter } from 'expo-router';

import { StartAgainScreen } from '../src/unlock';

export default function StartAgain() {
  const router = useRouter();
  return (
    <StartAgainScreen
      onContinue={() => router.push('/delete-family')}
      onKeep={() => router.back()}
    />
  );
}
