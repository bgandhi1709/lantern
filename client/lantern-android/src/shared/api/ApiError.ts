export class ApiError extends Error {
  constructor(
    readonly code: string,
    readonly status: number | null,
  ) {
    super(`The API call failed: ${code}`);
    this.name = 'ApiError';
  }
}
