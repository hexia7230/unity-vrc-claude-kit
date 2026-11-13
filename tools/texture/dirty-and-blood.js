// Dirty + blood stains for sleepdress white.png (4096x4096 RGBA raw). Only the garment islands are touched; background stays pure white.
const fs = require('fs');
const [,, inPath, outPath] = process.argv;
const W = 4096, H = 4096, N = W * H;
const src = fs.readFileSync(inPath);

// ---------- deterministic rng + noise ----------
let seed = 20261031;
const rnd = () => { seed = (seed * 1664525 + 1013904223) >>> 0; return seed / 4294967296; };
const hash2 = (x, y, s) => { let h = (x * 374761393 + y * 668265263 + s * 2147483647) | 0; h = (h ^ (h >>> 13)) * 1274126177 | 0; return ((h ^ (h >>> 16)) >>> 0) / 4294967296; };
function vnoise(x, y, s) {
  const xi = Math.floor(x), yi = Math.floor(y), xf = x - xi, yf = y - yi;
  const u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
  const a = hash2(xi, yi, s), b = hash2(xi + 1, yi, s), c = hash2(xi, yi + 1, s), d = hash2(xi + 1, yi + 1, s);
  return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
}
function fbm(x, y, s, oct) { let a = 0.5, f = 1, t = 0, n = 0; for (let i = 0; i < oct; i++) { t += a * vnoise(x * f, y * f, s + i * 17); n += a; a *= 0.5; f *= 2; } return t / n; }
function fbmField(freq, s, oct) { const f = new Float32Array(N); for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) f[y * W + x] = fbm(x * freq, y * freq, s, oct); return f; }

// ---------- garment mask: flood-fill the pure-white background from the border ----------
const isWhite = i => src[i * 4] >= 252 && src[i * 4 + 1] >= 252 && src[i * 4 + 2] >= 252;
const bg = new Uint8Array(N);
{
  const stack = new Int32Array(N); let sp = 0;
  const push = (x, y) => { const i = y * W + x; if (!bg[i] && isWhite(i)) { bg[i] = 1; stack[sp++] = i; } };
  for (let x = 0; x < W; x++) { push(x, 0); push(x, H - 1); }
  for (let y = 0; y < H; y++) { push(0, y); push(W - 1, y); }
  while (sp > 0) { const i = stack[--sp]; const x = i % W, y = (i / W) | 0; if (x > 0) push(x - 1, y); if (x < W - 1) push(x + 1, y); if (y > 0) push(x, y - 1); if (y < H - 1) push(x, y + 1); }
}
// white pixels trapped inside a garment island keep garment status; thin antialiased fringe of the background is treated as garment (it blends back to white below)
const garment = new Float32Array(N);
for (let i = 0; i < N; i++) garment[i] = bg[i] ? 0 : 1;

function boxBlur(a, r) {
  const tmp = new Float32Array(N), out = new Float32Array(N), k = 1 / (2 * r + 1);
  for (let y = 0; y < H; y++) { const o = y * W; let s = 0; for (let x = -r; x <= r; x++) s += a[o + Math.min(W - 1, Math.max(0, x))];
    for (let x = 0; x < W; x++) { tmp[o + x] = s * k; s += a[o + Math.min(W - 1, x + r + 1)] - a[o + Math.max(0, x - r)]; } }
  for (let x = 0; x < W; x++) { let s = 0; for (let y = -r; y <= r; y++) s += tmp[Math.min(H - 1, Math.max(0, y)) * W + x];
    for (let y = 0; y < H; y++) { out[y * W + x] = s * k; s += tmp[Math.min(H - 1, y + r + 1) * W + x] - tmp[Math.max(0, y - r) * W + x]; } }
  return out;
}
const inner1 = boxBlur(boxBlur(garment, 18), 18);   // ~0 at the island edge, 1 deep inside
const edgeness = new Float32Array(N); for (let i = 0; i < N; i++) edgeness[i] = Math.max(0, 1 - Math.min(1, inner1[i] * 1.6));

// ---------- dirt layers ----------
console.log('noise...');
const nLarge = fbmField(1 / 520, 11, 5);   // big stains / uneven yellowing
const nMid = fbmField(1 / 140, 23, 5);     // mottling
const nFine = fbmField(1 / 22, 37, 3);     // fabric grain

// ---------- blood ----------
// blobs: x, y (px), radius, strength; drips: start x,y, length, width
const blobs = [
  { x: 1050, y: 3050, r: 330, s: 1.0 },  // big stain on the skirt, left of centre
  { x: 1180, y: 2900, r: 150, s: 0.9 },
  { x: 2900, y: 3450, r: 240, s: 0.9 },  // second stain, right side
  { x: 2050, y: 3700, r: 120, s: 0.8 },  // hem
  { x: 2060, y: 1500, r: 170, s: 0.95 }, // bodice, lower
  { x: 2110, y: 1720, r: 90, s: 0.8 },
  { x: 2560, y: 560, r: 70, s: 0.7 },    // strap piece
  { x: 3500, y: 1300, r: 110, s: 0.8 },  // right sleeve piece
];
const drips = [];
for (const b of blobs) if (b.r > 100) { const n = 2 + Math.floor(rnd() * 3); for (let k = 0; k < n; k++) { const y0 = b.y + b.r * (-0.1 + rnd() * 0.2); const yEnd = b.y + b.r * (0.85 + rnd() * 0.3) + 120 + rnd() * 480; drips.push({ x: b.x + (rnd() - 0.5) * b.r * 1.2, y: y0, len: yEnd - y0, w: 9 + rnd() * 14 }); } }
const drops = [];
for (const b of blobs) { const n = Math.floor(b.r / 7); for (let k = 0; k < n; k++) { const a = rnd() * 6.283, d = b.r * (0.9 + rnd() * 2.4); drops.push({ x: b.x + Math.cos(a) * d, y: b.y + Math.sin(a) * d * 0.9, r: 2 + Math.pow(rnd(), 2.6) * 12 }); } }
console.log('blobs', blobs.length, 'drips', drips.length, 'drops', drops.length);

// blood amount (0..1) and wetness/age (0 = dried brown, 1 = fresher red)
const blood = new Float32Array(N), fresh = new Float32Array(N);
function stamp(cx, cy, rad, fn) {
  const x0 = Math.max(0, Math.floor(cx - rad)), x1 = Math.min(W - 1, Math.ceil(cx + rad)), y0 = Math.max(0, Math.floor(cy - rad)), y1 = Math.min(H - 1, Math.ceil(cy + rad));
  for (let y = y0; y < y1 + 1; y++) for (let x = x0; x < x1 + 1; x++) fn(x, y, y * W + x);
}
for (const b of blobs) {
  const R = b.r * 1.9;
  stamp(b.x, b.y, R, (x, y, i) => {
    const wx = x + (fbm(x / 90, y / 90, 91, 3) - 0.5) * b.r * 1.1, wy = y + (fbm(x / 90 + 7, y / 90 + 3, 92, 3) - 0.5) * b.r * 1.1;   // domain warp -> ragged edge
    const d = Math.hypot(wx - b.x, (wy - b.y) * 0.9) / b.r;
    const edge = Math.max(0, 1 - d), core = Math.min(1, edge * 8);
    const a = core * b.s;
    if (a > blood[i]) { blood[i] = a; fresh[i] = Math.max(fresh[i], Math.min(1, edge * 1.4) * 0.8); }
  });
}
for (const d of drips) {
  stamp(d.x, d.y + d.len / 2, d.len / 2 + 60, (x, y, i) => {
    const t = (y - d.y) / d.len; if (t < 0 || t > 1.12) return;
    const wob = (fbm(y / 70, d.x / 50, 93, 2) - 0.5) * 14;
    const w = d.w * (1 - 0.65 * Math.min(1, t)) * (0.8 + 0.4 * fbm(y / 25, d.x, 94, 2));
    const e = Math.abs(x - (d.x + wob)) / w; if (e > 1) return;
    let a = (1 - e * e) * (t > 0.9 ? Math.max(0, 1 - (t - 0.9) / 0.1) : 1) * 0.95;
    const bead = Math.hypot(x - (d.x + wob), y - (d.y + d.len)) / (d.w * 0.95); if (bead < 1) a = Math.max(a, 1 - bead * bead);
    if (a > blood[i]) { blood[i] = a; fresh[i] = Math.max(fresh[i], 0.55 * (1 - t) + 0.2); }
  });
}
for (const dr of drops) {
  const sx = 1 + rnd() * 0.6, rot = rnd() * 3.14, c = Math.cos(rot), s = Math.sin(rot);
  stamp(dr.x, dr.y, dr.r * 2, (x, y, i) => {
    const dx = x - dr.x, dy = y - dr.y, u = (dx * c + dy * s) / sx, v = -dx * s + dy * c;
    const e = Math.hypot(u, v) / dr.r; if (e > 1) return;
    const a = Math.min(1, (1 - e) * 3) * 0.95;
    if (a > blood[i]) { blood[i] = a; fresh[i] = Math.max(fresh[i], 0.5); }
  });
}

// ---------- compose ----------
console.log('compose...');
const out = Buffer.from(src);
const lerp = (a, b, t) => a + (b - a) * t;
const bloodDark = [0.10, 0.022, 0.022], bloodDry = [0.27, 0.04, 0.035], bloodFresh = [0.46, 0.035, 0.04];
const grime = [0.50, 0.40, 0.29], tea = [0.80, 0.68, 0.46];
const HEM = 2330;                    // skirt top edge in px
const stats = { blood: 0 };
for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
  const i = y * W + x; const g = garment[i]; if (g === 0) continue;
  let r = src[i * 4] / 255, gr = src[i * 4 + 1] / 255, b = src[i * 4 + 2] / 255;
  const lum = (r + gr + b) / 3;
  const grain = nFine[i];
  // 1) uneven yellowing (tea tint) in large patches
  const yel = Math.max(0, (nLarge[i] - 0.42) * 2.2);
  const ty = Math.min(0.4, yel * 0.45);
  r = lerp(r, r * tea[0], ty); gr = lerp(gr, gr * tea[1], ty); b = lerp(b, b * tea[2], ty);
  // 2) grime: edges, mottling, hem gets dirtier, dirt gathers in the dark folds
  const hemT = y > HEM ? Math.pow((y - HEM) / (H - HEM), 1.6) : 0;
  const fold = Math.max(0, 0.9 - lum) * 2.0;
  let dirt = 0.12 + 0.30 * edgeness[i] + 0.55 * hemT * (0.5 + nMid[i]) + 0.25 * fold + 0.35 * Math.max(0, nMid[i] - 0.5) + 0.18 * Math.max(0, nLarge[i] - 0.55) * 2;
  dirt = Math.min(0.88, dirt * (0.75 + 0.5 * grain));
  r = lerp(r, r * grime[0], dirt); gr = lerp(gr, gr * grime[1], dirt); b = lerp(b, b * grime[2], dirt);
  // 3) water rings: faint dark outline of old stains
  const ring = Math.exp(-Math.pow((nLarge[i] - 0.60) / 0.05, 2)) * 0.07;
  r *= 1 - ring * 0.5; gr *= 1 - ring * 0.5; b *= 1 - ring * 0.6;
  // 4) blood: soaks into the fabric, the folds of the base still show through
  const a = Math.min(1, blood[i] * (0.75 + 0.5 * nMid[i]) * (0.85 + 0.3 * grain));
  if (a > 0.003) {
    const fr = fresh[i] * 0.55 * (0.5 + 0.8 * nMid[i]);
    let c0, c1, c2;
    const dry = Math.min(1, Math.max(0, 1 - fr * 1.2));
    c0 = lerp(bloodFresh[0], bloodDry[0], dry); c1 = lerp(bloodFresh[1], bloodDry[1], dry); c2 = lerp(bloodFresh[2], bloodDry[2], dry);
    const rim = Math.max(0, 1 - a * 1.9);               // darker, browner crust at the edge
    c0 = lerp(c0, bloodDark[0], rim * 0.8); c1 = lerp(c1, bloodDark[1], rim * 0.8); c2 = lerp(c2, bloodDark[2], rim * 0.8);
    const oL = (src[i * 4] + src[i * 4 + 1] + src[i * 4 + 2]) / 765; const shade = Math.max(0.3, Math.min(1.25, 0.55 + 2.4 * (oL - 0.62)));   // keep the fabric shading
    const t = Math.min(1, a * 1.15);
    r = lerp(r, c0 * shade, t); gr = lerp(gr, c1 * shade, t); b = lerp(b, c2 * shade, t);
    stats.blood++;
  }
  // blend back to the original white where the background fringe is
  const orig = [src[i * 4] / 255, src[i * 4 + 1] / 255, src[i * 4 + 2] / 255];
  const k = Math.min(1, inner1[i] * 3);        // 0 right at the island edge
  const kk = k * k * (3 - 2 * k) * 0 + Math.min(1, 0.55 + k);   // edge pixels keep a little of the original so the outline is not eaten
  out[i * 4] = Math.max(0, Math.min(255, Math.round(255 * lerp(orig[0], r, kk))));
  out[i * 4 + 1] = Math.max(0, Math.min(255, Math.round(255 * lerp(orig[1], gr, kk))));
  out[i * 4 + 2] = Math.max(0, Math.min(255, Math.round(255 * lerp(orig[2], b, kk))));
}
fs.writeFileSync(outPath, out);
console.log('blood pixels', stats.blood, 'done');
