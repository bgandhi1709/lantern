export const SCREEN_NAMES = [
  'opening',
  'start',
  'about-you',
  'privacy-notice',
  'board',
  'recovery-code',
  'first-child',
  'home',
  'chapters',
  'chapter-history',
  'add-child',
  'appearance',
  'unlock',
  'start-again',
  'coming-next',
] as const;

export const TAP_NAMES = [
  'continue-with-google',
  'switch-google-account',
  'open-privacy-notice',
  'accept-privacy-notice',
  'about-continue',
  'board-continue',
  'share-recovery-code',
  'recovery-continue',
  'finish-registration',
  'add-child',
  'switch-child',
  'open-subject',
  'open-chapter-history',
  'open-settings',
  'change-appearance',
  'unlock',
  'start-again',
  'sign-out',
] as const;

export const REGISTRATION_STEPS = ['about-you', 'board', 'recovery-code', 'first-child'] as const;

export type ScreenName = (typeof SCREEN_NAMES)[number];
export type TapName = (typeof TAP_NAMES)[number];
export type RegistrationStep = (typeof REGISTRATION_STEPS)[number];
export type RegistrationState = 'reached' | 'finished';
