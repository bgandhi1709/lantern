import fs from 'node:fs';
import path from 'node:path';

import '@testing-library/react-native/matchers';
import { render, screen } from '@testing-library/react-native';
import { withBackgrounds } from '@storybook/addon-ondevice-backgrounds';
import { composeStories } from '@storybook/react';
import type { ComponentType } from 'react';

import preview from '../../.rnstorybook/preview';
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
  'shared/ui/PrimaryButton.stories.tsx': ['Default', 'LongLabel'],
};

// A component file with no story of its own must say why, so the list stays short and honest.
const withoutStory: Record<string, string> = {
  'auth/AuthGate.tsx': 'routes by session state; the screens it picks have the stories',
  'auth/AuthProvider.tsx': 'context provider with no look of its own',
  'auth/StartScreen.tsx': 'container for StartView, which has the stories',
  'auth/SignedInPlaceholder.tsx': 'stand-in for Home that asks the API; replaced by the Home story',
  'auth/IntroLayer.tsx': 'animation layer shown inside OpeningScreen',
  'auth/IntroMark.tsx': 'animation of Mark, shown inside OpeningScreen',
  'auth/PageTurnLoader.tsx': 'loader shown inside OpeningScreen',
  'shared/ui/FadeIn.tsx': 'animation wrapper with no look of its own',
  'shared/ui/icons.tsx': 'SVG parts used by FeatureList and GoogleButton',
  'shared/ui/markParts.tsx': 'SVG parts of Mark',
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

  describe.each(storyFiles.map((file) => [relative(file), file]))('%s', (_name, file) => {
    const stories = storiesOf(file);

    it.each(Object.entries(stories))('renders %s', async (_story, Story) => {
      await render(<Story />);

      expect(screen.toJSON()).not.toBeNull();
    });
  });
});
