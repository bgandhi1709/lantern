import '@testing-library/react-native/matchers';
import { render, screen, userEvent } from '@testing-library/react-native';

import { api } from '../shared/api';
import { StartAgainScreen } from '.';

jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'));

const del = api.delete as jest.Mock;
const onContinue = jest.fn();
const onKeep = jest.fn();

beforeEach(() => {
  for (const mock of [del, onContinue, onKeep]) mock.mockReset();
});

async function renderStartAgain() {
  const user = userEvent.setup();
  await render(<StartAgainScreen onContinue={onContinue} onKeep={onKeep} />);
  return user;
}

describe('Start again', () => {
  it('explains what it does and deletes nothing itself', async () => {
    await renderStartAgain();

    expect(screen.getByText('This deletes your Family')).toBeOnTheScreen();
    expect(del).not.toHaveBeenCalled();
  });

  it('keeps the Family when the Parent changes their mind', async () => {
    const user = await renderStartAgain();

    await user.press(screen.getByRole('button', { name: 'Keep my Family' }));

    expect(onKeep).toHaveBeenCalledTimes(1);
    expect(onContinue).not.toHaveBeenCalled();
    expect(del).not.toHaveBeenCalled();
  });

  it('leads into the Delete Family steps, because the delete needs a proof', async () => {
    const user = await renderStartAgain();

    await user.press(screen.getByRole('button', { name: 'Continue to delete my Family' }));

    expect(onContinue).toHaveBeenCalledTimes(1);
    expect(del).not.toHaveBeenCalled();
  });
});
