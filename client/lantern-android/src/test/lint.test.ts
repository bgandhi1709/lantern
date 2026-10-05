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

  it("fails when a feature imports another feature's internals", async () => {
    const code = "import { secret } from '../family/internal';\nexport { secret };\n";

    expect(await rulesBroken(code, 'src/auth/session.ts')).toContain('no-restricted-imports');
  });

  it("allows a feature to import another feature's public index", async () => {
    const code = "import { Family } from '../family';\nexport { Family };\n";

    expect(await rulesBroken(code, 'src/auth/session.ts')).not.toContain('no-restricted-imports');
  });
});
