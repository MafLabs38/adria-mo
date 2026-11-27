import { statusLabel } from '../../lib/labels';

export default function StatusBadge({ status }: Readonly<{ status: string }>) {
  const { label, tone } = statusLabel(status);
  return <span className={`inline-block rounded-full px-2.5 py-0.5 text-xs font-semibold ${tone}`}>{label}</span>;
}
