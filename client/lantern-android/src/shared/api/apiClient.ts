import { z } from 'zod';

import { ApiError } from './ApiError';

export type ApiClientOptions = {
  baseUrl: string;
  timeoutMs: number;
  // Wired to the Firebase session in the sign-in story; no session means no header.
  getIdToken?: () => Promise<string | null>;
  fetchImpl?: typeof fetch;
};

type Method = 'GET' | 'POST' | 'PUT' | 'DELETE';

const ProblemBody = z.object({ code: z.string() });

async function readJson(response: Response) {
  try {
    return await response.json();
  } catch {
    return undefined;
  }
}

async function failureOf(response: Response) {
  const problem = ProblemBody.safeParse(await readJson(response));
  return new ApiError(
    problem.success ? problem.data.code : `http-${response.status}`,
    response.status,
  );
}

export function createApiClient({
  baseUrl,
  timeoutMs,
  getIdToken,
  fetchImpl = fetch,
}: ApiClientOptions) {
  async function send(method: Method, path: string, body?: unknown) {
    const token = await getIdToken?.();
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), timeoutMs);
    try {
      return await fetchImpl(`${baseUrl}${path}`, {
        method,
        headers: {
          Accept: 'application/json',
          ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        body: body === undefined ? undefined : JSON.stringify(body),
        signal: controller.signal,
      });
    } catch (error) {
      const timedOut = error instanceof Error && error.name === 'AbortError';
      throw new ApiError(timedOut ? 'timeout' : 'network', null);
    } finally {
      clearTimeout(timer);
    }
  }

  async function request<T>(method: Method, path: string, schema: z.ZodType<T>, body?: unknown) {
    const response = await send(method, path, body);
    if (!response.ok) throw await failureOf(response);
    const payload = response.status === 204 ? undefined : await readJson(response);
    const parsed = schema.safeParse(payload);
    if (!parsed.success) throw new ApiError('invalid-response', response.status);
    return parsed.data;
  }

  return {
    get: <T>(path: string, schema: z.ZodType<T>) => request('GET', path, schema),
    post: <T>(path: string, schema: z.ZodType<T>, body: unknown) =>
      request('POST', path, schema, body),
    put: <T>(path: string, schema: z.ZodType<T>, body: unknown) =>
      request('PUT', path, schema, body),
    delete: <T>(path: string, schema: z.ZodType<T>) => request('DELETE', path, schema),
  };
}
