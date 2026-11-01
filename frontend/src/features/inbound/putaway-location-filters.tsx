import type { PutawayCandidate } from './putaway-api';
import { emptyLocationFilters, locationParts, matchesLocation, normalizeLocationText, type LocationFilters } from './putaway-location-filter';

const dimensions: { key: keyof LocationFilters; label: string }[] = [
  { key: 'zone', label: 'Zone' }, { key: 'row', label: 'Dãy' }, { key: 'rack', label: 'Kệ' }, { key: 'level', label: 'Tầng / Ô sàn' },
];

export function PutawayLocationFilters({ candidates, query, filters, onQuery, onFilters, onReset, disabled }: {
  candidates: PutawayCandidate[]; query: string; filters: LocationFilters; onQuery: (value: string) => void;
  onFilters: (value: LocationFilters) => void; onReset: () => void; disabled: boolean;
}) {
  const active = !!query.trim() || Object.values(filters).some(Boolean);
  return <section className="po-card putaway-location-filters" aria-label="Bộ lọc vị trí">
    <label htmlFor="putaway-location-search">Tìm vị trí</label>
    <div className="putaway-input-action"><input id="putaway-location-search" type="search" maxLength={120} value={query} disabled={disabled} placeholder="Ví dụ: zone C, kệ 3" onChange={e => onQuery(e.target.value)} /><button className="utility-button" type="button" disabled={disabled || !active} onClick={onReset}>Xóa bộ lọc</button></div>
    <div className="putaway-location-filter-fields">{dimensions.map(({ key, label }, index) => {
      const parents = Object.fromEntries(dimensions.slice(0, index).map(parent => [parent.key, filters[parent.key]]));
      const choices = new Map<string, string>();
      candidates.filter(candidate => matchesLocation(candidate, '', parents)).forEach(candidate => {
        const value = locationParts(candidate)[key];
        if (value && !choices.has(normalizeLocationText(value))) choices.set(normalizeLocationText(value), value);
      });
      const options = [...choices.values()].sort((a, b) => a.localeCompare(b, 'vi', { numeric: true }));
      return <label key={key} htmlFor={`putaway-filter-${key}`}>{label}<select id={`putaway-filter-${key}`} value={filters[key]} disabled={disabled || !options.length} onChange={e => {
        const next = { ...filters, [key]: e.target.value };
        dimensions.slice(index + 1).forEach(child => { next[child.key] = emptyLocationFilters[child.key]; });
        onFilters(next);
      }}><option value="">Tất cả</option>{options.map(value => <option key={value} value={value}>{value}</option>)}</select></label>;
    })}</div>
  </section>;
}
