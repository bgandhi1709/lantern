import { useRouter } from 'expo-router';

import Storybook from '../.rnstorybook';
import { AuthGate } from '../src/auth';
import { HomeScreen } from '../src/home';
import { RegistrationFlow } from '../src/registration';
import { config } from '../src/shared/config';

export default function Index() {
  const router = useRouter();
  if (config.storybookEnabled) return <Storybook />;
  return (
    <AuthGate
      home={
        <HomeScreen
          onOpenSubject={(childId, subjectId) =>
            router.push({ pathname: '/subject/[subjectId]', params: { subjectId, childId } })
          }
          onAddChild={(board) => router.push({ pathname: '/add-child', params: { board } })}
          onOpenSettings={() => router.push('/settings')}
          onOpenTab={() => router.push('/coming-next')}
          onUnlock={() => router.push('/unlock')}
        />
      }
      registration={<RegistrationFlow />}
    />
  );
}
