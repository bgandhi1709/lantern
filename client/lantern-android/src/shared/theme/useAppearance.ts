import { useContext } from 'react';

import { AppearanceContext } from './appearanceContext';

export const useAppearance = () => useContext(AppearanceContext);

export const useColors = () => useAppearance().colors;
