import { useStrings } from '../shared/i18n';
import { OutlineButton } from '../shared/ui/OutlineButton';
import { PrimaryButton } from '../shared/ui/PrimaryButton';

type Props = {
  canSend: boolean;
  sending: boolean;
  cancelLabel: string;
  onSend: () => void;
  onCancel: () => void;
};

export function SendCodeButtons({ canSend, sending, cancelLabel, onSend, onCancel }: Props) {
  const strings = useStrings().deleteCode;

  return (
    <>
      <PrimaryButton
        label={sending ? strings.sending : strings.sendCode}
        onPress={onSend}
        disabled={!canSend || sending}
      />
      <OutlineButton label={cancelLabel} onPress={onCancel} />
    </>
  );
}
