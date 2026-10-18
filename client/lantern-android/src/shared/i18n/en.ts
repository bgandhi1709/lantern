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
      shareFailed: "Couldn't open that. Try the other button, or write the code down.",
      saved: 'I have saved my recovery code somewhere safe.',
      continue: 'Continue',
      mailSubject: 'Your Lantern recovery code',
      message: (code: string) =>
        `Your Lantern recovery code is ${code}\n\nIf you forget your passphrase, this code is the only other way to open your family's details. Keep it somewhere safe. Lantern does not keep a copy.`,
    },
    firstChild: {
      heading: 'Add your first child',
      intro: 'One child is needed to start. You can add more below or later.',
      nameLabel: "Child's name",
      namePlaceholder: 'First name',
      classLabel: 'Class',
      classOption: (level: number) => `Class ${level}`,
      classPlaceholder: 'Select class',
      sscHint: 'Classes 6 to 10 are not available on SSC yet.',
      birthYearLabel: 'Birth year',
      birthYearPlaceholder: 'Select year',
      schoolLabel: 'School (optional)',
      schoolPlaceholder: 'School name',
      more: {
        heading: 'More children (optional)',
        intro:
          'Teaching more than one child? Add them now, or skip this and add them from Home later.',
        untitled: 'Another child',
        statusOptional: 'Optional, skip if you like',
        statusNeedsMore: 'Needs name, class and birth year',
        statusReady: (level: number) => `Class ${level}, ready to add`,
        add: 'Add another child',
        remove: 'Remove this child',
        removeNamed: (name: string) => `Remove this child, ${name}`,
        blocked: 'Finish or remove the extra child to continue.',
      },
      finish: 'Finish',
      finishing: 'Finishing…',
      tryAgain: 'Try again',
      failures: {
        classNotAvailable:
          "Lantern doesn't have books for one of these Classes on this board yet. Check each child's Class, then try again.",
        unreachable: "Couldn't reach Lantern. Check your connection, then try again.",
        other: 'Something went wrong, and your family was not set up. Please try again.',
      },
    },
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
  home: {
    greeting: {
      morning: (name: string) => `Good morning, ${name}`,
      afternoon: (name: string) => `Good afternoon, ${name}`,
      evening: (name: string) => `Good evening, ${name}`,
    },
    learningWith: 'Learning with',
    switchChild: (name: string) => `Switch child, now ${name}`,
    sheetTitle: 'Who is learning?',
    close: 'Close',
    classLabel: (classLevel: number) => `Class ${classLevel}`,
    gettingReady: (classLevel: number) => `Class ${classLevel} · getting ready`,
    addChild: 'Add a child',
    addChildNote: (max: number) => `A Family can have up to ${max} Children.`,
    banner: {
      title: 'Welcome to Lantern',
      text: (name: string) =>
        `Let's get ${name} started. Pick a Subject below to see its Chapters.`,
      dismiss: 'Got it',
    },
    subjectsTitle: (name: string, classLevel: number) => `${name}'s Subjects, Class ${classLevel}`,
    chapterCount: (count: number) => (count === 1 ? '1 Chapter' : `${count} Chapters`),
    subjectsLoading: (name: string) => `Getting ${name}'s Subjects ready…`,
    subjectsFailed: "Can't load the Subjects",
    tryAgain: 'Try again',
    tabs: {
      label: 'Main',
      home: 'Home',
      teach: 'Teach',
      check: 'Check',
      progress: 'Progress',
      later: (name: string) => `${name}, available later`,
    },
    locked: {
      heading: 'Unlock your Family',
      text: 'This phone does not hold your Family yet. Opening your Family on a new phone is coming soon.',
    },
    comingNext: { heading: 'Coming next', text: 'This screen is on its way.' },
    settings: 'Settings',
  },
  appearance: {
    title: 'Appearance',
    theme: {
      heading: 'Theme',
      system: 'Match phone',
      light: 'Light',
      dark: 'Dark',
      note: 'Dark is easier on the eyes in the evening and in dim rooms.',
    },
    textSize: {
      heading: 'Text size',
      sample: 'Aa',
      default: 'Normal text size',
      large: 'Large text size',
      largest: 'Largest text size',
      note: 'Lantern also follows the text size set on your phone.',
    },
    colour: {
      heading: 'Colour',
      teal: { name: 'Teal', note: 'Default. Calm.' },
      blue: { name: 'Blue', note: 'Classic.' },
      gray: { name: 'Gray', note: 'Quiet.' },
      golden: { name: 'Golden', note: 'Warm lamplight.' },
    },
    preview: {
      label: 'Preview',
      title: 'Transparent, translucent and opaque',
      meta: 'Concept 3 of 5 · About 15 min',
      body: 'Light passes fully through transparent materials, partly through translucent ones and not at all through opaque ones.',
      next: 'Next step',
    },
    savedNote: 'Saved on this phone only. Signing out keeps these.',
  },
};

export type Strings = typeof en;
