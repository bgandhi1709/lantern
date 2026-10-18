import { useRouter } from 'expo-router';

import { ComingNextView } from '../../src/home';

export default function SubjectRoute() {
  return <ComingNextView onBack={useRouter().back} />;
}
