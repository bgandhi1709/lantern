import { useIsFocused } from 'expo-router';
import { useEffect } from 'react';
import { AppState } from 'react-native';

import { trackScreenTime, trackScreenView } from './analytics';
import type { ScreenName } from './events';

const MS_PER_SECOND = 1000;

// A visit counts while the screen is in front and the app is in the foreground.
export function useTrackVisit(screen: ScreenName | null) {
  useEffect(() => {
    if (screen === null) return;
    trackScreenView(screen);
    let startedAt: number | null = Date.now();

    const stop = () => {
      if (startedAt === null) return;
      trackScreenTime(screen, Math.round((Date.now() - startedAt) / MS_PER_SECOND));
      startedAt = null;
    };
    const subscription = AppState.addEventListener('change', (state) => {
      if (state === 'active') startedAt ??= Date.now();
      else stop();
    });

    return () => {
      subscription.remove();
      stop();
    };
  }, [screen]);
}

// For a screen that other routes can open over, as Home is: it is in front only while focused.
export function useTrackScreen(screen: ScreenName) {
  useTrackVisit(useIsFocused() ? screen : null);
}
