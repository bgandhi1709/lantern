import type { ReactNode } from 'react';
import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import type { Parent } from '../shared/session';
import { GoogleG, MailIcon, UserIcon } from '../shared/ui/icons';

type Props = Pick<Parent, 'name' | 'email'>;

function Row({ icon, label, value }: { icon: ReactNode; label: string; value: string }) {
  return (
    <View className="flex-row items-center gap-3">
      <View className="size-11 items-center justify-center rounded-control bg-tile">{icon}</View>
      <View className="flex-1">
        <Text className="font-inter-medium text-small text-muted">{label}</Text>
        <Text className="font-inter-bold text-lead text-ink">{value}</Text>
      </View>
    </View>
  );
}

// What Google already told us. It is shown, not asked: the Parent cannot change it here.
export function GoogleAccountCard({ name, email }: Props) {
  const strings = useStrings().registration.about;

  return (
    <View className="gap-3 rounded-card border border-line bg-card p-4">
      <View className="flex-row items-center gap-2">
        <GoogleG />
        <Text className="font-inter-bold text-small tracking-wider text-muted">
          {strings.fromGoogle}
        </Text>
      </View>
      <Row icon={<UserIcon />} label={strings.name} value={name} />
      <Row icon={<MailIcon />} label={strings.email} value={email} />
    </View>
  );
}
