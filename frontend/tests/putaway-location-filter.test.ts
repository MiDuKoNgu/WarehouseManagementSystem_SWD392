import { expect, it } from 'vitest';
import { locationParts, matchesLocation, normalizeLocationText } from '../src/features/inbound/putaway-location-filter';

const tier = (rack = '03', level = '01') => ({ locationPath: `Kho A → C → Dãy 02 → Kệ ${rack} → Tầng ${level}`, locationType: 'TIER' });
const floor = { locationPath: 'Kho A → A → Dãy 04 → Ô sàn 01', locationType: 'FLOOR_SLOT' };

it('normalizes Vietnamese accents, case and surrounding whitespace', () => {
  expect(normalizeLocationText('  KỆ  ')).toBe(normalizeLocationText('ke'));
  expect(normalizeLocationText('ĐÃY')).toBe(normalizeLocationText('day'));
  expect(normalizeLocationText('Ô SÀN')).toBe(normalizeLocationText('o san'));
});

it('extracts warehouse path components for racks and floor slots without treating warehouse A as zone A', () => {
  expect(locationParts(tier())).toEqual({ zone: 'C', row: 'Dãy 02', rack: 'Kệ 03', level: 'Tầng 01' });
  expect(locationParts(floor)).toEqual({ zone: 'A', row: 'Dãy 04', rack: '', level: 'Ô sàn 01' });
  expect(matchesLocation(tier(), 'zone A')).toBe(false);
  expect(locationParts({ ...tier(), locationPath: 'Kho A → Zone C → Dãy 02 → Kệ 03 → Tầng 01' }).zone).toBe('C');
});

it('combines case, accent and numeric normalization for compact and spaced labeled queries', () => {
  for (const query of ['zone C, kệ 3', 'ZONEC KE03', 'zone c day2 ke3 tang1', 'Zone C Dãy 02 Kệ03 Tầng 01']) {
    expect(matchesLocation(tier(), query), query).toBe(true);
  }
  expect(matchesLocation(floor, 'zonea day4 osan1')).toBe(true);
  expect(matchesLocation(floor, 'Zone A, dãy 04, ô sàn 1')).toBe(true);
});

it('matches numeric components exactly instead of rack substrings or numbers from another component', () => {
  expect(matchesLocation(tier('13'), 'zone C ke3')).toBe(false);
  expect(matchesLocation(tier('01', '03'), 'ke3')).toBe(false);
  expect(matchesLocation(tier('03', '01'), 'ke1')).toBe(false);
  expect(matchesLocation(tier(), 'day1')).toBe(false);
  expect(matchesLocation(tier(), 'tang2')).toBe(false);
  expect(matchesLocation(floor, 'tang1')).toBe(false);
  expect(matchesLocation(floor, 'ke1')).toBe(false);
});

it('applies structured filters together with text and accepts normalized option values', () => {
  expect(matchesLocation(tier(), 'ke3', { zone: 'C', row: 'Dãy 02', rack: 'Kệ 03', level: 'Tầng 01' })).toBe(true);
  expect(matchesLocation(tier(), '', { zone: 'C', rack: 'Kệ 13' })).toBe(false);
  expect(matchesLocation(tier(), 'zone D', { zone: 'C' })).toBe(false);
  expect(matchesLocation(floor, '', { rack: 'Kệ 01' })).toBe(false);
  expect(matchesLocation(tier(), '', {})).toBe(true);
});

it('does not turn unknown zones or malformed paths into a match for a specific location query', () => {
  expect(matchesLocation(tier(), 'zone Z')).toBe(false);
  expect(matchesLocation(tier(), 'ke99')).toBe(false);
  const malformed = { locationPath: 'Kho A → Kệ 03', locationType: 'TIER' };
  expect(matchesLocation(malformed, 'zone C ke3')).toBe(false);
  expect(matchesLocation(malformed, '', { zone: 'C' })).toBe(false);
});
