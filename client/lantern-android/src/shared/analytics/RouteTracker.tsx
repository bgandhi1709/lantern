import { useSegments } from 'expo-router';

import { ROUTE_SCREENS } from './routeScreens';
import { useTrackVisit } from './useTrackScreen';

// The segments are the route's pattern, such as subject/[subjectId], never the ids in it.
export function RouteTracker() {
  useTrackVisit(ROUTE_SCREENS[useSegments().join('/')] ?? null);
  return null;
}
