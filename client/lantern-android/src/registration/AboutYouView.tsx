import { ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { CheckRow } from '../shared/ui/CheckRow';
import { PinIcon, ShieldCheckIcon } from '../shared/ui/icons';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { TextField } from '../shared/ui/TextField';
import { GoogleAccountCard } from './GoogleAccountCard';
import { REGION_MAX_LENGTH } from './region';
import { StepHeader } from './StepHeader';

type Props = {
  parent: { name: string; email: string };
  region: string;
  noticeAccepted: boolean;
  canContinue: boolean;
  onRegionChange: (region: string) => void;
  onNoticeToggle: () => void;
  onContinue: () => void;
  onBack: () => void;
};

export function AboutYouView({
  parent,
  region,
  noticeAccepted,
  canContinue,
  onRegionChange,
  onNoticeToggle,
  onContinue,
  onBack,
}: Props) {
  const strings = useStrings().registration;
  const about = strings.about;

  return (
    <View className="flex-1 bg-ground px-6 pb-safe pt-safe">
      <StepHeader step={1} total={4} onBack={onBack} />
      <ScrollView contentContainerClassName="gap-4 pb-3" keyboardShouldPersistTaps="handled">
        <View className="mt-5 gap-2">
          <Text accessibilityRole="header" className="font-inter-bold text-heading text-ink">
            {about.heading}
          </Text>
          <Text className="font-inter-medium text-body text-muted">{about.intro}</Text>
        </View>
        <GoogleAccountCard name={parent.name} email={parent.email} />
        <TextField
          label={about.regionLabel}
          hint={about.regionHint}
          placeholder={about.regionPlaceholder}
          value={region}
          onChangeText={onRegionChange}
          icon={<PinIcon />}
          maxLength={REGION_MAX_LENGTH}
        />
        <CheckRow
          checked={noticeAccepted}
          onToggle={onNoticeToggle}
          accessibilityLabel={about.agreeLabel}
          icon={<ShieldCheckIcon />}
        >
          <Text className="font-inter-medium text-body text-ink">
            {about.agreeBefore}
            <Text className="font-inter-bold text-primary underline">{about.noticeLink}</Text>
            {about.agreeAfter}
          </Text>
        </CheckRow>
      </ScrollView>
      <View className="pb-6 pt-2">
        <PrimaryButton label={about.continue} onPress={onContinue} disabled={!canContinue} />
      </View>
    </View>
  );
}
