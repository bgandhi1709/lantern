/** @jest-environment node */
import path from 'node:path';

import { ESLint } from 'eslint';

import eslintConfig from '../../eslint.config';

const root = path.resolve(__dirname, '../..');
// ESLint loads a config file with import(), which Jest's sandbox refuses, so hand it the same config directly.
const eslint = new ESLint({
  cwd: root,
  overrideConfigFile: true,
  overrideConfig: eslintConfig,
});

const rulesBroken = async (code: string, file = 'src/example.ts') => {
  const [result] = await eslint.lintText(code, { filePath: path.join(root, file) });
  return result.messages.map((message) => message.ruleId);
};

describe('npm run lint', () => {
  it('fails on any', async () => {
    expect(await rulesBroken('export const x = (value: any) => value;\n')).toContain(
      '@typescript-eslint/no-explicit-any',
    );
  });

  it('fails on a non-null assertion', async () => {
    expect(await rulesBroken('export const x = (value?: string) => value!.length;\n')).toContain(
      '@typescript-eslint/no-non-null-assertion',
    );
  });

  it('fails on console.log', async () => {
    expect(await rulesBroken("console.log('hi');\n")).toContain('no-console');
  });

  it.each(['@react-native-async-storage/async-storage', 'expo-secure-store'])(
    'fails on importing %s outside src/shared/storage',
    async (name) => {
      const code = `import * as store from '${name}';\nexport { store };\n`;

      expect(await rulesBroken(code)).toContain('no-restricted-imports');
      expect(await rulesBroken(code, 'src/auth/session.ts')).toContain('no-restricted-imports');
    },
  );

  it('allows the storage imports inside src/shared/storage', async () => {
    const code = "import * as store from 'expo-secure-store';\nexport { store };\n";

    expect(await rulesBroken(code, 'src/shared/storage/storage.ts')).not.toContain(
      'no-restricted-imports',
    );
  });

  it.each(['@react-native-firebase/app', '@react-native-firebase/auth'])(
    'fails on importing %s outside src/shared/session',
    async (name) => {
      const code = `import * as firebase from '${name}';\nexport { firebase };\n`;

      expect(await rulesBroken(code)).toContain('no-restricted-imports');
      expect(await rulesBroken(code, 'src/auth/example.ts')).toContain('no-restricted-imports');
    },
  );

  it('allows the Firebase import inside src/shared/session', async () => {
    const code = "import * as auth from '@react-native-firebase/auth';\nexport { auth };\n";

    expect(await rulesBroken(code, 'src/shared/session/session.ts')).not.toContain(
      'no-restricted-imports',
    );
  });

  it('fails on the Google sign-in import outside src/auth, and allows it inside', async () => {
    const code =
      "import { GoogleSignin } from '@react-native-google-signin/google-signin';\nexport { GoogleSignin };\n";

    expect(await rulesBroken(code, 'src/shared/session/session.ts')).toContain(
      'no-restricted-imports',
    );
    expect(await rulesBroken(code, 'src/family/example.ts')).toContain('no-restricted-imports');
    expect(await rulesBroken(code, 'src/auth/googleSignIn.ts')).not.toContain(
      'no-restricted-imports',
    );
  });

  it("fails when a feature imports another feature's internals", async () => {
    const code = "import { secret } from '../family/internal';\nexport { secret };\n";

    expect(await rulesBroken(code, 'src/auth/session.ts')).toContain('no-restricted-imports');
  });

  it("allows a feature to import another feature's public index", async () => {
    const code = "import { Family } from '../family';\nexport { Family };\n";

    expect(await rulesBroken(code, 'src/auth/session.ts')).not.toContain('no-restricted-imports');
  });

  it.each([
    ['text inside JSX', 'export const A = () => <Text>Hello</Text>;\n'],
    ['a text label prop', 'export const A = () => <Button label="Sign out" />;\n'],
    ['an accessibility label', 'export const A = () => <View accessibilityLabel="A mark" />;\n'],
    [
      'text chosen in JSX',
      "export const A = ({ busy }: { busy: boolean }) => <Text>{busy ? 'Wait' : 'Go'}</Text>;\n",
    ],
  ])('fails on %s written in the code instead of src/shared/i18n', async (_, code) => {
    expect(await rulesBroken(code, 'src/auth/Example.tsx')).toContain('no-restricted-syntax');
  });

  it('allows app text that comes from the dictionary', async () => {
    const code =
      'export const A = ({ s }: { s: { hi: string } }) => <Text accessibilityLabel={s.hi}>{s.hi}</Text>;\n';

    expect(await rulesBroken(code, 'src/auth/Example.tsx')).not.toContain('no-restricted-syntax');
  });
});
