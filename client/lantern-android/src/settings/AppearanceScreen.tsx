import { useAppearance } from '../shared/theme';
import { AppearanceView } from './AppearanceView';

export function AppearanceScreen({ onBack }: { onBack: () => void }) {
  const { appearance, setTheme, setTextSize, setColourScheme } = useAppearance();

  return (
    <AppearanceView
      theme={appearance.theme}
      textSize={appearance.textSize}
      colourScheme={appearance.colourScheme}
      onTheme={setTheme}
      onTextSize={setTextSize}
      onColourScheme={setColourScheme}
      onBack={onBack}
    />
  );
}
