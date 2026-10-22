import { useRouter } from 'expo-router';

import { AppearanceScreen } from '../src/settings';

export default function Settings() {
  const router = useRouter();
  return (
    <AppearanceScreen
      onBack={() => router.back()}
      onUnlock={() => router.push('/unlock')}
      onStartAgain={() => router.push('/start-again')}
    />
  );
}
