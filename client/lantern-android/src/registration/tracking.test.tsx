import '@testing-library/react-native/matchers';
import { fireEvent, render, screen, userEvent } from '@testing-library/react-native';
import { Linking, Text } from 'react-native';

import { AuthGate, AuthProvider } from '../auth';
import { ApiError, api } from '../shared/api';
import { setCollectionEnabled } from '../shared/analytics';
import { logEvent, logScreenView } from '../test/fakeFirebaseAnalytics';
import { fakeRestoredSession } from '../test/fakeFirebaseAuth';
import { RegistrationFlow } from '.';

jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'));

const get = api.get as jest.Mock;
const post = api.post as jest.Mock;

const THIS_YEAR = new Date().getFullYear();

const eventsNamed = (name: string) =>
  logEvent.mock.calls.filter(([, eventName]) => eventName === name).map(([, , params]) => params);
const steps = () => eventsNamed('registration_step');
const taps = () => eventsNamed('tap');
const viewedScreens = () => logScreenView.mock.calls.map(([, params]) => params);

const register = async () => {
  const user = userEvent.setup();
  fakeRestoredSession();
  get.mockRejectedValue(new ApiError('not-registered', 404));
  post.mockResolvedValue({ familyId: 'a-family' });
  await render(
    <AuthProvider>
      <AuthGate home={<Text>Home</Text>} registration={<RegistrationFlow />} />
    </AuthProvider>,
  );
  await user.type(await screen.findByLabelText('Where do you live?'), 'Pune, Maharashtra');
  await user.press(screen.getByRole('checkbox', { name: 'I agree to the privacy notice' }));
  await user.press(screen.getByRole('button', { name: 'Continue' }));
  await user.press(screen.getByRole('radio', { name: /^CBSE/ }));
  await user.press(screen.getByRole('button', { name: 'Continue' }));
  jest.spyOn(Linking, 'openURL').mockResolvedValue(true);
  await user.press(await screen.findByRole('button', { name: 'Email it to myself' }));
  await user.press(screen.getByRole('checkbox', { name: 'I have sent the email to myself.' }));
  await user.press(screen.getByRole('button', { name: 'Continue' }));
  await user.type(await screen.findByTestId('child-1-name'), 'Aarav');
  await user.press(screen.getByRole('radio', { name: 'Class 4' }));
  await fireEvent(screen.getAllByLabelText('Birth year')[0], 'valueChange', String(THIS_YEAR - 9));
  await user.press(screen.getByRole('button', { name: 'Finish' }));
  await screen.findByText('Home');
};

beforeEach(() => setCollectionEnabled(false));

describe('registering a Family with analytics on', () => {
  it('reports each step reached and finished, from the first one the Parent agreed on', async () => {
    await register();

    expect(steps()).toEqual([
      { step: 'about-you', state: 'finished' },
      { step: 'board', state: 'reached' },
      { step: 'board', state: 'finished' },
      { step: 'recovery-code', state: 'reached' },
      { step: 'recovery-code', state: 'finished' },
      { step: 'first-child', state: 'reached' },
      { step: 'first-child', state: 'finished' },
    ]);
  });

  it('reports the screens viewed and the buttons tapped by name only', async () => {
    await register();

    expect(viewedScreens().map((view) => view.screen_name)).toEqual([
      'board',
      'recovery-code',
      'first-child',
    ]);
    expect(taps()).toEqual([
      { name: 'about-continue' },
      { name: 'board-continue' },
      { name: 'share-recovery-code' },
      { name: 'recovery-continue' },
      { name: 'finish-registration' },
    ]);
  });

  it('never sends what the Parent typed or chose', async () => {
    await register();

    const sent = JSON.stringify([...logEvent.mock.calls, ...logScreenView.mock.calls]);
    for (const personal of ['Aarav', 'Pune', 'Maharashtra', 'Asha', 'Verma', 'gmail', 'cbse']) {
      expect(sent).not.toContain(personal);
    }
  });
});
