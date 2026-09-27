import type { NationId, Theatre } from "../sim/types";

export interface NationInfo {
  id: NationId;
  name: string;
  adjective: string;
  color: string;
  /** Game budget in $B/yr (navy + air + strategic forces), keyframed and interpolated. */
  budget: [number, number][];
  techLag: number; // years behind the frontier at start
  rdEff: number; // research efficiency multiplier
  extraKnown: string[];
  forbidden: string[]; // techs this nation cannot start with (it can research them later)
  bloc: "west" | "east";
  pitch: string;
  strengths: string[];
  weaknesses: string[];
  names: { ship: string[]; carrier?: string[]; air: string[] };
}

export const NATIONS: Record<NationId, NationInfo> = {
  USA: {
    id: "USA", name: "United States", adjective: "US", color: "#5B9BD5",
    budget: [[1990, 120], [1995, 100], [2000, 105], [2005, 150], [2010, 175], [2015, 160], [2020, 190], [2030, 215], [2050, 250]],
    techLag: 0, rdEff: 1.0, extraKnown: [], forbidden: [], bloc: "west",
    pitch: "The unipolar superpower. Eleven carriers, Aegis and a global base network, but commitments on every ocean.",
    strengths: ["Largest budget", "Carrier air power", "Global bases"], weaknesses: ["Overstretched", "Expensive platforms"],
    names: { carrier: ["Nimitz", "Eisenhower", "Vinson", "Roosevelt", "Lincoln", "Washington", "Stennis", "Truman", "Reagan", "Bush", "Ford", "Kennedy", "Enterprise", "Forrestal", "America"], ship: ["Arleigh Burke", "Barry", "John Paul Jones", "Curtis Wilbur", "Stout", "John S. McCain", "Mitscher", "Laboon", "Russell", "Paul Hamilton", "Ramage", "Fitzgerald", "Stethem", "Carney", "Benfold", "Gonzalez", "Cole", "The Sullivans", "Milius", "Hopper", "Ross", "Mahan", "Decatur", "McFaul", "Donald Cook", "Higgins", "O'Kane", "Porter", "Ticonderoga", "Yorktown", "Vincennes", "Valley Forge", "Thomas S. Gates", "Bunker Hill", "Mobile Bay", "Antietam", "Leyte Gulf", "San Jacinto", "Lake Champlain", "Princeton", "Normandy", "Monterey", "Chancellorsville", "Cowpens", "Gettysburg", "Chosin", "Hue City", "Shiloh", "Anzio", "Vella Gulf", "Port Royal", "Los Angeles", "Baton Rouge", "Philadelphia", "Memphis", "Groton", "Seawolf", "Virginia", "Texas", "Hawaii", "Oliver Hazard Perry", "McInerney", "Wadsworth", "Duncan", "Clark", "Halyburton"], air: ["VF", "VFA", "FW", "BW", "ACW"] },
  },
  RUS: {
    id: "RUS", name: "Russia", adjective: "Russian", color: "#D9534F",
    budget: [[1990, 55], [1992, 22], [1995, 12], [2000, 11], [2008, 25], [2014, 42], [2022, 45], [2035, 48], [2050, 55]],
    techLag: 0, rdEff: 1.15, extraKnown: [], forbidden: [], bloc: "east",
    pitch: "Heir to a Soviet fleet the country can no longer afford. Superb missiles and deep air defences on a collapsing budget.",
    strengths: ["Heavy supersonic missiles", "Long-range SAMs", "Large legacy fleet"], weaknesses: ["Budget collapse after 1991", "Few carriers"],
    names: { carrier: ["Admiral Kuznetsov", "Varyag", "Ulyanovsk", "Admiral Gorshkov", "Tbilisi"], ship: ["Pyotr Velikiy", "Admiral Nakhimov", "Moskva", "Marshal Ustinov", "Varyag", "Sovremenny", "Otlichny", "Bespokoiny", "Nastoychivy", "Burny", "Bystry", "Admiral Chabanenko", "Severomorsk", "Admiral Levchenko", "Admiral Tributs", "Admiral Panteleyev", "Admiral Vinogradov", "Neustrashimy", "Yaroslav Mudry", "Admiral Gorshkov", "Admiral Kasatonov", "Kursk", "Tomsk", "Omsk", "Tigr", "Vepr", "Gepard", "Pantera", "Severodvinsk", "Kazan", "Novosibirsk", "Krasnoyarsk", "Arkhangelsk", "Ladny", "Pytlivy", "Smetlivy", "Steregushchiy", "Soobrazitelny", "Buyan", "Grad Sviyazhsk", "Uglich", "Vyshny Volochyok"], air: ["IAP", "GvIAP", "TBAP", "MRAP", "AWACS"] },
  },
  CHN: {
    id: "CHN", name: "China", adjective: "Chinese", color: "#E0B84A",
    budget: [[1990, 7], [1995, 9], [2000, 14], [2005, 24], [2010, 42], [2015, 70], [2020, 95], [2030, 140], [2040, 165], [2050, 185]],
    techLag: 10, rdEff: 1.2, extraKnown: [], forbidden: ["nav_catobar"], bloc: "east",
    pitch: "A coastal navy of missile boats and old destroyers, with an economy about to grow tenfold. Build the future fast.",
    strengths: ["Explosive budget growth", "Ballistic missile force", "Cheap shipbuilding"], weaknesses: ["Ten years behind in 1990", "No carriers yet"],
    names: { carrier: ["Liaoning", "Shandong", "Fujian", "Sichuan", "Jiangsu", "Guangdong"], ship: ["Nanchang", "Lhasa", "Dalian", "Anshan", "Wuxi", "Yan'an", "Zunyi", "Kunming", "Changsha", "Hefei", "Yinchuan", "Xining", "Urumqi", "Taiyuan", "Guiyang", "Chengdu", "Qiqihar", "Suzhou", "Nanjing", "Zibo", "Luoyang", "Huanggang", "Yangzhou", "Handan", "Hengyang", "Yulin", "Xuzhou", "Zhoushan", "Wuhu", "Harbin", "Qingdao", "Shenzhen", "Guangzhou", "Lanzhou", "Haikou", "Hainan", "Guangxi", "Anhui", "Jiangsu"], air: ["Brigade", "Regiment", "Division"] },
  },
  FRA: {
    id: "FRA", name: "France", adjective: "French", color: "#8C8FE0",
    budget: [[1990, 17], [1998, 14], [2008, 16], [2015, 17], [2025, 22], [2050, 27]],
    techLag: 2, rdEff: 1.05, extraKnown: [], forbidden: [], bloc: "west",
    pitch: "An independent power with its own carrier, its own fighters and its own deterrent. It punches above its weight but every euro counts.",
    strengths: ["Self-reliant industry", "Carrier strike", "Exocet and Rafale"], weaknesses: ["Small numbers", "Tight budget"],
    names: { carrier: ["Clemenceau", "Foch", "Charles de Gaulle", "France Libre", "Richelieu"], ship: ["Forbin", "Chevalier Paul", "Cassard", "Jean Bart", "Aquitaine", "Provence", "Languedoc", "Auvergne", "Bretagne", "Normandie", "Alsace", "Lorraine", "La Fayette", "Surcouf", "Courbet", "Aconit", "Guépratte", "Suffren", "Duguay-Trouin", "Tourville", "De Grasse", "Rubis", "Casabianca", "Améthyste", "Perle", "Mistral", "Tonnerre", "Dixmude", "Amiral Ronarc'h"], air: ["Flottille", "EC", "EDCA"] },
  },
  GBR: {
    id: "GBR", name: "United Kingdom", adjective: "British", color: "#4DB6AC",
    budget: [[1990, 19], [1998, 15], [2008, 17], [2015, 15], [2025, 20], [2050, 24]],
    techLag: 2, rdEff: 1.05, extraKnown: [], forbidden: [], bloc: "west",
    pitch: "The Royal Navy after the Falklands. World-class submarines and escorts, big ambitions and a Treasury that says no.",
    strengths: ["Nuclear submarines", "Air-defence destroyers", "Close US ties"], weaknesses: ["Shrinking fleet", "Budget cuts"],
    names: { carrier: ["Invincible", "Illustrious", "Ark Royal", "Queen Elizabeth", "Prince of Wales", "Hermes", "Eagle"], ship: ["Daring", "Dauntless", "Diamond", "Dragon", "Defender", "Duncan", "Sheffield", "Birmingham", "Newcastle", "Glasgow", "Cardiff", "Coventry", "Exeter", "Southampton", "Nottingham", "Liverpool", "Manchester", "Gloucester", "Edinburgh", "York", "Norfolk", "Argyll", "Lancaster", "Marlborough", "Iron Duke", "Monmouth", "Montrose", "Westminster", "Northumberland", "Richmond", "Somerset", "Sutherland", "Kent", "Portland", "St Albans", "Trafalgar", "Turbulent", "Tireless", "Torbay", "Trenchant", "Talent", "Triumph", "Astute", "Ambush", "Artful", "Audacious", "Anson", "Ocean", "Albion", "Bulwark"], air: ["Sqn", "NAS"] },
  },
  ISR: {
    id: "ISR", name: "Israel", adjective: "Israeli", color: "#7FC8F0",
    budget: [[1990, 4], [2000, 5.5], [2010, 7], [2020, 9], [2030, 11], [2050, 13]],
    techLag: 3, rdEff: 1.45, extraKnown: ["unm_harpy"], forbidden: ["nav_nuke", "nav_catobar"], bloc: "west",
    pitch: "A small state with a world-class air force and the most advanced missile-defence industry anywhere. It can't afford to lose once.",
    strengths: ["Research efficiency", "Drones and missile defence", "Elite air force"], weaknesses: ["Tiny budget", "No strategic depth"],
    names: { ship: ["Eilat", "Lahav", "Hanit", "Magen", "Oz", "Atzmaut", "Nitzachon", "Sa'ar", "Reshef", "Keshet", "Romach", "Kidon", "Tarshish", "Yaffo", "Herev", "Hetz", "Dolphin", "Leviathan", "Tekumah", "Tanin", "Rahav", "Drakon"], air: ["Tayeset"] },
  },
  IRN: {
    id: "IRN", name: "Iran", adjective: "Iranian", color: "#74B86A",
    budget: [[1990, 2.2], [2000, 3], [2008, 5], [2012, 3.8], [2016, 5.2], [2020, 4.5], [2030, 6], [2050, 8]],
    techLag: 15, rdEff: 1.25, extraKnown: ["mis_arh", "unm_uav"], forbidden: ["nav_nuke", "nav_catobar"], bloc: "east",
    pitch: "Under sanctions and outgunned. Win with numbers, missiles, drones and the Strait of Hormuz.",
    strengths: ["Cheap missiles and drones", "Controls Hormuz", "Deep underground bases"], weaknesses: ["Smallest budget", "Obsolete air force", "Sanctions"],
    names: { ship: ["Jamaran", "Sahand", "Damavand", "Alborz", "Sabalan", "Alvand", "Bayandor", "Naghdi", "Dena", "Zagros", "Deylaman", "Tareq", "Noor", "Yunes", "Fateh", "Besat", "Makran", "Shahid Soleimani", "Shahid Roudaki", "Konarak", "Paykan", "Joshan", "Gorz", "Gardouneh", "Khanjar", "Neyzeh", "Tabarzin"], air: ["TFB"] },
  },
};

export function budgetAt(n: NationId, year: number): number {
  const k = NATIONS[n].budget;
  if (year <= k[0][0]) return k[0][1];
  for (let i = 1; i < k.length; i++) {
    if (year <= k[i][0]) {
      const [y0, v0] = k[i - 1];
      const [y1, v1] = k[i];
      return v0 + ((v1 - v0) * (year - y0)) / (y1 - y0);
    }
  }
  return k[k.length - 1][1];
}

/** Starting tension (0–100) between pairs. Missing pairs default to 15. */
export const START_TENSION: [NationId, NationId, number][] = [
  ["USA", "RUS", 40], ["USA", "CHN", 25], ["USA", "IRN", 58], ["ISR", "IRN", 62],
  ["GBR", "RUS", 35], ["FRA", "RUS", 30], ["GBR", "IRN", 42], ["FRA", "IRN", 32],
  ["CHN", "RUS", 22], ["RUS", "IRN", 10], ["CHN", "IRN", 10], ["USA", "GBR", 0], ["USA", "FRA", 5],
  ["USA", "ISR", 0], ["GBR", "FRA", 5], ["GBR", "ISR", 10], ["FRA", "ISR", 12], ["CHN", "GBR", 18],
  ["CHN", "FRA", 14], ["CHN", "ISR", 12], ["RUS", "ISR", 20],
];

export const THEATRES: Theatre[] = [
  { id: "NATL", name: "North Atlantic", lat: 52, lon: -28 },
  { id: "ARCT", name: "Barents Sea", lat: 72, lon: 35 },
  { id: "BALT", name: "Baltic Sea", lat: 56.5, lon: 18.5 },
  { id: "MED", name: "Central Mediterranean", lat: 36.5, lon: 16 },
  { id: "LEV", name: "Eastern Mediterranean", lat: 33.8, lon: 32.5 },
  { id: "BLK", name: "Black Sea", lat: 43.2, lon: 34 },
  { id: "GULF", name: "Persian Gulf", lat: 26.8, lon: 52.2 },
  { id: "ARAB", name: "Arabian Sea", lat: 18, lon: 61 },
  { id: "RED", name: "Red Sea", lat: 20, lon: 38.5 },
  { id: "IND", name: "Indian Ocean", lat: -5, lon: 74 },
  { id: "SCS", name: "South China Sea", lat: 14, lon: 114 },
  { id: "ECS", name: "East China Sea", lat: 28, lon: 125 },
  { id: "WPAC", name: "Western Pacific", lat: 20, lon: 138 },
  { id: "SOJ", name: "Sea of Japan", lat: 40, lon: 134 },
  { id: "EPAC", name: "Eastern Pacific", lat: 28, lon: -128 },
];

export const THEATRE_BY_ID: Record<string, Theatre> = Object.fromEntries(THEATRES.map((t) => [t.id, t]));

export interface BaseSeed {
  id: string;
  nation: NationId;
  name: string;
  lat: number;
  lon: number;
  type: "naval" | "air" | "missile" | "radar";
  theatre: string;
}

export const BASE_SEEDS: BaseSeed[] = [
  // USA
  { id: "us-norfolk", nation: "USA", name: "Naval Station Norfolk", lat: 36.95, lon: -76.33, type: "naval", theatre: "NATL" },
  { id: "us-sandiego", nation: "USA", name: "Naval Base San Diego", lat: 32.68, lon: -117.13, type: "naval", theatre: "EPAC" },
  { id: "us-yokosuka", nation: "USA", name: "Yokosuka Fleet Activities", lat: 35.29, lon: 139.67, type: "naval", theatre: "WPAC" },
  { id: "us-kadena", nation: "USA", name: "Kadena Air Base", lat: 26.35, lon: 127.77, type: "air", theatre: "ECS" },
  { id: "us-guam", nation: "USA", name: "Andersen AFB, Guam", lat: 13.58, lon: 144.93, type: "air", theatre: "WPAC" },
  { id: "us-diego", nation: "USA", name: "Diego Garcia", lat: -7.31, lon: 72.41, type: "air", theatre: "IND" },
  { id: "us-bahrain", nation: "USA", name: "NSA Bahrain", lat: 26.21, lon: 50.61, type: "naval", theatre: "GULF" },
  { id: "us-rota", nation: "USA", name: "Naval Station Rota", lat: 36.62, lon: -6.35, type: "naval", theatre: "MED" },
  { id: "us-lakenheath", nation: "USA", name: "RAF Lakenheath", lat: 52.41, lon: 0.56, type: "air", theatre: "NATL" },
  // RUS
  { id: "ru-severomorsk", nation: "RUS", name: "Severomorsk", lat: 69.07, lon: 33.42, type: "naval", theatre: "ARCT" },
  { id: "ru-olenya", nation: "RUS", name: "Olenya Air Base", lat: 68.15, lon: 33.46, type: "air", theatre: "ARCT" },
  { id: "ru-baltiysk", nation: "RUS", name: "Baltiysk", lat: 54.65, lon: 19.9, type: "naval", theatre: "BALT" },
  { id: "ru-kaliningrad", nation: "RUS", name: "Chkalovsk (Kaliningrad)", lat: 54.77, lon: 20.4, type: "missile", theatre: "BALT" },
  { id: "ru-sevastopol", nation: "RUS", name: "Sevastopol", lat: 44.62, lon: 33.53, type: "naval", theatre: "BLK" },
  { id: "ru-tartus", nation: "RUS", name: "Tartus", lat: 34.9, lon: 35.87, type: "naval", theatre: "LEV" },
  { id: "ru-vladivostok", nation: "RUS", name: "Vladivostok", lat: 43.11, lon: 131.88, type: "naval", theatre: "SOJ" },
  { id: "ru-engels", nation: "RUS", name: "Engels Air Base", lat: 51.48, lon: 46.2, type: "air", theatre: "BLK" },
  // CHN
  { id: "cn-qingdao", nation: "CHN", name: "Qingdao", lat: 36.07, lon: 120.38, type: "naval", theatre: "ECS" },
  { id: "cn-ningbo", nation: "CHN", name: "Ningbo", lat: 29.9, lon: 121.8, type: "naval", theatre: "ECS" },
  { id: "cn-zhanjiang", nation: "CHN", name: "Zhanjiang", lat: 21.2, lon: 110.4, type: "naval", theatre: "SCS" },
  { id: "cn-yulin", nation: "CHN", name: "Yulin (Sanya)", lat: 18.23, lon: 109.55, type: "naval", theatre: "SCS" },
  { id: "cn-lingshui", nation: "CHN", name: "Lingshui Air Base", lat: 18.5, lon: 110.03, type: "air", theatre: "SCS" },
  { id: "cn-longtian", nation: "CHN", name: "Longtian Air Base", lat: 25.58, lon: 119.46, type: "air", theatre: "ECS" },
  { id: "cn-huangshan", nation: "CHN", name: "Rocket Force Base, Huangshan", lat: 29.72, lon: 118.3, type: "missile", theatre: "ECS" },
  { id: "cn-shaoguan", nation: "CHN", name: "Rocket Force Base, Shaoguan", lat: 24.8, lon: 113.6, type: "missile", theatre: "SCS" },
  // FRA
  { id: "fr-toulon", nation: "FRA", name: "Toulon", lat: 43.1, lon: 5.92, type: "naval", theatre: "MED" },
  { id: "fr-brest", nation: "FRA", name: "Brest", lat: 48.38, lon: -4.49, type: "naval", theatre: "NATL" },
  { id: "fr-stdizier", nation: "FRA", name: "Saint-Dizier", lat: 48.64, lon: 4.9, type: "air", theatre: "NATL" },
  { id: "fr-istres", nation: "FRA", name: "Istres", lat: 43.52, lon: 4.92, type: "air", theatre: "MED" },
  { id: "fr-djibouti", nation: "FRA", name: "Djibouti", lat: 11.55, lon: 43.15, type: "air", theatre: "RED" },
  { id: "fr-abudhabi", nation: "FRA", name: "Abu Dhabi naval base", lat: 24.52, lon: 54.4, type: "naval", theatre: "GULF" },
  // GBR
  { id: "gb-portsmouth", nation: "GBR", name: "HMNB Portsmouth", lat: 50.8, lon: -1.1, type: "naval", theatre: "NATL" },
  { id: "gb-faslane", nation: "GBR", name: "HMNB Clyde (Faslane)", lat: 56.07, lon: -4.82, type: "naval", theatre: "NATL" },
  { id: "gb-lossiemouth", nation: "GBR", name: "RAF Lossiemouth", lat: 57.7, lon: -3.34, type: "air", theatre: "NATL" },
  { id: "gb-akrotiri", nation: "GBR", name: "RAF Akrotiri", lat: 34.59, lon: 32.99, type: "air", theatre: "LEV" },
  { id: "gb-bahrain", nation: "GBR", name: "HMS Jufair, Bahrain", lat: 26.2, lon: 50.62, type: "naval", theatre: "GULF" },
  { id: "gb-fylingdales", nation: "GBR", name: "RAF Fylingdales", lat: 54.36, lon: -0.67, type: "radar", theatre: "NATL" },
  // ISR
  { id: "il-haifa", nation: "ISR", name: "Haifa Naval Base", lat: 32.82, lon: 35.0, type: "naval", theatre: "LEV" },
  { id: "il-nevatim", nation: "ISR", name: "Nevatim Air Base", lat: 31.21, lon: 35.01, type: "air", theatre: "LEV" },
  { id: "il-hatzerim", nation: "ISR", name: "Hatzerim Air Base", lat: 31.23, lon: 34.66, type: "air", theatre: "LEV" },
  { id: "il-ramatdavid", nation: "ISR", name: "Ramat David Air Base", lat: 32.66, lon: 35.18, type: "air", theatre: "LEV" },
  { id: "il-palmachim", nation: "ISR", name: "Palmachim", lat: 31.9, lon: 34.69, type: "missile", theatre: "LEV" },
  { id: "il-eilat", nation: "ISR", name: "Eilat", lat: 29.55, lon: 34.95, type: "naval", theatre: "RED" },
  // IRN
  { id: "ir-bandarabbas", nation: "IRN", name: "Bandar Abbas", lat: 27.14, lon: 56.21, type: "naval", theatre: "GULF" },
  { id: "ir-bushehr", nation: "IRN", name: "Bushehr", lat: 28.95, lon: 50.83, type: "air", theatre: "GULF" },
  { id: "ir-jask", nation: "IRN", name: "Jask", lat: 25.64, lon: 57.77, type: "naval", theatre: "ARAB" },
  { id: "ir-isfahan", nation: "IRN", name: "Isfahan (8th TFB)", lat: 32.75, lon: 51.86, type: "air", theatre: "GULF" },
  { id: "ir-kermanshah", nation: "IRN", name: "Kermanshah missile base", lat: 34.35, lon: 47.16, type: "missile", theatre: "GULF" },
  { id: "ir-tabriz", nation: "IRN", name: "Tabriz missile base", lat: 38.13, lon: 46.24, type: "missile", theatre: "GULF" },
  { id: "ir-chabahar", nation: "IRN", name: "Konarak (Chabahar)", lat: 25.35, lon: 60.4, type: "naval", theatre: "ARAB" },
];
