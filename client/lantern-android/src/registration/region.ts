export const REGION_MAX_LENGTH = 60;

// Lantern reads the Region as typed (D66), so the phone applies the rule /v1/register applies:
// 1 to 60 characters and no control, format or unassigned characters (\p{C}).
export function isValidRegion(text: string) {
  const region = text.trim();
  return region.length > 0 && region.length <= REGION_MAX_LENGTH && !/\p{C}/u.test(region);
}
