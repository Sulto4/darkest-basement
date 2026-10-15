// Port exact în JS al GameRng (Model/Core/GameRng.cs) și MapGenerator (Model/Map/MapGenerator.cs), aritmetică uint32.
// Filmul arată harta reală a seed-ului 7 (aceeași rulare QA din care sunt capturile), pas cu pas, cum o construiește jocul.

export class GameRng {
  constructor(seed) {
    this.seed = seed | 0;
    this.state = ((seed >>> 0) ^ 0xa3c59ac3) >>> 0;
    if (this.state === 0) this.state = 1;
    this.log = null; // opțional: [{x, out}] pentru animații
  }
  next() {
    let x = this.state;
    x = (x ^ (x << 13)) >>> 0;
    x = (x ^ (x >>> 17)) >>> 0;
    x = (x ^ (x << 5)) >>> 0;
    this.state = x;
    return x;
  }
  range(min, maxExcl) {
    const span = (maxExcl - min) >>> 0;
    const limit = (4294967296 - span) % span; // (0u - span) % span
    let v;
    do { v = this.next(); } while (v < limit);
    return min + (v % span);
  }
  chance(p) { return this.next() / 4294967296 < p; }
  pick(arr) { return arr[this.range(0, arr.length)]; }
  shuffle(arr, onSwap) {
    for (let i = arr.length - 1; i > 0; i--) {
      const j = this.range(0, i + 1);
      if (onSwap) onSwap(i, j);
      const t = arr[i]; arr[i] = arr[j]; arr[j] = t;
    }
    return arr;
  }
  fork(name) {
    let h = 2166136261 >>> 0;
    for (let k = 0; k < name.length; k++) { h = (h ^ name.charCodeAt(k)) >>> 0; h = Math.imul(h, 16777619) >>> 0; }
    return new GameRng((h ^ (this.seed >>> 0)) | 0);
  }
}

// Generează harta etajului și înregistrează pașii (pentru animație). Întoarce { rooms, edges, steps, extra, boss, dist, ... }.
export function generateMap(rng, floor = 1) {
  let attempts = 0;
  for (;;) {
    attempts++;
    const rooms = [], corridors = [], steps = [];
    const link = (a, b) => {
      rooms[a].nb.push(b); rooms[b].nb.push(a);
      const seg = [];
      for (let i = 0; i < 3; i++) {
        let kind = 'none';
        if (rng.chance(0.25)) { const roll = rng.range(0, 7); kind = roll === 0 ? 'ambush' : roll <= 2 ? 'curio' : 'trap'; }
        seg.push(kind);
      }
      corridors.push({ a, b, seg });
    };
    const count = rng.range(9, 12);
    const sx = rng.range(0, 5), sy = rng.range(0, 4);
    rooms.push({ id: 0, x: sx, y: sy, nb: [], type: 'combat' });
    while (rooms.length < count) {
      const frontier = [];
      for (const r of rooms) {
        const dx = [-1, 1, 0, 0], dy = [0, 0, -1, 1];
        for (let i = 0; i < 4; i++) {
          const x = r.x + dx[i], y = r.y + dy[i];
          if (x < 0 || x >= 5 || y < 0 || y >= 4) continue;
          if (rooms.some((o) => o.x === x && o.y === y)) continue;
          frontier.push([r.id, x, y]);
        }
      }
      const nx = rng.pick(frontier), id = rooms.length;
      rooms.push({ id, x: nx[1], y: nx[2], nb: [], type: 'combat' });
      link(nx[0], id);
      steps.push({ frontier: frontier.map((f) => [f[1], f[2], f[0]]), parent: nx[0], id, x: nx[1], y: nx[2] });
    }
    const cand = [];
    for (const a of rooms) for (const b of rooms)
      if (a.id < b.id && Math.abs(a.x - b.x) + Math.abs(a.y - b.y) === 1 && !a.nb.includes(b.id)) cand.push([a.id, b.id]);
    rng.shuffle(cand);
    const extraWanted = rng.range(1, 3);
    let added = 0; const extra = [];
    const treeLeaves = rooms.filter((r) => r.nb.length === 1).map((r) => r.id);
    for (const e of cand) {
      let leaves = 0;
      for (const r of rooms) if (r.nb.length === 1 && r.id !== e[0] && r.id !== e[1]) leaves++;
      if (leaves < 2) continue; // păstrăm fundături pentru comoară și boss
      link(e[0], e[1]); extra.push(e);
      if (++added === extraWanted) break;
    }
    if (added === 0) continue;
    const dist = bfs(rooms, 0);
    let boss = 1;
    for (const r of rooms)
      if (dist.d[r.id] > dist.d[boss] || (dist.d[r.id] === dist.d[boss] && r.nb.length === 1 && rooms[boss].nb.length !== 1)) boss = r.id;
    rooms[boss].type = 'boss'; rooms[0].type = 'start';
    const dead = rooms.filter((r) => r.id !== 0 && r.id !== boss && r.nb.length === 1);
    if (!dead.length) continue;
    const treasure = rng.pick(dead); treasure.type = 'treasure';
    const rest = rooms.filter((r) => r.id !== 0 && r.id !== boss && r !== treasure);
    rng.shuffle(rest);
    rest[0].type = 'bedroom'; rest[1].type = 'curio';
    for (let i = 2; i < rest.length; i++) rest[i].type = 'combat';
    if (count === 11 && rng.chance(0.8)) rest[2].type = 'empty';
    return { rooms, corridors, steps, extra, extraWanted, cand, treeLeaves, boss, treasure: treasure.id, dist, count, attempts };
  }
}

// BFS din origine: distanțele și ordinea în care au intrat nodurile în coadă (pentru animație).
export function bfs(rooms, origin) {
  const d = { [origin]: 0 }, order = [origin], q = [origin], waves = [];
  while (q.length) {
    const id = q.shift();
    const snap = { pop: id, push: [] };
    for (const n of rooms[id].nb) if (d[n] === undefined) { d[n] = d[id] + 1; q.push(n); order.push(n); snap.push.push(n); }
    snap.queue = q.slice();
    waves.push(snap);
  }
  return { d, order, waves };
}

// Harta etajului 1 pentru un seed, ca în Run.StartRun: root = GameRng(seed); mapRng = root.Fork("map").
export function floorMap(seed, floor = 1) {
  const root = new GameRng(seed);
  return generateMap(root.fork('map'), floor);
}
