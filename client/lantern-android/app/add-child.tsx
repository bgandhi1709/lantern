import { useLocalSearchParams, useRouter } from 'expo-router';

import { AddChildScreen } from '../src/addChild';
import { BOARDS } from '../src/registration';

export default function AddChild() {
  const router = useRouter();
  const { board } = useLocalSearchParams<{ board: string }>();
  const knownBoard = BOARDS.find((known) => known === board);
  if (!knownBoard) throw new Error('Add a child needs the Family’s Board.');

  return (
    <AddChildScreen
      board={knownBoard}
      onAdded={() => router.dismissTo('/')}
      onBack={() => router.back()}
    />
  );
}
