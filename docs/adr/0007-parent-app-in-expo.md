# The parent app is React Native with Expo: one codebase for Android, web and later iOS

The parent app is built with React Native and Expo, mobile-first, from one codebase. The D12 pilot (2026-12-03) ships two targets: an Android build through Play Console internal testing, and the Expo web build (react-native-web) for PC and any phone without the Android app. Firebase Google sign-in stays the identity, through its Expo-supported SDK.

iOS is not shipped for the pilot: an Apple Developer account is $99 a year (about ₹8,300 at ₹84 to the dollar) and needs a Mac or a paid build service. The code stays iOS-compatible as a rule: only libraries that support Android, iOS and web, nothing Android-only, so adding iOS later is a build target, not a rewrite. The Play Console registration is a one-time $25 (about ₹2,100).

Considered and rejected: a web-only PWA (weaker sign-in and notifications on low-end Android, and less of an app to a Parent), native Kotlin (a new stack, Android only, nothing reused from React), and Flutter (no reuse of the React skills already in the routing table).
