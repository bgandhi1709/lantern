import { useRouter } from 'expo-router';

import { DeleteChildScreen } from '../src/deletion';

export default function DeleteChild() {
  const router = useRouter();
  return (
    <DeleteChildScreen
      onDeleted={() => router.dismissTo('/')}
      onBack={() => router.back()}
      onDeleteFamily={() => router.replace('/delete-family')}
    />
  );
}
