import { useRouter } from 'expo-router';

import { ComingNextView } from '../src/home';

export default function ComingNextRoute() {
  return <ComingNextView onBack={useRouter().back} />;
}
