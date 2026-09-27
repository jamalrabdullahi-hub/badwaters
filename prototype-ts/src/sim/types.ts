// Core data model shared by the campaign, designers and battle engine.

export type NationId = "USA" | "RUS" | "CHN" | "FRA" | "GBR" | "ISR" | "IRN";
export const NATION_IDS: NationId[] = ["USA", "RUS", "CHN", "FRA", "GBR", "ISR", "IRN"];

export type Category = "missile" | "aircraft" | "ship" | "vehicle" | "drone" | "base";
export const CATEGORIES: Category[] = ["missile", "aircraft", "ship", "vehicle", "drone", "base"];

export type FieldId = "MIS" | "SEN" | "AIR" | "NAV" | "UNM" | "DEF" | "ADV";

export interface Tech {
  id: string;
  name: string;
  field: FieldId;
  year: number; // historical year of availability
  cost: number; // research points at historical year
  requires: string[];
  desc: string;
}

/** A selectable part in a designer slot. `a` holds attributes the category's stat model reads. */
export interface Part {
  id: string;
  name: string;
  tech?: string;
  desc: string;
  a: Record<string, number | string | boolean>;
}

export interface Slot {
  id: string;
  name: string;
  parts: Part[];
}

export interface Param {
  id: string;
  name: string;
  unit: string;
  min: number;
  max: number;
  step: number;
  def: number;
  hint: string;
}

export interface LoadoutEntry {
  designId: string;
  count: number;
}

export type DesignStatus = "draft" | "developing" | "service" | "obsolete";

export interface Design {
  id: string;
  nation: NationId;
  category: Category;
  name: string;
  year: number;
  slots: Record<string, string>;
  params: Record<string, number>;
  loadout: LoadoutEntry[];
  status: DesignStatus;
  devMonthsLeft: number;
  lineage: string; // design family, e.g. "USA:ddg51"; successors keep it
}

export type Severity = "error" | "warn" | "info";
export interface Issue {
  sev: Severity;
  text: string;
}

/** Munition classes decide what a weapon can target and how it flies. */
export type MunitionRole =
  | "ashm" // anti-ship cruise missile
  | "lacm" // land-attack cruise missile
  | "sam" // surface-to-air (aircraft + cruise missiles)
  | "aam" // air-to-air
  | "bmd" // ballistic missile defence interceptor
  | "srbm" // ballistic missile (land or ship target)
  | "asbm" // anti-ship ballistic missile
  | "hgv" // hypersonic glide / hypersonic cruise
  | "arm" // anti-radiation
  | "loiter" // one-way attack drone / loitering munition
  | "torp"; // heavyweight torpedo

export type SizeClass = "S" | "M" | "L" | "XL";

/** Derived performance of any design. Only relevant keys are filled per category. */
export interface Stats {
  cost: number; // $M per unit
  devCost: number; // $M total development
  devMonths: number;
  buildMonths: number;
  upkeep: number; // $M per month
  // munitions
  role?: MunitionRole;
  size?: SizeClass;
  rangeKm?: number;
  mach?: number;
  pk?: number;
  warhead?: number; // kg
  rcs?: number; // m^2
  alt?: number; // cruise altitude m
  softkillResist?: number; // 0..1
  maneuver?: number; // 0..1 terminal maneuver (hurts interceptors)
  interceptAir?: number; // 0..1 effectiveness vs aircraft/cruise
  interceptBallistic?: number; // 0..1 vs ballistic
  interceptHyper?: number; // 0..1 vs hypersonic
  // platforms
  hp?: number;
  speedKts?: number;
  radarKm?: number; // detection range vs 1 m^2
  mastM?: number; // sensor height
  channels?: number; // simultaneous engagements
  ew?: number; // soft-kill / jamming 0..1
  ciwsKm?: number;
  ciwsPk?: number;
  laserKw?: number;
  cells?: number; // launch cells/tubes (ships, vehicles, bases): one missile per cell, S quad-packs
  hardpoints?: number; // aircraft/drone weapon capacity in S-units (S=1, M=2, L=4, XL=8)
  maxSize?: SizeClass;
  airWing?: number; // aircraft capacity
  radiusKm?: number; // combat radius for aircraft/drones
  displacement?: number;
  powerMw?: number;
  powerUse?: number;
  weight?: number;
  weightMax?: number;
  crew?: number;
  hardening?: number; // bases: 0..1 protection for parked aircraft
  mobility?: number; // vehicles: 0..1 chance to evade pre-planned strikes
  carrier?: "none" | "stovl" | "stobar" | "catobar";
  navy?: boolean;
  stealth?: number; // 0..1
  dataLink?: number; // 0..2 none/link/CEC
  aew?: boolean;
  exo?: boolean; // interceptor engages above the atmosphere
  sarh?: boolean; // needs launcher illumination until impact
  fixedOnly?: boolean; // can only hit fixed targets
  arm?: boolean; // homes on emitters
  boat?: boolean; // drone boat
  uav?: boolean;
  recon?: boolean;
  sub?: boolean;
  capacity?: number;
  baseKind?: string;
  loadUsed?: number;
}

// ---------- Campaign ----------

export type UnitLocation =
  | { kind: "base"; baseId: string }
  | { kind: "sea"; theatreId: string }
  | { kind: "transit"; to: { kind: "base"; baseId: string } | { kind: "sea"; theatreId: string }; monthsLeft: number };

export interface Unit {
  id: string;
  nation: NationId;
  designId: string;
  name: string;
  location: UnitLocation;
  damage: number; // 0..1 (1 = destroyed)
  readiness: number; // 0..1
  count: number; // aircraft/vehicles are held as squadrons/batteries; ships count 1
  embarkedOn?: string; // carrier unit id for embarked squadrons
}

export interface Base {
  id: string;
  nation: NationId;
  name: string;
  lat: number;
  lon: number;
  type: "naval" | "air" | "missile" | "radar";
  theatre: string;
  designId?: string;
  damage: number; // 0..1
}

export interface Theatre {
  id: string;
  name: string;
  lat: number;
  lon: number;
}

export interface ProductionOrder {
  id: string;
  designId: string;
  qty: number; // units remaining
  monthsPerUnit: number;
  progress: number; // months of work done on current unit
  destBaseId: string;
}

export interface NationState {
  id: NationId;
  treasury: number; // $B
  rdShare: number; // 0..0.5 share of budget spent on R&D
  prestige: number;
  mods: { id: string; mult: number; until: number }[]; // timed budget multipliers (sanctions, crises)
  known: string[]; // tech ids
  rp: Record<FieldId, number>; // accumulated research points per field
  focus: Record<FieldId, number>; // 0..5 research weight per field
  target: Partial<Record<FieldId, string>>; // pinned tech per field
  stock: Record<string, number>; // munition design id -> count
  orders: ProductionOrder[];
  lastDesignRefresh: Partial<Record<string, number>>; // AI: lineage -> month of last new design
  forceTargets: Record<string, number>; // AI: lineage -> desired unit count at 1990 budget
}

export type InboxKind = "crisis" | "war" | "budget" | "tech" | "fleet" | "battle" | "intel" | "world";

export interface InboxChoice {
  id: string;
  label: string;
  hint: string;
}

export interface InboxItem {
  id: string;
  month: number;
  kind: InboxKind;
  priority: number; // higher sorts first
  title: string;
  body: string;
  choices?: InboxChoice[];
  resolved?: string; // chosen choice id
  data?: Record<string, string | number>;
}

export interface War {
  id: string;
  a: NationId;
  b: NationId;
  started: number; // month index
  score: number; // + favours a
  theatres: string[];
}

export interface BattleRecord {
  id: string;
  month: number;
  title: string;
  summary: string;
  winner: NationId | null;
}

export interface Campaign {
  version: 1;
  seed: number;
  player: NationId;
  month: number; // months since Jan 1990
  nations: Record<NationId, NationState>;
  tension: Record<string, number>; // key "A|B" sorted
  designs: Record<string, Design>;
  units: Unit[];
  bases: Base[];
  wars: War[];
  inbox: InboxItem[];
  news: string[];
  battles: BattleRecord[];
  firedEvents: string[];
  pending: PendingBattle[];
  nextId: number;
  gameOver?: { reason: string; score: number };
}

export const START_YEAR = 1990;
export const END_YEAR = 2050;

export type PendingBattle =
  | { id: string; kind: "strike"; attacker: NationId; defender: NationId; targetBaseId: string; sources: string[]; title: string }
  | { id: string; kind: "engagement"; a: NationId; b: NationId; theatreId: string; title: string };
