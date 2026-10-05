const { withAppBuildGradle } = require('expo/config-plugins');

const RELEASE_SIGNING = `        release {
            if (System.getenv('ANDROID_KEYSTORE_FILE')) {
                storeFile file(System.getenv('ANDROID_KEYSTORE_FILE'))
                storePassword System.getenv('ANDROID_KEYSTORE_PASSWORD')
                keyAlias System.getenv('ANDROID_KEY_ALIAS')
                keyPassword System.getenv('ANDROID_KEY_PASSWORD')
            }
        }
`;

// Signs the release build with the keystore named by the ANDROID_KEYSTORE_* variables. Without them it keeps
// the generated debug signing, so a local build still works. Fails loudly if the generated Gradle changes shape.
module.exports = (config) =>
  withAppBuildGradle(config, (c) => {
    let gradle = c.modResults.contents;

    const buildTypes = gradle.indexOf('buildTypes {');
    const releaseUse = gradle.indexOf(
      'signingConfig signingConfigs.debug',
      gradle.indexOf('release {', buildTypes),
    );
    if (buildTypes < 0 || releaseUse < 0) {
      throw new Error('withReleaseSigning: release signingConfig not found in app/build.gradle');
    }
    gradle =
      gradle.slice(0, releaseUse) +
      "signingConfig System.getenv('ANDROID_KEYSTORE_FILE') ? signingConfigs.release : signingConfigs.debug" +
      gradle.slice(releaseUse + 'signingConfig signingConfigs.debug'.length);

    const signingConfigs = gradle.indexOf('signingConfigs {');
    if (signingConfigs < 0) {
      throw new Error('withReleaseSigning: signingConfigs block not found in app/build.gradle');
    }
    const insertAt = gradle.indexOf('\n', signingConfigs) + 1;
    c.modResults.contents = gradle.slice(0, insertAt) + RELEASE_SIGNING + gradle.slice(insertAt);
    return c;
  });
