// New weapon programmes AI nations launch as technology and history allow.
// Loadouts marked "auto" are filled with the nation's best in-service munitions by role mix.
import type { MunitionRole, NationId } from "../sim/types";
import type { DesignSeed } from "./starts";

export interface AiProgramme {
  id: string;
  nations: NationId[];
  techs: string[];
  minYear: number;
  names: Partial<Record<NationId, Record<string, string>>>; // nation -> design key -> name
  designs: DesignSeed[];
  autoLoad?: Record<string, [MunitionRole, number][]>; // design key -> role share of cells/hardpoints
  overrides?: Partial<Record<NationId, Record<string, { slots?: Record<string, string>; params?: Record<string, number> }>>>;
  targets: Partial<Record<NationId, [string, number][]>>;
}

const M = (key: string, role: string, prop: string, seeker: string, airframe: string, link: string, weight: number, warhead: number): DesignSeed => ({
  key, cat: "missile", name: key, slots: { role, prop, seeker, airframe, link }, params: { weight, warhead },
});

export const PROGRAMMES: AiProgramme[] = [
  {
    id: "owa", nations: ["IRN", "RUS", "CHN"], techs: ["unm_owa"], minYear: 2012,
    names: {
      IRN: { owa: "Shahed-136", owabat: "Shahed launcher battery" },
      RUS: { owa: "Geran-2", owabat: "Geran launcher battery" },
      CHN: { owa: "ASN-301", owabat: "ASN-301 launcher battery" },
    },
    designs: [
      { key: "owa", cat: "drone", name: "owa", slots: { type: "owa", engine: "piston", guide: "gps", sig: "conv" }, params: { weight: 200, warhead: 50, sqn: 1 } },
      { key: "owabat", cat: "vehicle", name: "owabat", slots: { chassis: "truck6", role: "drone", radar: "none", pd: "none" }, params: { launchers: 4 }, load: [["owa", 60]] },
    ],
    targets: { IRN: [["owabat", 14]], RUS: [["owabat", 8]], CHN: [["owabat", 5]] },
  },
  {
    id: "bmd", nations: ["USA", "ISR", "RUS", "CHN", "FRA", "GBR"], techs: ["def_bmd1"], minYear: 1991,
    names: {
      USA: { bmdm: "PAC-2 GEM", bmdbat: "Patriot BMD battery" }, ISR: { bmdm: "Arrow 2", bmdbat: "Arrow battery" },
      RUS: { bmdm: "9M82 (S-300V)", bmdbat: "S-300V battery" }, CHN: { bmdm: "HQ-9 ABM", bmdbat: "HQ-9 ABM battery" },
      FRA: { bmdm: "Aster 30 B1", bmdbat: "SAMP/T battery" }, GBR: { bmdm: "Aster 30 (UK)", bmdbat: "Sky Sabre BMD battery" },
    },
    designs: [
      M("bmdm", "bmd", "solid", "arh", "conv", "oneway", 1100, 70),
      { key: "bmdbat", cat: "vehicle", name: "bmdbat", slots: { chassis: "truck8", role: "sam", radar: "pesa", pd: "none" }, params: { launchers: 6 }, load: [["bmdm", 24]] },
    ],
    targets: { USA: [["bmdbat", 6]], ISR: [["bmdbat", 4]], RUS: [["bmdbat", 4]], CHN: [["bmdbat", 3]], FRA: [["bmdbat", 2]], GBR: [["bmdbat", 2]] },
  },
  {
    id: "cram", nations: ["ISR", "USA"], techs: ["def_cram"], minYear: 2010,
    names: { ISR: { tamir: "Tamir", dome: "Iron Dome battery" }, USA: { tamir: "Tamir (US)", dome: "Iron Dome (US Army) battery" } },
    designs: [
      M("tamir", "sam", "solid", "arh", "conv", "oneway", 90, 11),
      { key: "dome", cat: "vehicle", name: "dome", slots: { chassis: "truck6", role: "sam", radar: "aesa", pd: "cram" }, params: { launchers: 3 }, load: [["tamir", 48]] },
    ],
    targets: { ISR: [["dome", 10]], USA: [["dome", 2]] },
  },
  {
    id: "asbm", nations: ["CHN"], techs: ["mis_asbm"], minYear: 2010,
    names: { CHN: { asbm: "DF-21D", asbmbat: "DF-21D brigade" } },
    designs: [
      M("asbm", "asbm", "solidb", "arh", "marv", "oneway", 14700, 600),
      { key: "asbmbat", cat: "vehicle", name: "asbmbat", slots: { chassis: "mzkt", role: "tel", radar: "none", pd: "none" }, params: { launchers: 6 }, load: [["asbm", 6]] },
    ],
    targets: { CHN: [["asbmbat", 6]] },
  },
  {
    id: "hgv", nations: ["CHN", "RUS", "USA"], techs: ["mis_hgv"], minYear: 2017,
    names: {
      CHN: { hgvm: "DF-17", hgvbat: "DF-17 brigade" }, RUS: { hgvm: "Avangard-T", hgvbat: "Hypersonic strike battery" },
      USA: { hgvm: "LRHW Dark Eagle", hgvbat: "Dark Eagle battery" },
    },
    designs: [
      M("hgvm", "hgv", "glide", "arh", "conv", "twoway", 15000, 500),
      { key: "hgvbat", cat: "vehicle", name: "hgvbat", slots: { chassis: "mzkt", role: "tel", radar: "none", pd: "none" }, params: { launchers: 4 }, load: [["hgvm", 4]] },
    ],
    targets: { CHN: [["hgvbat", 5]], RUS: [["hgvbat", 3]], USA: [["hgvbat", 3]] },
  },
  {
    id: "shahab3", nations: ["IRN"], techs: ["mis_srbm"], minYear: 1998,
    names: { IRN: { mrbm: "Shahab-3", mrbmbat: "Shahab-3 brigade" } },
    designs: [
      M("mrbm", "srbm", "liquid", "ins", "conv", "none", 16000, 1000),
      { key: "mrbmbat", cat: "vehicle", name: "mrbmbat", slots: { chassis: "mzkt", role: "tel", radar: "none", pd: "none" }, params: { launchers: 4 }, load: [["mrbm", 4]] },
    ],
    targets: { IRN: [["mrbmbat", 8]] },
  },
  {
    id: "fateh", nations: ["IRN"], techs: ["mis_mrbm"], minYear: 2002,
    names: { IRN: { srbm2: "Fateh-110", srbm2bat: "Fateh-110 brigade" } },
    designs: [
      M("srbm2", "srbm", "solidb", "ins", "conv", "none", 3450, 450),
      { key: "srbm2bat", cat: "vehicle", name: "srbm2bat", slots: { chassis: "mzkt", role: "tel", radar: "none", pd: "none" }, params: { launchers: 4 }, load: [["srbm2", 4]] },
    ],
    targets: { IRN: [["srbm2bat", 10]] },
  },
  {
    id: "cv_chn", nations: ["CHN"], techs: [], minYear: 2009,
    names: { CHN: { cv: "Type 001 carrier", j15: "J-15 Flying Shark" } },
    designs: [
      { key: "j15", cat: "aircraft", name: "j15", slots: { frame: "g4", role: "mr", engine: "tf", radar: "pd", basing: "stobar", ew: "jam" }, params: { payload: 3000, sqn: 12 } },
      { key: "cv", cat: "ship", name: "cv", slots: { hull: "cvl", prop: "steam", radar: "r3d", cms: "ntds", launcher: "rail", pd: "gun", ew: "jam", sig: "conv" }, params: { disp: 60000, cells: 0, canisters: 0, wing: 36 } },
    ],
    autoLoad: { j15: [["aam", 0.5], ["ashm", 0.5]] },
    targets: { CHN: [["cv", 3], ["j15", 7]] },
  },
  {
    id: "modern_dd", nations: ["CHN", "RUS", "GBR", "FRA", "ISR", "IRN", "USA"], techs: ["nav_vls"], minYear: 2004,
    names: {
      CHN: { dd: "Type 052D destroyer" }, RUS: { dd: "Admiral Gorshkov-class frigate" }, GBR: { dd: "Type 45 destroyer" },
      FRA: { dd: "FREMM frigate" }, ISR: { dd: "Sa'ar 6 corvette" }, IRN: { dd: "Moudge-class frigate" }, USA: { dd: "Constellation-class frigate" },
    },
    designs: [
      { key: "dd", cat: "ship", name: "dd", slots: { hull: "ddg", prop: "cogag", radar: "r3d", cms: "ntds", launcher: "vls", pd: "gun", ew: "jam", sig: "conv" }, params: { disp: 7500, cells: 64, canisters: 8, wing: 1 } },
    ],
    autoLoad: { dd: [["sam", 0.55], ["ashm", 0.25], ["lacm", 0.2]] },
    overrides: {
      ISR: { dd: { slots: { hull: "cor" }, params: { disp: 1900, cells: 32, canisters: 8, wing: 1 } } },
      IRN: { dd: { slots: { hull: "cor" }, params: { disp: 1500, cells: 8, canisters: 4, wing: 1 } } },
      RUS: { dd: { slots: { hull: "ffg" }, params: { disp: 5400, cells: 32, canisters: 0, wing: 1 } } },
      USA: { dd: { slots: { hull: "ffg" }, params: { disp: 7300, cells: 32, canisters: 16, wing: 1 } } },
      FRA: { dd: { slots: { hull: "ffg" }, params: { disp: 6000, cells: 32, canisters: 8, wing: 1 } } },
      GBR: { dd: { params: { disp: 8000, cells: 48, canisters: 8, wing: 1 } } },
    },
    targets: { CHN: [["dd", 24]], RUS: [["dd", 8]], GBR: [["dd", 6]], FRA: [["dd", 8]], ISR: [["dd", 4]], IRN: [["dd", 4]], USA: [["dd", 10]] },
  },
  {
    id: "male", nations: ["USA", "ISR", "CHN", "IRN", "GBR", "FRA", "RUS"], techs: ["unm_ucav"], minYear: 2002,
    names: {
      USA: { ucav: "MQ-9 Reaper" }, ISR: { ucav: "Hermes 900" }, CHN: { ucav: "Wing Loong II" }, IRN: { ucav: "Shahed-129" },
      GBR: { ucav: "MQ-9B Protector" }, FRA: { ucav: "MQ-9 Reaper (FR)" }, RUS: { ucav: "Orion" },
    },
    designs: [
      { key: "ucav", cat: "drone", name: "ucav", slots: { type: "ucav", engine: "piston", guide: "sar", sig: "conv" }, params: { weight: 4700, warhead: 600, sqn: 6 } },
    ],
    autoLoad: { ucav: [["ashm", 1]] },
    targets: { USA: [["ucav", 4]], ISR: [["ucav", 3]], CHN: [["ucav", 4]], IRN: [["ucav", 3]], GBR: [["ucav", 1]], FRA: [["ucav", 1]], RUS: [["ucav", 2]] },
  },
];
