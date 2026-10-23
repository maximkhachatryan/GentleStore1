import { useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { App as AntApp } from 'antd';
import type { OrderPulseItem } from '@gentlestore/shared';
import { api } from '../api';

const POLL_MS = 30_000;
const SOUND_KEY = 'gs.console.alertSound';

export const alertSound = {
  enabled(): boolean {
    try {
      return localStorage.getItem(SOUND_KEY) !== 'off';
    } catch {
      return true;
    }
  },
  set(on: boolean): void {
    try {
      localStorage.setItem(SOUND_KEY, on ? 'on' : 'off');
    } catch {
      // Storage blocked: the default (on) still applies.
    }
  },
};

export function playChime(): void {
  try {
    const Ctx = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!Ctx) return;
    const ctx = new Ctx();
    const start = ctx.currentTime;
    [880, 1320].forEach((freq, i) => {
      const at = start + i * 0.16;
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.type = 'sine';
      osc.frequency.value = freq;
      gain.gain.setValueAtTime(0.0001, at);
      gain.gain.exponentialRampToValueAtTime(0.25, at + 0.02);
      gain.gain.exponentialRampToValueAtTime(0.0001, at + 0.45);
      osc.connect(gain).connect(ctx.destination);
      osc.start(at);
      osc.stop(at + 0.5);
    });
    window.setTimeout(() => void ctx.close(), 1200);
  } catch {
    // Audio unavailable or blocked until the user interacts; the visual alert still shows.
  }
}

const isNewer = (next: OrderPulseItem | null, prev: string | undefined) =>
  next !== null && (prev === undefined || Date.parse(next.at) > Date.parse(prev));

/**
 * Polls the store's order pulse and alerts on anything newer than what was there when the console
 * opened: an in-app notification, a chime, and a desktop notification while the tab is hidden.
 * Returns the number of orders waiting on the store, for the nav badge.
 */
export function useOrderAlerts(enabled: boolean): number {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const { notification } = AntApp.useApp();
  const baseline = useRef<{ placed?: string; paid?: string } | null>(null);

  const pulse = useQuery({
    queryKey: ['backoffice', 'orders', 'pulse'],
    queryFn: () => api.backoffice.orderPulse(),
    enabled,
    refetchInterval: POLL_MS,
    refetchIntervalInBackground: true,
  });

  useEffect(() => {
    const data = pulse.data;
    if (!data) return;

    const prev = baseline.current;
    baseline.current = { placed: data.latestPlaced?.at, paid: data.latestPaid?.at };
    // The first answer is what already existed — nothing to announce.
    if (!prev) return;

    const alerts: { item: OrderPulseItem; title: string }[] = [];
    if (isNewer(data.latestPlaced, prev.placed))
      alerts.push({ item: data.latestPlaced!, title: t('alerts.newOrder', { number: data.latestPlaced!.orderNumber }) });
    if (isNewer(data.latestPaid, prev.paid))
      alerts.push({ item: data.latestPaid!, title: t('alerts.orderPaid', { number: data.latestPaid!.orderNumber }) });
    if (alerts.length === 0) return;

    qc.invalidateQueries({ queryKey: ['backoffice', 'orders'], predicate: (q) => q.queryKey[2] !== 'pulse' });
    if (alertSound.enabled()) playChime();

    for (const { item, title } of alerts) {
      const open = () => navigate(`/store/orders?order=${item.id}`);

      notification.info({
        key: `${title}-${item.id}`,
        title,
        description: t('alerts.open'),
        placement: 'topRight',
        duration: 0,
        onClick: open,
        style: { cursor: 'pointer' },
      });

      if (document.hidden && 'Notification' in window && Notification.permission === 'granted') {
        const desktop = new Notification(title, { body: t('alerts.open'), tag: `${title}-${item.id}` });
        desktop.onclick = () => {
          window.focus();
          open();
          desktop.close();
        };
      }
    }
  }, [pulse.data, t, qc, navigate, notification]);

  return pulse.data?.pendingCount ?? 0;
}
