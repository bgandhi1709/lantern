import fs from 'node:fs';
import path from 'node:path';

import '@testing-library/react-native/matchers';
import { render, screen } from '@testing-library/react-native';
import { withBackgrounds } from '@storybook/addon-ondevice-backgrounds';
import { composeStories } from '@storybook/react';
import { isValidElement } from 'react';
import type { ComponentType } from 'react';

import preview from '../../.rnstorybook/preview';
import { en } from '../shared/i18n/en';
import { colors } from '../shared/ui/theme';

const src = path.resolve(__dirname, '..');

type StoryModule = Parameters<typeof composeStories>[0];
const storiesOf = (file: string) =>
  composeStories(jest.requireActual<StoryModule>(file), preview) as Record<string, ComponentType>;

const filesUnder = (dir: string): string[] =>
  fs
    .readdirSync(dir, { withFileTypes: true })
    .flatMap((entry) =>
      entry.isDirectory() ? filesUnder(path.join(dir, entry.name)) : [path.join(dir, entry.name)],
    );

const relative = (file: string) => path.relative(src, file);
const storyFiles = filesUnder(src).filter((file) => file.endsWith('.stories.tsx'));

// The states the Parent can reach on these screens. A state with no story is a state nobody reviews.
const requiredStories: Record<string, string[]> = {
  'auth/StartView.stories.tsx': ['Idle', 'SigningIn', 'Failed'],
  'auth/OpeningScreen.stories.tsx': ['Playing', 'Loading', 'LoadingStill'],
  'auth/GoogleButton.stories.tsx': ['Idle', 'Busy'],
  'auth/SignedInView.stories.tsx': [
    'Checking',
    'Waking',
    'NotRegistered',
    'Unreachable',
    'Registered',
  ],
  'shared/ui/PrimaryButton.stories.tsx': ['Default', 'Disabled', 'LongLabel'],
  'shared/ui/TextField.stories.tsx': ['Empty', 'Filled', 'LongLabel', 'Secret', 'WithError'],
  'shared/ui/CheckRow.stories.tsx': ['Unchecked', 'Checked', 'LongLabel', 'WithoutIcon'],
  'shared/ui/OutlineButton.stories.tsx': ['WithIcon', 'WithoutIcon', 'Danger', 'LongLabel'],
  'registration/NoticeView.stories.tsx': ['Default'],
  'shared/ui/Picker.stories.tsx': ['Empty', 'Selected', 'WithHint', 'WithoutIcon', 'LongLabel'],
  'registration/ClassChips.stories.tsx': ['NothingPicked', 'Picked', 'SscClasses'],
  'registration/ChildFields.stories.tsx': [
    'Empty',
    'Filled',
    'ClassAsList',
    'SscBoard',
    'LongName',
  ],
  'registration/ChildCard.stories.tsx': [
    'Optional',
    'NeedsMore',
    'Ready',
    'Open',
    'OpenAndEmpty',
    'LongName',
  ],
  'registration/FirstChildView.stories.tsx': [
    'Empty',
    'Ready',
    'SscBoard',
    'ExtraNotStarted',
    'ExtraHalfFilled',
    'ExtraHalfFilledOpen',
    'ExtraReady',
    'SixChildren',
    'Finishing',
    'Unreachable',
    'ClassNotAvailable',
    'SomethingWentWrong',
  ],
  'registration/RecoveryCodeView.stories.tsx': [
    'MakingTheCode',
    'CouldNotMakeTheCode',
    'NotSavedYet',
    'ShareFailed',
    'EmailedNotTicked',
    'Saved',
  ],
  'registration/RecoveryCodeCard.stories.tsx': ['Default', 'WidestLetters'],
  'registration/BoardView.stories.tsx': ['NothingPicked', 'CbsePicked', 'SscPicked'],
  'registration/BoardCard.stories.tsx': ['Unselected', 'Selected', 'LongLabel'],
  'registration/AboutYouView.stories.tsx': ['Empty', 'RegionOnly', 'AgreedOnly', 'Ready'],
  'registration/StepHeader.stories.tsx': ['Step1', 'Step2', 'Step3', 'Step4', 'Step3NoBack'],
  'registration/GoogleAccountCard.stories.tsx': ['Default', 'LongValues'],
  'home/HomeView.stories.tsx': [
    'Default',
    'WithWelcomeBanner',
    'Loading',
    'Failed',
    'SwitcherOpen',
    'SixChildren',
    'Locked',
  ],
  'home/SubjectList.stories.tsx': ['Ready', 'Loading', 'Failed'],
  'home/SubjectRow.stories.tsx': ['Default', 'OneChapter', 'LongName', 'SocialScience'],
  'home/ChildSheet.stories.tsx': ['TwoChildren', 'OneStillBeingMade', 'SixChildren'],
  'home/ChildRow.stories.tsx': ['Unselected', 'Selected', 'GettingReady', 'LongName'],
  'home/ChildSwitcherButton.stories.tsx': ['Default', 'LongName'],
  'home/WelcomeBanner.stories.tsx': ['Default', 'LongName'],
  'home/ChildAvatar.stories.tsx': ['Small', 'Large'],
  'home/HomeHeader.stories.tsx': ['Default'],
  'shared/ui/OptionGroup.stories.tsx': ['Theme', 'TextSize', 'LongLabels'],
  'settings/FamilyRow.stories.tsx': ['Unlock', 'StartAgain'],
  'unlock/UnlockView.stories.tsx': [
    'Typing',
    'Opening',
    'WrongCode',
    'WaitingAfterWrongTry',
    'LoadingFamily',
    'CannotReachLantern',
    'SomethingWentWrong',
  ],
  'unlock/StartAgainView.stories.tsx': ['Default', 'Deleting', 'Failed'],
  'settings/SchemeCard.stories.tsx': ['Unselected', 'Selected', 'LongNote'],
  'settings/PreviewCard.stories.tsx': ['Default'],
  'settings/AppearanceView.stories.tsx': [
    'Default',
    'DarkTeal',
    'BlueLargeText',
    'GrayDark',
    'GoldenLargestText',
    'FamilyLocked',
  ],
  'home/HomeTabs.stories.tsx': ['Default'],
  'home/AddChildRow.stories.tsx': ['Default'],
  'home/UnlockNotice.stories.tsx': ['Default'],
  'home/ComingNextView.stories.tsx': ['Default'],
};

// A component file with no story of its own must say why, so the list stays short and honest.
const withoutStory: Record<string, string> = {
  'auth/AuthGate.tsx': 'routes by session state; the screens it picks have the stories',
  'auth/AuthProvider.tsx': 'context provider with no look of its own',
  'auth/StartScreen.tsx': 'container for StartView, which has the stories',
  'registration/AboutYouScreen.tsx': 'container for AboutYouView, which has the stories',
  'registration/BoardScreen.tsx': 'container for BoardView, which has the stories',
  'settings/FamilyRows.tsx':
    'the Unlock and Start again rows of AppearanceView, whose stories show them',
  'unlock/UnlockScreen.tsx': 'container for UnlockView, which has the stories',
  'unlock/StartAgainScreen.tsx': 'container for StartAgainView, which has the stories',
  'registration/FirstChildScreen.tsx': 'container for FirstChildView, which has the stories',
  'registration/MoreChildren.tsx':
    'the More children section of FirstChildView, whose stories show it',
  'registration/FirstChildFooter.tsx': 'the Finish footer of FirstChildView, whose stories show it',
  'auth/RegisteredContext.tsx': 'context provider with no look of its own',
  'registration/RecoveryCodeScreen.tsx': 'container for RecoveryCodeView, which has the stories',
  'shared/ui/BackButton.tsx':
    'the back arrow shown inside StepHeader, NoticeView and ComingNextView',
  'registration/RegistrationDraft.tsx': 'context provider with no look of its own',
  'registration/RegistrationFlow.tsx':
    'moves between the registration steps, which have the stories',
  'auth/SignedInRoute.tsx': 'container for SignedInView, which has the stories',
  'auth/IntroLayer.tsx': 'animation layer shown inside OpeningScreen',
  'auth/IntroMark.tsx': 'animation of Mark, shown inside OpeningScreen',
  'shared/ui/PageTurnLoader.tsx': 'loader shown inside OpeningScreen and LoadingOverlay',
  'shared/ui/FadeIn.tsx': 'animation wrapper with no look of its own',
  'shared/ui/icons.tsx': 'SVG parts used by the screens and their components',
  'shared/ui/markParts.tsx': 'SVG parts of Mark',
  'home/HomeScreen.tsx': 'container for HomeView, which has the stories',
  'home/homeIcons.tsx': 'SVG parts used by the Home components',
  'settings/AppearanceScreen.tsx': 'container for AppearanceView, which has the stories',
  'shared/theme/LookProvider.tsx': 'provider with nothing to draw; the Appearance stories use it',
  'shared/theme/ThemeProvider.tsx':
    'provider with nothing to draw; the Appearance stories show every look',
};

describe('the stories', () => {
  it.each(Object.entries(requiredStories))('%s has a story for every state', (file, names) => {
    const exported = Object.keys(jest.requireActual<object>(path.join(src, file)));

    expect(exported).toEqual(expect.arrayContaining(names));
  });

  it('give every component a story, or a reason it has none', () => {
    const components = filesUnder(src)
      .filter((file) => file.endsWith('.tsx') && !/\.(test|stories)\.tsx$/.test(file))
      .map(relative)
      .filter((file) => !file.startsWith('test/'));

    const missing = components.filter(
      (file) =>
        !withoutStory[file] &&
        !fs.existsSync(path.join(src, file.replace(/\.tsx$/, '.stories.tsx'))),
    );

    expect(missing).toEqual([]);
  });

  it('do not list a component that now has a story, or no longer exists', () => {
    const stale = Object.keys(withoutStory).filter(
      (file) =>
        !fs.existsSync(path.join(src, file)) ||
        fs.existsSync(path.join(src, file.replace(/\.tsx$/, '.stories.tsx'))),
    );

    expect(stale).toEqual([]);
  });

  it('sit on the ground colour, because the Backgrounds addon only paints when its decorator is set', () => {
    expect(preview.decorators).toEqual(expect.arrayContaining([withBackgrounds]));
    const { default: name, values } = preview.parameters?.backgrounds ?? {};
    expect(values.find((background: { name: string }) => background.name === name)?.value).toBe(
      colors.ground,
    );
  });

  it('show the Opening mark and name while the intro plays, not a blank first frame', async () => {
    const { Playing } = storiesOf(path.join(src, 'auth/OpeningScreen.stories.tsx'));

    await render(<Playing />);

    expect(screen.getByText('Lantern')).toBeVisible();
    expect(screen.getByRole('image')).toBeVisible();
  });

  it('show the state each one is named for', async () => {
    const { Failed, SigningIn } = storiesOf(path.join(src, 'auth/StartView.stories.tsx'));

    await render(<Failed />);
    expect(screen.getByText("Couldn't sign in. Try again.")).toBeOnTheScreen();

    await render(<SigningIn />);
    expect(screen.getByRole('button', { name: 'Signing in…' })).toBeDisabled();
  });

  it('show what the signed-in Parent sees for each answer from the API', async () => {
    const { Waking, NotRegistered, Unreachable, Registered } = storiesOf(
      path.join(src, 'auth/SignedInView.stories.tsx'),
    );

    await render(<Waking />);
    expect(screen.getByText(en.opening.waking)).toBeOnTheScreen();

    await render(<NotRegistered />);
    expect(screen.getByText(en.registration.about.heading)).toBeOnTheScreen();

    await render(<Unreachable />);
    expect(screen.getByRole('button', { name: en.opening.tryAgain })).toBeOnTheScreen();

    await render(<Registered />);
    expect(screen.getByText(en.home.greeting.morning('Asha'))).toBeOnTheScreen();
  });

  it.each(storyFiles.map((file) => [relative(file), file]))(
    '%s passes no React element as an arg',
    (_name, file) => {
      // Storybook walks an element's internals to infer arg types and throws on React Navigation's context.
      const { default: meta, ...stories } = jest.requireActual<{
        default: { args?: object };
        [story: string]: { args?: object };
      }>(file);
      const args = [meta, ...Object.values(stories)].flatMap((entry) =>
        Object.values(entry.args ?? {}),
      );

      expect(args.filter((arg) => isValidElement(arg))).toEqual([]);
    },
  );

  describe.each(storyFiles.map((file) => [relative(file), file]))('%s', (_name, file) => {
    const stories = storiesOf(file);

    it.each(Object.entries(stories))('renders %s', async (_story, Story) => {
      await render(<Story />);

      expect(screen.toJSON()).not.toBeNull();
    });
  });
});
