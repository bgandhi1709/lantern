import { StartView } from './StartView';
import { useGoogleSignIn } from './useGoogleSignIn';

export function StartScreen() {
  const { status, signIn } = useGoogleSignIn();

  return <StartView status={status} onSignIn={signIn} />;
}
