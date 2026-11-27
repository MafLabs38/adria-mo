import type { Category } from './types';
import { inputClass } from './ui';

export interface ConstatFieldValues {
  title: string;
  category: string;
  location: string;
  isPublic: boolean;
}

interface Props {
  values: ConstatFieldValues;
  categories: Category[];
  onChange: (values: ConstatFieldValues) => void;
}

export default function ConstatFields({ values, categories, onChange }: Readonly<Props>) {
  return (
    <>
      <label className="block font-medium">
        <span>Titre</span>
        <input
          className={inputClass}
          required
          maxLength={120}
          placeholder="Porte du hall qui ne ferme plus"
          value={values.title}
          onChange={(e) => onChange({ ...values, title: e.target.value })}
        />
      </label>
      <label className="block font-medium">
        <span>Catégorie</span>
        <select
          className={inputClass}
          required
          value={values.category}
          onChange={(e) => onChange({ ...values, category: e.target.value })}
        >
          <option value="">Choisir…</option>
          {categories.map((c) => (
            <option key={c.key} value={c.key}>
              {c.label}
            </option>
          ))}
        </select>
      </label>
      <label className="block font-medium">
        <span>Emplacement</span>
        <input
          className={inputClass}
          maxLength={120}
          placeholder="Hall bât. B, palier du 3ᵉ…"
          value={values.location}
          onChange={(e) => onChange({ ...values, location: e.target.value })}
        />
      </label>
      <label className="flex items-center gap-2 font-medium">
        <input
          type="checkbox"
          className="h-5 w-5"
          checked={values.isPublic}
          onChange={(e) => onChange({ ...values, isPublic: e.target.checked })}
        />
        <span>Visible sur la page publique</span>
      </label>
    </>
  );
}
