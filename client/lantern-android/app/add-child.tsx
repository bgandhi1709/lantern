import { useRouter } from 'expo-router';

import { ComingNextView } from '../src/home';

export default function AddChildRoute() {
  return <ComingNextView onBack={useRouter().back} />;
}
