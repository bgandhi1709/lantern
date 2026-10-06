import type { ReactNode } from 'react';
import { View } from 'react-native';

import { colors } from '../shared/ui/theme';
import { useSessionState } from './AuthProvider';
import { StartScreen } from './StartScreen';

// While the saved sign-in is restored it shows an empty page; the Opening screen replaces that.
export function AuthGate({ signedIn }: { signedIn: ReactNode }) {
  const state = useSessionState();

  if (state === 'restoring') return <View style={{ flex: 1, backgroundColor: colors.ground }} />;
  return state === 'signedIn' ? signedIn : <StartScreen />;
}
