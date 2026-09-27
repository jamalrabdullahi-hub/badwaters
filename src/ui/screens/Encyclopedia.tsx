import { useMemo, useState } from "react";
import { NATIONS } from "../../data/nations";
import { SPECS } from "../../data/parts";
import { FIELDS, TECHS, TECH_BY_ID } from "../../data/techs";
import { evalDesign } from "../../sim/campaign/core";
import { CATEGORIES } from "../../sim/types";
import { Modal, Seg, Tag } from "../components/common";
import { setState, useApp } from "../store";

type Kind = "all" | "tech" | "part" | "design" | "nation";
interface Entry { kind: Exclude<Kind, "all">; title: string; tag: string; text: string; extra?: string }

export function Encyclopedia() {
  const c = useApp((s) => s.campaign);
  const q0 = useApp((s) => s.encyclopedia) ?? "";
  const [q, setQ] = useState(q0);
  const [kind, setKind] = useState<Kind>("all");
  const entries = useMemo(() => {
    const out: Entry[] = [];
    for (const t of TECHS) {
      out.push({ kind: "tech", title: t.name, tag: FIELDS.find((f) => f.id === t.field)!.short, text: t.desc,
        extra: `Year ${t.year} · base cost ${t.cost} RP${t.requires.length ? ` · needs ${t.requires.map((r) => TECH_BY_ID[r]?.name).join(", ")}` : ""}${c ? (c.nations[c.player].known.includes(t.id) ? " · KNOWN" : "") : ""}` });
    }
    for (const cat of CATEGORIES) for (const slot of SPECS[cat].slots) for (const p of slot.parts) {
      out.push({ kind: "part", title: p.name, tag: `${SPECS[cat].name} · ${slot.name}`, text: p.desc || "—", extra: p.tech ? `Requires ${TECH_BY_ID[p.tech]?.name}` : "Available from the start" });
    }
    if (c) for (const d of Object.values(c.designs)) {
      if (d.status === "draft" && d.nation !== c.player) continue;
      const s = evalDesign(c, d).stats;
      const bits = [
        s.rangeKm ? `${s.rangeKm} km` : "", s.mach ? `Mach ${s.mach}` : "", s.speedKts ? `${s.speedKts} kn` : "", s.radiusKm ? `radius ${s.radiusKm} km` : "",
        s.radarKm ? `radar ${s.radarKm} km` : "", s.cells ? `${s.cells} cells` : "", `$${s.cost}M`,
      ].filter(Boolean).join(" · ");
      out.push({ kind: "design", title: d.name, tag: `${NATIONS[d.nation].adjective} ${SPECS[d.category].name.toLowerCase()}`, text: bits, extra: `${d.status} · ${d.year}${d.nation !== c.player ? " · intelligence estimate" : ""}` });
    }
    for (const n of Object.values(NATIONS)) out.push({ kind: "nation", title: n.name, tag: n.bloc === "west" ? "Western bloc" : "Eastern bloc", text: n.pitch, extra: `Strengths: ${n.strengths.join(", ")}. Weaknesses: ${n.weaknesses.join(", ")}.` });
    return out;
  }, [c]);
  const list = entries.filter((e) => (kind === "all" || e.kind === kind) && (!q || `${e.title} ${e.tag} ${e.text}`.toLowerCase().includes(q.toLowerCase())));
  return (
    <Modal title="Encyclopedia" onClose={() => setState({ encyclopedia: null })} wide
      actions={<Seg<Kind> value={kind} onChange={setKind} options={[["all", "All"], ["tech", "Tech"], ["part", "Components"], ["design", "Designs"], ["nation", "Nations"]]} />}>
      <input type="search" autoFocus placeholder="Search missiles, radars, techs, ships…" value={q} onChange={(e) => setQ(e.target.value)} style={{ width: "100%", fontSize: 15, padding: "8px 10px" }} />
      <div className="dim small">{list.length} entries</div>
      <div className="col" style={{ gap: 0 }}>
        {list.slice(0, 200).map((e, i) => (
          <div key={i} className="inbox-item" style={{ gridTemplateColumns: "auto 1fr" }}>
            <Tag kind={e.kind === "tech" ? "tech" : e.kind === "design" ? "fleet" : "dim"}>{e.kind}</Tag>
            <span><b>{e.title}</b> <span className="dim small">{e.tag}</span></span>
            <p>{e.text}{e.extra && <><br /><span className="faint">{e.extra}</span></>}</p>
          </div>
        ))}
      </div>
    </Modal>
  );
}
