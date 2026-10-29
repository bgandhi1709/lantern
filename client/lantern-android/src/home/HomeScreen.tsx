import { useState } from 'react';

import { signOutOfLantern } from '../auth';
import { MAX_CHILDREN } from '../registration';
import { tapped, useTrackScreen } from '../shared/analytics';
import type { Board } from '../registration';
import { getParent } from '../shared/session';
import { dayPartAt, firstNameOf } from './greeting';
import { HomeView } from './HomeView';
import { useActiveChildOnReturn } from './useActiveChildOnReturn';
import { useHome } from './useHome';
import { useRetryOnReturn } from './useRetryOnReturn';
import { useWelcomeBanner } from './useWelcomeBanner';

type Props = {
  onOpenSubject: (childId: string, subjectId: string) => void;
  onAddChild: (board: Board) => void;
  onOpenSettings: () => void;
  onOpenTab: () => void;
  onUnlock: () => void;
};

export function HomeScreen({
  onOpenSubject,
  onAddChild,
  onOpenSettings,
  onOpenTab,
  onUnlock,
}: Props) {
  useTrackScreen('home');
  const home = useHome();
  useRetryOnReturn(home.locked, home.retry);
  useActiveChildOnReturn(home.activeChild?.id ?? null, home.children, home.switchTo, home.reload);
  const banner = useWelcomeBanner();
  const [sheetOpen, setSheetOpen] = useState(false);

  return (
    <HomeView
      dayPart={dayPartAt(new Date().getHours())}
      parentFirstName={firstNameOf(getParent().name)}
      locked={home.locked}
      activeChild={home.activeChild}
      switcherChildren={home.children}
      subjects={home.subjects}
      bannerVisible={banner.visible}
      sheetOpen={sheetOpen}
      canAddChild={home.board !== null && home.children.length < MAX_CHILDREN}
      onOpenSheet={() => setSheetOpen(true)}
      onCloseSheet={() => setSheetOpen(false)}
      onPickChild={tapped('switch-child', (childId: string) => {
        home.switchTo(childId);
        setSheetOpen(false);
      })}
      onAddChild={tapped('add-child', () => {
        setSheetOpen(false);
        if (home.board) onAddChild(home.board);
      })}
      onOpenSubject={tapped('open-subject', onOpenSubject)}
      onOpenSettings={tapped('open-settings', onOpenSettings)}
      onOpenTab={onOpenTab}
      onUnlock={tapped('unlock', onUnlock)}
      onDismissBanner={banner.dismiss}
      onTryAgain={home.retry}
      onSignOut={tapped('sign-out', signOutOfLantern)}
    />
  );
}
