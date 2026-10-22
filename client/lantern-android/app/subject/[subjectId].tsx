import { useLocalSearchParams, useRouter } from 'expo-router';

import { ChaptersScreen } from '../../src/home';

export default function SubjectRoute() {
  const router = useRouter();
  const { childId, subjectId } = useLocalSearchParams<{ childId: string; subjectId: string }>();

  return (
    <ChaptersScreen
      childId={childId}
      subjectId={subjectId}
      onBack={router.back}
      onOpenTab={() => router.push('/coming-next')}
      onAddChild={() => router.push('/add-child')}
    />
  );
}
