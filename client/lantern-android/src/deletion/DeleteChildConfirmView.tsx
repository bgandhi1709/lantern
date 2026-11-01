import { KeyboardAvoidingView, ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { BackButton } from '../shared/ui/BackButton';
import { MailIcon } from '../shared/ui/icons';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { TextField } from '../shared/ui/TextField';
import { WarningCard } from '../shared/ui/WarningCard';
import { ChildChoices } from './ChildChoices';
import type { DeleteFailure } from './deleteFailure';
import { FailureText } from './FailureText';
import { OnlyChildNotice } from './OnlyChildNotice';
import type { FamilyState } from './useDeletableFamily';

const NAME_MAX_LENGTH = 50;

type Props = {
  family: FamilyState;
  pickedId: string | null;
  pickedName: string | null;
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
  const { family, pickedName } = props;
  const children = family.status === 'ready' ? family.children : [];

  return (
    <View className="flex-1 bg-ground px-6 pb-safe pt-safe">
      <KeyboardAvoidingView behavior="padding" className="flex-1">
        <View className="h-16 flex-row items-center">
          <BackButton onBack={props.onBack} />
        </View>
        <ScrollView contentContainerClassName="gap-4 pb-3 pt-1" keyboardShouldPersistTaps="handled">
          <View className="gap-2">
            <Text accessibilityRole="header" className="font-inter-bold text-heading text-ink">
              {strings.heading}
            </Text>
            <Text className="font-inter-medium text-body text-muted">{strings.intro}</Text>
          </View>
          {family.status === 'loading' ? (
            <Text className="font-inter-medium text-body text-muted">{strings.loading}</Text>
          ) : null}
          {family.status === 'failed' ? (
            <Text
              accessibilityLiveRegion="polite"
              className="font-inter-semibold text-small text-danger"
            >
              {strings.loadFailed}
            </Text>
          ) : null}
          {family.status === 'ready' && children.length < 2 ? (
            <OnlyChildNotice onDeleteFamily={props.onDeleteFamily} />
          ) : null}
          {children.length >= 2 ? (
            <ChildChoices options={children} pickedId={props.pickedId} onPick={props.onPick} />
          ) : null}
          {pickedName === null ? null : (
            <>
              <WarningCard
                heading={strings.warningHeading(pickedName)}
                text={strings.warning(pickedName)}
              />
              <TextField
                label={strings.nameLabel(pickedName)}
                hint={strings.nameHint}
                value={props.typedName}
                onChangeText={props.onTypeName}
                maxLength={NAME_MAX_LENGTH}
              />
              <View className="flex-row items-start gap-2.5">
                <MailIcon />
                <Text className="flex-1 font-inter-medium text-small text-muted">
                  {strings.emailNote(props.email)}
                </Text>
              </View>
            </>
          )}
          <FailureText failure={props.failure} />
        </ScrollView>
        <View className="gap-3 pb-6 pt-2">
          <PrimaryButton
            label={props.sending ? strings.sending : strings.sendCode}
            onPress={props.onSend}
            disabled={!props.canSend || props.sending}
          />
          <PrimaryButton label={strings.cancel} onPress={props.onCancel} disabled={props.sending} />
        </View>
      </KeyboardAvoidingView>
    </View>
  );
}
