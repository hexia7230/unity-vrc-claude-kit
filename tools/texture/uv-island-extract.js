const fs = require('fs');
const W = 4096, H = 4096, N = W * H;
const src = fs.readFileSync('face.rgba');
// background colour = the corner pixel (uniform grey)
const bg = [src[0], src[1], src[2]];
console.log('bg', bg, 'alpha', src[3]);
const fg = new Uint8Array(N);
for (let i = 0; i < N; i++) { const d = Math.abs(src[i*4]-bg[0]) + Math.abs(src[i*4+1]-bg[1]) + Math.abs(src[i*4+2]-bg[2]); if (d > 9) fg[i] = 1; }
// connected components (4-neighbour) over fg; label the two containing the seed points
const label = new Int32Array(N); let next = 0; const stack = new Int32Array(N);
const seeds = [[614, 629], [3482, 629]];
const keep = new Set();
const sizes = {};
for (let s = 0; s < N; s++) {
  if (!fg[s] || label[s]) continue;
  next++; let sp = 0; stack[sp++] = s; label[s] = next; let cnt = 0;
  while (sp > 0) { const i = stack[--sp]; cnt++; const x = i % W, y = (i / W) | 0;
    if (x > 0 && fg[i-1] && !label[i-1]) { label[i-1] = next; stack[sp++] = i-1; }
    if (x < W-1 && fg[i+1] && !label[i+1]) { label[i+1] = next; stack[sp++] = i+1; }
    if (y > 0 && fg[i-W] && !label[i-W]) { label[i-W] = next; stack[sp++] = i-W; }
    if (y < H-1 && fg[i+W] && !label[i+W]) { label[i+W] = next; stack[sp++] = i+W; } }
  sizes[next] = cnt;
}
for (const [x, y] of seeds) { const l = label[y*W + x]; console.log('seed', x, y, 'label', l, 'size', sizes[l]); if (l) keep.add(l); }
// fill holes inside the kept islands (grey pixels fully enclosed): mark everything reachable from the border through non-kept pixels; the rest is inside
const outside = new Uint8Array(N); { let sp = 0; const push = (i) => { if (!outside[i] && !keep.has(label[i])) { outside[i] = 1; stack[sp++] = i; } };
  for (let x = 0; x < W; x++) { push(x); push((H-1)*W + x); } for (let y = 0; y < H; y++) { push(y*W); push(y*W + W-1); }
  while (sp > 0) { const i = stack[--sp]; const x = i % W, y = (i / W) | 0; if (x > 0) push(i-1); if (x < W-1) push(i+1); if (y > 0) push(i-W); if (y < H-1) push(i+W); } }
const out = Buffer.alloc(N * 4); let kept = 0; let minx = W, maxx = 0, miny = H, maxy = 0;
for (let i = 0; i < N; i++) {
  if (!outside[i]) { out[i*4] = src[i*4]; out[i*4+1] = src[i*4+1]; out[i*4+2] = src[i*4+2]; out[i*4+3] = 255; kept++; const x = i % W, y = (i / W) | 0; if (x < minx) minx = x; if (x > maxx) maxx = x; if (y < miny) miny = y; if (y > maxy) maxy = y; }
  else { out[i*4] = src[i*4]; out[i*4+1] = src[i*4+1]; out[i*4+2] = src[i*4+2]; out[i*4+3] = 0; }
}
console.log('kept px', kept, 'bbox', minx, maxx, miny, maxy);
fs.writeFileSync('eyes.rgba', out);
