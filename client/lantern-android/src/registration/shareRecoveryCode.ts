import { Linking, Share } from 'react-native';

export const shareRecoveryCode = (message: string) => Share.share({ message });

// The phone's own mail app sends it, from the Parent's own account: the code never goes through Lantern.
// With no mail app set up, the share sheet takes over.
export async function emailRecoveryCode(to: string, subject: string, message: string) {
  const link = `mailto:${to}?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(message)}`;
  try {
    await Linking.openURL(link);
  } catch {
    await shareRecoveryCode(message);
  }
}
