import { useSyncExternalStore } from "react";
import type { Campaign, PendingBattle } from "../sim/types";
import type { BattleState } from "../sim/battle/engine";

export type Screen = "command" | "forces" | "design" | "research" | "production" | "operations" | "world";

export interface Settings {
  scale: number;
  colorblind: boolean;
}

export interface AppState {
  campaign: Campaign | null;
  screen: Screen;
  busy: string | null;
  toast: string | null;
  battle: { state: BattleState; pending: PendingBattle } | null;
  encyclopedia: string | null; // search query when open
  settingsOpen: boolean;
  settings: Settings;
  designFocus: string | null; // design id to open in the bureau
}

const SAVE_KEY = "bad-waters-save-v1";
const SETTINGS_KEY = "bad-waters-settings";

function loadSettings(): Settings {
  try {
    const s = JSON.parse(localStorage.getItem(SETTINGS_KEY) ?? "null");
    if (s && typeof s.scale === "number") return { scale: s.scale, colorblind: !!s.colorblind };
  } catch { /* ignore */ }
  return { scale: 1, colorblind: false };
}

let state: AppState = {
  campaign: null, screen: "command", busy: null, toast: null, battle: null,
  encyclopedia: null, settingsOpen: false, settings: loadSettings(), designFocus: null,
};
const listeners = new Set<() => void>();

export function getState() {
  return state;
}

export function setState(patch: Partial<AppState> | ((s: AppState) => Partial<AppState>)) {
  const p = typeof patch === "function" ? patch(state) : patch;
  state = { ...state, ...p };
  if (p.settings) {
    try { localStorage.setItem(SETTINGS_KEY, JSON.stringify(state.settings)); } catch { /* ignore */ }
  }
  listeners.forEach((l) => l());
}

export function useApp<T>(sel: (s: AppState) => T): T {
  return useSyncExternalStore(
    (l) => { listeners.add(l); return () => listeners.delete(l); },
    () => sel(state),
  );
}

let toastTimer: number | undefined;
export function toast(msg: string) {
  setState({ toast: msg });
  window.clearTimeout(toastTimer);
  toastTimer = window.setTimeout(() => setState({ toast: null }), 4200);
}

/** Apply a command result: a new campaign, or an error message shown as a toast. */
export function commit(r: Campaign | string, ok?: string) {
  if (typeof r === "string") { toast(r); return false; }
  setState({ campaign: r });
  save(r);
  if (ok) toast(ok);
  return true;
}

export function save(c: Campaign) {
  try { localStorage.setItem(SAVE_KEY, JSON.stringify(c)); }
  catch { toast("Autosave failed: browser storage is full or blocked. Use Settings → Export save."); }
}

export function loadSave(): Campaign | null {
  try {
    const s = localStorage.getItem(SAVE_KEY);
    return s ? (JSON.parse(s) as Campaign) : null;
  } catch { return null; }
}

export function clearSave() {
  try { localStorage.removeItem(SAVE_KEY); } catch { /* ignore */ }
}

// ---------- month processing in a worker so the UI stays responsive
let worker: Worker | null = null;
export function endMonth() {
  const c = state.campaign;
  if (!c || state.busy) return;
  if (!worker) worker = new Worker(new URL("../sim/worker.ts", import.meta.url), { type: "module" });
  setState({ busy: "Processing the month: research, budgets, production, AI nations and battles…" });
  worker.onmessage = (e: MessageEvent<{ ok: boolean; c?: Campaign; err?: string }>) => {
    if (e.data.ok && e.data.c) {
      setState({ campaign: e.data.c, busy: null });
      save(e.data.c);
    } else {
      setState({ busy: null });
      toast(`Month processing failed: ${e.data.err}`);
    }
  };
  worker.postMessage(c);
}
