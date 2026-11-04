import { KeyboardAvoidingView, Pressable, ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { BackButton } from '../shared/ui/BackButton';
import { OutlineButton } from '../shared/ui/OutlineButton';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { TextField } from '../shared/ui/TextField';
import type { DeleteFailure } from './deleteFailure';
import { CODE_LENGTH } from './useOneTimeCode';
import type { Work } from './useOneTimeCode';
import { ScreenHeading } from '../shared/ui/ScreenHeading';
import { ErrorText } from '../shared/ui/ErrorText';

type Props = {
  intro: string;
  deleteLabel: string;
  cancelLabel: string;
  lastStep?: string;
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

export function DeleteCodeView(props: Props) {
  const strings = useStrings().deleteCode;
  const deleting = props.work === 'acting';
  const hint = hintFor(props, strings);

  return (
    <View className="lantern-screen">
      <KeyboardAvoidingView behavior="padding" className="flex-1">
        <View className="h-16 flex-row items-center">
          <BackButton onBack={props.onBack} />
        </View>
        <ScrollView contentContainerClassName="gap-4 pb-3 pt-1" keyboardShouldPersistTaps="handled">
          <ScreenHeading heading={strings.codeHeading} intro={props.intro} />
          <TextField
            numeric
            label={strings.codeLabel}
            hint={hint}
            value={props.code}
            onChangeText={props.onTypeCode}
            maxLength={CODE_LENGTH}
          />
          {props.failure ? <ErrorText>{strings.failures[props.failure]}</ErrorText> : null}
          <Pressable
            accessibilityRole="button"
            onPress={props.onResend}
            disabled={props.work !== null}
            className="min-h-12 justify-center"
          >
            <Text className="lantern-link underline">{strings.resend}</Text>
          </Pressable>
        </ScrollView>
        <View className="lantern-screen-footer">
          {props.verified && props.lastStep ? (
            <Text className="text-center lantern-field-error">{props.lastStep}</Text>
          ) : null}
          <PrimaryButton
            label={deleting ? strings.deleting : props.deleteLabel}
            onPress={props.onDelete}
            disabled={!props.verified || deleting}
          />
          <OutlineButton label={props.cancelLabel} onPress={props.onCancel} />
        </View>
      </KeyboardAvoidingView>
    </View>
  );
}

function hintFor(props: Props, strings: ReturnType<typeof useStrings>['deleteCode']) {
  if (props.verified) return strings.verified;
  if (props.work === 'verifying') return strings.checking;
  if (props.work === 'requesting') return strings.resending;
  if (props.resent) return strings.resent;
  return strings.codeHint;
}
