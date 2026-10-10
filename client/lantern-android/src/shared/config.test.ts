const loadConfig = (env: Record<string, string>, dev: boolean) => {
  const saved = { ...process.env };
  Object.assign(process.env, { EXPO_PUBLIC_API_BASE_URL: 'https://api.test' }, env);
  const wasDev = (globalThis as { __DEV__?: boolean }).__DEV__;
  (globalThis as { __DEV__?: boolean }).__DEV__ = dev;
  try {
    let loaded: { config: { storybookEnabled: boolean } } | undefined;
    jest.isolateModules(() => {
      jest.doMock('expo-constants', () => ({
        __esModule: true,
        default: { expoConfig: { extra: { googleWebClientId: 'web-client-id' } } },
      }));
      loaded = jest.requireActual('./config');
    });
    return loaded?.config;
  } finally {
    process.env = saved;
    (globalThis as { __DEV__?: boolean }).__DEV__ = wasDev;
  }
};

describe('the Storybook switch', () => {
  it('is off unless EXPO_PUBLIC_STORYBOOK_ENABLED is true', () => {
    expect(loadConfig({}, true)?.storybookEnabled).toBe(false);
    expect(loadConfig({ EXPO_PUBLIC_STORYBOOK_ENABLED: 'false' }, true)?.storybookEnabled).toBe(
      false,
    );
  });

  it('is on in a development build when asked for', () => {
    expect(loadConfig({ EXPO_PUBLIC_STORYBOOK_ENABLED: 'true' }, true)?.storybookEnabled).toBe(
      true,
    );
  });

  it('refuses to start a release build with Storybook on', () => {
    expect(() => loadConfig({ EXPO_PUBLIC_STORYBOOK_ENABLED: 'true' }, false)).toThrow(
      'EXPO_PUBLIC_STORYBOOK_ENABLED must not be set in a release build.',
    );
  });
});
