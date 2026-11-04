// QUADRA importer step 2: one 4096x2048 colour atlas for the whole car (ffmpeg) + a cell table for the importer.
// usage: node quadra-atlas.js <QuadraSource dir> [ffmpeg]
//   ext shell | interior | rear rim | front rim | chassis (1024 each) | tyre tread 3072x1024 (the 1024 tile laid 3x)
// Cells are given in Unity UV space (origin bottom-left). mode: "wrap" = uv fract (tiled sources), "u3" = uv.u / 3 (the tyre tiles 3x around), "const" = whole cell is one colour.
const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');
const os = require('os');
const dir = process.argv[2];
const ffmpeg = process.argv[3] || 'C:\\Program Files\\Virtual Desktop Streamer\\ffmpeg.exe';
const find = n => { for (const e of ['jpg', 'png']) { const p = path.join(dir, `img_${n}.${e}`); if (fs.existsSync(p)) return p; } throw new Error('missing img_' + n); };
const SW = 4096, SH = 2048;   // every source stays at its native 1024 px (the tyre tread tile is laid 3x): no quality is thrown away, ユーザー compresses at upload
// material index -> { image, x, y, w, h (pixels, y from the top), mode }
const cells = [
  { mat: 7, image: 24, x: 0, y: 0, w: 1024, h: 1024, mode: 'wrap', name: 'ext', process: 'paint' },
  { mat: 35, image: 66, x: 1024, y: 0, w: 1024, h: 1024, mode: 'wrap', name: 'int' },
  { mat: 51, image: 90, x: 2048, y: 0, w: 1024, h: 1024, mode: 'wrap', name: 'rimR' },
  { mat: 52, image: 93, x: 3072, y: 0, w: 1024, h: 1024, mode: 'wrap', name: 'rimF' },
  { mat: 33, image: 60, x: 0, y: 1024, w: 1024, h: 1024, mode: 'wrap', name: 'chassis' },
  { mat: 48, image: 81, x: 1024, y: 1024, w: 3072, h: 1024, mode: 'u3', name: 'tire' },
];
// the number plate is plain near-black: it samples a dark spot of the shell cell
const PLATE = { mat: 65, name: 'plate', mode: 'const', u: 700 / SW, v: 1 - 950 / SH };
const inputs = []; const filters = [];
// the glb textures are game-derived layer masks, not paint: grey patches would read as dull primer on the finished car. Grey (low chroma) -> near black, saturated trim (gold) -> brighter.
function paint(file, w, h) {
  const raw = execFileSync(ffmpeg, ['-v', 'error', '-i', file, '-vf', `scale=${w}:${h}`, '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-'], { maxBuffer: 1 << 28 });
  const sm = (a, b, x) => { const t = Math.max(0, Math.min(1, (x - a) / (b - a))); return t * t * (3 - 2 * t); };
  for (let i = 0; i < raw.length; i += 3) {
    const r = raw[i], g = raw[i + 1], b = raw[i + 2]; const chroma = Math.max(r, g, b) - Math.min(r, g, b);
    const gain = 0.12 + (1.7 - 0.12) * sm(24, 32, chroma);
    raw[i] = Math.min(255, r * gain); raw[i + 1] = Math.min(255, g * gain); raw[i + 2] = Math.min(255, b * gain);
  }
  const tmp = path.join(os.tmpdir(), `quadra_paint_${w}.raw`); fs.writeFileSync(tmp, raw); return tmp;
}
cells.forEach((c, i) => {
  if (c.process === 'paint') inputs.push('-f', 'rawvideo', '-pixel_format', 'rgb24', '-video_size', `${c.w}x${c.h}`, '-i', paint(find(c.image), c.w, c.h));
  else inputs.push('-i', find(c.image));
  if (c.mode === 'u3') filters.push(`[${i}:v]scale=${Math.round(c.w / 3)}:${c.h},split=3[t0][t1][t2];[t0][t1][t2]hstack=3,scale=${c.w}:${c.h}[a${i}]`);
  else filters.push(`[${i}:v]scale=${c.w}:${c.h}[a${i}]`);
});
filters.push(`color=c=0x202020:s=${SW}x${SH}:d=1[bg]`);
let last = 'bg';
cells.forEach((c, i) => { filters.push(`[${last}][a${i}]overlay=${c.x}:${c.y}[b${i}]`); last = `b${i}`; });
const out = path.join(dir, 'quadra_atlas.png');
execFileSync(ffmpeg, ['-y', '-loglevel', 'error', ...inputs, '-filter_complex', filters.join(';').replace(`[${last}]`, `[${last}]`), '-map', `[${last}]`, '-frames:v', '1', '-pix_fmt', 'rgb24', out], { stdio: 'inherit' });
const table = cells.map(c => ({ mat: c.mat, name: c.name, mode: c.mode, u0: c.x / SW, v0: 1 - (c.y + c.h) / SH, su: c.w / SW, sv: c.h / SH }));
table.push({ mat: PLATE.mat, name: PLATE.name, mode: 'const', u0: PLATE.u, v0: PLATE.v, su: 0, sv: 0 });
fs.writeFileSync(path.join(dir, 'quadra_cells.json'), JSON.stringify({ width: SW, height: SH, cells: table }, null, 1));
console.log('atlas', out, fs.statSync(out).size, 'bytes;', table.length, 'cells');
