import { config } from '../config';
import { getIdToken } from '../session';
import { createApiClient } from './apiClient';

export { ApiError } from './ApiError';

export const api = createApiClient({
  baseUrl: config.apiBaseUrl,
  timeoutMs: config.apiTimeoutMs,
  getIdToken,
});
