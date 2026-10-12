// The two Boards the API accepts (`board` on /v1/register). A Family has no default Board.
export const BOARDS = ['cbse', 'ssc'] as const;

export type Board = (typeof BOARDS)[number];
