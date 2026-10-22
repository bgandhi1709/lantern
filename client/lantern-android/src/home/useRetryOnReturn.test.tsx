import { renderHook } from '@testing-library/react-native';

import { useRetryOnReturn } from './useRetryOnReturn';

let mockFocused = true;
jest.mock('expo-router', () => ({ useIsFocused: () => mockFocused }));

const retry = jest.fn();

beforeEach(() => {
  mockFocused = true;
  retry.mockReset();
});

describe('a locked Home coming back from another screen', () => {
  it('asks again once, when the Parent returns', async () => {
    const { rerender } = await renderHook(() => useRetryOnReturn(true, retry));
    expect(retry).not.toHaveBeenCalled();

    mockFocused = false;
    await rerender({});
    mockFocused = true;
    await rerender({});

    expect(retry).toHaveBeenCalledTimes(1);
  });

  it('does not ask again for a Home that is not locked', async () => {
    const { rerender } = await renderHook(() => useRetryOnReturn(false, retry));

    mockFocused = false;
    await rerender({});
    mockFocused = true;
    await rerender({});

    expect(retry).not.toHaveBeenCalled();
  });
});
