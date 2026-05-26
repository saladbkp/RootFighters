// Glue: WebSocket client (protocol v1) → arena choreography + HUD.
// Also a keyboard dev console so you can preview every effect WITHOUT the
// backend running (handy while tuning visuals).
import { Arena } from "./arena.js";
import { CATEGORIES, cat, fmtTime, WS_URL } from "./config.js";

const $ = (id) => document.getElementById(id);
const arena = new Arena($("stage"));
arena.start();

// ---- local state mirror ---------------------------------------------------- //
const state = {
  round: "—", phase: "idle",
  timer: { remainingSec: 0, running: false },
  teamA: { name: "Team Alpha", score: 0, solves: [] },
  teamB: { name: "Team Bravo", score: 0, solves: [] },
  bannedCategories: [],
};

function renderHUD() {
  $("round").textContent = state.round;
  $("timer").textContent = fmtTime(state.timer.remainingSec);
  $("timer").classList.toggle("low", state.timer.remainingSec <= 30 && state.timer.running);
  for (const side of ["A", "B"]) {
    const t = state["team" + side];
    $("name" + side).textContent = t.name;
    $("score" + side).textContent = t.score;
    const dots = (t.solves || []).map((k) =>
      `<span class="dot" style="background:${cat(k).color};box-shadow:0 0 8px ${cat(k).glow}" title="${cat(k).label}"></span>`
    ).join("");
    $("solves" + side).innerHTML = dots;
  }
  $("banlist").innerHTML = state.bannedCategories.length
    ? "BANNED: " + state.bannedCategories.map((k) =>
        `<span class="ban" style="border-color:${cat(k).color};color:${cat(k).glow}">${cat(k).label}</span>`).join("")
    : "";
}

function log(text, color = "#cfd2ff") {
  const li = document.createElement("li");
  li.innerHTML = `<span style="color:${color}">${text}</span>`;
  $("log").prepend(li);
  while ($("log").children.length > 8) $("log").lastChild.remove();
}

let bannerTimer = null;
function banner(text, level = "info") {
  const b = $("banner");
  b.textContent = text;
  b.className = "show " + level;
  clearTimeout(bannerTimer);
  bannerTimer = setTimeout(() => (b.className = ""), 2600);
}

// ---- event handling -------------------------------------------------------- //
function applyState(s) { Object.assign(state, s); renderHUD(); }

function handle(msg) {
  const d = msg.data || {};
  switch (msg.type) {
    case "STATE": applyState(d); break;
    case "MATCH_START":
      state.round = d.round; state.phase = "live";
      state.teamA.score = 0; state.teamA.solves = [];
      state.teamB.score = 0; state.teamB.solves = [];
      state.bannedCategories = d.bannedCategories || [];
      state.timer.remainingSec = d.durationSec || 0;
      arena.fx.clear();
      banner(`${state.round}: ${d.teamA?.name} vs ${d.teamB?.name}`, "hype");
      renderHUD(); break;
    case "SOLVE": {
      const t = state["team" + d.team];
      if (d.scoreA != null) state.teamA.score = d.scoreA;
      if (d.scoreB != null) state.teamB.score = d.scoreB;
      t.solves = t.solves || []; t.solves.push(d.category);
      arena.triggerSolve(d.team, d.category);
      log(`${state["team" + d.team].name} solved <b>${cat(d.category).label}</b> (+${d.points})`, cat(d.category).glow);
      renderHUD(); break;
    }
    case "WRONG":
      arena.triggerWrong(d.team);
      log(`${state["team" + d.team].name} — wrong flag (${cat(d.category).label})`, "#ff7a7a");
      break;
    case "BAN":
      if (!state.bannedCategories.includes(d.category)) state.bannedCategories.push(d.category);
      log(`${state["team" + d.team].name} banned <b>${cat(d.category).label}</b>`, "#ffd27a");
      renderHUD(); break;
    case "TIMER":
      state.timer.remainingSec = d.remainingSec; state.timer.running = d.running;
      renderHUD(); break;
    case "MATCH_END": {
      state.phase = "ended";
      const who = d.winner === "DRAW" ? "DRAW" :
        `${state["team" + d.winner].name} WINS`;
      banner(`${who}  —  ${d.scoreA} : ${d.scoreB}`, "win");
      log(`Match ended (${d.reason}) — ${who}`, "#9affc4");
      break;
    }
    case "ANNOUNCE": banner(d.text, d.level || "info"); break;
  }
}

// ---- WebSocket with auto-reconnect ----------------------------------------- //
let ws = null;
function connect() {
  setStatus("connecting…", "#ffd27a");
  ws = new WebSocket(WS_URL);
  ws.onopen = () => setStatus("live", "#5cff9d");
  ws.onclose = () => { setStatus("offline — retrying", "#ff6b6b"); setTimeout(connect, 2000); };
  ws.onerror = () => ws.close();
  ws.onmessage = (e) => { try { handle(JSON.parse(e.data)); } catch (err) { console.warn(err); } };
}
function setStatus(text, color) {
  $("status").innerHTML = `<i style="background:${color}"></i>${text}`;
}
connect();
renderHUD();

// ---- keyboard dev console (works offline) ---------------------------------- //
const ORDER = Object.keys(CATEGORIES); // 1..9 → categories in config order
window.addEventListener("keydown", (e) => {
  if (e.key === "d") { $("devpanel").classList.toggle("hidden"); return; }
  const n = parseInt(e.key, 10);
  if (n >= 1 && n <= ORDER.length) {
    const category = ORDER[n - 1];
    const side = e.shiftKey ? "B" : "A";
    arena.triggerSolve(side, category);     // local preview only
    banner(`${side}: ${cat(category).label} (preview)`, "info");
  } else if (e.key === ",") arena.triggerWrong("A");
  else if (e.key === ".") arena.triggerWrong("B");
  else if (e.key === " ") {                 // quick simultaneous preview
    const c = ORDER[(Math.random() * ORDER.length) | 0];
    arena.triggerSolve("A", c); setTimeout(() => arena.triggerSolve("B", c), 350);
  }
});

// fill the dev legend
$("devlegend").innerHTML = ORDER.map((k, i) =>
  `<span><kbd>${i + 1}</kbd> ${cat(k).label}</span>`).join("");
