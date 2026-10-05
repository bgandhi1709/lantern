import { StyleSheet, Text, View } from 'react-native';

export default function Placeholder() {
  return (
    <View style={styles.screen}>
      <Text accessibilityRole="header" style={styles.wordmark}>
        Lantern
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: '#ffffff' },
  wordmark: { fontSize: 40, fontWeight: '800', color: '#000437' },
});
