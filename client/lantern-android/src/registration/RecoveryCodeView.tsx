import { ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { CheckRow } from '../shared/ui/CheckRow';
import { MailIcon, ShareIcon } from '../shared/ui/icons';
import { LoadingOverlay } from '../shared/ui/LoadingOverlay';
import { OutlineButton } from '../shared/ui/OutlineButton';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { RecoveryCodeCard } from './RecoveryCodeCard';
import { StepHeader } from './StepHeader';
import { REGISTRATION_STEP_COUNT } from './steps';
import { ErrorText } from '../shared/ui/ErrorText';
import { ScreenHeading } from '../shared/ui/ScreenHeading';

type Props = {
  code: string | null;
  saved: boolean;
  emailed: boolean;
  shareFailed?: boolean;
  makeFailed?: boolean;
  onMakeAgain: () => void;
  onSavedToggle: () => void;
  onEmail: () => void;
  onSave: () => void;
  onContinue: () => void;
  onBack: () => void;
};

export function RecoveryCodeView({
  code,
  saved,
  emailed,
  shareFailed = false,
  makeFailed = false,
  onMakeAgain,
  onSavedToggle,
  onEmail,
  onSave,
  onContinue,
  onBack,
}: Props) {
  const strings = useStrings().registration.recovery;

  return (
    <View className="screen">
      <StepHeader step={3} total={REGISTRATION_STEP_COUNT} onBack={onBack} />
      <ScrollView contentContainerClassName="gap-4 pb-3">
        <ScreenHeading className="mt-5" heading={strings.heading} intro={strings.intro} />
        {code === null ? null : <RecoveryCodeCard code={code} />}
        {makeFailed ? (
          <View className="gap-3">
            <ErrorText>{strings.makeFailed}</ErrorText>
            <OutlineButton label={strings.tryAgain} onPress={onMakeAgain} />
          </View>
        ) : null}
        <OutlineButton label={strings.email} onPress={onEmail} icon={<MailIcon />} />
        <OutlineButton label={strings.save} onPress={onSave} icon={<ShareIcon />} />
        {shareFailed ? <ErrorText>{strings.shareFailed}</ErrorText> : null}
        <Text className="type-small-muted">{strings.hint}</Text>
        <CheckRow checked={saved} onToggle={onSavedToggle} accessibilityLabel={strings.saved}>
          <Text className="type-body">{strings.saved}</Text>
        </CheckRow>
      </ScrollView>
      <View className="pb-6 pt-2">
        {emailed ? null : (
          <Text className="pb-2 text-center type-small-muted">{strings.emailFirst}</Text>
        )}
        <PrimaryButton
          label={strings.continue}
          onPress={onContinue}
          disabled={!saved || !emailed || code === null}
        />
      </View>
      {code === null && !makeFailed ? <LoadingOverlay label={strings.working} /> : null}
    </View>
  );
}
