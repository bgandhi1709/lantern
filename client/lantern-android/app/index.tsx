import { AuthGate, SignedInPlaceholder } from '../src/auth';

export default function Index() {
  return <AuthGate signedIn={<SignedInPlaceholder />} />;
}
