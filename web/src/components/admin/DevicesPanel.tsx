import { useCallback, useEffect, useState, type SyntheticEvent } from 'react';
import type { Api } from '../../lib/api';
import { formatDateTime } from '../../lib/labels';
import { pairingUrl } from '../../lib/pairing';
import QrCode from './QrCode';
import type { DeviceSummary, PairingCode } from './types';
import { ErrorMessage, inputClass, Panel, primaryButton, secondaryButton } from './ui';

/** How often the list is read while a code waits to be scanned. */
const POLL_MS = 3000;

function DeviceItem({
  device,
  timeZone,
  onRemove,
}: Readonly<{ device: DeviceSummary; timeZone: string; onRemove: () => void }>) {
  return (
    <li className="flex flex-wrap items-center justify-between gap-2 rounded-lg bg-brand-50 px-3 py-2">
      <span>
        <strong>{device.name}</strong>
        {device.isCurrent ? ' (ce téléphone)' : ''}
        <br />
        <span className="text-sm text-slate-700">
          {device.pairedAt ? `Appairé le ${formatDateTime(device.pairedAt, timeZone)}` : 'Code en attente de scan'}
        </span>
      </span>
      <button type="button" className="text-red-800 underline" onClick={onRemove}>
        Retirer
      </button>
    </li>
  );
}

/**
 * "Mes téléphones" (spec §4.1): on a computer, a QR code to scan with the phone's camera; the phone opens the administration
 * already logged in, without its owner typing the password. Each phone can be removed, which ends its session.
 */
export default function DevicesPanel({ api, timeZone }: Readonly<{ api: Api; timeZone: string }>) {
  const [devices, setDevices] = useState<DeviceSummary[]>([]);
  const [name, setName] = useState('Mon téléphone');
  const [pairing, setPairing] = useState<PairingCode | null>(null);
  const [paired, setPaired] = useState<string | null>(null);
  const [error, setError] = useState<unknown>(null);

  const load = useCallback(() => api.get<DeviceSummary[]>('/devices').then(setDevices, setError), [api]);
  useEffect(() => void load(), [load]);

  // While a code waits, the list is read again until the phone shows up as paired.
  useEffect(() => {
    if (!pairing) {
      return undefined;
    }
    const timer = globalThis.setInterval(() => {
      api.get<DeviceSummary[]>('/devices').then((list) => {
        setDevices(list);
        const scanned = list.find((d) => d.id === pairing.id && d.pairedAt !== null);
        if (scanned) {
          setPairing(null);
          setPaired(scanned.name);
        }
      }, setError);
    }, POLL_MS);
    return () => globalThis.clearInterval(timer);
  }, [pairing, api]);

  const create = async (event: SyntheticEvent) => {
    event.preventDefault();
    setError(null);
    setPaired(null);
    try {
      setPairing(await api.post<PairingCode>('/devices', { name }));
      await load();
    } catch (error_) {
      setError(error_);
    }
  };

  const remove = async (device: DeviceSummary) => {
    const warning = device.isCurrent ? ' Vous serez déconnecté de ce téléphone.' : '';
    if (!globalThis.confirm(`Retirer « ${device.name} » ?${warning}`)) {
      return;
    }
    try {
      await api.delete(`/devices/${encodeURIComponent(device.id)}`);
      if (device.isCurrent) {
        globalThis.location.reload();
        return;
      }
      if (device.id === pairing?.id) {
        setPairing(null);
      }
      await load();
    } catch (error_) {
      setError(error_);
    }
  };

  return (
    <Panel title="Mes téléphones">
      <p className="text-sm text-slate-700">
        Appairez votre téléphone une fois : il s'ouvrira sur l'administration déjà connecté, sans taper de mot de passe.
      </p>
      {devices.length > 0 ? (
        <ul className="space-y-2">
          {devices.map((device) => (
            <DeviceItem key={device.id} device={device} timeZone={timeZone} onRemove={() => void remove(device)} />
          ))}
        </ul>
      ) : null}
      {paired ? (
        <output className="block rounded-lg bg-brand-100 p-3 text-sm text-brand-900">
          « {paired} » est appairé : il est connecté.
        </output>
      ) : null}
      {pairing ? (
        <div className="space-y-2 rounded-xl bg-white p-3 ring-1 ring-brand-200">
          <QrCode
            text={pairingUrl(globalThis.location.origin, import.meta.env.BASE_URL, pairing.code)}
            label="Code QR d'appairage du téléphone"
          />
          <p className="text-center text-sm">
            Scannez ce code avec l'appareil photo du téléphone. Il sert une seule fois, jusqu'à{' '}
            {new Intl.DateTimeFormat('fr-FR', { timeStyle: 'short', timeZone }).format(new Date(pairing.expiresAt))}.
          </p>
          <button type="button" className={`${secondaryButton} w-full`} onClick={() => setPairing(null)}>
            Fermer le code
          </button>
        </div>
      ) : (
        <form onSubmit={(e) => void create(e)} className="space-y-3">
          <label className="block font-medium">
            <span>Nom du téléphone</span>
            <input
              className={inputClass}
              required
              maxLength={60}
              value={name}
              onChange={(e) => setName(e.target.value)}
            />
          </label>
          <button type="submit" className={`${primaryButton} w-full`}>
            Ajouter un téléphone
          </button>
        </form>
      )}
      <ErrorMessage error={error} />
    </Panel>
  );
}
