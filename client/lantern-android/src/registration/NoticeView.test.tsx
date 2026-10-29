import '@testing-library/react-native/matchers';
import { render, screen } from '@testing-library/react-native';

import { NoticeView } from './NoticeView';

const section = (title: string) => screen.getByRole('header', { name: title });

describe('the privacy notice', () => {
  it('lists everything the usage numbers collect, including what Google adds', async () => {
    await render(<NoticeView version="draft" onBack={() => undefined} />);

    expect(section('While you use the app')).toBeOnTheScreen();
    for (const collected of [
      /which screens open and for how long/,
      /which buttons are tapped/,
      /which step of setting up you reach/,
      /how fast the app starts/,
      /how long its calls take/,
      /your phone model, your Android version/,
      /roughly which city your connection comes from/,
      /random number that stands for this installation/,
    ]) {
      expect(screen.getByText(collected)).toBeOnTheScreen();
    }
  });

  it('says what the usage counting keeps on the phone, and that none of it is personal', async () => {
    await render(<NoticeView version="draft" onBack={() => undefined} />);

    expect(screen.getByText(/keeps a random number on the phone/)).toBeOnTheScreen();
    expect(screen.getByText(/None of your details go with these numbers/)).toBeOnTheScreen();
    expect(screen.getByText(/the phone's advertising ID/)).toBeOnTheScreen();
  });
});
