import '@testing-library/react-native/matchers';
import { render, screen, userEvent } from '@testing-library/react-native';
import { Text } from 'react-native';

import { AuthGate, AuthProvider } from '../auth';
import { ApiError, api } from '../shared/api';
import { clearAll, readFamilyKey, readValue, writeValue } from '../shared/storage';
import { lockText } from '../shared/crypto';
import { AARYA, ROHAN, answerLikeTheApi, registerFamilyOnThisPhone } from '../test/fakeFamily';
import { FAKE_UID, fakeRestoredSession } from '../test/fakeFirebaseAuth';
import { GoogleSignin } from '../test/fakeGoogleSignIn';
import { HomeScreen } from './HomeScreen';

jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'));
let mockFocused = true;
jest.mock('expo-router', () => ({ useIsFocused: () => mockFocused }));

const get = api.get as jest.Mock;

const onOpenSubject = jest.fn();
const onAddChild = jest.fn();
const onOpenSettings = jest.fn();
const onOpenTab = jest.fn();
const onUnlock = jest.fn();

async function openHome() {
  answerLikeTheApi(get, await registerFamilyOnThisPhone());
  fakeRestoredSession();
  const user = userEvent.setup();
  const view = await render(
    <HomeScreen
      onOpenSubject={onOpenSubject}
      onAddChild={onAddChild}
      onOpenSettings={onOpenSettings}
      onOpenTab={onOpenTab}
      onUnlock={onUnlock}
    />,
  );
  await screen.findByText('Science');
  return { user, view };
}

beforeEach(async () => {
  mockFocused = true;
  await clearAll();
  get.mockReset();
  onOpenSubject.mockReset();
  onAddChild.mockReset();
  onOpenSettings.mockReset();
  onOpenTab.mockReset();
  onUnlock.mockReset();
});

describe('Home', () => {
  it('greets the Parent by name and lists the active Child’s Subjects with their Chapters', async () => {
    await openHome();

    expect(screen.getByRole('header', { name: /, Asha$/ })).toBeOnTheScreen();
    expect(screen.getByText("Aarya's Subjects, Class 6")).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Science, 12 Chapters' })).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Mathematics, 14 Chapters' })).toBeOnTheScreen();
  });

  it('opens Settings from the gear in the header', async () => {
    const { user } = await openHome();

    await user.press(screen.getByRole('button', { name: 'Settings' }));

    expect(onOpenSettings).toHaveBeenCalledTimes(1);
  });

  it('opens a Subject with the Child and the Subject ids', async () => {
    const { user } = await openHome();

    await user.press(screen.getByRole('button', { name: 'Mathematics, 14 Chapters' }));

    expect(onOpenSubject).toHaveBeenCalledWith(AARYA, 'mathematics');
  });

  it('switches Child from the sheet and keeps only the id on the device', async () => {
    const { user } = await openHome();

    await user.press(screen.getByRole('button', { name: 'Switch child, now Aarya' }));
    await user.press(await screen.findByRole('button', { name: 'Rohan, Class 3' }));

    expect(await screen.findByText('English')).toBeOnTheScreen();
    expect(screen.queryByText('Science')).not.toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Switch child, now Rohan' })).toBeOnTheScreen();
    expect(await readValue('activeChildId')).toBe(ROHAN);
  });

  it('opens on the Child chosen last time after the app restarts', async () => {
    const { user, view } = await openHome();
    await user.press(screen.getByRole('button', { name: 'Switch child, now Aarya' }));
    await user.press(await screen.findByRole('button', { name: 'Rohan, Class 3' }));
    await screen.findByText('English');
    await view.unmount();

    await render(
      <HomeScreen
        onOpenSubject={onOpenSubject}
        onAddChild={onAddChild}
        onOpenSettings={onOpenSettings}
        onOpenTab={onOpenTab}
        onUnlock={onUnlock}
      />,
    );

    expect(await screen.findByText('English')).toBeOnTheScreen();
  });

  it('shows the Child chosen on another screen when the Parent comes back', async () => {
    const { view } = await openHome();
    const home = () => (
      <HomeScreen
        onOpenSubject={onOpenSubject}
        onAddChild={onAddChild}
        onOpenSettings={onOpenSettings}
        onOpenTab={onOpenTab}
        onUnlock={onUnlock}
      />
    );

    mockFocused = false;
    await view.rerender(home());
    await writeValue('activeChildId', ROHAN);
    mockFocused = true;
    await view.rerender(home());

    expect(await screen.findByText('English')).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Switch child, now Rohan' })).toBeOnTheScreen();
  });

  it('offers Add a child in the sheet and opens that route', async () => {
    const { user } = await openHome();

    await user.press(screen.getByRole('button', { name: 'Switch child, now Aarya' }));
    await user.press(await screen.findByRole('button', { name: 'Add a child' }));

    expect(onAddChild).toHaveBeenCalledTimes(1);
  });

  it('does not offer Add a child once the Family has six Children', async () => {
    const familyKey = await registerFamilyOnThisPhone();
    answerLikeTheApi(get, familyKey);
    const answer = get.getMockImplementation();
    get.mockImplementation(async (path: string, schema: unknown) => {
      if (path !== '/v1/me') return answer?.(path, schema);
      const names = ['Aarya', 'Rohan', 'Mira', 'Dev', 'Isha', 'Kabir'];
      return {
        children: names.map((name, index) => ({
          childId: index === 0 ? AARYA : `00000000-0000-4000-8000-00000000000${index}`,
          name: lockText(familyKey, name),
          classLevel: 3,
        })),
      };
    });
    fakeRestoredSession();
    const user = userEvent.setup();
    await render(
      <HomeScreen
        onOpenSubject={onOpenSubject}
        onAddChild={onAddChild}
        onOpenSettings={onOpenSettings}
        onOpenTab={onOpenTab}
        onUnlock={onUnlock}
      />,
    );

    await user.press(await screen.findByRole('button', { name: 'Switch child, now Aarya' }));

    expect(await screen.findByText('Who is learning?')).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Add a child' })).not.toBeOnTheScreen();
  });

  it('shows the welcome banner until it is dismissed, and not again after a restart', async () => {
    const { user, view } = await openHome();
    expect(await screen.findByText('Welcome to Lantern')).toBeOnTheScreen();

    await user.press(screen.getByRole('button', { name: 'Got it' }));

    expect(screen.queryByText('Welcome to Lantern')).not.toBeOnTheScreen();
    await view.unmount();
    await render(
      <HomeScreen
        onOpenSubject={onOpenSubject}
        onAddChild={onAddChild}
        onOpenSettings={onOpenSettings}
        onOpenTab={onOpenTab}
        onUnlock={onUnlock}
      />,
    );
    await screen.findByText('Science');
    expect(screen.queryByText('Welcome to Lantern')).not.toBeOnTheScreen();
  });

  it('opens the Coming next screen from Teach, Check and Progress, and stays on Home for Home', async () => {
    const { user } = await openHome();

    await user.press(screen.getByRole('tab', { name: 'Teach' }));
    await user.press(screen.getByRole('tab', { name: 'Check' }));
    await user.press(screen.getByRole('tab', { name: 'Progress' }));
    await user.press(screen.getByRole('tab', { name: 'Home' }));

    expect(onOpenTab).toHaveBeenCalledTimes(3);
    expect(screen.getByRole('tab', { name: 'Home' })).toBeSelected();
  });

  it('shows Try again when the Subjects cannot be loaded, and loads them on pressing it', async () => {
    answerLikeTheApi(get, await registerFamilyOnThisPhone());
    const answer = get.getMockImplementation();
    let failing = true;
    get.mockImplementation(async (path: string, schema: unknown) => {
      if (failing && path.endsWith('/subjects')) throw new ApiError('network', null);
      return answer?.(path, schema);
    });
    fakeRestoredSession();
    const user = userEvent.setup();
    await render(
      <HomeScreen
        onOpenSubject={onOpenSubject}
        onAddChild={onAddChild}
        onOpenSettings={onOpenSettings}
        onOpenTab={onOpenTab}
        onUnlock={onUnlock}
      />,
    );

    await user.press(await screen.findByRole('button', { name: 'Try again' }));
    failing = false;
    await user.press(await screen.findByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('Science')).toBeOnTheScreen();
  });

  it('shows a loading state, never the error, while the Child’s content is still being made', async () => {
    answerLikeTheApi(get, await registerFamilyOnThisPhone());
    const answer = get.getMockImplementation();
    get.mockImplementation(async (path: string, schema: unknown) => {
      if (path.endsWith('/subjects')) throw new ApiError('content-not-ready', 409);
      return answer?.(path, schema);
    });
    fakeRestoredSession();
    await render(
      <HomeScreen
        onOpenSubject={onOpenSubject}
        onAddChild={onAddChild}
        onOpenSettings={onOpenSettings}
        onOpenTab={onOpenTab}
        onUnlock={onUnlock}
      />,
    );

    expect(await screen.findByText("Getting Aarya's Subjects ready…")).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Try again' })).not.toBeOnTheScreen();
  });

  it('offers Unlock your Family on a phone with no Family key, and asks the API for nothing', async () => {
    answerLikeTheApi(get, 'unused');
    fakeRestoredSession();
    const user = userEvent.setup();
    await render(
      <HomeScreen
        onOpenSubject={onOpenSubject}
        onAddChild={onAddChild}
        onOpenSettings={onOpenSettings}
        onOpenTab={onOpenTab}
        onUnlock={onUnlock}
      />,
    );

    expect(await screen.findByRole('header', { name: 'Unlock your Family' })).toBeOnTheScreen();
    await user.press(screen.getByRole('button', { name: 'Unlock your Family' }));
    expect(onUnlock).toHaveBeenCalledTimes(1);
    expect(get).not.toHaveBeenCalled();
  });

  it('signs out to Start and keeps this phone’s Family key and choices', async () => {
    answerLikeTheApi(get, await registerFamilyOnThisPhone());
    fakeRestoredSession();
    const user = userEvent.setup();
    await render(
      <AuthProvider>
        <AuthGate
          home={
            <HomeScreen
              onOpenSubject={onOpenSubject}
              onAddChild={onAddChild}
              onOpenSettings={onOpenSettings}
              onOpenTab={onOpenTab}
              onUnlock={onUnlock}
            />
          }
          registration={<Text>Registration</Text>}
        />
      </AuthProvider>,
    );
    await screen.findByText('Science');
    await user.press(screen.getByRole('button', { name: 'Switch child, now Aarya' }));
    await user.press(await screen.findByRole('button', { name: 'Rohan, Class 3' }));
    await user.press(screen.getByRole('button', { name: 'Got it' }));

    await user.press(screen.getByRole('button', { name: 'Sign out' }));

    expect(await screen.findByRole('button', { name: 'Continue with Google' })).toBeOnTheScreen();
    expect(GoogleSignin.signOut).toHaveBeenCalled();
    expect(await readValue('activeChildId')).toBe(ROHAN);
    expect(await readValue('welcomeDismissed')).toBe(true);
    expect(await readFamilyKey(FAKE_UID)).not.toBeNull();
  });
});
