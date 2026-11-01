import type { PutawayCandidate } from './putaway-api';

export type LocationFilters = { zone: string; row: string; rack: string; level: string };
export const emptyLocationFilters: LocationFilters = { zone: '', row: '', rack: '', level: '' };
type LocationPath = Pick<PutawayCandidate, 'locationPath' | 'locationType'>;

export function normalizeLocationText(value: string): string {
  return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/[đĐ]/g, 'd').toLowerCase()
    .replace(/\bzone(?=[a-z0-9])/g, 'zone ').replace(/\bosan(?=\s*\d)/g, 'o san ')
    .replace(/([a-z])(\d)/g, '$1 $2').replace(/[^a-z0-9]+/g, ' ')
    .trim().replace(/\b0+(?=\d)/g, '').replace(/\s+/g, ' ');
}

export function locationParts(candidate: LocationPath): LocationFilters {
  const parts = candidate.locationPath.split('→').map(part => part.trim());
  const floor = candidate.locationType === 'FLOOR_SLOT';
  const depth = floor ? 3 : 4;
  if (parts.length < depth + 1) return { ...emptyLocationFilters };
  const nodes = parts.slice(-depth);
  return { zone: nodes[0].replace(/^zone\s*/i, ''), row: nodes[1], rack: floor ? '' : nodes[2], level: nodes[depth - 1] };
}

export function matchesLocation(candidate: LocationPath, query: string, filters: Partial<LocationFilters> = {}): boolean {
  const parts = locationParts(candidate);
  for (const key of Object.keys(filters) as (keyof LocationFilters)[]) {
    if (filters[key] && normalizeLocationText(parts[key]) !== normalizeLocationText(filters[key]!)) return false;
  }
  const tokens = normalizeLocationText(query).split(' ').filter(Boolean);
  const words = new Set(normalizeLocationText(`${candidate.locationPath} zone ${parts.zone}`).split(' '));
  // A labelled number must match that component: "kệ 3" cannot match "kệ 1 → tầng 3".
  for (let i = 0; i < tokens.length; i++) {
    const token = tokens[i];
    const dimension = token === 'zone' ? 'zone' : token === 'day' ? 'row' : token === 'ke' ? 'rack' : null;
    if (dimension && tokens[i + 1]) {
      const value = tokens[++i];
      if (normalizeLocationText(parts[dimension]) !== (dimension === 'zone' ? value : `${token} ${value}`)) return false;
    } else if (token === 'tang' && tokens[i + 1]) {
      if (normalizeLocationText(parts.level) !== `tang ${tokens[++i]}`) return false;
    } else if (token === 'o' && tokens[i + 1] === 'san' && tokens[i + 2]) {
      i += 2;
      if (normalizeLocationText(parts.level) !== `o san ${tokens[i]}`) return false;
    } else if (!words.has(token)) return false;
  }
  return true;
}
