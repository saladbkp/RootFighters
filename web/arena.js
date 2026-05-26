// The 2v2 battle arena: background, two procedural mascot creatures, the
// attack choreography (lunge → projectile → impact → flinch → shake), and the
// render loop. Creatures are ORIGINAL stylized blobs — deliberately not any
// existing IP. Swap them for real rigged models in the Unity layer later.
import { Effects } from "./effects.js";
import { TEAMS, cat } from "./config.js";

const TAU = Math.PI * 2;
const rand = (a, b) => a + Math.random() * (b - a);

class Creature {
  constructor(side) {
    this.side = side;
    this.color = TEAMS[side].color;
    this.glow = TEAMS[side].glow;
    this.facing = TEAMS[side].facing; // +1 looks right, -1 looks left
    this.x = 0; this.y = 0; this.baseX = 0; this.baseY = 0;
    this.state = "idle";              // idle | attack | flinch
    this.t = 0;                       // state timer
    this.bob = Math.random() * TAU;   // idle bob phase
    this.scale = 1;
  }

  attack() { this.state = "attack"; this.t = 0; }
  flinch() { this.state = "flinch"; this.t = 0; }

  update(dt) {
    this.bob += dt * 2;
    this.t += dt;
    let dx = 0, dy = 0;
    if (this.state === "attack") {
      const p = Math.min(1, this.t / 0.45);
      const lunge = Math.sin(p * Math.PI);          // out and back
      dx = this.facing * lunge * 60;
      dy = -lunge * 20;
      this.scale = 1 + lunge * 0.12;
      if (this.t > 0.45) { this.state = "idle"; this.scale = 1; }
    } else if (this.state === "flinch") {
      const p = Math.min(1, this.t / 0.4);
      dx = -this.facing * Math.sin(p * Math.PI) * 34 + rand(-3, 3);
      this.scale = 1 - Math.sin(p * Math.PI) * 0.1;
      if (this.t > 0.4) { this.state = "idle"; this.scale = 1; }
    } else {
      this.scale += (1 - this.scale) * 0.2;
    }
    this.x = this.baseX + dx;
    this.y = this.baseY + dy + Math.sin(this.bob) * 8;
  }

  draw(ctx) {
    const r = 64 * this.scale;
    const flinching = this.state === "flinch";
    ctx.save();
    ctx.translate(this.x, this.y);

    // ground shadow
    ctx.save();
    ctx.globalAlpha = 0.35;
    ctx.fillStyle = "#000";
    ctx.beginPath(); ctx.ellipse(0, r + 18, r * 0.9, r * 0.26, 0, 0, TAU); ctx.fill();
    ctx.restore();

    // orbiting aura dots (team-colored energy)
    ctx.save();
    ctx.globalCompositeOperation = "lighter";
    for (let i = 0; i < 7; i++) {
      const a = this.bob * 1.3 + (i / 7) * TAU;
      const rr = r + 22 + Math.sin(this.bob * 2 + i) * 6;
      ctx.globalAlpha = 0.7;
      ctx.fillStyle = this.glow; ctx.shadowColor = this.glow; ctx.shadowBlur = 16;
      ctx.beginPath();
      ctx.arc(Math.cos(a) * rr, Math.sin(a) * rr * 0.7, 4, 0, TAU); ctx.fill();
    }
    ctx.restore();

    // body (egg-ish blob with gradient)
    const g = ctx.createRadialGradient(-r * 0.3, -r * 0.4, r * 0.2, 0, 0, r);
    g.addColorStop(0, "#ffffff");
    g.addColorStop(0.25, this.glow);
    g.addColorStop(1, this.color);
    ctx.shadowColor = this.glow; ctx.shadowBlur = flinching ? 6 : 26;
    ctx.fillStyle = flinching ? "#ff6b6b" : g;
    ctx.beginPath();
    ctx.ellipse(0, 0, r * 0.92, r, 0, 0, TAU); ctx.fill();
    ctx.shadowBlur = 0;

    // little feet
    ctx.fillStyle = this.color;
    ctx.beginPath(); ctx.ellipse(-r * 0.4, r * 0.92, r * 0.26, r * 0.16, 0, 0, TAU); ctx.fill();
    ctx.beginPath(); ctx.ellipse(r * 0.4, r * 0.92, r * 0.26, r * 0.16, 0, 0, TAU); ctx.fill();

    // eyes look toward the opponent
    const ex = this.facing * r * 0.26;
    for (const s of [-1, 1]) {
      const eye = ex + s * r * 0.26;
      ctx.fillStyle = "#1a1320";
      ctx.beginPath(); ctx.ellipse(eye, -r * 0.12, r * 0.16, r * 0.22, 0, 0, TAU); ctx.fill();
      ctx.fillStyle = "#fff";
      ctx.beginPath(); ctx.arc(eye + this.facing * 3, -r * 0.2, r * 0.06, 0, TAU); ctx.fill();
    }
    // cheeks
    ctx.fillStyle = "rgba(255,120,170,0.55)";
    ctx.beginPath(); ctx.arc(ex - r * 0.34, r * 0.14, r * 0.1, 0, TAU); ctx.fill();
    ctx.beginPath(); ctx.arc(ex + r * 0.34, r * 0.14, r * 0.1, 0, TAU); ctx.fill();

    ctx.restore();
  }
}

export class Arena {
  constructor(canvas) {
    this.canvas = canvas;
    this.ctx = canvas.getContext("2d");
    this.fx = new Effects();
    this.A = new Creature("A");
    this.B = new Creature("B");
    this.projectiles = [];
    this.shake = 0;
    this.stars = [];
    this.last = performance.now();
    this._resize();
    window.addEventListener("resize", () => this._resize());
  }

  _resize() {
    const dpr = Math.min(2, window.devicePixelRatio || 1);
    this.w = this.canvas.clientWidth;
    this.h = this.canvas.clientHeight;
    this.canvas.width = this.w * dpr;
    this.canvas.height = this.h * dpr;
    this.ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    this.A.baseX = this.w * 0.22; this.A.baseY = this.h * 0.6;
    this.B.baseX = this.w * 0.78; this.B.baseY = this.h * 0.6;
    if (this.stars.length === 0) {
      for (let i = 0; i < 70; i++) {
        this.stars.push({ x: Math.random(), y: Math.random() * 0.7,
          s: rand(0.5, 2), tw: rand(0, TAU) });
      }
    }
  }

  creature(side) { return side === "A" ? this.A : this.B; }

  // --- public choreography ------------------------------------------------- //
  triggerSolve(side, category) {
    const c = cat(category);
    const atk = this.creature(side);
    const def = this.creature(side === "A" ? "B" : "A");
    atk.attack();
    const big = category === "pwn" || category === "b2r";
    this.projectiles.push({
      x: atk.x + atk.facing * 60, y: atk.y - 10,
      tx: def.baseX, ty: def.baseY, t: 0, dur: 0.32,
      color: c.color, glow: c.glow,
      onArrive: () => {
        this.fx.spawn(c.effect, { x: def.baseX, y: def.baseY,
          color: c.color, glow: c.glow, fromX: atk.x, fromY: atk.y });
        def.flinch();
        this.shake = big ? 22 : 12;
      },
    });
  }

  triggerWrong(side) {
    const me = this.creature(side);
    me.flinch();
    this.fx.fx_miss({ x: me.baseX, y: me.baseY - 10 });
    this.shake = 6;
  }

  // For previewing an effect on a side without the attack travel.
  previewEffect(side, category) {
    const c = cat(category);
    const me = this.creature(side);
    this.fx.spawn(c.effect, { x: me.baseX, y: me.baseY,
      color: c.color, glow: c.glow, fromX: me.baseX, fromY: me.baseY });
    this.shake = 8;
  }

  // --- loop ---------------------------------------------------------------- //
  start() { requestAnimationFrame((t) => this._frame(t)); }

  _frame(now) {
    const dt = Math.min(0.05, (now - this.last) / 1000);
    this.last = now;
    this._update(dt);
    this._draw();
    requestAnimationFrame((t) => this._frame(t));
  }

  _update(dt) {
    this.A.update(dt); this.B.update(dt);
    this.fx.update(dt);
    this.shake *= 0.86;
    for (let i = this.projectiles.length - 1; i >= 0; i--) {
      const p = this.projectiles[i];
      p.t += dt;
      const k = Math.min(1, p.t / p.dur);
      p.cx = p.x + (p.tx - p.x) * k;
      p.cy = p.y + (p.ty - p.y) * k - Math.sin(k * Math.PI) * 60; // arc
      if (k >= 1) { p.onArrive(); this.projectiles.splice(i, 1); }
    }
  }

  _draw() {
    const ctx = this.ctx, w = this.w, h = this.h;
    ctx.save();
    if (this.shake > 0.5) ctx.translate(rand(-this.shake, this.shake), rand(-this.shake, this.shake));

    // backdrop
    const bg = ctx.createLinearGradient(0, 0, 0, h);
    bg.addColorStop(0, "#0b0a1a"); bg.addColorStop(0.55, "#141029"); bg.addColorStop(1, "#1d1136");
    ctx.fillStyle = bg; ctx.fillRect(-40, -40, w + 80, h + 80);

    // stars
    for (const s of this.stars) {
      s.tw += 0.03;
      ctx.globalAlpha = 0.4 + Math.sin(s.tw) * 0.4;
      ctx.fillStyle = "#cfe9ff";
      ctx.fillRect(s.x * w, s.y * h, s.s, s.s);
    }
    ctx.globalAlpha = 1;

    this._drawFloor(ctx, w, h);

    // draw creatures back-to-front by y
    [this.A, this.B].sort((a, b) => a.y - b.y).forEach((c) => c.draw(ctx));

    // projectiles
    ctx.globalCompositeOperation = "lighter";
    for (const p of this.projectiles) {
      ctx.shadowColor = p.glow; ctx.shadowBlur = 24; ctx.fillStyle = p.color;
      ctx.beginPath(); ctx.arc(p.cx, p.cy, 12, 0, TAU); ctx.fill();
      ctx.globalAlpha = 0.5; ctx.fillStyle = "#fff";
      ctx.beginPath(); ctx.arc(p.cx, p.cy, 5, 0, TAU); ctx.fill();
      ctx.globalAlpha = 1;
    }
    ctx.globalCompositeOperation = "source-over";

    this.fx.draw(ctx);

    // vignette
    const v = ctx.createRadialGradient(w / 2, h / 2, h * 0.3, w / 2, h / 2, h * 0.8);
    v.addColorStop(0, "rgba(0,0,0,0)"); v.addColorStop(1, "rgba(0,0,0,0.55)");
    ctx.fillStyle = v; ctx.fillRect(-40, -40, w + 80, h + 80);
    ctx.restore();
  }

  _drawFloor(ctx, w, h) {
    const horizon = h * 0.62;
    ctx.save();
    ctx.strokeStyle = "rgba(120,90,220,0.25)"; ctx.lineWidth = 1;
    // perspective verticals
    for (let i = -10; i <= 10; i++) {
      const fx = w / 2 + i * (w / 14);
      ctx.beginPath(); ctx.moveTo(w / 2 + i * 12, horizon); ctx.lineTo(fx, h); ctx.stroke();
    }
    // horizontals getting denser toward horizon
    for (let i = 1; i <= 8; i++) {
      const y = horizon + (h - horizon) * (i / 8) ** 1.8;
      ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(w, y); ctx.stroke();
    }
    // center divide glow
    ctx.strokeStyle = "rgba(180,140,255,0.5)"; ctx.lineWidth = 2;
    ctx.shadowColor = "#8a6bff"; ctx.shadowBlur = 16;
    ctx.beginPath(); ctx.moveTo(w / 2, horizon); ctx.lineTo(w / 2, h); ctx.stroke();
    ctx.restore();
  }
}
