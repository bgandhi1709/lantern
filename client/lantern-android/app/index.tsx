import Storybook from '../.rnstorybook';
import { AuthGate, SignedInPlaceholder } from '../src/auth';
import { RegistrationFlow } from '../src/registration';
import { config } from '../src/shared/config';

export default function Index() {
  if (config.storybookEnabled) return <Storybook />;
  return <AuthGate home={<SignedInPlaceholder />} registration={<RegistrationFlow />} />;
}
