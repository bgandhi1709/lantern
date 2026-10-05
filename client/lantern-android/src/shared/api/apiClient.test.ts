import { z } from 'zod';

import { hangingFetch, jsonResponse } from '../../test/fakeApi';
import { ApiError } from './ApiError';
import { createApiClient } from './apiClient';

const Me = z.object({ name: z.string() });

const clientWith = (fetchImpl: jest.Mock, getIdToken?: () => Promise<string | null>) =>
  createApiClient({ baseUrl: 'https://api.test', timeoutMs: 1_000, fetchImpl, getIdToken });

const failureOf = async (call: Promise<unknown>) => {
  try {
    await call;
  } catch (error) {
    return error;
  }
  throw new Error('expected the call to fail');
};

describe('a call that works', () => {
  it('returns the response parsed by its schema', async () => {
    const fetchImpl = jest.fn().mockResolvedValue(jsonResponse(200, { name: 'Asha' }));

    const me = await clientWith(fetchImpl).get('/v1/me', Me);

    expect(me).toEqual({ name: 'Asha' });
    expect(fetchImpl).toHaveBeenCalledWith(
      'https://api.test/v1/me',
      expect.objectContaining({ method: 'GET' }),
    );
  });

  it('sends the ID token from the hook, and none when there is no session', async () => {
    const fetchImpl = jest.fn().mockImplementation(async () => jsonResponse(200, { name: 'Asha' }));

    await clientWith(fetchImpl, async () => 'abc').get('/v1/me', Me);
    await clientWith(fetchImpl, async () => null).get('/v1/me', Me);

    expect(fetchImpl.mock.calls[0][1].headers).toMatchObject({ Authorization: 'Bearer abc' });
    expect(fetchImpl.mock.calls[1][1].headers).not.toHaveProperty('Authorization');
  });

  it('sends a body as JSON and accepts an empty 204 for a void schema', async () => {
    const fetchImpl = jest.fn().mockResolvedValue(new Response(null, { status: 204 }));

    await clientWith(fetchImpl).post('/v1/family/children', z.void(), { name: 'Mia' });

    expect(fetchImpl.mock.calls[0][1]).toMatchObject({
      method: 'POST',
      body: JSON.stringify({ name: 'Mia' }),
    });
  });
});

describe('a call that is rejected', () => {
  it('throws ApiError carrying the problem code and status', async () => {
    const fetchImpl = jest
      .fn()
      .mockResolvedValue(
        jsonResponse(409, { code: 'already-registered', title: 'Already registered' }),
      );

    const error = await failureOf(clientWith(fetchImpl).post('/v1/register', Me, {}));

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ code: 'already-registered', status: 409 });
  });

  it('uses http-<status> when the rejection has no problem code', async () => {
    const fetchImpl = jest.fn().mockResolvedValue(new Response('<html>', { status: 502 }));

    const error = await failureOf(clientWith(fetchImpl).get('/v1/me', Me));

    expect(error).toMatchObject({ code: 'http-502', status: 502 });
  });

  it('never puts the token or the address in the error message', async () => {
    const fetchImpl = jest
      .fn()
      .mockResolvedValue(jsonResponse(401, { code: 'caller-not-identified' }));

    const error = await failureOf(
      clientWith(fetchImpl, async () => 'secret-token').get('/v1/me', Me),
    );

    expect(String((error as Error).message)).not.toMatch(/secret-token|api\.test/);
  });
});

describe('a call that cannot finish', () => {
  beforeEach(() => jest.useFakeTimers());
  afterEach(() => jest.useRealTimers());

  it('throws ApiError timeout after the timeout, with one attempt', async () => {
    hangingFetch.mockClear();
    const call = failureOf(clientWith(hangingFetch).get('/v1/me', Me));

    await jest.advanceTimersByTimeAsync(1_000);

    expect(await call).toMatchObject({ code: 'timeout' });
    expect(hangingFetch).toHaveBeenCalledTimes(1);
  });

  it('throws ApiError network when the phone cannot reach the server', async () => {
    const fetchImpl = jest.fn().mockRejectedValue(new TypeError('Network request failed'));

    expect(await failureOf(clientWith(fetchImpl).get('/v1/me', Me))).toMatchObject({
      code: 'network',
    });
  });
});

describe('a response that does not parse', () => {
  it('throws ApiError invalid-response when the body does not match the schema', async () => {
    const fetchImpl = jest.fn().mockResolvedValue(jsonResponse(200, { name: 42 }));

    const error = await failureOf(clientWith(fetchImpl).get('/v1/me', Me));

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ code: 'invalid-response' });
  });

  it('throws ApiError invalid-response when the body is not JSON', async () => {
    const fetchImpl = jest.fn().mockResolvedValue(new Response('oops', { status: 200 }));

    expect(await failureOf(clientWith(fetchImpl).get('/v1/me', Me))).toMatchObject({
      code: 'invalid-response',
    });
  });
});

describe('writes are never retried', () => {
  it.each([
    [
      'a network failure',
      () => jest.fn().mockRejectedValue(new TypeError('Network request failed')),
    ],
    ['a 503', () => jest.fn().mockResolvedValue(jsonResponse(503, { code: 'unavailable' }))],
  ])('sends a POST once after %s', async (_name, makeFetch) => {
    const fetchImpl = makeFetch();

    await failureOf(clientWith(fetchImpl).post('/v1/register', Me, {}));

    expect(fetchImpl).toHaveBeenCalledTimes(1);
  });
});
