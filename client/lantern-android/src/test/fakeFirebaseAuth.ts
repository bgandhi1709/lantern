export const FAKE_UID = 'asha-uid';

type FakeUser = {
  uid: string;
  getIdToken: jest.Mock<Promise<string>, []>;
  displayName: string | null;
  email: string | null;
};
type Profile = Pick<FakeUser, 'displayName' | 'email'> & { uid?: string };
type Listener = (user: FakeUser | null) => void;

let user: FakeUser | null = null;
const listeners = new Set<Listener>();

const notify = () => listeners.forEach((listener) => listener(user));
const asha: Profile = { displayName: 'Asha Verma', email: 'asha.verma@gmail.com' };
const userWithToken = (token: string, profile: Profile = asha): FakeUser => ({
  uid: profile.uid ?? FAKE_UID,
  getIdToken: jest.fn(async () => token),
  ...profile,
});

const auth = {
  get currentUser() {
    return user;
  },
};

export const getAuth = () => auth;
export const connectAuthEmulator = jest.fn();
export const GoogleAuthProvider = { credential: (idToken: string) => ({ idToken }) };

// Like the real SDK, the first call reports the restored session after a tick.
export const onAuthStateChanged = (_auth: unknown, listener: Listener) => {
  listeners.add(listener);
  queueMicrotask(() => listener(user));
  return () => {
    listeners.delete(listener);
  };
};

export const signInWithCredential = jest.fn(
  async (_auth: unknown, credential: { idToken: string }) => {
    user = userWithToken(credential.idToken);
    notify();
  },
);

export const signOut = jest.fn(async () => {
  user = null;
  notify();
});

export function fakeRestoredSession(token = 'restored-id-token', profile?: Profile) {
  user = userWithToken(token, profile);
}

// Makes the restored user's token refresh fail, like Firebase does for a revoked sign-in.
export function failTokenRefresh(code: string) {
  user?.getIdToken.mockRejectedValue(
    Object.assign(new Error(`[${code}] The sign-in failed.`), { code }),
  );
}

export function resetFakeAuth() {
  user = null;
  listeners.clear();
  signInWithCredential.mockClear();
  signOut.mockClear();
}
