/* eslint-disable @typescript-eslint/no-require-imports */
import { z } from 'zod';

import { jsonResponse } from '../../test/fakeApi';

describe('the app API client', () => {
  const fetchMock = jest.fn(async () => jsonResponse(200, {}));
  const originalFetch = global.fetch;

  beforeEach(() => {
    fetchMock.mockClear();
    global.fetch = fetchMock as unknown as typeof fetch;
  });
  afterAll(() => {
    global.fetch = originalFetch;
  });

  // The client takes `fetch` when it is created, so it is loaded fresh, with the fake in place and its own session.
  async function authorizationOfOneCall(restoredToken?: string) {
    let call = async () => {};
    jest.isolateModules(() => {
      if (restoredToken) require('../../test/fakeFirebaseAuth').fakeRestoredSession(restoredToken);
      const { api } = require('.');
      call = () => api.get('/v1/me', z.unknown());
    });

    await call();

    const [, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    return new Headers(init.headers).get('Authorization');
  }

  it('sends the current ID token of the session', async () => {
    expect(await authorizationOfOneCall('current-token')).toBe('Bearer current-token');
  });

  it('sends no Authorization header when nobody is signed in', async () => {
    expect(await authorizationOfOneCall()).toBeNull();
  });
});
