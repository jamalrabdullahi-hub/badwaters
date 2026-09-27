import { NATIONS, THEATRES } from "../../data/nations";
import type { Campaign, InboxChoice, NationId } from "../types";
import { NATION_IDS } from "../types";
import { addTension, dateLabel, monthRng, pushNews, statsOf, tension } from "./core";

const ym = (y: number, m: number) => (y - 1990) * 12 + (m - 1);

function mod(c: Campaign, n: NationId, id: string, mult: number, months: number) {
  c.nations[n].mods.push({ id, mult, until: c.month + months });
}

function tell(c: Campaign, who: NationId[] | "all", title: string, body: string, opts: { kind?: "crisis" | "world" | "budget" | "intel"; priority?: number; choices?: InboxChoice[]; data?: Record<string, string | number> } = {}) {
  pushNews(c, title);
  if (who === "all" || who.includes(c.player)) {
    c.inbox.push({
      id: `ev-${c.month}-${c.inbox.length}`, month: c.month, kind: opts.kind ?? "world", priority: opts.priority ?? (opts.choices ? 80 : 20),
      title, body: `${dateLabel(c.month)}. ${body}`, choices: opts.choices, data: opts.data,
    });
  }
}

interface Scripted { id: string; at: number; run: (c: Campaign) => void }

const SCRIPTED: Scripted[] = [
  { id: "kuwait", at: ym(1990, 8), run: (c) => {
    tell(c, "all", "Iraq invades Kuwait", "Iraqi armour has overrun Kuwait. The Gulf states are asking for help, and a coalition is forming.", {
      kind: "crisis",
      choices: ["USA", "GBR", "FRA"].includes(c.player) ? [
        { id: "join", label: "Join the coalition", hint: "+6 prestige, supplemental budget of 4% for 12 months" },
        { id: "stay", label: "Stay out", hint: "−4 prestige" },
      ] : undefined, data: { handler: "kuwait" },
    });
    addTension(c, "USA", "IRN", 3);
  } },
  { id: "ussr", at: ym(1991, 12), run: (c) => {
    tell(c, "all", "The Soviet Union dissolves", "The red flag comes down over the Kremlin. Russia inherits most of the Soviet fleet and almost none of the money to run it.", {
      kind: "crisis",
      choices: c.player === "RUS" ? [
        { id: "mothball", label: "Mothball a third of the fleet", hint: "Scraps older hulls and cuts upkeep sharply. −3 prestige" },
        { id: "keep", label: "Keep the fleet together", hint: "+5 prestige, but upkeep may bankrupt you" },
      ] : undefined, data: { handler: "ussr" },
    });
    for (const n of ["USA", "GBR", "FRA"] as NationId[]) addTension(c, n, "RUS", -18);
    for (const u of c.units) if (u.nation === "RUS") u.readiness = Math.max(0.3, u.readiness - 0.3);
    if (c.player !== "RUS") mothballRussia(c);
  } },
  { id: "taiwan96", at: ym(1996, 3), run: (c) => {
    addTension(c, "USA", "CHN", 18);
    tell(c, "all", "Taiwan Strait missile crisis", "China fires ballistic missiles into waters off Taiwan. Two US carrier groups move toward the strait.", { kind: "crisis" });
  } },
  { id: "sep11", at: ym(2001, 9), run: (c) => {
    mod(c, "USA", "sep11", 1.15, 120);
    addTension(c, "USA", "IRN", 5);
    tell(c, "all", "Terror attacks on New York and Washington", "US defence spending surges as the country goes to war in Afghanistan.", { kind: "world" });
  } },
  { id: "iraq03", at: ym(2003, 3), run: (c) => {
    addTension(c, "USA", "IRN", 6);
    tell(c, "all", "Coalition invades Iraq", "US and British forces invade Iraq. Iran now has American troops on two borders.", { kind: "world" });
  } },
  { id: "leb06", at: ym(2006, 7), run: (c) => {
    addTension(c, "ISR", "IRN", 10);
    tell(c, "all", "War in Lebanon", "An Iranian-made anti-ship missile hits an Israeli corvette off Beirut.", { kind: "world" });
  } },
  { id: "gfc", at: ym(2008, 10), run: (c) => {
    for (const n of ["USA", "GBR", "FRA", "ISR"] as NationId[]) mod(c, n, "gfc", 0.93, 36);
    mod(c, "RUS", "gfc", 0.9, 24);
    mod(c, "CHN", "gfc", 0.97, 12);
    tell(c, "all", "Global financial crisis", "Banks collapse and treasuries tighten defence budgets.", { kind: "budget" });
  } },
  { id: "sanct10", at: ym(2010, 6), run: (c) => {
    mod(c, "IRN", "sanct10", 0.8, 60);
    addTension(c, "USA", "IRN", 6);
    addTension(c, "ISR", "IRN", 6);
    tell(c, "all", "UN sanctions on Iran tighten", "New sanctions target Iranian oil exports and the missile programme.", { kind: "budget" });
  } },
  { id: "crimea", at: ym(2014, 3), run: (c) => {
    addTension(c, "RUS", "USA", 20); addTension(c, "RUS", "GBR", 16); addTension(c, "RUS", "FRA", 12);
    mod(c, "RUS", "crimea", 0.93, 48);
    tell(c, "all", "Russia annexes Crimea", "Russian troops seize Crimea. Western sanctions follow and NATO wakes up.", { kind: "crisis" });
  } },
  { id: "jcpoa", at: ym(2015, 7), run: (c) => {
    mod(c, "IRN", "jcpoa", 1.25, 36);
    addTension(c, "USA", "IRN", -12); addTension(c, "ISR", "IRN", 4);
    tell(c, "all", "Iran nuclear deal signed", "Sanctions relief frees Iranian money, and hardliners spend part of it on missiles.", { kind: "world" });
  } },
  { id: "jcpoaout", at: ym(2018, 5), run: (c) => {
    mod(c, "IRN", "maxpressure", 0.78, 48);
    addTension(c, "USA", "IRN", 15);
    tell(c, "all", "US withdraws from the Iran deal", "'Maximum pressure' sanctions return. Tankers are seized in the Gulf.", { kind: "crisis" });
  } },
  { id: "covid", at: ym(2020, 3), run: (c) => {
    for (const n of NATION_IDS) mod(c, n, "covid", 0.96, 18);
    tell(c, "all", "Pandemic", "A global pandemic shuts down economies and ties up deployments.", { kind: "budget" });
  } },
  { id: "ukraine", at: ym(2022, 2), run: (c) => {
    addTension(c, "RUS", "USA", 25); addTension(c, "RUS", "GBR", 25); addTension(c, "RUS", "FRA", 20);
    mod(c, "USA", "ukr", 1.04, 60); mod(c, "GBR", "ukr", 1.1, 60); mod(c, "FRA", "ukr", 1.1, 60); mod(c, "RUS", "ukr", 1.12, 60);
    tell(c, "all", "Russia invades Ukraine", "Europe rearms. Ukrainian drone boats and missiles sink Russia's Black Sea flagship.", { kind: "crisis" });
  } },
  { id: "oct7", at: ym(2023, 10), run: (c) => {
    addTension(c, "ISR", "IRN", 15);
    mod(c, "ISR", "oct7", 1.25, 30);
    tell(c, "all", "War in Gaza", "Iranian-backed groups open fronts from Lebanon, Yemen and Iraq. Drones and missiles fly at Israel from every direction.", { kind: "crisis" });
  } },
  { id: "april24", at: ym(2024, 4), run: (c) => {
    addTension(c, "ISR", "IRN", 12);
    tell(c, "all", "Iran strikes Israel directly", "For the first time Iran launches hundreds of drones and missiles straight at Israel. A multinational air defence effort shoots down almost all of them.", { kind: "crisis" });
  } },
];

function mothballRussia(c: Campaign) {
  const ships = c.units.filter((u) => u.nation === "RUS" && c.designs[u.designId]?.category === "ship");
  ships.sort((a, b) => (statsOf(c, a.designId)?.cost ?? 0) - (statsOf(c, b.designId)?.cost ?? 0));
  const cut = new Set(ships.slice(0, Math.floor(ships.length / 3)).map((u) => u.id));
  c.units = c.units.filter((u) => !cut.has(u.id));
}

/** Player choices on event inbox items. */
export function resolveEventChoice(c: Campaign, handler: string, choice: string) {
  const p = c.player;
  if (handler === "kuwait") {
    if (choice === "join") { c.nations[p].prestige += 6; mod(c, p, "kuwait", 1.04, 12); }
    else c.nations[p].prestige -= 4;
  }
  if (handler === "ussr") {
    if (choice === "mothball") { mothballRussia(c); c.nations.RUS.prestige -= 3; }
    else c.nations.RUS.prestige += 5;
  }
}

export function runEvents(c: Campaign) {
  for (const e of SCRIPTED) {
    if (e.at === c.month && !c.firedEvents.includes(e.id)) {
      c.firedEvents.push(e.id);
      e.run(c);
    }
  }
  randomEvent(c);
}

function randomEvent(c: Campaign) {
  const r = monthRng(c, 7);
  const p = c.month > ym(2025, 1) ? 0.14 : 0.05;
  if (r() > p) return;
  const pairs: [NationId, NationId][] = [];
  for (let i = 0; i < NATION_IDS.length; i++) for (let j = i + 1; j < NATION_IDS.length; j++) {
    const a = NATION_IDS[i], b = NATION_IDS[j];
    if (NATIONS[a].bloc !== NATIONS[b].bloc) pairs.push([a, b]);
  }
  const [a, b] = pairs[Math.floor(r() * pairs.length)];
  const th = THEATRES[Math.floor(r() * THEATRES.length)];
  const kind = Math.floor(r() * 6);
  const A = NATIONS[a].adjective, B = NATIONS[b].adjective;
  switch (kind) {
    case 0:
      addTension(c, a, b, 8);
      tell(c, [a, b], `Naval incident in the ${th.name}`, `${A} and ${B} warships come within metres of each other. Each side blames the other.`, { kind: "intel" });
      break;
    case 1:
      addTension(c, a, b, 10);
      tell(c, [a, b], `Cyberattack blamed on ${NATIONS[a].name}`, `${B} naval logistics systems are knocked offline for a week.`, { kind: "intel" });
      break;
    case 2:
      addTension(c, a, b, -10);
      tell(c, [a, b], `${NATIONS[a].name} and ${NATIONS[b].name} agree to talks`, "A new hotline and incident-at-sea agreement lower the temperature.", { kind: "world" });
      break;
    case 3: {
      const n = r() < 0.5 ? a : b;
      mod(c, n, `boom${c.month}`, 1.06, 24);
      tell(c, [n], `${NATIONS[n].adjective} economy booms`, "Tax receipts are up and parliament approves a larger defence budget.", { kind: "budget" });
      break;
    }
    case 4: {
      const n = r() < 0.5 ? a : b;
      mod(c, n, `bust${c.month}`, 0.93, 24);
      tell(c, [n], `${NATIONS[n].adjective} recession`, "The finance ministry demands cuts across the board.", { kind: "budget" });
      break;
    }
    default:
      if (tension(c, a, b) > 35) {
        addTension(c, a, b, 12);
        tell(c, [a, b], `Drone shot down over the ${th.name}`, `A ${B} surveillance drone is destroyed by ${A} air defences.`, { kind: "intel" });
      }
  }
}
