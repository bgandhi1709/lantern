// The two Boards the API accepts (`board` on /v1/register). A Family has no default Board.
export type Board = 'cbse' | 'ssc';

export const BOARDS: readonly Board[] = ['cbse', 'ssc'];
