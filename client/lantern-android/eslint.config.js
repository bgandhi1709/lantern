const expo = require('eslint-config-expo/flat');

const features = ['auth', 'family', 'catalog'];

const storagePaths = [
  {
    name: '@react-native-async-storage/async-storage',
    message: 'Only src/shared/storage touches the device store.',
  },
  { name: 'expo-secure-store', message: 'Only src/shared/storage touches the device store.' },
];

const otherFeaturesInternals = (own) =>
  features
    .filter((feature) => feature !== own)
    .map((feature) => ({
      group: [`**/${feature}/*`, `!**/${feature}/index`],
      message: `Import ${feature} through its index.ts, not its internals.`,
    }));

module.exports = [
  ...expo,
  { ignores: ['dist/*', '.expo/*'] },
  {
    files: ['**/*.{ts,tsx}'],
    rules: {
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/no-non-null-assertion': 'error',
    },
  },
  {
    rules: {
      'no-console': 'error',
      'no-restricted-imports': ['error', { paths: storagePaths }],
    },
  },
  ...features.map((feature) => ({
    files: [`src/${feature}/**`],
    rules: {
      'no-restricted-imports': [
        'error',
        { paths: storagePaths, patterns: otherFeaturesInternals(feature) },
      ],
    },
  })),
  {
    files: ['src/shared/storage/**', 'src/test/**', 'jest.setup.ts'],
    rules: { 'no-restricted-imports': 'off' },
  },
];
