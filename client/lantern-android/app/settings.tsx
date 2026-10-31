import { useRouter } from 'expo-router';

import { SettingsScreen } from '../src/settings';

export default function Settings() {
  const router = useRouter();
  return (
    <SettingsScreen
      onBack={() => router.back()}
      onUnlock={() => router.push('/unlock')}
      onStartAgain={() => router.push('/start-again')}
      onDeleteChild={() => router.push('/delete-child')}
      onDeleteFamily={() => router.push('/delete-family')}
    />
  );
}
