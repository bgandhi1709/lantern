const { getDefaultConfig } = require('expo/metro-config');
const { withStorybook } = require('@storybook/react-native/metro/withStorybook');
const { withNativewind } = require('nativewind/metro');

// Storybook sends usage statistics unless told not to; Lantern sends none.
process.env.STORYBOOK_DISABLE_TELEMETRY ??= '1';

// With the switch off, Metro swaps every Storybook module for an empty one, so a release bundle has none of it.
module.exports = withStorybook(withNativewind(getDefaultConfig(__dirname)), {
  enabled: process.env.EXPO_PUBLIC_STORYBOOK_ENABLED === 'true',
  experimental_mcp: true,
});
