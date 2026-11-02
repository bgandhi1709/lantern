import type { ReactNode } from 'react';
import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import type { Parent } from '../shared/session';
import { GoogleG, MailIcon, UserIcon } from '../shared/ui/icons';

type Props = Pick<Parent, 'name' | 'email'>;

function Row({ icon, label, value }: { icon: ReactNode; label: string; value: string }) {
  return (
    <View className="flex-row items-center gap-3">
      <View className="lantern-icon-badge">{icon}</View>
      <View className="flex-1">
        <Text className="lantern-hint">{label}</Text>
        <Text className="lantern-item-title">{value}</Text>
      </View>
    </View>
  );
}

// What Google already told us. It is shown, not asked: the Parent cannot change it here.
export function GoogleAccountCard({ name, email }: Props) {
  const strings = useStrings().registration.about;

  return (
    <View className="gap-3 lantern-card-padded">
      <View className="flex-row items-center gap-2">
        <GoogleG />
        <Text className="lantern-section-label tracking-wider">{strings.fromGoogle}</Text>
      </View>
      <Row icon={<UserIcon />} label={strings.name} value={name} />
      <Row icon={<MailIcon />} label={strings.email} value={email} />
    </View>
  );
}
