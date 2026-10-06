const fs = require('fs');
const path = require('path');

// The Firebase file is git-ignored. CI writes it from a secret and points GOOGLE_SERVICES_JSON at it.
const googleServicesFile = process.env.GOOGLE_SERVICES_JSON ?? './google-services.json';

// Google sign-in needs the project's web client (type 3), which the same file already lists.
function googleWebClientId() {
  try {
    const { client } = JSON.parse(
      fs.readFileSync(path.resolve(__dirname, googleServicesFile), 'utf8'),
    );
    return client
      .flatMap((entry) => entry.oauth_client ?? [])
      .find((oauth) => oauth.client_type === 3)?.client_id;
  } catch {
    return undefined;
  }
}

module.exports = ({ config }) => ({
  ...config,
  android: { ...config.android, googleServicesFile },
  extra: { ...config.extra, googleWebClientId: googleWebClientId() },
});
