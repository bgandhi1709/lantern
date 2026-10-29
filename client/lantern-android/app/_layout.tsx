import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';

import '../global.css';
import { AuthProvider } from '../src/auth';
import { RouteTracker } from '../src/shared/analytics';
import '../src/shared/config';
import { ThemeProvider, useAppearance } from '../src/shared/theme';

function ThemedStatusBar() {
  const { dark } = useAppearance();
  return <StatusBar style={dark ? 'light' : 'dark'} />;
}

export default function RootLayout() {
  return (
    <ThemeProvider>
      <AuthProvider>
        <ThemedStatusBar />
        <RouteTracker />
        <Stack screenOptions={{ headerShown: false }} />
      </AuthProvider>
    </ThemeProvider>
  );
}
