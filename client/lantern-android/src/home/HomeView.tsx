import { ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { ChildSheet } from './ChildSheet';
import { ChildSwitcherButton } from './ChildSwitcherButton';
import type { DayPart } from './greeting';
import { HomeHeader } from './HomeHeader';
import { HomeTabs } from './HomeTabs';
import { SubjectList } from './SubjectList';
import { UnlockNotice } from './UnlockNotice';
import type { HomeChild, SubjectsState, SwitcherChild } from './useHome';
import { WelcomeBanner } from './WelcomeBanner';

type Props = {
  dayPart: DayPart;
  parentFirstName: string;
  locked: boolean;
  activeChild: HomeChild | null;
  switcherChildren: SwitcherChild[];
  subjects: SubjectsState;
  bannerVisible: boolean;
  sheetOpen: boolean;
  canAddChild: boolean;
  onOpenSheet: () => void;
  onCloseSheet: () => void;
  onPickChild: (childId: string) => void;
  onAddChild: () => void;
  onOpenSubject: (childId: string, subjectId: string) => void;
  onOpenSettings: () => void;
  onOpenTab: () => void;
  onDismissBanner: () => void;
  onTryAgain: () => void;
  onSignOut: () => void;
};

export function HomeView(props: Props) {
  const { activeChild, subjects, locked } = props;
  const greeting = useStrings().home.greeting[props.dayPart](props.parentFirstName);

  return (
    <View className="flex-1 bg-ground pt-safe">
      <HomeHeader onOpenSettings={props.onOpenSettings} onSignOut={props.onSignOut} />
      <ScrollView contentContainerClassName="gap-4 px-4 pb-4 pt-1">
        <Text accessibilityRole="header" className="font-inter-bold text-heading text-ink">
          {greeting}
        </Text>
        {locked ? <UnlockNotice onSignOut={props.onSignOut} /> : null}
        {activeChild ? (
          <>
            <ChildSwitcherButton name={activeChild.name} onPress={props.onOpenSheet} />
            {props.bannerVisible ? (
              <WelcomeBanner childName={activeChild.name} onDismiss={props.onDismissBanner} />
            ) : null}
            <SubjectList
              childName={activeChild.name}
              classLevel={activeChild.classLevel}
              state={subjects}
              onOpenSubject={(subjectId) => props.onOpenSubject(activeChild.id, subjectId)}
              onTryAgain={props.onTryAgain}
            />
            <ChildSheet
              visible={props.sheetOpen}
              activeId={activeChild.id}
              onPick={props.onPickChild}
              onClose={props.onCloseSheet}
              onAddChild={props.canAddChild ? props.onAddChild : undefined}
              switcherChildren={props.switcherChildren}
            />
          </>
        ) : null}
      </ScrollView>
      <HomeTabs onOpenTab={props.onOpenTab} />
    </View>
  );
}
