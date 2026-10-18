export const { ApiError } = jest.requireActual('../shared/api/ApiError');
export const api = {
  get: jest.fn().mockResolvedValue({}),
  post: jest.fn(),
  put: jest.fn(),
  delete: jest.fn(),
};
