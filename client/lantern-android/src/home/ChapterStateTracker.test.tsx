import { render, screen } from '@testing-library/react-native';

import { ChapterStateTracker } from './ChapterStateTracker';

describe('ChapterStateTracker', () => {
  it.each(['Planned', 'Taught', 'Checked'])(
    'keeps the %s label on one line and shrinks it to fit',
    async (label) => {
      await render(<ChapterStateTracker state="not-started" />);

      const text = screen.getByText(label, { includeHiddenElements: true });
      expect(text.props.numberOfLines).toBe(1);
      expect(text.props.adjustsFontSizeToFit).toBe(true);
    },
  );
});
