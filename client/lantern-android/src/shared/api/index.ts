import { config } from '../config';
import { createApiClient } from './apiClient';

export { ApiError } from './ApiError';

export const api = createApiClient({ baseUrl: config.apiBaseUrl, timeoutMs: config.apiTimeoutMs });
