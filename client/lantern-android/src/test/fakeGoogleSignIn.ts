type Outcome =
  { type: 'success'; data: { idToken: string | null } } | { type: 'cancelled'; data: null } | Error;

const success: Outcome = { type: 'success', data: { idToken: 'google-id-token' } };
let outcome: Outcome = success;
let configured = false;

// Like the native module, signOut fails until configure() has been called in this app run.
export const GoogleSignin = {
  configure: jest.fn(() => {
    configured = true;
  }),
  signIn: jest.fn(async () => {
    if (outcome instanceof Error) throw outcome;
    return outcome;
  }),
  signOut: jest.fn(async () => {
    if (!configured) throw new Error('apiClient is null - call configure() first');
  }),
};

export function setGoogleOutcome(next: Outcome) {
  outcome = next;
}

export function resetFakeGoogleSignIn() {
  outcome = success;
  configured = false;
  GoogleSignin.configure.mockClear();
  GoogleSignin.signIn.mockClear();
  GoogleSignin.signOut.mockClear();
}
