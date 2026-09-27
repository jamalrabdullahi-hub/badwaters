/// <reference lib="webworker" />
import { advanceMonth } from "./campaign/turn";
import type { Campaign } from "./types";

self.onmessage = (e: MessageEvent<Campaign>) => {
  try {
    const c = advanceMonth(e.data);
    (self as unknown as Worker).postMessage({ ok: true, c });
  } catch (err) {
    (self as unknown as Worker).postMessage({ ok: false, err: err instanceof Error ? err.message : String(err) });
  }
};
