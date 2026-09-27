import { createRoot } from "react-dom/client";
import "./styles/app.css";
import { App } from "./ui/App";
import { getState, setState } from "./ui/store";
import { declareWar } from "./sim/campaign/war";
import { planAiStrike } from "./sim/campaign/war";
import { buildBattle } from "./sim/battle/setup";

createRoot(document.getElementById("root")!).render(<App />);

// Dev-only console hooks for testing (stripped from production builds).
if (import.meta.env.DEV) {
  (window as unknown as Record<string, unknown>).__bw = { getState, setState, declareWar, planAiStrike, buildBattle };
}
