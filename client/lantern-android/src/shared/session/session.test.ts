import { failTokenRefresh, fakeRestoredSession } from '../../test/fakeFirebaseAuth';
import { keepFamilyKey, readFamilyKey, readValue, writeValue } from '../storage';
import { endSession, getIdToken, getParent, onSessionChange, startSession } from './session';

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

  it('ends only the sign-in, so the phone keeps its Family key and settings', async () => {
    await startSession('google-token');
    await writeValue('welcomeDismissed', true);
    await keepFamilyKey('some-uid', 'key');

    await endSession();

    expect(await getIdToken()).toBeNull();
    expect(await readValue('welcomeDismissed')).toBe(true);
    expect(await readFamilyKey('some-uid')).toBe('key');
  });

  it.each([
    'auth/invalid-refresh',
    'auth/user-token-expired',
    'auth/invalid-user-token',
    'auth/user-not-found',
    'auth/user-disabled',
  ])('ends the session when Firebase says the sign-in is no longer valid (%s)', async (code) => {
    fakeRestoredSession();
    failTokenRefresh(code);
    await writeValue('welcomeDismissed', true);
    const seen: boolean[] = [];
    onSessionChange((signedIn) => seen.push(signedIn));
    await flush();

    await expect(getIdToken()).rejects.toThrow();

    expect(seen).toEqual([true, false]);
    expect(await readValue('welcomeDismissed')).toBe(true);
  });

  it('keeps the session when the token cannot be refreshed only because of the network', async () => {
    fakeRestoredSession();
    failTokenRefresh('auth/network-request-failed');
    const seen: boolean[] = [];
    onSessionChange((signedIn) => seen.push(signedIn));
    await flush();

    await expect(getIdToken()).rejects.toThrow();

    expect(seen).toEqual([true]);
  });

  it('names the Parent from the Google account', () => {
    fakeRestoredSession('abc', { displayName: 'Asha Verma', email: 'asha.verma@gmail.com' });

    expect(getParent()).toEqual({
      uid: 'asha-uid',
      name: 'Asha Verma',
      email: 'asha.verma@gmail.com',
    });
  });

  it('uses the email as the name when Google gives none, and has nothing when signed out', () => {
    fakeRestoredSession('abc', { displayName: null, email: 'asha.verma@gmail.com' });
    expect(getParent().name).toBe('asha.verma@gmail.com');

    fakeRestoredSession('abc', { displayName: '', email: 'asha.verma@gmail.com' });
    expect(getParent().name).toBe('asha.verma@gmail.com');
  });
});
