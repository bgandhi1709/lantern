import { useRouter } from 'expo-router';

import { ComingNextScreen } from '../src/home';

export default function ComingNextRoute() {
  return <ComingNextScreen onBack={useRouter().back} />;
}
