import { useState } from 'react';

import { signOutOfLantern } from '../auth';
import { MAX_CHILDREN } from '../registration';
import { getParent } from '../shared/session';
import { dayPartAt, firstNameOf } from './greeting';
import { HomeView } from './HomeView';
import { useActiveChildOnReturn } from './useActiveChildOnReturn';
import { useHome } from './useHome';
import { useRetryOnReturn } from './useRetryOnReturn';
import { useWelcomeBanner } from './useWelcomeBanner';

type Props = {
  onOpenSubject: (childId: string, subjectId: string) => void;
  onAddChild: () => void;
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
  const home = useHome();
  useRetryOnReturn(home.locked, home.retry);
  useActiveChildOnReturn(home.activeChild?.id ?? null, home.children, home.switchTo);
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
      canAddChild={home.children.length < MAX_CHILDREN}
      onOpenSheet={() => setSheetOpen(true)}
      onCloseSheet={() => setSheetOpen(false)}
      onPickChild={(childId) => {
        home.switchTo(childId);
        setSheetOpen(false);
      }}
      onAddChild={() => {
        setSheetOpen(false);
        onAddChild();
      }}
      onOpenSubject={onOpenSubject}
      onOpenSettings={onOpenSettings}
      onOpenTab={onOpenTab}
      onUnlock={onUnlock}
      onDismissBanner={banner.dismiss}
      onTryAgain={home.retry}
      onSignOut={signOutOfLantern}
    />
  );
}
