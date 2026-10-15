// Cronologia filmului, calculată din datele vocii (voice/<scena>.lines.json): nimic nu e legat de secunde scrise de mână.
// Fiecare scenă are un `plan` (titlu, replici, pauze, sloturi de gameplay); din el ies momentele replicilor, ale cuvintelor
// și ale sloturilor, durata scenei, segmentele de randare și planul de sunet. Dacă vocea se regenerează, totul se re-sincronizează.
export const FPS = 30;
export const GAP = 0.42; // pauza implicită dintre replici (s)

const fold = (s) => s.toLowerCase().normalize('NFKD').replace(/[̀-ͯ]/g, '').replace(/[^a-z0-9]/g, '');

export function layoutScene(mod, lj) {
  const src = lj.lines;
  const lines = {}, slots = [], vo = [], marks = {};
  let cur = 0;
  const place = (n, gap) => {
    const l = src[n - 1];
    if (!l) throw new Error(`${mod.id}: replica ${n} lipsește din vocea scenei`);
    const start = cur + (gap ?? GAP);
    const shift = start - l.speechStart;
    const words = l.words.map((w) => ({ w: w.w, k: fold(w.w), start: w.start + shift, end: w.end + shift }));
    const end = start + (l.speechEnd - l.speechStart);
    lines[n] = { n, text: l.text, start, end, words };
    const from = Math.max(0, l.speechStart - 0.04), to = Math.min(l.dur, l.speechEnd + 0.25);
    vo.push({ src: l.src, at: start - (l.speechStart - from), from, to });
    cur = end;
    return lines[n];
  };
  for (const it of mod.plan) {
    if (it.title != null) { marks.title = [cur, cur + it.title]; cur += it.title; }
    else if (it.pause != null) cur += it.pause;
    else if (it.mark) marks[it.mark] = cur;
    else if (it.line != null) place(it.line, it.gap);
    else if (it.slot && it.lines) { // slot de gameplay peste replici (vocea continuă peste capturi)
      const [a, b] = it.lines;
      for (let n = a; n <= b; n++) place(n, n === a ? it.gap : it.inner);
      const t0 = lines[a].start - (it.lead ?? 0.3), t1 = Math.max(cur, lines[b].end + (it.tail ?? 0.35));
      slots.push({ ...it.slot, t0, t1, lines: [a, b] });
      cur = t1;
    } else if (it.slot) { // slot fără voce: muzica urcă, gameplay pe tot ecranul
      const t0 = cur + (it.gap ?? 0.25);
      slots.push({ ...it.slot, t0, t1: t0 + it.dur });
      cur = t0 + it.dur;
    }
  }
  const dur = cur + (mod.post ?? 1.0);
  const S = {
    id: mod.id, dur, lines, slots, marks, vo,
    L: (n) => lines[n].start,
    E: (n) => lines[n].end,
    // momentul cuvântului din replica n care începe cu `pref` (fără diacritice); occ = a câta apariție
    w: (n, pref, occ = 0) => {
      const f = fold(pref), ws = lines[n].words.filter((w) => w.k.startsWith(f));
      if (!ws.length) { console.warn(`${mod.id}: nu găsesc „${pref}” în replica ${n}`); return lines[n].start; }
      return ws[Math.min(occ, ws.length - 1)].start;
    },
    we: (n, pref, occ = 0) => {
      const f = fold(pref), ws = lines[n].words.filter((w) => w.k.startsWith(f));
      return ws.length ? ws[Math.min(occ, ws.length - 1)].end : lines[n].end;
    },
    slot: (id) => slots.find((s) => s.id === id),
  };
  return S;
}

// Scenele una după alta, fiecare începând pe un cadru întreg (segmentele se randează separat și se lipesc).
export function buildFilm(mods, voice) {
  let f = 0;
  const scenes = mods.map((mod) => {
    const S = layoutScene(mod, voice[mod.id]);
    const frames = Math.ceil(S.dur * FPS);
    S.f0 = f; S.f1 = f + frames; S.t0 = f / FPS; S.t1 = S.f1 / FPS; S.mod = mod;
    f = S.f1;
    return S;
  });
  return { scenes, duration: f / FPS, frames: f };
}

// Segmentele de randare: părțile de scenă + sloturile de gameplay (fiecare slot e segmentul lui, ca să poată fi înlocuit
// cu un clip live: out/footage/<slot>.mp4).
export function segments(film) {
  const out = [];
  for (const S of film.scenes) {
    const cuts = S.slots.map((s) => ({ s, a: S.f0 + Math.round(s.t0 * FPS), b: S.f0 + Math.round(s.t1 * FPS) })).sort((x, y) => x.a - y.a);
    let cur = S.f0, part = 0;
    for (const c of cuts) {
      if (c.a > cur) out.push({ id: `${S.id}-p${part++}`, scene: S.id, kind: 'scene', f0: cur, f1: c.a });
      out.push({ id: c.s.id, scene: S.id, kind: 'slot', f0: Math.max(cur, c.a), f1: c.b, footage: c.s.id });
      cur = c.b;
    }
    if (S.f1 > cur) out.push({ id: `${S.id}-p${part++}`, scene: S.id, kind: 'scene', f0: cur, f1: S.f1 });
  }
  return out;
}
