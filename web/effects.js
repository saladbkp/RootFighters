// Canvas 2D particle engine + the 9 per-category "attack" effects.
// Everything is procedural — no image assets — so it runs the instant you open
// the page. (Real 3D models/VFX are the Unity layer later; the *timing & color
// language* you tune here is what Unity will mirror.)

const TAU = Math.PI * 2;
const rand = (a, b) => a + Math.random() * (b - a);
const pick = (arr) => arr[(Math.random() * arr.length) | 0];
const MATRIX_GLYPHS = "01ｱｲｳｴｵｶｷｸ#$%&XYZ<>=/\\".split("");
const MATH_GLYPHS = "∑∫√≠≈λπΔΩ⊕→∞{}[]".split("");

export class Effects {
  constructor() {
    this.particles = [];
    this.bolts = []; // lightning is drawn specially
  }

  clear() { this.particles.length = 0; this.bolts.length = 0; }

  _add(p) {
    this.particles.push(Object.assign({
      x: 0, y: 0, vx: 0, vy: 0, life: 1, maxLife: 1, size: 4,
      color: "#fff", glow: "#fff", kind: "spark",
      rot: 0, vrot: 0, gravity: 0, drag: 1, grow: 0, text: "",
    }, p));
  }

  // o: { x, y, color, glow, fromX, fromY }  — impact point + attacker origin.
  spawn(effect, o) {
    const f = this["fx_" + effect];
    if (f) f.call(this, o);
    else this.fx_flash(o);
  }

  // ---- the nine signatures ------------------------------------------------ //

  fx_explosion({ x, y, color, glow }) {              // PWN — angular blast
    this._ring(x, y, color, 520, 7);
    this._ring(x, y, "#fff", 320, 4);
    for (let i = 0; i < 46; i++) {
      const a = rand(0, TAU), sp = rand(120, 620);
      this._add({
        x, y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp - 60,
        life: rand(0.4, 0.9), maxLife: 0.9, size: rand(6, 16),
        color, glow, kind: "shard", rot: a, vrot: rand(-12, 12),
        gravity: 900, drag: 0.93,
      });
    }
    for (let i = 0; i < 24; i++) {
      const a = rand(0, TAU), sp = rand(40, 200);
      this._add({ x, y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp,
        life: rand(0.5, 1.1), maxLife: 1.1, size: rand(20, 46),
        color: "#3a3a3a", glow: "#000", kind: "smoke", grow: 60, drag: 0.95 });
    }
  }

  fx_matrix({ x, y, color, glow }) {                 // WEB — code rain
    for (let i = 0; i < 60; i++) {
      this._add({
        x: x + rand(-150, 150), y: y + rand(-260, -40),
        vx: 0, vy: rand(180, 460), life: rand(0.6, 1.4), maxLife: 1.4,
        size: rand(14, 24), color, glow, kind: "glyph",
        text: pick(MATRIX_GLYPHS),
      });
    }
    this._ring(x, y, color, 360, 5);
  }

  fx_lightning({ x, y, color, glow, fromX, fromY }) { // WIFI — strike
    const sx = fromX ?? x, sy = (fromY ?? y) - 320;
    this.bolts.push(this._makeBolt(sx, sy, x, y, color, glow, 0.28, 6));
    this.bolts.push(this._makeBolt(sx, sy, x + rand(-40, 40), y, glow, "#fff", 0.2, 3));
    for (let i = 0; i < 30; i++) {
      const a = rand(0, TAU), sp = rand(120, 460);
      this._add({ x, y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp,
        life: rand(0.25, 0.6), maxLife: 0.6, size: rand(3, 7),
        color, glow, kind: "spark", drag: 0.9, gravity: 200 });
    }
    this._ring(x, y, color, 420, 5);
  }

  fx_gears({ x, y, color, glow }) {                  // REVERSE — gears + math
    for (let i = 0; i < 6; i++) {
      this._add({ x: x + rand(-80, 80), y: y + rand(-60, 60),
        vx: rand(-30, 30), vy: rand(-120, -40), life: rand(0.8, 1.5), maxLife: 1.5,
        size: rand(16, 34), color, glow, kind: "gear",
        rot: rand(0, TAU), vrot: rand(-6, 6), drag: 0.97 });
    }
    for (let i = 0; i < 22; i++) {
      this._add({ x: x + rand(-60, 60), y, vx: rand(-20, 20), vy: rand(-160, -60),
        life: rand(0.8, 1.6), maxLife: 1.6, size: rand(14, 22),
        color: glow, glow, kind: "glyph", text: pick(MATH_GLYPHS), drag: 0.98 });
    }
  }

  fx_psychic({ x, y, color, glow }) {                // FORENSICS — spiral aura
    for (let i = 0; i < 60; i++) {
      const a = (i / 60) * TAU * 3, r = 30 + i * 3.2;
      this._add({
        x: x + Math.cos(a) * r, y: y + Math.sin(a) * r,
        vx: -Math.cos(a) * 80, vy: -Math.sin(a) * 80,
        life: rand(0.7, 1.3), maxLife: 1.3, size: rand(4, 9),
        color, glow, kind: "spark", drag: 0.97 });
    }
    this._ring(x, y, glow, 300, 6);
    this._ring(x, y, color, 460, 3);
  }

  fx_vortex({ x, y, color, glow }) {                 // CRYPTO — energy vortex
    for (let i = 0; i < 70; i++) {
      const a = rand(0, TAU), r = rand(140, 260);
      this._add({
        x: x + Math.cos(a) * r, y: y + Math.sin(a) * r,
        vx: Math.cos(a + 1.6) * rand(160, 320), vy: Math.sin(a + 1.6) * rand(160, 320),
        life: rand(0.6, 1.2), maxLife: 1.2, size: rand(3, 8),
        color: i % 3 ? color : glow, glow, kind: "spark", drag: 0.94 });
    }
    this._ring(x, y, color, 380, 5);
  }

  fx_shadow({ x, y, color, glow }) {                 // IOT — shadow tendrils
    for (let i = 0; i < 40; i++) {
      const a = rand(0, TAU), sp = rand(40, 220);
      this._add({ x, y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp - 80,
        life: rand(0.7, 1.5), maxLife: 1.5, size: rand(18, 44),
        color: "#15151c", glow, kind: "smoke", grow: 40, drag: 0.95 });
    }
    for (let i = 0; i < 18; i++) {           // purple rim sparks so black reads
      const a = rand(0, TAU), sp = rand(120, 300);
      this._add({ x, y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp,
        life: rand(0.5, 1.0), maxLife: 1.0, size: rand(3, 6),
        color: glow, glow, kind: "spark", drag: 0.92 });
    }
    this._ring(x, y, glow, 360, 4);
  }

  fx_flash({ x, y, color, glow }) {                  // OSINT — light / radar
    this._ring(x, y, "#fff", 600, 9);
    this._ring(x, y, glow, 440, 4);
    this._ring(x, y, glow, 300, 3);
    for (let i = 0; i < 28; i++) {
      const a = rand(0, TAU), sp = rand(80, 360);
      this._add({ x, y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp,
        life: rand(0.4, 0.9), maxLife: 0.9, size: rand(4, 10),
        color: "#fff", glow, kind: "star", rot: rand(0, TAU), drag: 0.93 });
    }
  }

  fx_root({ x, y, color, glow }) {                   // B2R — root-shell crown
    this._ring(x, y, color, 480, 6);
    for (let i = 0; i < 7; i++) {                    // rising crown spikes
      this._add({ x: x + (i - 3) * 18, y, vx: rand(-10, 10), vy: rand(-260, -180),
        life: rand(0.8, 1.3), maxLife: 1.3, size: rand(10, 18),
        color, glow, kind: "shard", rot: -Math.PI / 2, vrot: 0,
        gravity: 260, drag: 0.99 });
    }
    for (let i = 0; i < 18; i++) {                   // terminal glitch glyphs
      this._add({ x: x + rand(-80, 80), y: y + rand(-20, 20),
        vx: rand(-40, 40), vy: rand(-120, -40), life: rand(0.6, 1.2), maxLife: 1.2,
        size: rand(13, 19), color: glow, glow, kind: "glyph",
        text: pick(["#", "$", "~", "root", "0x", "/"]), drag: 0.97 });
    }
    for (let i = 0; i < 24; i++) {
      const a = rand(-1.4, -1.74) - 0; // upward-ish gold sparks
      this._add({ x, y, vx: Math.cos(a) * rand(120, 320), vy: Math.sin(a) * rand(180, 420),
        life: rand(0.5, 1.0), maxLife: 1.0, size: rand(3, 6),
        color, glow, kind: "spark", gravity: 500, drag: 0.96 });
    }
  }

  // For WRONG: a small dull self-hit (no big damage to the other team).
  fx_miss({ x, y }) {
    this._ring(x, y, "#ff5252", 200, 4);
    for (let i = 0; i < 14; i++) {
      const a = rand(0, TAU), sp = rand(60, 180);
      this._add({ x, y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp,
        life: rand(0.3, 0.6), maxLife: 0.6, size: rand(3, 6),
        color: "#ff5252", glow: "#ff9a9a", kind: "spark", drag: 0.9, gravity: 300 });
    }
  }

  // ---- helpers ------------------------------------------------------------ //
  _ring(x, y, color, grow, width) {
    this._add({ x, y, life: 0.6, maxLife: 0.6, size: width, color, glow: color,
      kind: "ring", grow, r: 8 });
  }

  _makeBolt(x1, y1, x2, y2, color, glow, life, width) {
    const segs = 14, pts = [];
    for (let i = 0; i <= segs; i++) {
      const t = i / segs;
      const jitter = i === 0 || i === segs ? 0 : rand(-26, 26);
      const nx = -(y2 - y1), ny = (x2 - x1);
      const len = Math.hypot(nx, ny) || 1;
      pts.push({ x: x1 + (x2 - x1) * t + (nx / len) * jitter,
                 y: y1 + (y2 - y1) * t + (ny / len) * jitter });
    }
    return { pts, life, maxLife: life, color, glow, width };
  }

  // ---- simulation --------------------------------------------------------- //
  update(dt) {
    const ps = this.particles;
    for (let i = ps.length - 1; i >= 0; i--) {
      const p = ps[i];
      p.life -= dt;
      if (p.life <= 0) { ps.splice(i, 1); continue; }
      if (p.kind === "ring") { p.r += p.grow * dt; continue; }
      p.vy += p.gravity * dt;
      p.vx *= p.drag; p.vy *= p.drag;
      p.x += p.vx * dt; p.y += p.vy * dt;
      p.rot += p.vrot * dt;
      if (p.kind === "smoke") p.size += p.grow * dt;
    }
    for (let i = this.bolts.length - 1; i >= 0; i--) {
      this.bolts[i].life -= dt;
      if (this.bolts[i].life <= 0) this.bolts.splice(i, 1);
    }
  }

  draw(ctx) {
    ctx.save();
    ctx.globalCompositeOperation = "lighter";
    for (const p of this.particles) {
      const a = Math.max(0, p.life / p.maxLife);
      ctx.globalAlpha = a;
      ctx.shadowBlur = 18; ctx.shadowColor = p.glow;
      ctx.fillStyle = p.color; ctx.strokeStyle = p.color;
      switch (p.kind) {
        case "ring":
          ctx.globalCompositeOperation = "screen";
          ctx.lineWidth = p.size; ctx.beginPath();
          ctx.arc(p.x, p.y, p.r, 0, TAU); ctx.stroke();
          ctx.globalCompositeOperation = "lighter";
          break;
        case "shard": this._tri(ctx, p); break;
        case "gear": this._gear(ctx, p); break;
        case "star": this._star(ctx, p); break;
        case "glyph":
          ctx.font = `bold ${p.size}px "Courier New", monospace`;
          ctx.fillText(p.text, p.x, p.y); break;
        case "smoke":
          ctx.globalCompositeOperation = "source-over";
          ctx.globalAlpha = a * 0.5;
          ctx.beginPath(); ctx.arc(p.x, p.y, p.size, 0, TAU); ctx.fill();
          ctx.globalCompositeOperation = "lighter"; break;
        default:
          ctx.beginPath(); ctx.arc(p.x, p.y, p.size, 0, TAU); ctx.fill();
      }
    }
    for (const b of this.bolts) {
      ctx.globalAlpha = Math.max(0, b.life / b.maxLife) * (0.6 + Math.random() * 0.4);
      ctx.strokeStyle = b.color; ctx.shadowColor = b.glow; ctx.shadowBlur = 24;
      ctx.lineWidth = b.width; ctx.lineJoin = "round"; ctx.beginPath();
      b.pts.forEach((pt, i) => {
        const jx = i && i < b.pts.length - 1 ? rand(-4, 4) : 0;
        const jy = i && i < b.pts.length - 1 ? rand(-4, 4) : 0;
        i ? ctx.lineTo(pt.x + jx, pt.y + jy) : ctx.moveTo(pt.x, pt.y);
      });
      ctx.stroke();
    }
    ctx.restore();
  }

  _tri(ctx, p) {
    ctx.save(); ctx.translate(p.x, p.y); ctx.rotate(p.rot);
    ctx.beginPath();
    ctx.moveTo(0, -p.size); ctx.lineTo(p.size, p.size); ctx.lineTo(-p.size, p.size);
    ctx.closePath(); ctx.fill(); ctx.restore();
  }

  _gear(ctx, p) {
    ctx.save(); ctx.translate(p.x, p.y); ctx.rotate(p.rot);
    ctx.lineWidth = Math.max(2, p.size * 0.18);
    const teeth = 8;
    ctx.beginPath();
    for (let i = 0; i < teeth; i++) {
      const a0 = (i / teeth) * TAU, a1 = ((i + 0.5) / teeth) * TAU;
      ctx.lineTo(Math.cos(a0) * p.size, Math.sin(a0) * p.size);
      ctx.lineTo(Math.cos(a1) * p.size * 0.7, Math.sin(a1) * p.size * 0.7);
    }
    ctx.closePath(); ctx.stroke();
    ctx.beginPath(); ctx.arc(0, 0, p.size * 0.32, 0, TAU); ctx.stroke();
    ctx.restore();
  }

  _star(ctx, p) {
    ctx.save(); ctx.translate(p.x, p.y); ctx.rotate(p.rot);
    ctx.beginPath();
    for (let i = 0; i < 4; i++) {
      const a = (i / 4) * TAU;
      ctx.lineTo(Math.cos(a) * p.size, Math.sin(a) * p.size);
      ctx.lineTo(Math.cos(a + 0.4) * p.size * 0.3, Math.sin(a + 0.4) * p.size * 0.3);
    }
    ctx.closePath(); ctx.fill(); ctx.restore();
  }
}
