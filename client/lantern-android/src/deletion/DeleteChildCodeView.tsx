import { Pressable, ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { BackButton } from '../shared/ui/BackButton';
import { OutlineButton } from '../shared/ui/OutlineButton';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { TextField } from '../shared/ui/TextField';
import type { DeleteFailure } from './deleteFailure';
import { FailureText } from './FailureText';
import type { Work } from './useDeleteChild';

const CODE_LENGTH = 6;

type Props = {
  email: string;
  code: string;
  verified: boolean;
  work: Work | null;
  failure: DeleteFailure | null;
  resent: boolean;
  onTypeCode: (text: string) => void;
  onResend: () => void;
  onDelete: () => void;
  onCancel: () => void;
  onBack: () => void;
};

export function DeleteChildCodeView(props: Props) {
  const strings = useStrings().deleteChild;
  const deleting = props.work === 'deleting';
  const hint = props.verified
    ? strings.verified
    : props.work === 'verifying'
      ? strings.checking
      : props.resent
        ? strings.resent
        : strings.codeHint;

  return (
    <View className="flex-1 bg-ground px-6 pb-safe pt-safe">
      <View className="h-16 flex-row items-center">
        <BackButton onBack={props.onBack} />
      </View>
      <ScrollView contentContainerClassName="gap-4 pb-3 pt-1" keyboardShouldPersistTaps="handled">
        <View className="gap-2">
          <Text accessibilityRole="header" className="font-inter-bold text-heading text-ink">
            {strings.codeHeading}
          </Text>
          <Text className="font-inter-medium text-body text-muted">
            {strings.codeIntro(props.email)}
          </Text>
        </View>
        <TextField
          numeric
          label={strings.codeLabel}
          hint={hint}
          value={props.code}
          onChangeText={props.onTypeCode}
          maxLength={CODE_LENGTH}
        />
        <FailureText failure={props.failure} />
        <Pressable
          accessibilityRole="button"
          onPress={props.onResend}
          disabled={props.work !== null}
          className="min-h-12 justify-center"
        >
          <Text className="font-inter-bold text-body text-primary-deep underline">
            {strings.resend}
          </Text>
        </Pressable>
      </ScrollView>
      <View className="gap-3 pb-6 pt-2">
        <PrimaryButton
          label={deleting ? strings.deleting : strings.delete}
          onPress={props.onDelete}
          disabled={!props.verified || deleting}
        />
        <OutlineButton label={strings.cancel} onPress={props.onCancel} />
      </View>
    </View>
  );
}
