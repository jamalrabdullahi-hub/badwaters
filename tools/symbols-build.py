"""Builds the Theatre Ops unit symbols (Option B · silhouettes) as white SVGs in data/core/theatre/symbols.
White is tinted to the side colour at draw time; dark details stay dark. Abstract game art, not real symbology.
Each code gets CODE.svg (friendly/own) and CODE_h.svg (hostile: diamond tile or corner tick)."""
import os
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data", "core", "theatre", "symbols")
BG = "#06090b"
AIR = [("FTR", "Fighter"), ("5GEN", "Stealth fighter"), ("STK", "Strike / multirole"), ("BMR", "Bomber"), ("SB", "Stealth bomber"),
       ("AEW", "AEW & control"), ("TKR", "Tanker"), ("MPA", "Maritime patrol"), ("EW", "Electronic warfare"), ("TPT", "Transport"),
       ("MALE", "ISR drone (MALE)"), ("HALE", "ISR drone (HALE)"), ("UCAV", "Combat drone"), ("HEL", "Helicopter")]
MSL = [("SRBM", "Short-range ballistic"), ("MRBM", "Medium-range ballistic"), ("HGV", "Hypersonic glide"), ("CM", "Cruise missile"),
       ("ASM", "Anti-ship missile"), ("SAM", "SAM interceptor"), ("AAM", "Air-to-air missile"), ("LM", "Loitering drone")]
SEA = [("CV", "Carrier"), ("DDG", "Destroyer / cruiser"), ("FFG", "Frigate"), ("PC", "Corvette / patrol"), ("LHD", "Amphibious"),
       ("AOR", "Replenishment"), ("USV", "Uncrewed surface"), ("SSK", "Diesel submarine"), ("SSN", "Nuclear submarine")]
GND = [("AB", "Air base"), ("NB", "Naval base"), ("TEL", "Missile launcher (TEL)"), ("MB", "Missile base"), ("SAMB", "SAM battery"),
       ("RDR", "Radar site"), ("C2", "Command centre"), ("AMO", "Munitions depot"), ("POL", "Fuel depot"), ("PRT", "Port"),
       ("IND", "Defence industry"), ("PWR", "Power station"), ("YD", "Shipyard"), ("GS", "Ground station"), ("CITY", "City · protected")]
CATS = [("Aircraft", "air", AIR), ("Large missiles & munitions", "msl", MSL), ("Naval", "sea", SEA), ("Ground installations", "gnd", GND)]
def g(code, c, sw=2.2):
    """Line glyph for the inside of a frame, stroke colour c."""
    L = lambda d: f'<path d="{d}" fill="none" stroke="{c}" stroke-width="{sw}" stroke-linecap="round" stroke-linejoin="round"/>'
    F = lambda d: f'<path d="{d}" fill="{c}"/>'
    T = lambda t, s=11: f'<text x="0" y="{s * 0.36:.1f}" text-anchor="middle" font-family="IBM Plex Mono, monospace" font-weight="700" font-size="{s}" fill="{c}">{t}</text>'
    return {
        "FTR": L("M0 -12 L0 12 M-11 3 L0 -4 L11 3 M-5 11 L0 8 L5 11"),
        "5GEN": L("M0 -12 L11 6 L4 3 L0 11 L-4 3 L-11 6 Z"),
        "STK": L("M0 -12 L0 12 M-12 2 L0 -4 L12 2 M-5 11 L5 11") + F("M-3 5 h6 v3 h-6 z"),
        "BMR": L("M0 -12 L0 12 M-14 0 L0 -5 L14 0 M-6 11 L6 11"),
        "SB": L("M0 -9 L14 5 L7 5 L4 9 L0 5 L-4 9 L-7 5 L-14 5 Z"),
        "AEW": L("M0 -12 L0 12 M-12 0 L12 0") + f'<ellipse cx="0" cy="-2" rx="8" ry="3.4" fill="none" stroke="{c}" stroke-width="{sw}"/>',
        "TKR": L("M0 -12 L0 12 M-13 -1 L13 -1 M-5 10 L5 10 M0 12 L0 16"),
        "MPA": L("M0 -12 L0 12 M-13 -2 L13 -2 M-5 10 L5 10") + L("M-9 14 q9 -5 18 0"),
        "EW": L("M-11 -6 L-4 6 L2 -6 L9 6"),
        "TPT": L("M0 -12 L0 12 M-14 -3 L14 -3 M-6 10 L6 10") + L("M-8 -3 L-8 1 M8 -3 L8 1"),
        "MALE": L("M0 -10 L0 12 M-15 -2 L15 -2 M-4 11 L4 11"),
        "HALE": L("M0 -10 L0 12 M-17 -1 L17 -1 M-3 11 L3 11") + L("M-17 -1 L-17 -4 M17 -1 L17 -4"),
        "UCAV": L("M0 -9 L13 7 L0 3 L-13 7 Z"),
        "HEL": L("M-12 -12 L12 12 M-12 12 L12 -12") + f'<circle r="4" fill="{c}"/>',
        "SRBM": L("M0 -13 L0 11 M-4 11 L4 11") + L("M-3 -8 L0 -13 L3 -8") + L("M-4 7 L-4 11 M4 7 L4 11"),
        "MRBM": L("M0 -14 L0 12 M-5 12 L5 12") + L("M-3 -9 L0 -14 L3 -9") + L("M-3 0 L3 0 M-5 8 L-5 12 M5 8 L5 12"),
        "HGV": L("M-12 6 L0 -10 L12 6 L0 2 Z"),
        "CM": L("M-12 0 L12 0 M8 -3 L12 0 L8 3 M-2 -6 L2 0 L-2 6 M-12 -3 L-12 3"),
        "ASM": L("M-12 -2 L12 -2 M8 -5 L12 -2 L8 1 M-2 -8 L2 -2 L-2 4") + L("M-12 8 q6 -4 12 0 q6 4 12 0"),
        "SAM": L("M0 12 L0 -12 M-4 -7 L0 -12 L4 -7 M-4 8 L4 8") + L("M-10 -4 q10 -12 20 0"),
        "AAM": L("M-13 0 L13 0 M9 -3 L13 0 L9 3 M-10 -4 L-10 4 M-4 -3 L-4 3"),
        "LM": L("M0 -10 L12 8 L0 4 L-12 8 Z") + L("M0 4 L0 11"),
        "CV": L("M-13 5 L13 5 L10 -5 L-10 -5 Z M-4 -5 L6 5"),
        "DDG": L("M-14 3 L10 3 L14 0 L10 -3 L-14 -3 Z") + F("M-4 -2 h5 v4 h-5 z"),
        "FFG": L("M-11 2 L8 2 L12 0 L8 -2 L-11 -2 Z") + F("M-3 -1.5 h4 v3 h-4 z"),
        "PC": L("M-8 2 L6 2 L9 0 L6 -2 L-8 -2 Z"),
        "LHD": L("M-13 4 L13 4 L13 -4 L-13 -4 Z M-13 0 L-6 0"),
        "AOR": L("M-12 3 L10 3 L13 0 L10 -3 L-12 -3 Z M-6 -3 L-6 -7 M2 -3 L2 -7"),
        "USV": L("M-8 2 L6 2 L9 0 L6 -2 L-8 -2 Z") + T("U", 8).replace('y="2.9"', 'y="-5"'),
        "SSK": L("M-13 0 q0 -4 5 -4 L8 -4 q5 0 5 4 q0 4 -5 4 L-8 4 q-5 0 -5 -4 Z M-1 -4 L-1 -8 L3 -8 L3 -4"),
        "SSN": L("M-14 0 q0 -4 5 -4 L9 -4 q5 0 5 4 q0 4 -5 4 L-9 4 q-5 0 -5 -4 Z M-2 -4 L-2 -8 L2 -8 L2 -4") + f'<circle cx="7" cy="0" r="1.6" fill="{c}"/>',
        "AB": L("M-13 5 L13 -5 M-9 -2 L-7 3 M-1 -5 L1 0 M7 -8 L9 -3"),
        "NB": L("M0 -11 L0 11 M-6 -6 L6 -6 M-10 4 q10 12 20 0"),
        "TEL": L("M-13 6 L13 6 M-9 6 L-9 9 M9 6 L9 9 M-10 3 L8 -9 L11 -7 L-7 5"),
        "MB": L("M-12 10 L12 10 M-12 10 L-12 -2 L12 -2 L12 10") + L("M0 -2 L0 -13 M-3 -9 L0 -13 L3 -9"),
        "SAMB": L("M-12 8 L12 8 M-6 8 L-6 -6 M-9 -2 L-6 -6 L-3 -2 M6 8 L6 -6 M3 -2 L6 -6 L9 -2"),
        "RDR": L("M-10 -8 q12 2 14 14 M-3 -2 L6 -9 M0 10 L-6 10 M-3 10 L-1 4"),
        "C2": L("M-8 12 L-8 -12 M-8 -12 L10 -7 L-8 -2"),
        "AMO": L("M-11 -6 L11 -6 L11 9 L-11 9 Z M-11 -6 L0 -12 L11 -6") + F("M-2 -2 h4 v7 h-4 z"),
        "POL": L("M0 -12 q10 12 0 22 q-10 -10 0 -22 Z"),
        "PRT": L("M-12 10 L12 10 M6 10 L6 -12 L-8 -12 M-8 -12 L-8 -4") + L("M-11 -4 L-5 -4"),
        "IND": L("M-12 10 L-12 -2 L-4 -8 L-4 -2 L4 -8 L4 -2 L12 -8 L12 10 Z"),
        "PWR": L("M3 -12 L-6 1 L1 1 L-3 12 L7 -2 L0 -2 Z"),
        "YD": L("M-12 10 L12 10 M-8 10 L-8 -12 L10 -12 M4 -12 L4 -4") + L("M-12 4 L6 4 L9 7"),
        "GS": L("M-8 10 L8 10 M0 10 L0 2 M-9 -6 q9 16 18 0") + L("M0 -1 L7 -10"),
        "CITY": L("M-12 10 L12 10 M-10 10 L-10 -2 L-4 -2 L-4 10 M-2 10 L-2 -10 L5 -10 L5 10 M7 10 L7 2 L11 2 L11 10"),
    }[code]
SIL = {
    "FTR": "M0 -22 L3 -12 L3 -4 L18 6 L18 9 L3 5 L3 13 L8 18 L8 20 L0 18 L-8 20 L-8 18 L-3 13 L-3 5 L-18 9 L-18 6 L-3 -4 L-3 -12 Z",
    "5GEN": "M0 -22 L5 -8 L20 6 L20 10 L7 8 L9 18 L3 16 L0 20 L-3 16 L-9 18 L-7 8 L-20 10 L-20 6 L-5 -8 Z",
    "STK": "M0 -22 L3 -10 L3 -2 L20 4 L20 8 L3 6 L3 14 L9 19 L9 21 L0 19 L-9 21 L-9 19 L-3 14 L-3 6 L-20 8 L-20 4 L-3 -2 L-3 -10 Z",
    "BMR": "M0 -22 L3 -14 L3 -6 L22 4 L22 7 L3 2 L3 14 L10 19 L10 21 L0 20 L-10 21 L-10 19 L-3 14 L-3 2 L-22 7 L-22 4 L-3 -6 L-3 -14 Z",
    "SB": "M0 -14 L24 10 L16 12 L12 8 L6 12 L0 8 L-6 12 L-12 8 L-16 12 L-24 10 Z",
    "AEW": "M0 -22 L2 -12 L2 -2 L20 3 L20 6 L2 4 L2 15 L8 19 L8 21 L0 19 L-8 21 L-8 19 L-2 15 L-2 4 L-20 6 L-20 3 L-2 -2 L-2 -12 Z M-9 0 a9 3.6 0 1 0 18 0 a9 3.6 0 1 0 -18 0 Z",
    "TKR": "M0 -22 L2.5 -14 L2.5 -3 L22 5 L22 8 L2.5 3 L2.5 15 L9 19 L9 21 L0 19 L-9 21 L-9 19 L-2.5 15 L-2.5 3 L-22 8 L-22 5 L-2.5 -3 L-2.5 -14 Z M-1 21 h2 v4 h-2 Z",
    "MPA": "M0 -22 L2.5 -14 L2.5 -4 L21 -1 L21 2 L2.5 1 L2.5 15 L9 19 L9 21 L0 19 L-9 21 L-9 19 L-2.5 15 L-2.5 1 L-21 2 L-21 -1 L-2.5 -4 L-2.5 -14 Z M-1 21 h2 v5 h-2 Z",
    "EW": "M0 -22 L3 -12 L3 -4 L18 6 L18 9 L3 5 L3 13 L8 18 L8 20 L0 18 L-8 20 L-8 18 L-3 13 L-3 5 L-18 9 L-18 6 L-3 -4 L-3 -12 Z M-19 8 h4 v5 h-4 Z M15 8 h4 v5 h-4 Z",
    "TPT": "M0 -22 L3.5 -15 L3.5 -5 L23 -2 L23 1 L3.5 1 L3.5 14 L10 18 L10 20 L0 19 L-10 20 L-10 18 L-3.5 14 L-3.5 1 L-23 1 L-23 -2 L-3.5 -5 L-3.5 -15 Z",
    "MALE": "M0 -16 L1.5 -8 L1.5 -3 L22 -1 L22 1 L1.5 1 L1.5 12 L7 17 L6 18 L0 15 L-6 18 L-7 17 L-1.5 12 L-1.5 1 L-22 1 L-22 -1 L-1.5 -3 L-1.5 -8 Z",
    "HALE": "M0 -14 L1.5 -8 L1.5 -2 L26 -1 L26 1 L1.5 1 L1.5 12 L6 16 L5 17 L0 14 L-5 17 L-6 16 L-1.5 12 L-1.5 1 L-26 1 L-26 -1 L-1.5 -2 L-1.5 -8 Z",
    "UCAV": "M0 -12 L20 8 L12 10 L6 7 L0 10 L-6 7 L-12 10 L-20 8 Z",
    "HEL": "M-1.8 -6 h3.6 v20 h-3.6 Z M-22 -1 L22 -1 L22 1 L-22 1 Z M-1 -21 L1 -21 L1 21 L-1 21 Z M-4 12 h8 v2 h-8 Z",
    "SRBM": "M-2.5 -22 Q0 -26 2.5 -22 L2.5 14 L6 20 L-6 20 L-2.5 14 Z",
    "MRBM": "M-3 -24 Q0 -28 3 -24 L3 16 L7 22 L-7 22 L-3 16 Z M-3.5 -4 h7 v1.5 h-7 Z",
    "HGV": "M0 -22 L9 16 L0 12 L-9 16 Z",
    "CM": "M-1.8 -20 Q0 -23 1.8 -20 L1.8 12 L6 17 L-6 17 L-1.8 12 Z M-10 -1 L10 -1 L10 1.5 L-10 1.5 Z",
    "ASM": "M-1.8 -20 Q0 -23 1.8 -20 L1.8 12 L6 17 L-6 17 L-1.8 12 Z M-8 0 L8 0 L8 2 L-8 2 Z M-6 -14 L6 -14 L6 -12.5 L-6 -12.5 Z",
    "SAM": "M-1.5 -22 Q0 -25 1.5 -22 L1.5 14 L5 19 L-5 19 L-1.5 14 Z M-5 -12 L5 -12 L5 -10.5 L-5 -10.5 Z",
    "AAM": "M-1.2 -20 Q0 -23 1.2 -20 L1.2 13 L4 17 L-4 17 L-1.2 13 Z M-4 -10 L4 -10 L4 -8.5 L-4 -8.5 Z",
    "LM": "M0 -16 L16 12 L3 9 L0 14 L-3 9 L-16 12 Z",
    "CV": "M-6 -24 L6 -24 L10 -18 L10 22 L-10 22 L-10 -18 Z",
    "DDG": "M0 -25 L6 -12 L6 22 L-6 22 L-6 -12 Z",
    "FFG": "M0 -21 L5 -10 L5 19 L-5 19 L-5 -10 Z",
    "PC": "M0 -15 L4 -6 L4 14 L-4 14 L-4 -6 Z",
    "LHD": "M-8 -22 L8 -22 L9 -18 L9 22 L-9 22 L-9 -18 Z",
    "AOR": "M0 -24 L6 -14 L6 22 L-6 22 L-6 -14 Z",
    "USV": "M0 -12 L3.5 -4 L3.5 12 L-3.5 12 L-3.5 -4 Z",
    "SSK": "M0 -20 Q5 -18 5 -6 L5 14 Q5 20 0 21 Q-5 20 -5 14 L-5 -6 Q-5 -18 0 -20 Z",
    "SSN": "M0 -24 Q6 -22 6 -8 L6 16 Q6 23 0 24 Q-6 23 -6 16 L-6 -8 Q-6 -22 0 -24 Z",
}

def icon(cat, code, hostile):
    col = "#ffffff"
    if code in SIL:
        extra = ""
        if code == "CV": extra = f'<path d="M-7 18 L6 -14" stroke="{BG}" stroke-width="1.6"/>'
        if code in ("DDG", "FFG", "AOR"): extra = f'<rect x="-2.6" y="-2" width="5.2" height="8" fill="{BG}"/>'
        if code in ("SSK", "SSN"): extra = f'<rect x="-2" y="-8" width="4" height="7" fill="{BG}"/>'
        if code == "LHD": extra = f'<path d="M5 -18 L5 8" stroke="{BG}" stroke-width="2.2"/>'
        dash = ' stroke-dasharray="3 2"' if code in ("SSK", "SSN") else ""
        body = f'<path d="{SIL[code]}" fill="{col}" fill-rule="evenodd" stroke="#050607" stroke-width="1.2"{dash}/>' + extra
        if hostile: body += f'<path d="M-25 -25 L-18 -25 M-25 -25 L-25 -18 M25 25 L18 25 M25 25 L25 18" stroke="{col}" stroke-width="2.2"/>'
    else:
        if code == "CITY": tile, gc = f'<circle r="20" fill="none" stroke="{col}" stroke-width="2.4" stroke-dasharray="5 3"/>', col
        elif hostile: tile, gc = f'<path d="M0 -25 L25 0 L0 25 L-25 0 Z" fill="{col}"/>', BG
        else: tile, gc = f'<rect x="-22" y="-22" width="44" height="44" rx="4" fill="{col}"/>', BG
        body = f'{tile}<g transform="scale({0.62 if hostile and code != "CITY" else 0.78})">{g(code, gc, 2.6)}</g>'
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="56" height="56" viewBox="-28 -28 56 56">{body}</svg>'

os.makedirs(OUT, exist_ok=True)
n = 0
for _, cat, items in CATS:
    for code, _ in items:
        for h in (False, True):
            open(os.path.join(OUT, f"{code}{'_h' if h else ''}.svg"), "w").write(icon(cat, code, h)); n += 1
print(f"{n} symbols -> {os.path.normpath(OUT)}")
