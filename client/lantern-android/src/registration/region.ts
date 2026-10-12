export const REGION_MAX_LENGTH = 60;

// Lantern reads the Region as typed (D66), so the phone applies the rule /v1/register applies to the
// text it will send: 1 to 60 characters and no control, format or unassigned characters (\p{C}).
// The server does not trim, so neither do we; the one extra rule is that spaces alone are not a Region.
export function isValidRegion(text: string) {
  return text.trim().length > 0 && text.length <= REGION_MAX_LENGTH && !/\p{C}/u.test(text);
}
