// Every piece of text the Parent sees. A new language is a copy of this file with the values
// translated; its type is Strings, so a missing or extra key fails the build.
export const en = {
  appName: 'Lantern',
  common: {
    signOut: 'Sign out',
  },
  mark: {
    label: 'A mother teaching her child from an open book',
  },
  opening: {
    status: 'Opening Lantern',
    waking: 'Waking Lantern up, this can take up to a minute…',
    unreachable: "Can't reach Lantern",
    tryAgain: 'Try again',
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
  registration: {
    back: 'Back',
    step: (step: number, total: number) => `Step ${step} of ${total}`,
    board: {
      heading: 'Which board does your child follow?',
      intro: "This decides which school books Lantern uses. You can't change it later.",
      groupLabel: 'Board',
      continue: 'Continue',
      cards: {
        cbse: { title: 'CBSE', subtitle: 'NCERT books', classes: 'Classes 1 to 10' },
        ssc: {
          title: 'Maharashtra State Board',
          subtitle: 'SSC · Balbharati books, English medium',
          classes: 'Classes 1 to 5',
        },
      },
    },
    passphrase: {
      heading: 'Choose a family passphrase',
      intro:
        "Your children's names and your details are private. This passphrase locks them on your phone before they are sent, so nobody at Lantern, and nobody who broke into our systems, could read them. It is there to protect you and your children.",
      noteTitle: 'Please keep it safe',
      note: "Lantern never sees your passphrase, so we can't reset or change it for you. That is what keeps your family's details safe: if we could open them, someone else could too. If you lose it and your recovery code, they can't be opened again, so pick something you will remember.",
      passphraseLabel: 'Passphrase',
      passphraseHint: 'At least 8 characters. A few words are easier to remember.',
      againLabel: 'Type it again',
      mismatch: 'The two passphrases are not the same.',
      understood: "I understand Lantern can't reset or change it, and I will keep it safe.",
      create: 'Create passphrase',
      creating: 'Creating…',
      failed: "Couldn't create the passphrase. Try again.",
    },
    recovery: {
      heading: 'Save your recovery code',
      intro:
        "If you forget the passphrase, this code is the only other way to open your family's details. We show it once because we don't keep a copy: that is what keeps it private to you.",
      codeLabel: 'RECOVERY CODE',
      email: 'Email it to myself',
      save: 'Save elsewhere',
      hint: "Email opens your mail app with a message to your Google address, and you send it yourself. Save elsewhere opens your phone's share sheet, for example to keep it in a notes app. Lantern never sends or sees it.",
      saved: 'I have saved my recovery code somewhere safe.',
      continue: 'Continue',
      mailSubject: 'Your Lantern recovery code',
      message: (code: string) =>
        `Your Lantern recovery code is ${code}\n\nIf you forget your passphrase, this code is the only other way to open your family's details. Keep it somewhere safe. Lantern does not keep a copy.`,
    },
    firstChildNext: 'First child comes next',
    notice: {
      heading: 'Your privacy, in plain words',
      intro:
        'Lantern is open about what it asks of you. Here is everything it collects, and why. If something here is not what you expected, do not agree to it.',
      version: (version: string) => `Notice version ${version}`,
      sections: [
        {
          title: 'What you tell us',
          text: [
            'Your name and email, from Google. They set up your Family. Your phone locks them before they reach Lantern, so Lantern cannot read them.',
            'Where you live (city and state). Lantern can read this. It helps us understand where Lantern is used.',
            "Your school board and each child's Class. Lantern can read these, so it can pick the right books and chapters.",
            "Each child's name, birth year and school. Your phone locks these before they reach Lantern, so Lantern cannot read them.",
          ],
        },
        {
          title: 'While you use the app',
          text: [
            'Lantern counts how the app is used, without knowing who you are: which screens open and for how long, which buttons are tapped, and how fast the app starts. Google, who provides this, also notes your phone model, your Android version and roughly which city your connection comes from. It is not your exact location.',
            'None of your details go with these numbers: not your name, email, your children, your Region or anything you type. They start only after you agree.',
          ],
        },
        {
          title: 'What stays on your phone',
          text: [
            "Your Google sign-in, kept by the phone itself. A secret key for your Family, kept in the phone's secure store so you do not have to type your passphrase every time. Nothing else about you is saved on the phone.",
          ],
        },
        {
          title: 'What we never do',
          text: [
            "We do not collect your exact location, contacts, photos or microphone, or the phone's advertising ID. We show no ads and we do not sell your data.",
            'Lantern runs on Microsoft Azure and Google Firebase, who store and process data on our behalf.',
          ],
        },
        {
          title: 'Your passphrase and your data',
          text: [
            'You will choose a passphrase for your Family. It cannot be reset. If you lose it and its recovery code, nobody, including Lantern, can open the locked details.',
            'Deleting your Family erases everything stored for it.',
          ],
        },
        {
          title: 'Who runs Lantern',
          text: ['Lantern is free and not run for profit.'],
        },
      ],
    },
    about: {
      heading: 'A little about you',
      intro: 'We took your name and email from your Google account.',
      fromGoogle: 'FROM GOOGLE',
      differentAccount: 'Not you? Use a different account',
      name: 'Name',
      email: 'Email',
      regionLabel: 'Where do you live?',
      regionPlaceholder: 'City, State',
      regionHint: 'For example, Pune, Maharashtra',
      agreeBefore: 'I have read the ',
      noticeLink: 'privacy notice',
      agreeAfter: ' and I agree to it.',
      agreeLabel: 'I agree to the privacy notice',
      continue: 'Continue',
    },
  },
  signedIn: {
    message: 'Signed in. Lantern knows your Family.',
  },
};

export type Strings = typeof en;
