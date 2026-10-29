const analytics = {};

export const getAnalytics = () => analytics;
export const logEvent = jest.fn(async (_analytics: unknown, _name: string, _params?: object) => {});
export const logScreenView = jest.fn(async () => {});
export const setAnalyticsCollectionEnabled = jest.fn(async () => {});

export function resetFakeAnalytics() {
  logEvent.mockClear();
  logScreenView.mockClear();
  setAnalyticsCollectionEnabled.mockClear();
}
