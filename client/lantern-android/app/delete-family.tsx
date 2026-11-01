import { useRouter } from 'expo-router';

import { ComingNextView } from '../src/home';

export default function DeleteFamilyRoute() {
  return <ComingNextView onBack={useRouter().back} />;
}
