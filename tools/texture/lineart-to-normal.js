// Normal map from the Milfy lineart overlay: ink lines become grooves (seams), colored accents shallow grooves.
// in: lineart.rgba (4096x4096 RGBA raw)  out: normal.rgb (raw RGB), height.gray (raw), preview_lit.rgb (raw RGB)
const fs = require('fs');
const [,, inPath, outDir, strengthArg] = process.argv;
const W = 4096, H = 4096, N = W * H;
const src = fs.readFileSync(inPath);

// 1. groove mask: dark ink = full depth, colored ink (red stripes, logos) = shallow
const mask = new Float32Array(N);
for (let i = 0; i < N; i++) {
  const a = src[i * 4 + 3] / 255; if (a <= 0) continue;
  const r = src[i * 4], g = src[i * 4 + 1], b = src[i * 4 + 2];
  const lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
  const sat = (Math.max(r, g, b) - Math.min(r, g, b)) / 255;
  const dark = 1 - lum;                      // black ink -> 1
  const depth = sat > 0.25 ? 0.35 * a : dark * a;
  mask[i] = depth;
}

// separable box blur x3 ~ gaussian
function boxBlur(srcA, dst, r) {
  const tmp = new Float32Array(N);
  const k = 1 / (2 * r + 1);
  for (let y = 0; y < H; y++) {
    const o = y * W; let s = 0;
    for (let x = -r; x <= r; x++) s += srcA[o + Math.min(W - 1, Math.max(0, x))];
    for (let x = 0; x < W; x++) {
      tmp[o + x] = s * k;
      const xo = x - r, xi = x + r + 1;
      s += srcA[o + Math.min(W - 1, xi)] - srcA[o + Math.max(0, xo)];
    }
  }
  for (let x = 0; x < W; x++) {
    let s = 0;
    for (let y = -r; y <= r; y++) s += tmp[Math.min(H - 1, Math.max(0, y)) * W + x];
    for (let y = 0; y < H; y++) {
      dst[y * W + x] = s * k;
      const yo = y - r, yi = y + r + 1;
      s += tmp[Math.min(H - 1, yi) * W + x] - tmp[Math.max(0, yo) * W + x];
    }
  }
}
function gauss(a, sigma) {
  const r = Math.max(1, Math.round(sigma * 0.9));
  let p = a, q = new Float32Array(N);
  for (let i = 0; i < 3; i++) { boxBlur(p, q, r); const t = p === a ? new Float32Array(N) : p; p = q; q = t; }
  return p;
}

// 2. height: sharp groove + soft bevel around it (negative = recessed)
const narrow = gauss(mask, 1.6);
const wide = gauss(mask, 7);
const height = new Float32Array(N);
let hmax = 0;
for (let i = 0; i < N; i++) { const h = -(narrow[i] + 0.35 * wide[i]); height[i] = h; if (-h > hmax) hmax = -h; }
for (let i = 0; i < N; i++) height[i] /= hmax;   // -1..0

// 3. gradients (Sobel), strength normalised so the 99.5th percentile slope is ~55 deg
const gx = new Float32Array(N), gy = new Float32Array(N);
const at = (x, y) => height[Math.min(H - 1, Math.max(0, y)) * W + Math.min(W - 1, Math.max(0, x))];
const mags = [];
for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
  const i = y * W + x;
  const dx = (at(x + 1, y - 1) + 2 * at(x + 1, y) + at(x + 1, y + 1) - at(x - 1, y - 1) - 2 * at(x - 1, y) - at(x - 1, y + 1)) / 8;
  const dy = (at(x - 1, y + 1) + 2 * at(x, y + 1) + at(x + 1, y + 1) - at(x - 1, y - 1) - 2 * at(x, y - 1) - at(x + 1, y - 1)) / 8;
  gx[i] = dx; gy[i] = dy;
  if ((i & 63) === 0) { const m = Math.hypot(dx, dy); if (m > 1e-4) mags.push(m); }
}
mags.sort((a, b) => a - b);
const p995 = mags[Math.floor(mags.length * 0.995)] || 1;
const strength = strengthArg ? parseFloat(strengthArg) : Math.tan(55 * Math.PI / 180) / p995;
console.log('groove px', mask.reduce((c, v) => c + (v > 0.05 ? 1 : 0), 0), 'p99.5 slope', p995.toFixed(4), 'strength', strength.toFixed(2));

// 4. tangent-space normal, OpenGL/Unity convention: R = +X right, G = +Y up (texture V up), B out
const nrm = Buffer.alloc(N * 3);
const hgt = Buffer.alloc(N);
const lit = Buffer.alloc(N * 3);
const L = [-0.5, 0.6, 0.62]; const ll = Math.hypot(...L); L[0] /= ll; L[1] /= ll; L[2] /= ll; // light from upper-left
for (let i = 0; i < N; i++) {
  let nx = -gx[i] * strength, ny = gy[i] * strength, nz = 1;  // gy is in image coords (down), flip for V-up
  const inv = 1 / Math.hypot(nx, ny, nz); nx *= inv; ny *= inv; nz *= inv;
  nrm[i * 3] = Math.round((nx * 0.5 + 0.5) * 255); nrm[i * 3 + 1] = Math.round((ny * 0.5 + 0.5) * 255); nrm[i * 3 + 2] = Math.round((nz * 0.5 + 0.5) * 255);
  hgt[i] = Math.round((1 + height[i]) * 255);
  const d = Math.max(0, nx * L[0] + ny * L[1] + nz * L[2]);
  const spec = Math.pow(Math.max(0, nz * 0.95 + ny * 0.3), 24) * 0.25;
  const base = 0.62 * (0.25 + 0.75 * d) + spec;
  // show the ink faintly so the grooves can be compared with the drawn lines
  const a = src[i * 4 + 3] / 255; const v = Math.round(255 * (base * (1 - 0.25 * a)));
  lit[i * 3] = v; lit[i * 3 + 1] = v; lit[i * 3 + 2] = v;
}
fs.writeFileSync(outDir + '/normal.rgb', nrm);
fs.writeFileSync(outDir + '/height.gray', hgt);
fs.writeFileSync(outDir + '/preview_lit.rgb', lit);
console.log('done');
