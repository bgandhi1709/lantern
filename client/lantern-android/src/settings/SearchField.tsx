import { Pressable, TextInput, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { useColors } from '../shared/theme';
import { CloseIcon, SearchIcon } from '../shared/ui/icons';

type Props = { value: string; onChangeText: (text: string) => void; onClear: () => void };

export function SearchField({ value, onChangeText, onClear }: Props) {
  const colors = useColors();
  const label = useStrings().settings.searchLabel;
  const clear = useStrings().settings.clearSearchBox;

  return (
    <View className="justify-center">
      <View className="absolute left-3.5 z-10">
        <SearchIcon />
      </View>
      <TextInput
        accessibilityLabel={label}
        placeholder={label}
        placeholderTextColor={colors.textSecondary}
        value={value}
        onChangeText={onChangeText}
        autoCapitalize="none"
        autoCorrect={false}
        autoComplete="off"
        returnKeyType="search"
        className="h-13 field pl-12 pr-12 type-body focus:border-primary-deep"
      />
      {value === '' ? null : (
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={clear}
          onPress={onClear}
          className="absolute right-0.5 size-12 items-center justify-center"
        >
          <CloseIcon />
        </Pressable>
      )}
    </View>
  );
}
