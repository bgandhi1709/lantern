type Outcome =
  { type: 'success'; data: { idToken: string | null } } | { type: 'cancelled'; data: null } | Error;

const success: Outcome = { type: 'success', data: { idToken: 'google-id-token' } };
let outcome: Outcome = success;

export const GoogleSignin = {
  configure: jest.fn(),
  signIn: jest.fn(async () => {
    if (outcome instanceof Error) throw outcome;
    return outcome;
  }),
  signOut: jest.fn(async () => {}),
};

export function setGoogleOutcome(next: Outcome) {
  outcome = next;
}

export function resetFakeGoogleSignIn() {
  outcome = success;
  GoogleSignin.configure.mockClear();
  GoogleSignin.signIn.mockClear();
  GoogleSignin.signOut.mockClear();
}
