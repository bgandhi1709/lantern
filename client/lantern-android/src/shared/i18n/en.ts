// Every piece of text the Parent sees. A new language is a copy of this file with the values
// translated; its type is Strings, so a missing or extra key fails the build.
export const en = {
  appName: 'Lantern',
  mark: {
    label: 'A mother teaching her child from an open book',
  },
  opening: {
    status: 'Opening Lantern',
  },
  start: {
    heading: 'Teach your child with confidence.',
    subheading:
      "Lantern does the preparation from your child's own school books, so your time goes into teaching.",
    note: 'Free and non-commercial.',
    continueWithGoogle: 'Continue with Google',
    signingIn: 'Signing in…',
    signInFailed: "Couldn't sign in. Try again.",
    features: {
      teach: {
        title: 'Teach a chapter',
        text: 'A ready plan for each chapter: what to teach, and how.',
      },
      answer: {
        title: 'Answer their questions',
        text: 'Answers from their own book, ready when they ask.',
      },
      progress: {
        title: 'See their progress',
        text: 'Know what stuck, and where to help next.',
      },
    },
  },
  signedIn: {
    checking: 'Reaching Lantern…',
    waking: 'Waking Lantern up, this can take up to a minute…',
    registered: 'Signed in. Lantern knows your Family.',
    notRegistered: 'Signed in. You have not registered a Family yet.',
    unreachable: "Can't reach Lantern",
    checkAgain: 'Check again',
    signOut: 'Sign out',
  },
};

export type Strings = typeof en;
