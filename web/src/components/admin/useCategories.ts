import { useEffect, useState } from 'react';
import type { Api } from '../../lib/api';
import type { Category } from './types';

export function useCategories(api: Api): Category[] {
  const [categories, setCategories] = useState<Category[]>([]);
  useEffect(() => {
    api.get<Category[]>('/categories').then(setCategories, () => setCategories([]));
  }, [api]);
  return categories;
}
