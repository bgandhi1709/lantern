/** @jest-environment node */
import fs from 'node:fs';
import path from 'node:path';

import { ROUTE_SCREENS } from '../shared/analytics/routeScreens';

const app = path.resolve(__dirname, '../../app');

// The front door shows Home and the registration steps, which report themselves.
const reportThemselves = ['index'];

const filesUnder = (dir: string): string[] =>
  fs
    .readdirSync(dir, { withFileTypes: true })
    .flatMap((entry) =>
      entry.isDirectory() ? filesUnder(path.join(dir, entry.name)) : [path.join(dir, entry.name)],
    );

const routes = filesUnder(app)
  .map((file) => path.relative(app, file).replace(/\.tsx$/, ''))
  .filter((route) => !route.startsWith('_') && !reportThemselves.includes(route))
  .sort();

describe('every route in app/', () => {
  it('has a screen name, so the router-level tracker reports it', () => {
    expect(routes.filter((route) => !(route in ROUTE_SCREENS))).toEqual([]);
  });

  it('is the only kind of route in the screen names, so none is left over after a rename', () => {
    expect(Object.keys(ROUTE_SCREENS).filter((route) => !routes.includes(route))).toEqual([]);
  });
});
