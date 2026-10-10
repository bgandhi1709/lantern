import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';

import '../global.css';
import { AuthProvider } from '../src/auth';
import '../src/shared/config';

export default function RootLayout() {
  return (
    <AuthProvider>
      <StatusBar style="dark" />
      <Stack screenOptions={{ headerShown: false }} />
    </AuthProvider>
  );
}
