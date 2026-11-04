import { Pressable, Text } from 'react-native';

import { useStrings } from '../shared/i18n';
import { OutlineButton } from '../shared/ui/OutlineButton';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { TextField } from '../shared/ui/TextField';
import type { DeleteFailure } from './deleteFailure';
import { CODE_LENGTH } from './useOneTimeCode';
import type { Work } from './useOneTimeCode';
import { ScreenHeading } from '../shared/ui/ScreenHeading';
import { ErrorText } from '../shared/ui/ErrorText';
import { DeletionPage } from './DeletionPage';

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

  const footer = (
    <>
      {props.verified && props.lastStep ? (
        <Text className="text-center lantern-field-error">{props.lastStep}</Text>
      ) : null}
      <PrimaryButton
        label={deleting ? strings.deleting : props.deleteLabel}
        onPress={props.onDelete}
        disabled={!props.verified || deleting}
      />
      <OutlineButton label={props.cancelLabel} onPress={props.onCancel} />
    </>
  );

  return (
    <DeletionPage onBack={props.onBack} footer={footer}>
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
    </DeletionPage>
  );
}

function hintFor(props: Props, strings: ReturnType<typeof useStrings>['deleteCode']) {
  if (props.verified) return strings.verified;
  if (props.work === 'verifying') return strings.checking;
  if (props.work === 'requesting') return strings.resending;
  if (props.resent) return strings.resent;
  return strings.codeHint;
}
