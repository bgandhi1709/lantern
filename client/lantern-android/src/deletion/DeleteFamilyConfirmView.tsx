import { KeyboardAvoidingView, ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { BackButton } from '../shared/ui/BackButton';
import { ErrorText } from '../shared/ui/ErrorText';
import { MailIcon } from '../shared/ui/icons';
import { OutlineButton } from '../shared/ui/OutlineButton';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { ScreenHeading } from '../shared/ui/ScreenHeading';
import { TextField } from '../shared/ui/TextField';
import { WarningCard } from '../shared/ui/WarningCard';
import type { DeleteFailure } from './deleteFailure';
import { childrenOf } from './useDeletableFamily';
import type { FamilyState } from './useDeletableFamily';

const CONFIRM_MAX_LENGTH = 20;

const listOf = (names: string[]) =>
  names.length < 2
    ? names.join('')
    : `${names.slice(0, -1).join(', ')} and ${names[names.length - 1]}`;

type Props = {
  family: FamilyState;
  typed: string;
  canSend: boolean;
  sending: boolean;
  failure: DeleteFailure | null;
  email: string;
  onType: (text: string) => void;
  onSend: () => void;
  onCancel: () => void;
  onBack: () => void;
};

export function DeleteFamilyConfirmView(props: Props) {
  const strings = useStrings().deleteFamily;
  const code = useStrings().deleteCode;
  const names = childrenOf(props.family).map((child) => child.name);
  const warning = names.length > 0 ? strings.warningWithNames(listOf(names)) : strings.warning;

  return (
    <View className="lantern-screen">
      <KeyboardAvoidingView behavior="padding" className="flex-1">
        <View className="h-16 flex-row items-center">
          <BackButton onBack={props.onBack} />
        </View>
        <ScrollView contentContainerClassName="gap-4 pb-3 pt-1" keyboardShouldPersistTaps="handled">
          <ScreenHeading heading={strings.heading} intro={strings.intro} />
          <WarningCard heading={strings.warningHeading} text={warning} />
          <TextField
            label={strings.typeLabel}
            hint={strings.typeHint}
            value={props.typed}
            onChangeText={props.onType}
            maxLength={CONFIRM_MAX_LENGTH}
          />
          <View className="flex-row items-start gap-2.5">
            <MailIcon />
            <Text className="flex-1 lantern-hint">{code.emailNote(props.email)}</Text>
          </View>
          {props.failure ? <ErrorText>{code.failures[props.failure]}</ErrorText> : null}
        </ScrollView>
        <View className="lantern-screen-footer">
          <PrimaryButton
            label={props.sending ? code.sending : code.sendCode}
            onPress={props.onSend}
            disabled={!props.canSend || props.sending}
          />
          <OutlineButton label={strings.keep} onPress={props.onCancel} />
        </View>
      </KeyboardAvoidingView>
    </View>
  );
}
