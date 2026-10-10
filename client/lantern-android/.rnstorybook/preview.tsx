import { withBackgrounds } from '@storybook/addon-ondevice-backgrounds';
import type { Preview } from '@storybook/react-native';

import { colors } from '../src/shared/ui/theme';

// Stories show on the app's own colours, so a component is judged where it will sit.
const preview: Preview = {
  decorators: [withBackgrounds],
  parameters: {
    backgrounds: {
      default: 'ground',
      values: [
        { name: 'ground', value: colors.ground },
        { name: 'card', value: colors.card },
        { name: 'primary', value: colors.primary },
      ],
    },
  },
};

export default preview;
