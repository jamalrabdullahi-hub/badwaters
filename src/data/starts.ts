// 1990 starting designs, stockpiles and orders of battle.
// Designs are built from the same parts the player uses, so every one is editable and upgradable.
import type { Category, NationId } from "../sim/types";

export interface DesignSeed {
  key: string;
  cat: Category;
  name: string;
  slots: Record<string, string>;
  params: Record<string, number>;
  load?: [string, number][];
  status?: "service" | "developing" | "draft";
  devLeft?: number;
}

/** [designKey, count, location]: location is a base id or "sea:THEATRE". `wing` lists squadrons embarked per carrier. */
export interface ForceSeed {
  key: string;
  n: number;
  at: string;
  wing?: [string, number][];
}

export interface StartSeed {
  designs: DesignSeed[];
  stock: [string, number][];
  forces: ForceSeed[];
}

const M = (key: string, name: string, role: string, prop: string, seeker: string, airframe: string, link: string, weight: number, warhead: number, extra: Partial<DesignSeed> = {}): DesignSeed => ({
  key, cat: "missile", name, slots: { role, prop, seeker, airframe, link }, params: { weight, warhead }, ...extra,
});
const A = (key: string, name: string, frame: string, role: string, engine: string, radar: string, basing: string, ew: string, payload: number, sqn: number, load: [string, number][] = [], extra: Partial<DesignSeed> = {}): DesignSeed => ({
  key, cat: "aircraft", name, slots: { frame, role, engine, radar, basing, ew }, params: { payload, sqn }, load, ...extra,
});
const S = (key: string, name: string, hull: string, prop: string, radar: string, cms: string, launcher: string, pd: string, ew: string, sig: string, disp: number, cells: number, canisters: number, wing: number, load: [string, number][] = [], extra: Partial<DesignSeed> = {}): DesignSeed => ({
  key, cat: "ship", name, slots: { hull, prop, radar, cms, launcher, pd, ew, sig }, params: { disp, cells, canisters, wing }, load, ...extra,
});
const V = (key: string, name: string, chassis: string, role: string, radar: string, pd: string, launchers: number, load: [string, number][] = [], extra: Partial<DesignSeed> = {}): DesignSeed => ({
  key, cat: "vehicle", name, slots: { chassis, role, radar, pd }, params: { launchers }, load, ...extra,
});
const Dr = (key: string, name: string, type: string, engine: string, guide: string, sig: string, weight: number, warhead: number, sqn: number, load: [string, number][] = []): DesignSeed => ({
  key, cat: "drone", name, slots: { type, engine, guide, sig }, params: { weight, warhead, sqn }, load,
});
const B = (key: string, name: string, type: string, hard: string, radar: string, pd: string, capacity: number, pads: number): DesignSeed => ({
  key, cat: "base", name, slots: { type, hard, radar, pd }, params: { capacity, pads },
});

export const STARTS: Record<NationId, StartSeed> = {
  // ================================================================ USA
  USA: {
    designs: [
      M("harpoon", "RGM-84 Harpoon", "ashm", "turbojet", "arh", "skim", "none", 690, 220),
      M("tlam", "BGM-109 Tomahawk", "lacm", "turbofan", "ins", "skim", "none", 1450, 450),
      M("sm1", "RIM-66 SM-1MR", "sam", "solid", "sarh", "conv", "none", 620, 62),
      M("sm2", "RIM-66 SM-2MR", "sam", "solid", "sarh", "conv", "oneway", 700, 62),
      M("rim7", "RIM-7 Sea Sparrow", "sam", "solid", "sarh", "conv", "none", 230, 40),
      M("aim7", "AIM-7M Sparrow", "aam", "solid", "sarh", "conv", "none", 230, 40),
      M("aim54", "AIM-54C Phoenix", "aam", "solid", "arh", "conv", "none", 460, 60),
      M("aim120", "AIM-120A AMRAAM", "aam", "solid", "arh", "conv", "oneway", 152, 18, { status: "developing", devLeft: 14 }),
      M("patriot", "MIM-104 Patriot PAC-2", "sam", "solid", "sarh", "conv", "oneway", 900, 90),
      M("harm", "AGM-88 HARM", "arm", "solid", "rf", "conv", "none", 360, 66),
      M("mk48", "Mk 48 ADCAP", "torp", "torpedo", "sonar", "conv", "none", 1660, 290),
      A("f14", "F-14D Tomcat", "g4", "as", "tf", "pd", "catobar", "jam", 3000, 12, [["aim54", 4], ["aim7", 2]]),
      A("f18", "F/A-18C Hornet", "g4", "mr", "tf", "pd", "catobar", "jam", 3000, 12, [["aim7", 2], ["harpoon", 2]]),
      A("e2c", "E-2C Hawkeye", "large", "aew", "tf", "dome", "catobar", "none", 0, 4),
      A("f15c", "F-15C Eagle", "g4", "as", "tf", "pd", "land", "jam", 3000, 18, [["aim7", 6]]),
      A("f16c", "F-16C Fighting Falcon", "g4", "mr", "tf", "pd", "land", "jam", 3000, 18, [["aim7", 2], ["harm", 2]]),
      A("b52", "B-52H Stratofortress", "bomber", "st", "tj", "pd", "land", "jam", 20000, 12, [["tlam", 12]]),
      A("e3", "E-3 Sentry", "large", "aew", "tf", "dome", "land", "jam", 0, 4),
      A("p3", "P-3C Orion", "large", "mpa", "tf", "pd", "land", "none", 2000, 8, [["harpoon", 4]]),
      S("cvn", "Nimitz-class carrier", "cvn", "nuke", "r3d", "ntds", "rail", "gun", "jam", "conv", 100000, 16, 0, 70, [["rim7", 16]]),
      S("cg47", "Ticonderoga-class cruiser", "cg", "cogag", "pesa", "aegis", "vlssl", "gun", "jam", "conv", 9800, 122, 8, 2, [["sm2", 80], ["tlam", 26], ["harpoon", 8]]),
      S("ddg51", "Arleigh Burke-class destroyer", "ddg", "cogag", "pesa", "aegis", "vlssl", "gun", "jam", "conv", 8300, 90, 8, 0, [["sm2", 56], ["tlam", 30], ["harpoon", 8]]),
      S("ffg7", "Oliver Hazard Perry-class frigate", "ffg", "cogag", "r3d", "ntds", "rail", "gun", "jam", "conv", 4100, 40, 0, 2, [["sm1", 36], ["harpoon", 4]]),
      S("ssn688", "Los Angeles-class submarine", "ssn", "nuke", "r2d", "ntds", "tubes", "none", "chaff", "conv", 6900, 26, 0, 0, [["mk48", 14], ["tlam", 8], ["harpoon", 4]]),
      V("patbat", "Patriot battery", "truck8", "sam", "pesa", "none", 8, [["patriot", 32]]),
      Dr("pioneer", "RQ-2 Pioneer", "recon", "piston", "eo", "conv", 200, 0, 6),
      B("usair", "USAF main operating base", "air", "has", "ew", "gun", 40, 0),
      B("usnav", "US Navy fleet base", "naval", "rev", "ew", "gun", 60, 0),
    ],
    stock: [["harpoon", 900], ["tlam", 700], ["sm1", 700], ["sm2", 2000], ["rim7", 700], ["aim7", 2500], ["aim54", 700], ["patriot", 500], ["harm", 800], ["mk48", 900]],
    forces: [
      { key: "cvn", n: 1, at: "sea:ARAB", wing: [["f14", 1], ["f18", 2], ["e2c", 1]] },
      { key: "cvn", n: 1, at: "sea:MED", wing: [["f14", 1], ["f18", 2], ["e2c", 1]] },
      { key: "cvn", n: 1, at: "sea:WPAC", wing: [["f14", 1], ["f18", 2], ["e2c", 1]] },
      { key: "cvn", n: 3, at: "us-norfolk", wing: [["f14", 1], ["f18", 2], ["e2c", 1]] },
      { key: "cvn", n: 2, at: "us-sandiego", wing: [["f14", 1], ["f18", 2], ["e2c", 1]] },
      { key: "cvn", n: 1, at: "us-yokosuka", wing: [["f14", 1], ["f18", 2], ["e2c", 1]] },
      { key: "cg47", n: 2, at: "sea:ARAB" }, { key: "cg47", n: 2, at: "sea:MED" }, { key: "cg47", n: 2, at: "sea:WPAC" },
      { key: "cg47", n: 5, at: "us-norfolk" }, { key: "cg47", n: 4, at: "us-sandiego" }, { key: "cg47", n: 1, at: "us-yokosuka" },
      { key: "ddg51", n: 1, at: "us-norfolk" },
      { key: "ffg7", n: 1, at: "sea:ARAB" }, { key: "ffg7", n: 1, at: "sea:MED" }, { key: "ffg7", n: 1, at: "sea:WPAC" },
      { key: "ffg7", n: 2, at: "us-bahrain" }, { key: "ffg7", n: 4, at: "us-norfolk" }, { key: "ffg7", n: 3, at: "us-sandiego" }, { key: "ffg7", n: 2, at: "us-rota" },
      { key: "ssn688", n: 2, at: "sea:NATL" }, { key: "ssn688", n: 1, at: "sea:MED" }, { key: "ssn688", n: 2, at: "sea:WPAC" },
      { key: "ssn688", n: 6, at: "us-norfolk" }, { key: "ssn688", n: 5, at: "us-sandiego" },
      { key: "f15c", n: 2, at: "us-lakenheath" }, { key: "f15c", n: 3, at: "us-kadena" },
      { key: "f16c", n: 2, at: "us-lakenheath" }, { key: "f16c", n: 2, at: "us-kadena" }, { key: "f16c", n: 1, at: "us-guam" },
      { key: "b52", n: 2, at: "us-guam" }, { key: "b52", n: 2, at: "us-diego" },
      { key: "e3", n: 1, at: "us-kadena" }, { key: "e3", n: 1, at: "us-lakenheath" }, { key: "e3", n: 1, at: "us-diego" },
      { key: "p3", n: 1, at: "us-rota" }, { key: "p3", n: 1, at: "us-bahrain" }, { key: "p3", n: 1, at: "us-kadena" }, { key: "p3", n: 1, at: "us-diego" },
      { key: "patbat", n: 2, at: "us-kadena" }, { key: "patbat", n: 2, at: "us-bahrain" }, { key: "patbat", n: 1, at: "us-lakenheath" },
      { key: "pioneer", n: 1, at: "us-bahrain" },
    ],
  },

  // ================================================================ RUSSIA
  RUS: {
    designs: [
      M("moskit", "P-270 Moskit", "ashm", "ramjet", "arh", "weave", "none", 3950, 320),
      M("granit", "P-700 Granit", "ashm", "ramjet", "arh", "weave", "oneway", 7000, 750),
      M("kh22", "Kh-22 Burya", "ashm", "solid", "arh", "conv", "none", 5800, 960),
      M("kh55", "Kh-55 Granat", "lacm", "turbofan", "ins", "skim", "none", 1500, 410),
      M("s300", "48N6 (S-300)", "sam", "solid", "sarh", "conv", "oneway", 1000, 143),
      M("shtil", "9M38 Shtil", "sam", "solid", "sarh", "conv", "none", 690, 70),
      M("osa", "9M33 Osa-M", "sam", "solid", "sarh", "conv", "none", 128, 15),
      M("r27", "R-27R", "aam", "solid", "sarh", "conv", "none", 253, 39),
      M("r73", "R-73", "aam", "solid", "ir", "conv", "none", 105, 7),
      M("scud", "R-17 Elbrus (Scud-B)", "srbm", "liquid", "ins", "conv", "none", 5900, 985),
      M("tochka", "OTR-21 Tochka-U", "srbm", "solidb", "ins", "conv", "none", 2000, 480),
      M("usetT", "USET-80 torpedo", "torp", "torpedo", "sonar", "conv", "none", 1650, 300),
      A("su27", "Su-27S Flanker", "g4", "as", "tf", "pd", "land", "jam", 3000, 18, [["r27", 4], ["r73", 4]]),
      A("su27k", "Su-33 Flanker-D", "g4", "as", "tf", "pd", "stobar", "jam", 3000, 12, [["r27", 4], ["r73", 4]]),
      A("mig29", "MiG-29 Fulcrum", "g4", "mr", "tf", "pd", "land", "jam", 2500, 18, [["r27", 2], ["r73", 4]]),
      A("tu22m", "Tu-22M3 Backfire", "bomber", "st", "tf", "pd", "land", "jam", 12000, 12, [["kh22", 3]]),
      A("tu95", "Tu-95MS Bear", "bomber", "st", "tj", "pd", "land", "jam", 12000, 12, [["kh55", 6]]),
      A("a50", "A-50 Mainstay", "large", "aew", "tf", "dome", "land", "jam", 0, 4),
      S("kuz", "Admiral Kuznetsov-class carrier", "cvl", "steam", "r3d", "ntds", "heavy", "gun", "jam", "conv", 58500, 12, 0, 24, [["granit", 12]]),
      S("slava", "Slava-class cruiser", "cg", "cogag", "r3d", "ntds", "heavy", "gun", "jam", "conv", 11500, 80, 0, 1, [["moskit", 16], ["s300", 64]]),
      S("sovr", "Sovremenny-class destroyer", "ddg", "steam", "r3d", "ntds", "heavy", "gun", "jam", "conv", 7900, 56, 0, 1, [["moskit", 8], ["shtil", 48]]),
      S("krivak", "Krivak-class frigate", "ffg", "cogag", "r3d", "ntds", "rail", "gun", "jam", "conv", 3500, 40, 0, 0, [["osa", 40]]),
      S("tarantul", "Tarantul III corvette", "fac", "cogag", "r2d", "man", "heavy", "gun", "chaff", "conv", 455, 4, 0, 0, [["moskit", 4]]),
      S("oscar", "Oscar II-class submarine", "ssn", "nuke", "r2d", "ntds", "heavy", "none", "chaff", "conv", 24000, 32, 0, 0, [["granit", 24], ["usetT", 8]]),
      S("akula", "Akula-class submarine", "ssn", "nuke", "r2d", "ntds", "tubes", "none", "chaff", "conv", 12700, 28, 0, 0, [["usetT", 20], ["kh55", 8]]),
      S("kilo", "Kilo-class submarine", "ssk", "diesel", "r2d", "man", "tubes", "none", "chaff", "conv", 3100, 18, 0, 0, [["usetT", 18]]),
      V("s300bat", "S-300PMU battalion", "truck8", "sam", "pesa", "none", 8, [["s300", 32]]),
      V("scudbat", "Scud-B brigade", "mzkt", "tel", "none", "none", 4, [["scud", 4]]),
      V("tochkabat", "Tochka-U battalion", "mzkt", "tel", "none", "none", 4, [["tochka", 4]]),
      V("rubezh", "Rubezh coastal battery", "mzkt", "shore", "pd", "none", 4, [["moskit", 4]]),
      B("ruair", "VVS air base", "air", "has", "ew", "gun", 40, 0),
      B("runav", "VMF fleet base", "naval", "rev", "ew", "gun", 60, 0),
      B("rumis", "Missile brigade garrison", "missile", "has", "none", "gun", 20, 0),
    ],
    stock: [["moskit", 320], ["granit", 220], ["kh22", 240], ["kh55", 300], ["s300", 1400], ["shtil", 600], ["osa", 900], ["r27", 2400], ["r73", 2600], ["scud", 240], ["tochka", 160], ["usetT", 700]],
    forces: [
      { key: "kuz", n: 1, at: "ru-severomorsk", wing: [["su27k", 2]] },
      { key: "slava", n: 1, at: "ru-severomorsk" }, { key: "slava", n: 1, at: "ru-sevastopol" }, { key: "slava", n: 1, at: "ru-vladivostok" },
      { key: "sovr", n: 3, at: "ru-severomorsk" }, { key: "sovr", n: 2, at: "ru-baltiysk" }, { key: "sovr", n: 3, at: "ru-vladivostok" },
      { key: "krivak", n: 3, at: "ru-baltiysk" }, { key: "krivak", n: 3, at: "ru-sevastopol" }, { key: "krivak", n: 2, at: "ru-severomorsk" },
      { key: "tarantul", n: 3, at: "ru-baltiysk" }, { key: "tarantul", n: 3, at: "ru-sevastopol" }, { key: "tarantul", n: 3, at: "ru-vladivostok" },
      { key: "oscar", n: 3, at: "ru-severomorsk" }, { key: "oscar", n: 1, at: "sea:NATL" }, { key: "oscar", n: 2, at: "ru-vladivostok" },
      { key: "akula", n: 3, at: "ru-severomorsk" }, { key: "akula", n: 2, at: "ru-vladivostok" },
      { key: "kilo", n: 2, at: "ru-baltiysk" }, { key: "kilo", n: 2, at: "ru-sevastopol" }, { key: "kilo", n: 3, at: "ru-vladivostok" },
      { key: "su27", n: 2, at: "ru-olenya" }, { key: "su27", n: 2, at: "ru-kaliningrad" }, { key: "su27", n: 2, at: "ru-vladivostok" }, { key: "su27", n: 1, at: "ru-sevastopol" },
      { key: "mig29", n: 2, at: "ru-kaliningrad" }, { key: "mig29", n: 2, at: "ru-sevastopol" }, { key: "mig29", n: 1, at: "ru-engels" },
      { key: "tu22m", n: 2, at: "ru-olenya" }, { key: "tu22m", n: 1, at: "ru-engels" },
      { key: "tu95", n: 2, at: "ru-engels" },
      { key: "a50", n: 1, at: "ru-olenya" }, { key: "a50", n: 1, at: "ru-engels" },
      { key: "s300bat", n: 2, at: "ru-kaliningrad" }, { key: "s300bat", n: 2, at: "ru-severomorsk" }, { key: "s300bat", n: 2, at: "ru-sevastopol" }, { key: "s300bat", n: 2, at: "ru-vladivostok" }, { key: "s300bat", n: 1, at: "ru-engels" },
      { key: "scudbat", n: 2, at: "ru-kaliningrad" }, { key: "tochkabat", n: 2, at: "ru-kaliningrad" }, { key: "tochkabat", n: 1, at: "ru-sevastopol" },
      { key: "rubezh", n: 1, at: "ru-sevastopol" }, { key: "rubezh", n: 1, at: "ru-baltiysk" },
    ],
  },

  // ================================================================ CHINA
  CHN: {
    designs: [
      M("c801", "YJ-8 (C-801)", "ashm", "solid", "arh", "skim", "none", 815, 165),
      M("hy2", "HY-2 Silkworm", "ashm", "solid", "arh", "skim", "none", 2900, 513),
      M("c601", "YJ-6 (C-601)", "ashm", "solid", "arh", "conv", "none", 2440, 513),
      M("hq61", "HQ-61", "sam", "solid", "sarh", "conv", "none", 300, 40),
      M("hq2", "HQ-2", "sam", "solid", "sarh", "conv", "none", 700, 190),
      M("pl5", "PL-5", "aam", "solid", "ir", "conv", "none", 150, 12),
      M("df15", "DF-15", "srbm", "solidb", "ins", "conv", "none", 6200, 500),
      M("df21", "DF-21", "srbm", "solidb", "ins", "conv", "none", 14700, 600),
      M("yu4", "Yu-4 torpedo", "torp", "torpedo", "sonar", "conv", "none", 1650, 220),
      A("j7", "J-7 Fishbed", "g3", "as", "tj", "pd", "land", "none", 1000, 18, [["pl5", 4]]),
      A("j8", "J-8II Finback", "g3", "mr", "tj", "pd", "land", "none", 2000, 18, [["pl5", 4], ["c801", 2]]),
      A("h6", "H-6D Badger", "bomber", "st", "tj", "pd", "land", "none", 10000, 12, [["c601", 2]]),
      S("luda", "Type 051 Luda destroyer", "ffg", "steam", "r2d", "man", "heavy", "gun", "chaff", "conv", 3700, 6, 0, 0, [["hy2", 6]]),
      S("jiangwei", "Type 053H2G Jiangwei frigate", "cor", "diesel", "r3d", "man", "rail", "none", "chaff", "conv", 2250, 14, 0, 2, [["c801", 6], ["hq61", 8]]),
      S("t021", "Type 021 missile boat", "fac", "diesel", "r2d", "man", "rail", "none", "chaff", "conv", 205, 4, 0, 0, [["c801", 4]]),
      S("han", "Type 091 Han submarine", "ssn", "nuke", "r2d", "man", "tubes", "none", "chaff", "conv", 5500, 20, 0, 0, [["yu4", 20]]),
      S("ming", "Type 035 Ming submarine", "ssk", "diesel", "r2d", "man", "tubes", "none", "chaff", "conv", 2100, 14, 0, 0, [["yu4", 14]]),
      V("hq2bat", "HQ-2 battalion", "truck8", "sam", "pd", "none", 6, [["hq2", 24]]),
      V("df15bat", "DF-15 brigade", "mzkt", "tel", "none", "none", 6, [["df15", 6]]),
      V("df21bat", "DF-21 brigade", "mzkt", "tel", "none", "none", 4, [["df21", 4]]),
      V("hy2bat", "HY-2 coastal battery", "mzkt", "shore", "pd", "none", 4, [["hy2", 4]]),
      B("cnair", "PLAAF air base", "air", "rev", "ew", "gun", 40, 0),
      B("cnnav", "PLAN fleet base", "naval", "rev", "ew", "gun", 60, 0),
      B("cnmis", "Rocket Force base", "missile", "has", "none", "none", 30, 0),
    ],
    stock: [["c801", 500], ["hy2", 260], ["c601", 120], ["hq61", 300], ["hq2", 700], ["pl5", 1500], ["df15", 60], ["df21", 40], ["yu4", 500]],
    forces: [
      { key: "luda", n: 4, at: "cn-qingdao" }, { key: "luda", n: 4, at: "cn-ningbo" }, { key: "luda", n: 4, at: "cn-zhanjiang" },
      { key: "jiangwei", n: 2, at: "cn-ningbo" }, { key: "jiangwei", n: 2, at: "cn-zhanjiang" },
      { key: "t021", n: 4, at: "cn-qingdao" }, { key: "t021", n: 4, at: "cn-ningbo" }, { key: "t021", n: 4, at: "cn-yulin" },
      { key: "han", n: 4, at: "cn-qingdao" },
      { key: "ming", n: 4, at: "cn-ningbo" }, { key: "ming", n: 4, at: "cn-yulin" },
      { key: "j7", n: 3, at: "cn-longtian" }, { key: "j7", n: 3, at: "cn-lingshui" },
      { key: "j8", n: 3, at: "cn-longtian" }, { key: "j8", n: 2, at: "cn-lingshui" },
      { key: "h6", n: 2, at: "cn-longtian" }, { key: "h6", n: 1, at: "cn-lingshui" },
      { key: "hq2bat", n: 2, at: "cn-longtian" }, { key: "hq2bat", n: 2, at: "cn-lingshui" }, { key: "hq2bat", n: 2, at: "cn-qingdao" },
      { key: "df15bat", n: 1, at: "cn-huangshan" }, { key: "df21bat", n: 1, at: "cn-huangshan" }, { key: "df21bat", n: 1, at: "cn-shaoguan" },
      { key: "hy2bat", n: 2, at: "cn-ningbo" }, { key: "hy2bat", n: 2, at: "cn-yulin" },
    ],
  },

  // ================================================================ FRANCE
  FRA: {
    designs: [
      M("mm40", "MM40 Exocet", "ashm", "solid", "arh", "skim", "none", 855, 165),
      M("sm1f", "SM-1MR (Marine nationale)", "sam", "solid", "sarh", "conv", "none", 620, 62),
      M("crotale", "Crotale Naval", "sam", "solid", "sarh", "conv", "none", 85, 14),
      M("magic2", "R.550 Magic 2", "aam", "solid", "ir", "conv", "none", 89, 13),
      M("s530", "Super 530D", "aam", "solid", "sarh", "conv", "none", 270, 30),
      M("f17", "F17 torpedo", "torp", "torpedo", "sonar", "conv", "none", 1400, 250),
      A("m2000", "Mirage 2000-5", "g4", "as", "tf", "pd", "land", "jam", 2500, 18, [["s530", 2], ["magic2", 4]]),
      A("sue", "Super Étendard", "g3", "st", "tj", "pd", "catobar", "jam", 2000, 12, [["mm40", 2], ["magic2", 2]]),
      A("rafm", "Rafale M", "g45", "mr", "tf", "pesa", "catobar", "jam", 6000, 12, [["s530", 4], ["magic2", 4], ["mm40", 2]], { status: "draft" }),
      A("atl", "Atlantique 2", "large", "mpa", "tf", "pd", "land", "none", 3000, 6, [["mm40", 2]]),
      S("clem", "Clemenceau-class carrier", "cvn", "steam", "r3d", "ntds", "rail", "gun", "jam", "conv", 32700, 0, 0, 26),
      S("cassard", "Cassard-class frigate", "ffg", "cogag", "r3d", "ntds", "rail", "gun", "jam", "conv", 4700, 40, 8, 1, [["sm1f", 40], ["mm40", 8]]),
      S("leygues", "Georges Leygues-class frigate", "ffg", "cogag", "r3d", "ntds", "rail", "gun", "jam", "conv", 4500, 26, 8, 2, [["crotale", 26], ["mm40", 8]]),
      S("rubis", "Rubis-class submarine", "ssn", "nuke", "r2d", "man", "tubes", "none", "chaff", "conv", 2600, 14, 0, 0, [["f17", 10], ["mm40", 4]]),
      V("crotbat", "Crotale battery", "truck6", "sam", "pd", "none", 4, [["crotale", 32]]),
      B("frair", "Armée de l'air base", "air", "has", "ew", "gun", 40, 0),
      B("frnav", "Marine nationale base", "naval", "rev", "ew", "gun", 50, 0),
    ],
    stock: [["mm40", 380], ["sm1f", 180], ["crotale", 500], ["magic2", 900], ["s530", 500], ["f17", 200]],
    forces: [
      { key: "clem", n: 1, at: "sea:MED", wing: [["sue", 2]] },
      { key: "clem", n: 1, at: "fr-toulon", wing: [["sue", 2]] },
      { key: "cassard", n: 1, at: "sea:MED" }, { key: "cassard", n: 1, at: "fr-toulon" },
      { key: "leygues", n: 1, at: "sea:MED" }, { key: "leygues", n: 3, at: "fr-toulon" }, { key: "leygues", n: 3, at: "fr-brest" },
      { key: "rubis", n: 3, at: "fr-toulon" }, { key: "rubis", n: 2, at: "fr-brest" },
      { key: "m2000", n: 3, at: "fr-stdizier" }, { key: "m2000", n: 2, at: "fr-istres" }, { key: "m2000", n: 1, at: "fr-djibouti" },
      { key: "atl", n: 2, at: "fr-brest" }, { key: "atl", n: 1, at: "fr-djibouti" },
      { key: "crotbat", n: 2, at: "fr-toulon" }, { key: "crotbat", n: 1, at: "fr-djibouti" },
    ],
  },

  // ================================================================ UNITED KINGDOM
  GBR: {
    designs: [
      M("harpoonuk", "RGM-84 Harpoon (RN)", "ashm", "turbojet", "arh", "skim", "none", 690, 220),
      M("seaeagle", "Sea Eagle", "ashm", "turbojet", "arh", "skim", "none", 580, 230),
      M("seadart", "Sea Dart", "sam", "solid", "sarh", "conv", "none", 550, 22),
      M("seawolf", "Sea Wolf", "sam", "solid", "sarh", "conv", "none", 82, 14),
      M("rapier", "Rapier", "sam", "solid", "sarh", "conv", "none", 45, 2),
      M("skyflash", "Skyflash", "aam", "solid", "sarh", "conv", "none", 193, 30),
      M("aim9uk", "AIM-9L Sidewinder", "aam", "solid", "ir", "conv", "none", 85, 9),
      M("spearfish", "Spearfish torpedo", "torp", "torpedo", "sonar", "conv", "none", 1650, 300),
      A("tf3", "Tornado F3", "g4", "as", "tf", "pd", "land", "jam", 2500, 16, [["skyflash", 4], ["aim9uk", 4]]),
      A("tgr1", "Tornado GR1B", "g4", "st", "tf", "pd", "land", "jam", 3000, 16, [["seaeagle", 2], ["aim9uk", 2]]),
      A("shar", "Sea Harrier FA2", "g3", "mr", "tj", "pd", "stovl", "jam", 1500, 8, [["aim9uk", 2], ["seaeagle", 2]]),
      A("nimrod", "Nimrod MR2", "large", "mpa", "tf", "pd", "land", "none", 2500, 6, [["harpoonuk", 4]]),
      A("sentry", "Sentry AEW1", "large", "aew", "tf", "dome", "land", "jam", 0, 4),
      S("invincible", "Invincible-class carrier", "cvl", "cogag", "r3d", "ntds", "rail", "gun", "jam", "conv", 20600, 22, 0, 12, [["seadart", 22]]),
      S("t42", "Type 42 destroyer", "ffg", "cogag", "r3d", "ntds", "rail", "gun", "jam", "conv", 4800, 22, 0, 1, [["seadart", 22]]),
      S("t22", "Type 22 frigate", "ffg", "cogag", "r3d", "ntds", "rail", "gun", "jam", "conv", 4800, 32, 4, 1, [["seawolf", 32], ["harpoonuk", 4]]),
      S("t23", "Type 23 frigate", "ffg", "cogag", "r3d", "ntds", "vls", "none", "jam", "conv", 4900, 32, 8, 1, [["seawolf", 32], ["harpoonuk", 8]]),
      S("traf", "Trafalgar-class submarine", "ssn", "nuke", "r2d", "ntds", "tubes", "none", "chaff", "conv", 5200, 25, 0, 0, [["spearfish", 20], ["harpoonuk", 5]]),
      V("rapbat", "Rapier battery", "truck6", "sam", "pd", "none", 4, [["rapier", 32]]),
      B("gbair", "RAF station", "air", "has", "ew", "gun", 40, 0),
      B("gbnav", "HM Naval Base", "naval", "rev", "ew", "gun", 50, 0),
      B("gbrad", "BMEWS radar station", "radar", "rev", "pesa", "none", 10, 0),
    ],
    stock: [["harpoonuk", 300], ["seaeagle", 200], ["seadart", 450], ["seawolf", 700], ["rapier", 600], ["skyflash", 500], ["aim9uk", 900], ["spearfish", 300]],
    forces: [
      { key: "invincible", n: 1, at: "sea:NATL", wing: [["shar", 1]] },
      { key: "invincible", n: 1, at: "gb-portsmouth", wing: [["shar", 1]] },
      { key: "t42", n: 1, at: "sea:NATL" }, { key: "t42", n: 4, at: "gb-portsmouth" }, { key: "t42", n: 1, at: "gb-bahrain" },
      { key: "t22", n: 1, at: "sea:NATL" }, { key: "t22", n: 3, at: "gb-portsmouth" }, { key: "t22", n: 1, at: "gb-bahrain" },
      { key: "t23", n: 1, at: "gb-portsmouth" },
      { key: "traf", n: 1, at: "sea:NATL" }, { key: "traf", n: 4, at: "gb-faslane" },
      { key: "tf3", n: 2, at: "gb-lossiemouth" }, { key: "tf3", n: 1, at: "gb-akrotiri" },
      { key: "tgr1", n: 2, at: "gb-lossiemouth" }, { key: "tgr1", n: 1, at: "gb-akrotiri" },
      { key: "nimrod", n: 2, at: "gb-lossiemouth" },
      { key: "sentry", n: 1, at: "gb-lossiemouth" },
      { key: "rapbat", n: 1, at: "gb-akrotiri" }, { key: "rapbat", n: 1, at: "gb-lossiemouth" },
    ],
  },

  // ================================================================ ISRAEL
  ISR: {
    designs: [
      M("gabriel", "Gabriel II", "ashm", "solid", "arh", "skim", "none", 522, 180),
      M("harpoonil", "RGM-84 Harpoon (IN)", "ashm", "turbojet", "arh", "skim", "none", 690, 220),
      M("barak", "Barak-1", "sam", "solid", "sarh", "conv", "none", 98, 22, { status: "developing", devLeft: 20 }),
      M("hawkil", "MIM-23 Hawk", "sam", "solid", "sarh", "conv", "none", 590, 54),
      M("python3", "Python 3", "aam", "solid", "ir", "conv", "none", 120, 11),
      M("aim7il", "AIM-7M Sparrow (IAF)", "aam", "solid", "sarh", "conv", "none", 230, 40),
      M("nt37", "NT-37 torpedo", "torp", "torpedo", "sonar", "conv", "none", 750, 150),
      A("f15i", "F-15C Baz", "g4", "as", "tf", "pd", "land", "jam", 3000, 24, [["aim7il", 4], ["python3", 4]]),
      A("f16i", "F-16C Barak", "g4", "mr", "tf", "pd", "land", "jam", 3000, 24, [["python3", 2], ["aim7il", 2], ["harpoonil", 2]]),
      A("e2il", "E-2C Daya", "large", "aew", "tf", "dome", "land", "jam", 0, 4),
      S("saar45", "Sa'ar 4.5 missile boat", "fac", "diesel", "r2d", "ntds", "rail", "gun", "jam", "conv", 488, 8, 0, 0, [["harpoonil", 4], ["gabriel", 4]]),
      S("saar5", "Sa'ar 5 corvette", "cor", "cogag", "r3d", "ntds", "vls", "gun", "jam", "conv", 1250, 16, 0, 1, [["barak", 32], ["harpoonil", 8]], { status: "developing", devLeft: 30 }),
      S("gal", "Gal-class submarine", "ssk", "diesel", "r2d", "man", "tubes", "none", "chaff", "conv", 600, 10, 0, 0, [["nt37", 10]]),
      V("hawkbat", "Hawk battery", "truck6", "sam", "pd", "none", 6, [["hawkil", 24]]),
      V("harpybat", "Harpy launcher battery", "truck6", "drone", "none", "none", 3, [["harpy", 36]]),
      Dr("harpy", "IAI Harpy", "owa", "piston", "arm", "conv", 135, 32, 1),
      Dr("scout", "IAI Scout", "recon", "piston", "eo", "conv", 160, 0, 6),
      B("ilair", "IAF air base", "air", "has", "ew", "gun", 40, 0),
      B("ilnav", "Navy base", "naval", "rev", "ew", "gun", 30, 0),
      B("ilmis", "Palmachim test & launch site", "missile", "has", "ew", "none", 20, 0),
    ],
    stock: [["gabriel", 120], ["harpoonil", 140], ["hawkil", 400], ["python3", 700], ["aim7il", 500], ["nt37", 60], ["harpy", 150]],
    forces: [
      { key: "f15i", n: 1, at: "il-hatzerim" }, { key: "f15i", n: 1, at: "il-ramatdavid" },
      { key: "f16i", n: 2, at: "il-nevatim" }, { key: "f16i", n: 2, at: "il-ramatdavid" }, { key: "f16i", n: 1, at: "il-hatzerim" },
      { key: "e2il", n: 1, at: "il-hatzerim" },
      { key: "saar45", n: 5, at: "il-haifa" }, { key: "saar45", n: 2, at: "il-eilat" },
      { key: "gal", n: 3, at: "il-haifa" },
      { key: "hawkbat", n: 2, at: "il-nevatim" }, { key: "hawkbat", n: 1, at: "il-haifa" }, { key: "hawkbat", n: 1, at: "il-hatzerim" },
      { key: "harpybat", n: 2, at: "il-palmachim" },
      { key: "scout", n: 1, at: "il-palmachim" },
    ],
  },

  // ================================================================ IRAN
  IRN: {
    designs: [
      M("harpoonir", "RGM-84 Harpoon (IRIN stock)", "ashm", "turbojet", "arh", "skim", "none", 690, 220),
      M("silkworm", "HY-2 Silkworm (Seersucker)", "ashm", "solid", "arh", "skim", "none", 2900, 513),
      M("hawkir", "MIM-23 Hawk (IRIADF)", "sam", "solid", "sarh", "conv", "none", 590, 54),
      M("aim54ir", "AIM-54A Phoenix", "aam", "solid", "arh", "conv", "none", 460, 60),
      M("aim7ir", "AIM-7E Sparrow", "aam", "solid", "sarh", "conv", "none", 230, 40),
      M("shahab1", "Shahab-1 (Scud-B)", "srbm", "liquid", "ins", "conv", "none", 5900, 985),
      M("t53", "53-65 torpedo", "torp", "torpedo", "sonar", "conv", "none", 1650, 300),
      A("f14ir", "F-14A Tomcat (IRIAF)", "g4", "as", "tf", "pd", "land", "jam", 3000, 12, [["aim54ir", 4], ["aim7ir", 2]]),
      A("f4e", "F-4E Phantom II", "g3", "mr", "tj", "pd", "land", "jam", 3000, 12, [["aim7ir", 2], ["harpoonir", 2]]),
      S("alvand", "Alvand-class frigate", "cor", "cogag", "r2d", "man", "rail", "none", "jam", "conv", 1540, 5, 0, 0, [["harpoonir", 4]]),
      S("kaman", "Kaman-class missile boat", "fac", "diesel", "r2d", "man", "rail", "none", "chaff", "conv", 275, 4, 0, 0, [["harpoonir", 4]]),
      S("tareq", "Tareq (Kilo)-class submarine", "ssk", "diesel", "r2d", "man", "tubes", "none", "chaff", "conv", 3100, 18, 0, 0, [["t53", 18]]),
      V("hawkirbat", "Hawk battalion", "fixed", "sam", "pd", "none", 6, [["hawkir", 18]]),
      V("shahabbat", "Shahab-1 brigade", "mzkt", "tel", "none", "none", 4, [["shahab1", 4]]),
      V("silkbat", "Silkworm coastal battery", "mzkt", "shore", "pd", "none", 4, [["silkworm", 4]]),
      Dr("mohajer", "Mohajer-2", "recon", "piston", "eo", "conv", 85, 0, 6),
      B("irair", "IRIAF tactical fighter base", "air", "rev", "ew", "none", 30, 0),
      B("irnav", "IRIN naval base", "naval", "rev", "ew", "none", 40, 0),
      B("irmis", "IRGC missile base", "missile", "has", "none", "none", 30, 0),
    ],
    stock: [["harpoonir", 60], ["silkworm", 120], ["hawkir", 300], ["aim54ir", 60], ["aim7ir", 200], ["shahab1", 180], ["t53", 0]],
    forces: [
      { key: "f14ir", n: 1, at: "ir-isfahan" }, { key: "f14ir", n: 1, at: "ir-bushehr" },
      { key: "f4e", n: 2, at: "ir-bushehr" }, { key: "f4e", n: 1, at: "ir-isfahan" },
      { key: "alvand", n: 2, at: "ir-bandarabbas" }, { key: "alvand", n: 1, at: "ir-jask" },
      { key: "kaman", n: 4, at: "ir-bandarabbas" }, { key: "kaman", n: 3, at: "ir-bushehr" }, { key: "kaman", n: 2, at: "ir-chabahar" },
      { key: "hawkirbat", n: 2, at: "ir-bushehr" }, { key: "hawkirbat", n: 2, at: "ir-bandarabbas" }, { key: "hawkirbat", n: 2, at: "ir-isfahan" },
      { key: "shahabbat", n: 2, at: "ir-kermanshah" }, { key: "shahabbat", n: 1, at: "ir-tabriz" },
      { key: "silkbat", n: 3, at: "ir-bandarabbas" }, { key: "silkbat", n: 1, at: "ir-jask" },
      { key: "mohajer", n: 1, at: "ir-bandarabbas" },
    ],
  },
};

/** Which starting base design each base type uses, per nation. */
export const BASE_DESIGN_KEYS: Record<NationId, Partial<Record<"naval" | "air" | "missile" | "radar", string>>> = {
  USA: { naval: "usnav", air: "usair", missile: "usair", radar: "usair" },
  RUS: { naval: "runav", air: "ruair", missile: "rumis", radar: "ruair" },
  CHN: { naval: "cnnav", air: "cnair", missile: "cnmis", radar: "cnair" },
  FRA: { naval: "frnav", air: "frair", missile: "frair", radar: "frair" },
  GBR: { naval: "gbnav", air: "gbair", missile: "gbair", radar: "gbrad" },
  ISR: { naval: "ilnav", air: "ilair", missile: "ilmis", radar: "ilair" },
  IRN: { naval: "irnav", air: "irair", missile: "irmis", radar: "irair" },
};
