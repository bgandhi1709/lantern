import { KeyboardAvoidingView, ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { BackButton } from '../shared/ui/BackButton';
import { MailIcon } from '../shared/ui/icons';
import { OutlineButton } from '../shared/ui/OutlineButton';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { TextField } from '../shared/ui/TextField';
import { WarningCard } from '../shared/ui/WarningCard';
import { ChildChoices } from './ChildChoices';
import type { HomeChild } from '../home';
import type { DeleteFailure } from './deleteFailure';
import { OnlyChildNotice } from './OnlyChildNotice';
import { childrenOf } from './useDeletableFamily';
import type { FamilyState } from './useDeletableFamily';
import { ScreenHeading } from '../shared/ui/ScreenHeading';
import { ErrorText } from '../shared/ui/ErrorText';

const NAME_MAX_LENGTH = 50;

type Props = {
  family: FamilyState;
  picked: HomeChild | null;
  typedName: string;
  canSend: boolean;
  sending: boolean;
  failure: DeleteFailure | null;
  email: string;
  onPick: (childId: string) => void;
  onTypeName: (text: string) => void;
  onSend: () => void;
  onCancel: () => void;
  onBack: () => void;
  onDeleteFamily: () => void;
};

export function DeleteChildConfirmView(props: Props) {
  const strings = useStrings().deleteChild;
  const code = useStrings().deleteCode;
  const { family, picked } = props;
  const children = childrenOf(family);

  const failureMessage =
    family.status === 'failed' ? strings.loadFailed : props.failure && code.failures[props.failure];

  return (
    <View className="lantern-screen">
      <KeyboardAvoidingView behavior="padding" className="flex-1">
        <View className="h-16 flex-row items-center">
          <BackButton onBack={props.onBack} />
        </View>
        <ScrollView contentContainerClassName="gap-4 pb-3 pt-1" keyboardShouldPersistTaps="handled">
          <ScreenHeading heading={strings.heading} intro={strings.intro} />
          {family.status === 'loading' ? (
            <Text className="lantern-intro-text">{strings.loading}</Text>
          ) : null}
          {family.status === 'ready' && children.length < 2 ? (
            <OnlyChildNotice onDeleteFamily={props.onDeleteFamily} />
          ) : null}
          {children.length >= 2 ? (
            <ChildChoices options={children} pickedId={picked?.id ?? null} onPick={props.onPick} />
          ) : null}
          {picked === null ? null : (
            <>
              <WarningCard
                heading={strings.warningHeading(picked.name)}
                text={strings.warning(picked.name)}
              />
              <TextField
                label={strings.nameLabel(picked.name)}
                hint={strings.nameHint}
                value={props.typedName}
                onChangeText={props.onTypeName}
                maxLength={NAME_MAX_LENGTH}
              />
              <View className="flex-row items-start gap-2.5">
                <MailIcon />
                <Text className="flex-1 lantern-hint">{code.emailNote(props.email)}</Text>
              </View>
            </>
          )}
          {failureMessage ? <ErrorText>{failureMessage}</ErrorText> : null}
        </ScrollView>
        <View className="lantern-screen-footer">
          <PrimaryButton
            label={props.sending ? code.sending : code.sendCode}
            onPress={props.onSend}
            disabled={!props.canSend || props.sending}
          />
          <OutlineButton label={code.cancel} onPress={props.onCancel} />
        </View>
      </KeyboardAvoidingView>
    </View>
  );
}
