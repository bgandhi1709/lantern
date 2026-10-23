import { useLocalSearchParams, useRouter } from 'expo-router';

import { ChapterHistoryScreen } from '../../../src/home';

export default function ChapterHistoryRoute() {
  const router = useRouter();
  const { childId, chapterId } = useLocalSearchParams<{ childId: string; chapterId: string }>();

  return (
    <ChapterHistoryScreen
      childId={childId}
      chapterId={chapterId}
      onBack={router.back}
      onAction={() => router.push('/coming-next')}
    />
  );
}
