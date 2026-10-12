import { ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { CheckRow } from '../shared/ui/CheckRow';
import { MailIcon, ShareIcon } from '../shared/ui/icons';
import { OutlineButton } from '../shared/ui/OutlineButton';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { RecoveryCodeCard } from './RecoveryCodeCard';
import { StepHeader } from './StepHeader';
import { REGISTRATION_STEP_COUNT } from './steps';

type Props = {
  code: string;
  saved: boolean;
  shareFailed?: boolean;
  onSavedToggle: () => void;
  onEmail: () => void;
  onSave: () => void;
  onContinue: () => void;
};

// No Back here: the code is shown once, and leaving would lose it.
export function RecoveryCodeView({
  code,
  saved,
  shareFailed = false,
  onSavedToggle,
  onEmail,
  onSave,
  onContinue,
}: Props) {
  const strings = useStrings().registration.recovery;

  return (
    <View className="flex-1 bg-ground px-6 pb-safe pt-safe">
      <StepHeader step={3} total={REGISTRATION_STEP_COUNT} />
      <ScrollView contentContainerClassName="gap-4 pb-3">
        <View className="mt-5 gap-2">
          <Text accessibilityRole="header" className="font-inter-bold text-heading text-ink">
            {strings.heading}
          </Text>
          <Text className="font-inter-medium text-body text-muted">{strings.intro}</Text>
        </View>
        <RecoveryCodeCard code={code} />
        <OutlineButton label={strings.email} onPress={onEmail} icon={<MailIcon />} />
        <OutlineButton label={strings.save} onPress={onSave} icon={<ShareIcon />} />
        {shareFailed ? (
          <Text
            accessibilityLiveRegion="polite"
            className="font-inter-semibold text-small text-danger"
          >
            {strings.shareFailed}
          </Text>
        ) : null}
        <Text className="font-inter-medium text-small text-muted">{strings.hint}</Text>
        <CheckRow checked={saved} onToggle={onSavedToggle} accessibilityLabel={strings.saved}>
          <Text className="font-inter-medium text-body text-ink">{strings.saved}</Text>
        </CheckRow>
      </ScrollView>
      <View className="pb-6 pt-2">
        <PrimaryButton label={strings.continue} onPress={onContinue} disabled={!saved} />
      </View>
    </View>
  );
}
