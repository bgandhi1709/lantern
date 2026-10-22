const FIRST_WAIT_SECONDS = 5;
const LONGEST_WAIT_SECONDS = 300;

export const waitSeconds = (failures: number) =>
  failures < 1
    ? 0
    : Math.min(FIRST_WAIT_SECONDS * 2 ** Math.min(failures - 1, 16), LONGEST_WAIT_SECONDS);
