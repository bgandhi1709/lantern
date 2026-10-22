import '@testing-library/react-native/matchers';
import { act, render, screen, userEvent } from '@testing-library/react-native';

import { ApiError, api } from '../shared/api';
import { clearAll, readValue, writeValue } from '../shared/storage';
import { fakeRestoredSession } from '../test/fakeFirebaseAuth';
import { StartAgainScreen } from '.';

jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'));

const del = api.delete as jest.Mock;
const onDeleted = jest.fn();
const onKeep = jest.fn();

beforeEach(async () => {
  for (const mock of [del, onDeleted, onKeep]) mock.mockReset();
  await clearAll();
  fakeRestoredSession();
});

const deleteButton = () =>
  screen.getByRole('button', { name: /^(Delete my Family and start again|Deleting…)$/ });

async function renderStartAgain() {
  const user = userEvent.setup();
  await render(<StartAgainScreen onDeleted={onDeleted} onKeep={onKeep} />);
  return user;
}

describe('Start again', () => {
  it('explains what it does and deletes nothing until the Parent confirms', async () => {
    await renderStartAgain();

    expect(screen.getByText('This deletes your Family')).toBeOnTheScreen();
    expect(del).not.toHaveBeenCalled();
  });

  it('keeps the Family when the Parent changes their mind', async () => {
    const user = await renderStartAgain();

    await user.press(screen.getByRole('button', { name: 'Keep my Family' }));

    expect(onKeep).toHaveBeenCalledTimes(1);
    expect(del).not.toHaveBeenCalled();
  });

  it('deletes the Family with one call, wipes the phone, then tells the screen', async () => {
    del.mockResolvedValue(undefined);
    await writeValue('theme', 'dark');
    const user = await renderStartAgain();

    await user.press(deleteButton());

    expect(del).toHaveBeenCalledTimes(1);
    expect(del.mock.calls[0][0]).toBe('/v1/family');
    expect(onDeleted).toHaveBeenCalledTimes(1);
    expect(await readValue('theme')).toBe('light');
  });

  it('ignores a second press, and Keep, while it deletes', async () => {
    let finish: () => void = () => undefined;
    del.mockReturnValue(new Promise<void>((resolve) => (finish = resolve)));
    const user = await renderStartAgain();

    await user.press(deleteButton());
    await user.press(deleteButton());
    await user.press(screen.getByRole('button', { name: 'Keep my Family' }));
    await act(async () => finish());

    expect(del).toHaveBeenCalledTimes(1);
    expect(onKeep).not.toHaveBeenCalled();
  });

  it('says nothing was changed when the API cannot delete, and the Parent can try again', async () => {
    del.mockRejectedValueOnce(new ApiError('unreachable', null)).mockResolvedValueOnce(undefined);
    const user = await renderStartAgain();

    await user.press(deleteButton());
    expect(await screen.findByText(/Nothing was changed/)).toBeOnTheScreen();
    expect(onDeleted).not.toHaveBeenCalled();

    await user.press(deleteButton());
    expect(onDeleted).toHaveBeenCalledTimes(1);
  });
});
