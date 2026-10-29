/** @jest-environment node */
import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(__dirname, '../..');
const readJson = (file: string) => JSON.parse(fs.readFileSync(path.join(root, file), 'utf8'));

const { expo } = readJson('app.json');
const firebase = readJson('firebase.json')['react-native'];

describe('the native build settings for usage numbers', () => {
  it('start with Analytics and Performance Monitoring collection off', () => {
    expect(firebase.analytics_auto_collection_enabled).toBe(false);
    expect(firebase.perf_auto_collection_enabled).toBe(false);
  });

  it('leave out the advertising ID and ad personalisation', () => {
    expect(expo.android.blockedPermissions).toEqual(
      expect.arrayContaining([
        'com.google.android.gms.permission.AD_ID',
        'android.permission.ACCESS_ADSERVICES_AD_ID',
      ]),
    );
    expect(firebase.google_analytics_adid_collection_enabled).toBe(false);
    expect(firebase.google_analytics_ssaid_collection_enabled).toBe(false);
    expect(firebase.analytics_default_allow_ad_storage).toBe(false);
    expect(firebase.analytics_default_allow_ad_user_data).toBe(false);
    expect(firebase.analytics_default_allow_ad_personalization_signals).toBe(false);
  });

  it('turn off the SDK’s own screen reporting, since the app reports its screens by name', () => {
    expect(firebase.google_analytics_automatic_screen_reporting_enabled).toBe(false);
  });

  it('add both Firebase modules to the build', () => {
    expect(expo.plugins).toEqual(
      expect.arrayContaining(['@react-native-firebase/analytics', '@react-native-firebase/perf']),
    );
  });
});
