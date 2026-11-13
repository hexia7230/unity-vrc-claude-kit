// QUADRA importer step 1: glb -> JSON in Unity space (x, y, -z; winding reversed; uv.v flipped), classified into parts and material groups.
// usage: node quadra-dump.js <glb> <outDir>   (writes quadra.json + the images the kept groups need, as img_N.jpg/png)
//
// Parts:  Body (shell, interior, bumpers, lights, glass), DoorL / DoorR (slide + tilt open), WheelFL / WheelFR / WheelBL / WheelBR (tyre + rim)
// Groups: body (opaque, atlas), tire (tiled), glass (clear), glassR (tail-light covers, red), lights (emissive palette)
// Dropped on purpose: bobble head, engine bay, hinge arms, pedals, dome light, UI screens, buttons, stitching, labels, decals, stickers, red/white tyre lettering.
const fs = require('fs');
const path = require('path');
const [, , glbPath, outDir] = process.argv;
const b = fs.readFileSync(glbPath);
const jl = b.readUInt32LE(12);
const j = JSON.parse(b.slice(20, 20 + jl).toString());
const bin = b.slice(20 + jl + 8);

// ---------- accessors
const TYPE_N = { SCALAR: 1, VEC2: 2, VEC3: 3, VEC4: 4 };
function readAcc(i) {
  const a = j.accessors[i]; const bv = j.bufferViews[a.bufferView]; const n = TYPE_N[a.type];
  const base = (bv.byteOffset || 0) + (a.byteOffset || 0);
  const csize = { 5126: 4, 5125: 4, 5123: 2, 5121: 1 }[a.componentType];
  const stride = bv.byteStride || n * csize;
  const out = a.componentType === 5126 ? new Float32Array(a.count * n) : new Uint32Array(a.count * n);
  for (let k = 0; k < a.count; k++) for (let c = 0; c < n; c++) {
    const o = base + k * stride + c * csize;
    out[k * n + c] = a.componentType === 5126 ? bin.readFloatLE(o) : a.componentType === 5125 ? bin.readUInt32LE(o) : a.componentType === 5123 ? bin.readUInt16LE(o) : bin.readUInt8(o);
  }
  return out;
}

// ---------- node world matrices (column-major 4x4, glTF right-handed)
function mul(a, b2) { const o = new Array(16).fill(0); for (let c = 0; c < 4; c++) for (let r = 0; r < 4; r++) for (let k = 0; k < 4; k++) o[c * 4 + r] += a[k * 4 + r] * b2[c * 4 + k]; return o; }
function trs(n) {
  if (n.matrix) return n.matrix.slice();
  const t = n.translation || [0, 0, 0], q = n.rotation || [0, 0, 0, 1], s = n.scale || [1, 1, 1];
  const [x, y, z, w] = q;
  const r = [1 - 2 * (y * y + z * z), 2 * (x * y + z * w), 2 * (x * z - y * w), 0, 2 * (x * y - z * w), 1 - 2 * (x * x + z * z), 2 * (y * z + x * w), 0, 2 * (x * z + y * w), 2 * (y * z - x * w), 1 - 2 * (x * x + y * y), 0, 0, 0, 0, 1];
  r[0] *= s[0]; r[1] *= s[0]; r[2] *= s[0]; r[4] *= s[1]; r[5] *= s[1]; r[6] *= s[1]; r[8] *= s[2]; r[9] *= s[2]; r[10] *= s[2];
  r[12] = t[0]; r[13] = t[1]; r[14] = t[2];
  return r;
}
const world = new Array(j.nodes.length);
const parent = new Array(j.nodes.length).fill(-1);
j.nodes.forEach((n, i) => (n.children || []).forEach(c => (parent[c] = i)));
function wm(i) { if (world[i]) return world[i]; const l = trs(j.nodes[i]); return (world[i] = parent[i] < 0 ? l : mul(wm(parent[i]), l)); }
function inv3T(m) { // inverse-transpose of the upper 3x3 (normals)
  const a = m[0], b3 = m[4], c = m[8], d = m[1], e = m[5], f = m[9], g = m[2], h = m[6], i = m[10];
  const A = e * i - f * h, B = -(d * i - f * g), C = d * h - e * g, D = -(b3 * i - c * h), E = a * i - c * g, F = -(a * h - b3 * g), G = b3 * f - c * e, H = -(a * f - c * d), I = a * e - b3 * d;
  const det = a * A + b3 * B + c * C || 1;
  return [A / det, B / det, C / det, D / det, E / det, F / det, G / det, H / det, I / det]; // rows of cofactor = inverse transpose
}

// ---------- classification
// mesh nodes are called Object_N; the meaningful name is the parent node
function nameOf(i) { let n = j.nodes[i].name; if (/^Object_[0-9]+$/.test(n) && parent[i] >= 0) n = j.nodes[parent[i]].name; return n; }
const dropNode = /^(bobble_head|ext01_engine|ext01_door_hinge|v_common__int01_pedal|v_common__int01_light)/;
function partOf(name, wmat) {
  const x = wmat[12]; // node origin x (glTF); tyres sit at x = +-0.78
  if (/^ext01_door_l/.test(name)) return 'DoorL';
  if (/^ext01_door_r/.test(name)) return 'DoorR';
  const m = name.match(/^ext01_(tire|wheel)_(fl|bl)/);
  if (m) return (m[2] === 'fl' ? 'WheelF' : 'WheelB') + (x < 0 ? 'L' : 'R');
  return 'Body';
}
const dropMat = /(bobble|stiches|dec_stickers|^GLB_stickers|stickers_barcode|roach|decals|windows_frame_sticker|vehicle_branding|buttons|blinking|ui_|_ui|interior_buttons|license_plate_holo|vehicle_lights|GLB_eye|GLB_emissive$|ml_tire_red|ml_tire_white|engine)/;
function groupOf(mat) {
  const n = mat.name;
  if (dropMat.test(n)) return 'drop';
  if (/_ON$/.test(n)) return 'lights';
  if (/glass_lightcovers_red|glass_lightcovers_orange/.test(n)) return 'glassR';
  if (/glass/.test(n)) return 'glass';
  if (/ml_tire$/.test(n)) return 'tire';
  return 'body';
}
// emissive palette for the lights group (swatch index -> colour); each *_ON material maps to the closest
const PALETTE = [[1, 0.6, 0.29], [1, 1, 1], [1, 0.22, 0], [1, 0, 0]];
function swatchOf(mat) { const e = mat.emissiveFactor || [1, 1, 1]; let best = 0, bd = 9; PALETTE.forEach((p, i) => { const d = Math.abs(p[0] - e[0]) + Math.abs(p[1] - e[1]) + Math.abs(p[2] - e[2]); if (d < bd) { bd = d; best = i; } }); return best; }

// ---------- dump
const R = v => Math.round(v * 1e5) / 1e5;
const prims = []; const stats = {}; const uvRange = {};
j.nodes.forEach((n, ni) => {
  if (n.mesh == null) return;
  const nname = nameOf(ni);
  if (dropNode.test(nname)) return;
  const M = wm(ni); const N3 = inv3T(M);
  const part = partOf(nname, M);
  j.meshes[n.mesh].primitives.forEach(p => {
    const mat = j.materials[p.material]; const group = groupOf(mat);
    if (group === 'drop') return;
    if (part === 'Body' && /ext01_chassis/.test(n.name) === false && false) return;
    const pos = readAcc(p.attributes.POSITION), nrm = readAcc(p.attributes.NORMAL);
    const tc = (mat.pbrMetallicRoughness && mat.pbrMetallicRoughness.baseColorTexture && mat.pbrMetallicRoughness.baseColorTexture.texCoord) || 0;
    const uvAcc = p.attributes['TEXCOORD_' + tc] != null ? p.attributes['TEXCOORD_' + tc] : p.attributes.TEXCOORD_0;
    const uv = readAcc(uvAcc); const idx = readAcc(p.indices);
    const cnt = pos.length / 3; const P = new Array(cnt * 3), Nn = new Array(cnt * 3), U = new Array(cnt * 2);
    for (let i = 0; i < cnt; i++) {
      const x = pos[i * 3], y = pos[i * 3 + 1], z = pos[i * 3 + 2];
      const wx = M[0] * x + M[4] * y + M[8] * z + M[12], wy = M[1] * x + M[5] * y + M[9] * z + M[13], wz = M[2] * x + M[6] * y + M[10] * z + M[14];
      P[i * 3] = R(wx); P[i * 3 + 1] = R(wy); P[i * 3 + 2] = R(-wz);
      const nx = nrm[i * 3], ny = nrm[i * 3 + 1], nz = nrm[i * 3 + 2];
      let ax = N3[0] * nx + N3[1] * ny + N3[2] * nz, ay = N3[3] * nx + N3[4] * ny + N3[5] * nz, az = N3[6] * nx + N3[7] * ny + N3[8] * nz;
      const l = Math.hypot(ax, ay, az) || 1; Nn[i * 3] = R(ax / l); Nn[i * 3 + 1] = R(ay / l); Nn[i * 3 + 2] = R(-az / l);
      U[i * 2] = R(uv[i * 2]); U[i * 2 + 1] = R(1 - uv[i * 2 + 1]);
    }
    const I = new Array(idx.length);
    for (let i = 0; i + 2 < idx.length; i += 3) { I[i] = idx[i]; I[i + 1] = idx[i + 2]; I[i + 2] = idx[i + 1]; }
    const key = part + '/' + group; stats[key] = (stats[key] || 0) + idx.length / 3;
    const r = (uvRange[p.material] = uvRange[p.material] || { min: [9e9, 9e9], max: [-9e9, -9e9] });
    for (let i = 0; i < cnt; i++) { r.min[0] = Math.min(r.min[0], uv[i * 2]); r.max[0] = Math.max(r.max[0], uv[i * 2]); r.min[1] = Math.min(r.min[1], uv[i * 2 + 1]); r.max[1] = Math.max(r.max[1], uv[i * 2 + 1]); }
    prims.push({ node: nname, part, group, material: p.material, swatch: group === 'lights' ? swatchOf(mat) : -1, pos: P, nrm: Nn, uv: U, idx: I });
  });
});

// ---------- materials + image extraction (only images the kept groups use)
const used = new Set(prims.map(p => p.material));
const imageOfTex = t => (t == null ? -1 : j.textures[t.index].source);
const materials = j.materials.map((m, i) => {
  const pbr = m.pbrMetallicRoughness || {};
  return { index: i, name: m.name, group: groupOf(m), used: used.has(i), base: imageOfTex(pbr.baseColorTexture), mr: imageOfTex(pbr.metallicRoughnessTexture), normal: imageOfTex(m.normalTexture), emissive: m.emissiveFactor || [0, 0, 0],
    emissiveStrength: (m.extensions && m.extensions.KHR_materials_emissive_strength && m.extensions.KHR_materials_emissive_strength.emissiveStrength) || 1, alpha: m.alphaMode || 'OPAQUE', baseFactor: pbr.baseColorFactor || [1, 1, 1, 1],
    tris: prims.filter(p => p.material === i).reduce((s, p) => s + p.idx.length / 3, 0), uvMin: uvRange[i] ? uvRange[i].min : null, uvMax: uvRange[i] ? uvRange[i].max : null };
});
fs.mkdirSync(outDir, { recursive: true });
const needImg = new Set(); materials.filter(m => m.used).forEach(m => { if (m.base >= 0) needImg.add(m.base); });
needImg.forEach(i => {
  const im = j.images[i]; const bv = j.bufferViews[im.bufferView]; const data = bin.slice(bv.byteOffset || 0, (bv.byteOffset || 0) + bv.byteLength);
  fs.writeFileSync(path.join(outDir, `img_${i}.${data[0] === 0x89 ? 'png' : 'jpg'}`), data);
});

// door open poses from the glb animations (see quadra-door-anim.js): translation delta + rotation delta at the peak, Unity space
function qmul(a, c) { return [a[3] * c[0] + a[0] * c[3] + a[1] * c[2] - a[2] * c[1], a[3] * c[1] - a[0] * c[2] + a[1] * c[3] + a[2] * c[0], a[3] * c[2] + a[0] * c[1] - a[1] * c[0] + a[2] * c[3], a[3] * c[3] - a[0] * c[0] - a[1] * c[1] - a[2] * c[2]]; }
function accAny(i) { return readAcc(i); }
function doorPose(animIndex, nodeName) {
  const A = j.animations[animIndex]; const nid = j.nodes.findIndex(n => n.name === nodeName);
  const ch = {}; A.channels.filter(c => c.target.node === nid).forEach(c => { const s = A.samplers[c.sampler]; ch[c.target.path] = { t: accAny(s.input), v: accAny(s.output) }; });
  const rot = ch.rotation, n = rot.t.length, q0 = Array.from(rot.v.slice(0, 4)); const inv = [-q0[0], -q0[1], -q0[2], q0[3]];
  let best = 0, bi = 0;
  for (let i = 0; i < n; i++) { const d = qmul(Array.from(rot.v.slice(i * 4, i * 4 + 4)), inv); const ang = 2 * Math.acos(Math.min(1, Math.abs(d[3]))); if (ang > best) { best = ang; bi = i; } }
  const dq = qmul(Array.from(rot.v.slice(bi * 4, bi * 4 + 4)), inv);
  const tt = ch.translation; const dT = [0, 1, 2].map(k => tt.v[Math.min(bi, tt.t.length - 1) * 3 + k] - tt.v[k]);
  const M = wm(nid);
  return { pivot: [R(M[12]), R(M[13]), R(-M[14])], dPos: [R(dT[0]), R(dT[1]), R(-dT[2])], dRot: [R(-dq[0]), R(-dq[1]), R(dq[2]), R(dq[3])], peakTime: rot.t[bi] };
}
const doors = { DoorL: doorPose(0, 'ext01_door_l_01__101_38'), DoorR: doorPose(1, 'ext01_door_r_01__117_53') };

fs.writeFileSync(path.join(outDir, 'quadra.json'), JSON.stringify({ prims, materials: materials.filter(m => m.used), palette: PALETTE, doors }));
console.log('tris per part/group:', JSON.stringify(stats, null, 1));
console.log('total tris', Object.values(stats).reduce((a, c) => a + c, 0), 'prims', prims.length, 'images', needImg.size);
console.log('materials used:'); materials.filter(m => m.used).forEach(m => console.log(' ', m.index, m.name, m.group, 'tris', m.tris, 'base', m.base, 'uv', JSON.stringify(m.uvMin), JSON.stringify(m.uvMax)));
console.log('doors', JSON.stringify(doors));
// bounds per part (Unity space)
const bounds = {};
prims.forEach(p => { const bb = (bounds[p.part] = bounds[p.part] || { mn: [9e9, 9e9, 9e9], mx: [-9e9, -9e9, -9e9] }); for (let i = 0; i < p.pos.length; i += 3) for (let k = 0; k < 3; k++) { bb.mn[k] = Math.min(bb.mn[k], p.pos[i + k]); bb.mx[k] = Math.max(bb.mx[k], p.pos[i + k]); } });
for (const [k, v] of Object.entries(bounds)) console.log('bounds', k, v.mn.map(x => +x.toFixed(2)).join(','), '->', v.mx.map(x => +x.toFixed(2)).join(','));
