import '@testing-library/react-native/matchers';
import { render, screen, userEvent } from '@testing-library/react-native';
import { Text } from 'react-native';

import { AuthGate, AuthProvider } from '../auth';
import { ApiError, api } from '../shared/api';
import { clearAll, readValue } from '../shared/storage';
import { lockText } from '../shared/crypto';
import { AARYA, ROHAN, answerLikeTheApi, registerFamilyOnThisPhone } from '../test/fakeFamily';
import { fakeRestoredSession } from '../test/fakeFirebaseAuth';
import { GoogleSignin } from '../test/fakeGoogleSignIn';
import { HomeScreen } from './HomeScreen';

jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'));

const get = api.get as jest.Mock;

const onOpenSubject = jest.fn();
const onAddChild = jest.fn();
const onOpenSettings = jest.fn();

async function openHome() {
  answerLikeTheApi(get, await registerFamilyOnThisPhone());
  fakeRestoredSession();
  const user = userEvent.setup();
  const view = await render(
    <HomeScreen
      onOpenSubject={onOpenSubject}
      onAddChild={onAddChild}
      onOpenSettings={onOpenSettings}
    />,
  );
  await screen.findByText('Science');
  return { user, view };
}

beforeEach(async () => {
  await clearAll();
  get.mockReset();
  onOpenSubject.mockReset();
  onAddChild.mockReset();
  onOpenSettings.mockReset();
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
      />,
    );

    expect(await screen.findByText('English')).toBeOnTheScreen();
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
      />,
    );
    await screen.findByText('Science');
    expect(screen.queryByText('Welcome to Lantern')).not.toBeOnTheScreen();
  });

  it('shows Teach, Check and Progress disabled', async () => {
    await openHome();

    ['Teach', 'Check', 'Progress'].forEach((tab) =>
      expect(screen.getByLabelText(`${tab}, available later`)).toBeDisabled(),
    );
    expect(screen.getByLabelText('Home')).not.toBeDisabled();
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
      />,
    );

    expect(await screen.findByText("Getting Aarya's Subjects ready…")).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Try again' })).not.toBeOnTheScreen();
  });

  it('asks the Parent to unlock the Family on a phone with no Family key', async () => {
    answerLikeTheApi(get, 'unused');
    fakeRestoredSession();
    await render(
      <HomeScreen
        onOpenSubject={onOpenSubject}
        onAddChild={onAddChild}
        onOpenSettings={onOpenSettings}
      />,
    );

    expect(await screen.findByText('Unlock your Family')).toBeOnTheScreen();
    expect(get).not.toHaveBeenCalled();
  });

  it('signs out, clears everything stored and returns to Start', async () => {
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
    expect(await readValue('activeChildId')).toBeNull();
    expect(await readValue('welcomeDismissed')).toBe(false);
    expect(await readValue('familyKey')).toBeNull();
  });
});
