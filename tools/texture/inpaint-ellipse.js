// Remove the paw logo: fill an ellipse around it by diffusing the surrounding pixels inward (Jacobi), per channel.
// usage: node remove_paw.js in.rgba out.rgba cx cy rx ry
const fs = require('fs');
const [,, inPath, outPath, cxs, cys, rxs, rys] = process.argv;
const W = 4096, H = 4096;
const cx = +cxs, cy = +cys, rx = +rxs, ry = +rys;
const img = fs.readFileSync(inPath);
const x0 = Math.max(0, Math.floor(cx - rx - 4)), x1 = Math.min(W - 1, Math.ceil(cx + rx + 4));
const y0 = Math.max(0, Math.floor(cy - ry - 4)), y1 = Math.min(H - 1, Math.ceil(cy + ry + 4));
const bw = x1 - x0 + 1, bh = y1 - y0 + 1;
const inside = new Uint8Array(bw * bh);
let cnt = 0;
for (let y = 0; y < bh; y++) for (let x = 0; x < bw; x++) {
  const dx = (x + x0 - cx) / rx, dy = (y + y0 - cy) / ry;
  if (dx * dx + dy * dy <= 1) { inside[y * bw + x] = 1; cnt++; }
}
const ch = [new Float32Array(bw * bh), new Float32Array(bw * bh), new Float32Array(bw * bh), new Float32Array(bw * bh)];
for (let y = 0; y < bh; y++) for (let x = 0; x < bw; x++) { const si = ((y + y0) * W + (x + x0)) * 4; for (let c = 0; c < 4; c++) ch[c][y * bw + x] = img[si + c]; }
// initialise interior with the mean of the boundary ring, then iterate
for (let c = 0; c < 4; c++) {
  let s = 0, n = 0;
  for (let y = 1; y < bh - 1; y++) for (let x = 1; x < bw - 1; x++) { const i = y * bw + x; if (!inside[i] && (inside[i - 1] || inside[i + 1] || inside[i - bw] || inside[i + bw])) { s += ch[c][i]; n++; } }
  const m = n ? s / n : 128;
  for (let i = 0; i < bw * bh; i++) if (inside[i]) ch[c][i] = m;
  let a = ch[c], b = new Float32Array(a);
  for (let it = 0; it < 4000; it++) {
    let maxd = 0;
    for (let y = 1; y < bh - 1; y++) for (let x = 1; x < bw - 1; x++) {
      const i = y * bw + x; if (!inside[i]) continue;
      const v = 0.25 * (a[i - 1] + a[i + 1] + a[i - bw] + a[i + bw]);
      maxd = Math.max(maxd, Math.abs(v - a[i])); b[i] = v;
    }
    const t = a; a = b; b = t;
    if (maxd < 0.002) break;
  }
  ch[c] = a;
}
const out = Buffer.from(img);
for (let y = 0; y < bh; y++) for (let x = 0; x < bw; x++) {
  const i = y * bw + x; if (!inside[i]) continue;
  const di = ((y + y0) * W + (x + x0)) * 4;
  for (let c = 0; c < 4; c++) out[di + c] = Math.max(0, Math.min(255, Math.round(ch[c][i])));
}
fs.writeFileSync(outPath, out);
console.log('filled', cnt, 'px at', cx, cy, rx, ry);
