import { fakeRestoredSession } from '../../test/fakeFirebaseAuth';
import { readValue, writeValue } from '../storage';
import { endSession, getIdToken, onSessionChange, startSession } from './session';

const flush = () => new Promise<void>((resolve) => setImmediate(resolve));

describe('session', () => {
  it('has no token when nobody is signed in', async () => {
    expect(await getIdToken()).toBeNull();
  });

  it('gives the current token of a restored session', async () => {
    fakeRestoredSession('abc');

    expect(await getIdToken()).toBe('abc');
  });

  it('reports the restored session, then each sign-in and sign-out', async () => {
    fakeRestoredSession();
    const seen: boolean[] = [];
    const stop = onSessionChange((signedIn) => seen.push(signedIn));
    await flush();

    await endSession();
    await startSession('google-token');
    stop();
    await endSession();

    expect(seen).toEqual([true, false, true]);
  });

  it('signs in with the Google ID token', async () => {
    await startSession('google-token');

    expect(await getIdToken()).toBe('google-token');
  });

  it('wipes the device storage when the session ends', async () => {
    await startSession('google-token');
    await writeValue('welcomeDismissed', true);
    await writeValue('familyKey', 'key');

    await endSession();

    expect(await getIdToken()).toBeNull();
    expect(await readValue('welcomeDismissed')).toBe(false);
    expect(await readValue('familyKey')).toBeNull();
  });
});
