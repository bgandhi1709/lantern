// The module that replaces `../shared/api` in screen tests: the real ApiError, and a `get` the test
// steers. Use it as `jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'))`.
export const { ApiError } = jest.requireActual('../shared/api/ApiError');
export const api = { get: jest.fn().mockResolvedValue({}) };
