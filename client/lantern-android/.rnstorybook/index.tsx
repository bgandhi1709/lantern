import { view } from './storybook.requires';

// Storybook wants a store for the last story it opened. This one keeps nothing, so nothing reaches the device.
export default view.getStorybookUI({
  storage: { getItem: async () => null, setItem: async () => {} },
});
