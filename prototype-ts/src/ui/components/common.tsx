import { useEffect, useState, useSyncExternalStore, type ReactNode } from "react";
import { NATIONS } from "../../data/nations";
import type { NationId } from "../../sim/types";

export const fmtB = (v: number, dp = 1) => `${v < 0 ? "−" : ""}$${Math.abs(v).toFixed(dp)}B`;
export const fmtM = (v: number) => (Math.abs(v) >= 1000 ? fmtB(v / 1000, 2) : v >= 10 ? `$${Math.round(v)}M` : v >= 1 ? `$${v.toFixed(1)}M` : `$${Math.round(v * 1000)}k`);
export const pct = (v: number) => `${Math.round(v * 100)}%`;

export function Panel(props: { title: ReactNode; sub?: ReactNode; children: ReactNode; className?: string; scroll?: boolean; flush?: boolean; style?: React.CSSProperties; actions?: ReactNode }) {
  return (
    <section className={`panel ${props.className ?? ""}`} style={props.style}>
      <header>
        <h3>{props.title}</h3>
        {props.actions}
        {props.sub !== undefined && <span className="sub">{props.sub}</span>}
      </header>
      <div className={`body ${props.scroll ? "scroll" : ""} ${props.flush ? "flush" : ""}`}>{props.children}</div>
    </section>
  );
}

export function Meter({ v, kind, thin }: { v: number; kind?: "good" | "bad" | "friend"; thin?: boolean }) {
  return <div className={`meter ${kind ?? ""} ${thin ? "thin" : ""}`}><i style={{ width: `${Math.max(0, Math.min(1, v)) * 100}%` }} /></div>;
}

export function Tag({ kind, children }: { kind: string; children: ReactNode }) {
  return <span className={`tag ${kind}`}>{children}</span>;
}

export function NationChip({ n, name = true }: { n: NationId; name?: boolean }) {
  return <span className="nowrap"><span className="nation-chip" style={{ background: NATIONS[n].color }} />{name ? NATIONS[n].name : null}</span>;
}

// ---------- hover cards: one shared card rendered at the app root
type Card = { title: string; text: ReactNode; x: number; y: number } | null;
let card: Card = null;
const cardListeners = new Set<() => void>();
const setCard = (c: Card) => { card = c; cardListeners.forEach((l) => l()); };

export function Hint({ title, text, children, icon = true }: { title: string; text: ReactNode; children: ReactNode; icon?: boolean }) {
  return (
    <span
      className="hint"
      onMouseEnter={(e) => {
        const r = (e.currentTarget as HTMLElement).getBoundingClientRect();
        setCard({ title, text, x: Math.min(r.left, window.innerWidth - 340), y: r.bottom + 6 });
      }}
      onMouseLeave={() => setCard(null)}
    >
      {children}
      {icon && <span className="q">?</span>}
    </span>
  );
}

export function HoverCard() {
  const c = useSyncExternalStore((l) => { cardListeners.add(l); return () => cardListeners.delete(l); }, () => card);
  if (!c) return null;
  const top = c.y + 160 > window.innerHeight ? c.y - 190 : c.y;
  return <div className="hovercard" style={{ left: Math.max(8, c.x), top }}><b>{c.title}</b>{c.text}</div>;
}

export function Modal({ title, onClose, children, actions, wide }: { title: ReactNode; onClose: () => void; children: ReactNode; actions?: ReactNode; wide?: boolean }) {
  useEffect(() => {
    const k = (e: KeyboardEvent) => { if (e.key === "Escape") onClose(); };
    window.addEventListener("keydown", k);
    return () => window.removeEventListener("keydown", k);
  }, [onClose]);
  return (
    <div className="modal-bg" onMouseDown={(e) => { if (e.target === e.currentTarget) onClose(); }}>
      <div className="modal" role="dialog" aria-modal="true" style={wide ? { width: "min(1180px, 100%)" } : undefined}>
        <header>
          <h2>{title}</h2>
          <span className="grow" />
          {actions}
          <button className="iconbtn" onClick={onClose} aria-label="Close">Close</button>
        </header>
        <div className="body">{children}</div>
      </div>
    </div>
  );
}

export function Stat({ label, value, v, kind, hint }: { label: string; value: ReactNode; v?: number; kind?: "good" | "bad" | "friend"; hint?: string }) {
  return (
    <div className="stat">
      <span>{hint ? <Hint title={label} text={hint}>{label}</Hint> : label}</span>
      <b>{value}</b>
      {v !== undefined && <Meter v={v} kind={kind} thin />}
    </div>
  );
}

export function Seg<T extends string | number>({ value, options, onChange }: { value: T; options: [T, string][]; onChange: (v: T) => void }) {
  return (
    <span className="seg">
      {options.map(([v, l]) => <button key={String(v)} className={v === value ? "on" : ""} onClick={() => onChange(v)}>{l}</button>)}
    </span>
  );
}

/** Number input that commits on blur/enter so typing isn't interrupted. */
export function NumField({ value, min, max, step, onChange, width = 90 }: { value: number; min: number; max: number; step: number; onChange: (v: number) => void; width?: number }) {
  const [txt, setTxt] = useState(String(value));
  useEffect(() => setTxt(String(value)), [value]);
  const apply = () => {
    const v = Number(txt);
    if (Number.isFinite(v)) onChange(Math.max(min, Math.min(max, v)));
    else setTxt(String(value));
  };
  return <input type="number" style={{ width }} value={txt} min={min} max={max} step={step} onChange={(e) => setTxt(e.target.value)} onBlur={apply} onKeyDown={(e) => { if (e.key === "Enter") apply(); }} />;
}
