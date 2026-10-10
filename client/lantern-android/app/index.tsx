import Storybook from '../.rnstorybook';
import { AuthGate, SignedInPlaceholder } from '../src/auth';
import { config } from '../src/shared/config';

export default function Index() {
  if (config.storybookEnabled) return <Storybook />;
  return <AuthGate home={<SignedInPlaceholder />} />;
}
