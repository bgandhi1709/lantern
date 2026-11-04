import { useRouter } from 'expo-router';

import { useRegistrationCheck } from '../src/auth';
import { DeleteFamilyScreen } from '../src/deletion';

export default function DeleteFamilyRoute() {
  const router = useRouter();
  const { recheck } = useRegistrationCheck();
  return (
    <DeleteFamilyScreen
      onDeleted={() => {
        recheck();
        router.dismissTo('/');
      }}
      onBack={() => router.back()}
    />
  );
}
