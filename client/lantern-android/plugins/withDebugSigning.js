const { withAppBuildGradle } = require('expo/config-plugins');

const TEMPLATE_KEYSTORE = "storeFile file('debug.keystore')";
const USER_KEYSTORE =
  'storeFile file("${System.getProperty(\'user.home\')}/.android/debug.keystore")';

// Signs the debug build with this computer's own debug key (~/.android/debug.keystore, created by the Android
// build if missing) instead of the template's android/app/debug.keystore, which every React Native project shares.
// Google sign-in only accepts a build whose SHA-1 is registered in Firebase, and a shared key must not be.
module.exports = (config) =>
  withAppBuildGradle(config, (c) => {
    const gradle = c.modResults.contents;
    if (gradle.includes(USER_KEYSTORE)) return c;

    const debug = gradle.indexOf('debug {', gradle.indexOf('signingConfigs {'));
    const storeFile = gradle.indexOf(TEMPLATE_KEYSTORE, debug);
    if (debug < 0 || storeFile < 0) {
      throw new Error('withDebugSigning: debug storeFile not found in app/build.gradle');
    }
    c.modResults.contents =
      gradle.slice(0, storeFile) +
      USER_KEYSTORE +
      gradle.slice(storeFile + TEMPLATE_KEYSTORE.length);
    return c;
  });
