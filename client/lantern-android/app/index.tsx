import { Pressable, StyleSheet, Text, View } from 'react-native';

import { type ApiStatus, useApiReachable } from '../src/useApiReachable';

const messages: Record<ApiStatus, string> = {
  checking: 'Reaching Lantern…',
  waking: 'Waking Lantern up, this can take up to a minute…',
  reachable: 'Lantern is reachable',
  unreachable: "Can't reach Lantern",
};

export default function Placeholder() {
  const { status, retry } = useApiReachable();

  return (
    <View style={styles.screen}>
      <Text accessibilityRole="header" style={styles.wordmark}>
        Lantern
      </Text>
      <Text accessibilityLiveRegion="polite" style={styles.status}>
        {messages[status]}
      </Text>
      {status === 'unreachable' && (
        <Pressable accessibilityRole="button" onPress={retry} style={styles.button}>
          <Text style={styles.buttonText}>Try again</Text>
        </Pressable>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  screen: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 16,
    padding: 24,
    backgroundColor: '#ffffff',
  },
  wordmark: { fontSize: 40, fontWeight: '800', color: '#000437' },
  status: { fontSize: 16, color: '#55606e', textAlign: 'center' },
  button: {
    minHeight: 48,
    justifyContent: 'center',
    paddingHorizontal: 24,
    borderRadius: 12,
    backgroundColor: '#1a5fd0',
  },
  buttonText: { fontSize: 16, fontWeight: '700', color: '#ffffff' },
});
