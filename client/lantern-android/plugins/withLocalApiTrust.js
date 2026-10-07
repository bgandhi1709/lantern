const fs = require('fs');
const path = require('path');
const { withDangerousMod } = require('expo/config-plugins');

// The local Docker API's CA (deploy/local/setup.sh writes it). Git-ignored, so CI never has it.
const CA_FILE = path.resolve(__dirname, '../../../deploy/local/certs/ca.crt');

// Debug only: the CA is trusted next to the system ones, and http:// stays allowed for Metro and the Auth Emulator
// (a network security config replaces the debug manifest's usesCleartextTraffic, so it must say so itself).
const NETWORK_SECURITY_CONFIG = `<?xml version="1.0" encoding="utf-8"?>
<network-security-config>
    <base-config cleartextTrafficPermitted="true">
        <trust-anchors>
            <certificates src="system" />
            <certificates src="@raw/local_api_ca" />
        </trust-anchors>
    </base-config>
</network-security-config>
`;

const APPLICATION_TAG = '<application android:usesCleartextTraffic="true"';

// Lets a debug build trust the local Docker API's certificate. Everything goes into the debug source set, so a
// release build never contains it. Without the CA file this plugin does nothing.
module.exports = (config) => {
  if (!fs.existsSync(CA_FILE)) return config;

  return withDangerousMod(config, [
    'android',
    (c) => {
      const debug = path.join(c.modRequest.platformProjectRoot, 'app/src/debug');
      fs.mkdirSync(path.join(debug, 'res/raw'), { recursive: true });
      fs.mkdirSync(path.join(debug, 'res/xml'), { recursive: true });
      fs.copyFileSync(CA_FILE, path.join(debug, 'res/raw/local_api_ca.crt'));
      fs.writeFileSync(
        path.join(debug, 'res/xml/network_security_config.xml'),
        NETWORK_SECURITY_CONFIG,
      );

      const manifestFile = path.join(debug, 'AndroidManifest.xml');
      const manifest = fs.readFileSync(manifestFile, 'utf8');
      if (!manifest.includes(APPLICATION_TAG)) {
        throw new Error(
          'withLocalApiTrust: <application> not found in the debug AndroidManifest.xml',
        );
      }
      fs.writeFileSync(
        manifestFile,
        manifest.replace(
          APPLICATION_TAG,
          `${APPLICATION_TAG} android:networkSecurityConfig="@xml/network_security_config"`,
        ),
      );
      return c;
    },
  ]);
};
