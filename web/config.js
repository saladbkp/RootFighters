// Category → color + signature effect. THE one place to retune the look.
// `effect` names map to spawn functions in effects.js.
export const CATEGORIES = {
  pwn:       { label: "Pwn",       color: "#ff2d2d", glow: "#ff7a7a", effect: "explosion" }, // red
  web:       { label: "Web",       color: "#39ff14", glow: "#a6ff96", effect: "matrix"    }, // green
  wifi:      { label: "WiFi",      color: "#ffe600", glow: "#fff59a", effect: "lightning" }, // yellow
  reverse:   { label: "Reverse",   color: "#3b82f6", glow: "#9ec1ff", effect: "gears"     }, // blue
  forensics: { label: "Forensics", color: "#a64bff", glow: "#d3a6ff", effect: "psychic"   }, // purple
  crypto:    { label: "Crypto",    color: "#ff8c00", glow: "#ffc46b", effect: "vortex"    }, // orange
  iot:       { label: "IoT",       color: "#202028", glow: "#7b5cff", effect: "shadow"    }, // black (purple rim)
  osint:     { label: "OSINT",     color: "#ffffff", glow: "#bfe9ff", effect: "flash"     }, // white
  b2r:       { label: "B2R",       color: "#ffd700", glow: "#fff0a0", effect: "root"      }, // gold (9th)
};

// Team identity (left = A, right = B).
export const TEAMS = {
  A: { color: "#33d6ff", glow: "#bff3ff", facing: 1  }, // cyan, faces right
  B: { color: "#ff4fd8", glow: "#ffc0f1", facing: -1 }, // magenta, faces left
};

export const WS_URL = "ws://localhost:8080";

export function cat(key) {
  return CATEGORIES[key] || { label: key || "?", color: "#888", glow: "#ccc", effect: "flash" };
}

export function fmtTime(sec) {
  sec = Math.max(0, Math.floor(sec));
  const m = Math.floor(sec / 60), s = sec % 60;
  return `${m}:${s.toString().padStart(2, "0")}`;
}
