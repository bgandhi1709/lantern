import { useStrings } from '../shared/i18n';
import { TextField } from '../shared/ui/TextField';
import { SearchIcon } from './homeIcons';

const SEARCH_MAX_LENGTH = 64;

type Props = { value: string; onChange: (text: string) => void };

export function ChapterSearch({ value, onChange }: Props) {
  const text = useStrings().home.chapters;

  return (
    <TextField
      label={text.find}
      placeholder={text.findPlaceholder}
      value={value}
      onChangeText={onChange}
      maxLength={SEARCH_MAX_LENGTH}
      icon={<SearchIcon />}
    />
  );
}
