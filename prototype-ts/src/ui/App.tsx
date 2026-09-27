import { useEffect, useRef } from "react";
import { NATIONS } from "../data/nations";
import { STARTS } from "../data/starts";
import { budgetAt } from "../data/nations";
import { dateLabel, END_MONTH, eraName, monthlyIncome, tension, yearOf } from "../sim/campaign/core";
import { newCampaign } from "../sim/campaign/create";
import type { Campaign, NationId } from "../sim/types";
import { NATION_IDS } from "../sim/types";
import { fmtB, HoverCard, Modal, NationChip } from "./components/common";
import { clearSave, commit, endMonth, loadSave, setState, toast, useApp, type Screen } from "./store";
import { CommandScreen } from "./screens/Command";
import { ForcesScreen } from "./screens/Forces";
import { DesignScreen } from "./screens/Design";
import { ResearchScreen } from "./screens/Research";
import { ProductionScreen } from "./screens/Production";
import { OperationsScreen } from "./screens/Operations";
import { WorldScreen } from "./screens/World";
import { BattleView } from "./screens/Battle";
import { Encyclopedia } from "./screens/Encyclopedia";
import { finalScore } from "../sim/campaign/turn";

const NAV: { id: Screen; name: string; key: string }[] = [
  { id: "command", name: "Command", key: "1" },
  { id: "forces", name: "Forces", key: "2" },
  { id: "design", name: "Design Bureau", key: "3" },
  { id: "research", name: "Research", key: "4" },
  { id: "production", name: "Production", key: "5" },
  { id: "operations", name: "Operations", key: "6" },
  { id: "world", name: "World", key: "7" },
];

export function App() {
  const settings = useApp((s) => s.settings);
  const campaign = useApp((s) => s.campaign);
  const battle = useApp((s) => s.battle);
  const busy = useApp((s) => s.busy);
  const toastMsg = useApp((s) => s.toast);
  const enc = useApp((s) => s.encyclopedia);
  const settingsOpen = useApp((s) => s.settingsOpen);

  useEffect(() => {
    document.documentElement.dataset.cb = settings.colorblind ? "1" : "0";
    (document.getElementById("root") as HTMLElement).style.zoom = String(settings.scale);
  }, [settings]);

  return (
    <>
      {!campaign ? <MainMenu /> : campaign.gameOver ? <GameOver c={campaign} /> : <Shell c={campaign} />}
      {battle && <BattleView />}
      {enc !== null && <Encyclopedia />}
      {settingsOpen && <SettingsModal />}
      {busy && <div className="busy" role="status"><div><span className="spinner" />{busy}</div></div>}
      {toastMsg && <div className="toast" role="status">{toastMsg}</div>}
      <HoverCard />
    </>
  );
}

function Shell({ c }: { c: Campaign }) {
  const screen = useApp((s) => s.screen);
  const me = c.nations[c.player];
  const income = monthlyIncome(c, c.player) * 12;
  const worst = NATION_IDS.filter((n) => n !== c.player).map((n) => [n, tension(c, c.player, n)] as const).sort((a, b) => b[1] - a[1])[0];
  const openItems = c.inbox.filter((i) => (i.choices && !i.resolved) || i.data?.pending).length;
  const progress = c.month / END_MONTH;

  useEffect(() => {
    const k = (e: KeyboardEvent) => {
      const tag = (e.target as HTMLElement).tagName;
      if (tag === "INPUT" || tag === "SELECT" || tag === "TEXTAREA") return;
      const n = NAV.find((x) => x.key === e.key);
      if (n) setState({ screen: n.id });
    };
    window.addEventListener("keydown", k);
    return () => window.removeEventListener("keydown", k);
  }, []);

  return (
    <div className="app">
      <nav className="nav" aria-label="Main">
        <div className="brand">BAD WATERS<small>NAVAL · AIR · STRATEGIC</small></div>
        {NAV.map((n) => (
          <button key={n.id} className={`nav-item ${screen === n.id ? "on" : ""}`} onClick={() => setState({ screen: n.id })}>
            {n.name}
            {n.id === "command" && openItems > 0 ? <span className="badge">{openItems}</span> : <span className="k">{n.key}</span>}
          </button>
        ))}
        <div className="nav-foot">
          <span><NationChip n={c.player} /></span>
          <span className="mono">Prestige {me.prestige.toFixed(0)}</span>
          <button onClick={() => setState({ encyclopedia: "" })}>Encyclopedia</button>
          <button onClick={() => setState({ settingsOpen: true })}>Settings & save</button>
        </div>
      </nav>
      <header className="top">
        <div className="t"><small>Date</small><b>{dateLabel(c.month)}</b></div>
        <div className="t"><small>Era</small><b>{eraName(yearOf(c.month))}</b></div>
        <div className="t"><small>Budget</small><b>{fmtB(income)}/yr</b></div>
        <div className="t"><small>Reserve</small><b className={me.treasury < 0 ? "bad" : ""}>{fmtB(me.treasury, 2)}</b></div>
        <div className="t"><small>Wars</small><b className={c.wars.some((w) => w.a === c.player || w.b === c.player) ? "bad" : ""}>{c.wars.filter((w) => w.a === c.player || w.b === c.player).length || "None"}</b></div>
        <span className="sp" />
        {worst && <span className={`tag ${worst[1] >= 75 ? "bad" : worst[1] >= 50 ? "warn" : "dim"}`}>Tension {Math.round(worst[1])} · {NATIONS[worst[0]].name}</span>}
        <button className="iconbtn" onClick={() => setState({ encyclopedia: "" })}>Search</button>
        <button className="endturn" onClick={endMonth} disabled={!!c.gameOver}>End month ▸</button>
      </header>
      <main className="main">
        {screen === "command" && <CommandScreen c={c} />}
        {screen === "forces" && <ForcesScreen c={c} />}
        {screen === "design" && <DesignScreen c={c} />}
        {screen === "research" && <ResearchScreen c={c} />}
        {screen === "production" && <ProductionScreen c={c} />}
        {screen === "operations" && <OperationsScreen c={c} />}
        {screen === "world" && <WorldScreen c={c} />}
      </main>
      <footer className="foot">
        <span>1990</span>
        <span className="era-mini">
          <span className="bar">
            <i style={{ width: `${progress * 100}%` }} />
            <em style={{ left: `${(20 / 60) * 100}%` }} /><em style={{ left: `${(40 / 60) * 100}%` }} />
          </span>
        </span>
        <span>2050</span>
        <span className="ticker">{c.news[0] ?? ""}</span>
        <span style={{ marginLeft: "auto" }}>AUTOSAVE {dateLabel(c.month)}</span>
      </footer>
    </div>
  );
}

function MainMenu() {
  const saved = loadSave();
  const start = (n: NationId) => {
    const c = newCampaign(n);
    commit(c);
    setState({ screen: "command" });
  };
  const fileRef = useRef<HTMLInputElement>(null);
  return (
    <div className="menu">
      <h1>BAD WATERS</h1>
      <p className="lede">Modern naval, air and strategic warfare, 1990–2050. Design your missiles, aircraft, ships, launchers, drones and bases, research a 60-year tech tree, and fight the battles yourself on a 2D tactical plot. Everything from sea-skimmers and Aegis to hypersonic gliders and drone swarms.</p>
      <div className="era-rail">
        <div><b>1990</b>Aegis, VLS and the end of the Cold War</div>
        <div><b>2010</b>Networks, drones and the carrier-killers</div>
        <div><b>2030 → 2050</b>Hypersonics, lasers, autonomy and swarms</div>
      </div>
      <div className="row wrap" style={{ marginBottom: 22, gap: 10 }}>
        {saved && !saved.gameOver && <button className="btn primary" onClick={() => setState({ campaign: saved })}>Continue: {NATIONS[saved.player].name}, {dateLabel(saved.month)}</button>}
        <button className="btn" onClick={() => fileRef.current?.click()}>Load save file…</button>
        <input ref={fileRef} type="file" accept=".json,application/json" hidden onChange={async (e) => {
          const f = e.target.files?.[0];
          if (!f) return;
          try { const c = JSON.parse(await f.text()) as Campaign; if (!c.nations || !c.player) throw new Error("not a save"); commit(c, "Save loaded."); }
          catch { toast("That file isn't a Bad Waters save."); }
        }} />
      </div>
      <h2 style={{ fontSize: 22, marginBottom: 12 }}>Choose your nation</h2>
      <div className="nations">
        {NATION_IDS.map((n) => {
          const info = NATIONS[n];
          const f = STARTS[n].forces;
          const count = (cat: string) => f.filter((x) => STARTS[n].designs.find((d) => d.key === x.key)?.cat === cat).reduce((a, x) => a + x.n, 0)
            + (cat === "aircraft" ? f.reduce((a, x) => a + (x.wing ?? []).reduce((q, w) => q + w[1], 0) * x.n, 0) : 0);
          return (
            <button key={n} className="nation-card" style={{ ["--c" as string]: info.color }} onClick={() => start(n)}>
              <div className="row between"><h2>{info.name}</h2><span className="mono dim">{fmtB(budgetAt(n, 1990), 1)}/yr</span></div>
              <p>{info.pitch}</p>
              <div className="row wrap small mono dim" style={{ gap: 14 }}>
                <span>{count("ship")} ships</span><span>{count("aircraft")} squadrons</span><span>{count("vehicle")} batteries</span><span>{info.techLag ? `${info.techLag} yrs behind` : "tech leader"}</span>
              </div>
              <div className="row" style={{ alignItems: "flex-start", gap: 16 }}>
                <ul className="good">{info.strengths.map((s) => <li key={s}><span style={{ color: "var(--ink)" }}>{s}</span></li>)}</ul>
                <ul className="bad">{info.weaknesses.map((s) => <li key={s}><span style={{ color: "var(--ink)" }}>{s}</span></li>)}</ul>
              </div>
              <span className="accent" style={{ fontWeight: 600 }}>Take command ▸</span>
            </button>
          );
        })}
      </div>
    </div>
  );
}

function GameOver({ c }: { c: Campaign }) {
  const g = c.gameOver!;
  return (
    <div className="menu">
      <h1>{c.month > END_MONTH ? "2050" : "RELIEVED"}</h1>
      <p className="lede">{g.reason}</p>
      <div className="kpis" style={{ maxWidth: 900, marginTop: 24 }}>
        <div className="kpi"><span>FINAL SCORE</span><b>{g.score}</b></div>
        <div className="kpi"><span>PRESTIGE</span><b>{c.nations[c.player].prestige.toFixed(0)}</b></div>
        <div className="kpi"><span>TECHS</span><b>{c.nations[c.player].known.length}</b></div>
        <div className="kpi"><span>BATTLES</span><b>{c.battles.filter((b) => b.summary).length}</b></div>
      </div>
      <div className="row" style={{ marginTop: 24 }}>
        <button className="btn primary" onClick={() => { clearSave(); setState({ campaign: null }); }}>New campaign</button>
      </div>
    </div>
  );
}

function SettingsModal() {
  const s = useApp((x) => x.settings);
  const c = useApp((x) => x.campaign);
  const close = () => setState({ settingsOpen: false });
  const exportSave = () => {
    if (!c) return;
    const blob = new Blob([JSON.stringify(c)], { type: "application/json" });
    const a = document.createElement("a");
    a.href = URL.createObjectURL(blob);
    a.download = `bad-waters-${c.player}-${dateLabel(c.month).replace(" ", "-")}.json`;
    a.click();
    URL.revokeObjectURL(a.href);
  };
  return (
    <Modal title="Settings" onClose={close}>
      <div className="param">
        <label>UI scale</label>
        <input type="range" min={0.8} max={1.5} step={0.05} value={s.scale} onChange={(e) => setState({ settings: { ...s, scale: Number(e.target.value) } })} />
        <span className="mono">{Math.round(s.scale * 100)}%</span>
      </div>
      <label className="row"><input type="checkbox" checked={s.colorblind} onChange={(e) => setState({ settings: { ...s, colorblind: e.target.checked } })} /> Colour-blind safe palette (blue/orange; friend and foe also differ by shape)</label>
      {c && (
        <div className="row wrap" style={{ marginTop: 8 }}>
          <button className="btn" onClick={exportSave}>Export save file</button>
          <button className="btn danger" onClick={() => { setState({ campaign: null, settingsOpen: false }); }}>Quit to menu (autosaved)</button>
          {c && <span className="dim small">Score if the campaign ended now: {finalScore(c)}</span>}
        </div>
      )}
      <p className="dim small">Shortcuts: 1–7 switch screens. In battle: Space pauses, right-click moves the selected unit, the mouse wheel zooms.</p>
    </Modal>
  );
}
