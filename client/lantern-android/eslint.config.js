const expo = require('eslint-config-expo/flat');

const features = ['auth', 'family', 'catalog'];

const storagePaths = [
  {
    name: '@react-native-async-storage/async-storage',
    message: 'Only src/shared/storage touches the device store.',
  },
  { name: 'expo-secure-store', message: 'Only src/shared/storage touches the device store.' },
];

const firebasePaths = [
  {
    name: '@react-native-firebase/app',
    message: 'Only src/shared/session touches the Firebase session.',
  },
  {
    name: '@react-native-firebase/auth',
    message: 'Only src/shared/session touches the Firebase session.',
  },
];

const googlePaths = [
  {
    name: '@react-native-google-signin/google-signin',
    message: 'Only src/auth talks to the Google account sheet.',
  },
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
      'no-restricted-imports': [
        'error',
        { paths: [...storagePaths, ...firebasePaths, ...googlePaths] },
      ],
    },
  },
  ...features.map((feature) => ({
    files: [`src/${feature}/**`],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          paths: [...storagePaths, ...firebasePaths, ...(feature === 'auth' ? [] : googlePaths)],
          patterns: otherFeaturesInternals(feature),
        },
      ],
    },
  })),
  {
    files: ['src/shared/session/**'],
    rules: {
      'no-restricted-imports': ['error', { paths: [...storagePaths, ...googlePaths] }],
    },
  },
  {
    // App text lives in src/shared/i18n, and the look lives in class names (global.css holds the tokens).
    files: ['src/**/*.tsx', 'app/**/*.tsx'],
    ignores: ['**/*.test.tsx'],
    rules: {
      'no-restricted-syntax': [
        'error',
        ...[
          'JSXText[value=/[A-Za-z]/]',
          'JSXAttribute[name.name=/^(label|title|placeholder|accessibilityLabel|accessibilityHint)$/] > Literal',
          'JSXElement > JSXExpressionContainer > ConditionalExpression > Literal[value=/[A-Za-z]/]',
        ].map((selector) => ({
          selector,
          message: 'Put app text in src/shared/i18n/en.ts and read it with useStrings().',
        })),
        ...[
          "CallExpression[callee.object.name='StyleSheet'][callee.property.name='create']",
          // Animated values and SVG cannot be class names, so only they may take a style prop.
          "JSXOpeningElement:not([name.object.name='Animated'], [name.name='Svg']) > JSXAttribute[name.name='style'] > JSXExpressionContainer > :matches(ObjectExpression, ArrayExpression, ArrowFunctionExpression)",
        ].map((selector) => ({
          selector,
          message: 'Style with class names (global.css holds the tokens), not a style object.',
        })),
      ],
    },
  },
  {
    files: ['app.config.js', 'plugins/*.js'],
    languageOptions: { globals: { __dirname: 'readonly' } },
  },
  {
    files: ['src/shared/storage/**', 'src/test/**', 'jest.setup.ts'],
    rules: { 'no-restricted-imports': 'off' },
  },
];
