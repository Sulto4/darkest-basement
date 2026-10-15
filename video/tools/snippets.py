#!/usr/bin/env python3
"""Extrage din repo bucățile de cod arătate în film → video/film/data/snippets.json.
Fiecare bucată e găsită după o linie-ancoră (text exact), nu după număr de linie: codul jocului se mai schimbă.
Rândurile lungi se rup pentru afișare (la „; ” sau „{ ”), iar continuarea nu primește număr de linie.
python3 tools/snippets.py   (rulează-l din nou înainte de randarea finală; afișează ce ancoră lipsește)"""
import json, os, re, sys

V = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(os.path.dirname(V), 'Assets/_Game/Scripts')
WRAP = 76

# id: (fișier, ancoră, câte rânduri, [rânduri de sărit relativ la ancoră])
SNIPS = {
    'asmdef': ('Model/DarkestBasement.Model.asmdef', '{', 14),
    'flow_comment': ('Core/GameFlow.cs', '// Mașina de stări care conduce un run', 4),
    'flow_machine': ('Core/GameFlow.cs', 'IEnumerator Machine(FlowState first)', 10),
    'flow_routine': ('Core/GameFlow.cs', 'IEnumerator Routine(FlowState state)', 12),
    'map_grow': ('Model/Map/MapGenerator.cs', 'while (map.Rooms.Count < count)', 16),
    'map_extra': ('Model/Map/MapGenerator.cs', 'rng.Shuffle(edges); int extra = rng.Range(1, 3), added = 0;', 8),
    'map_bfs': ('Model/Map/MapGenerator.cs', 'public Dictionary<int, int> Distances(int origin)', 7),
    'map_boss': ('Model/Map/MapGenerator.cs', 'var distances = map.Distances(0); int boss = 1;', 3),
    'rng_next': ('Model/Core/GameRng.cs', 'private uint Next()', 2),
    'rng_ctor': ('Model/Core/GameRng.cs', 'public GameRng(int seed)', 1),
    'rng_range': ('Model/Core/GameRng.cs', 'public int Range(int minInclusive, int maxExclusive)', 9),
    'rng_shuffle': ('Model/Core/GameRng.cs', 'public void Shuffle<T>(IList<T> values)', 2),
    'rng_fork': ('Model/Core/GameRng.cs', 'public GameRng Fork(string streamName)', 8),
    'run_fork': ('Model/Core/Run.cs', 'public void StartRun(int seed)', 3),
    'turn_order': ('Model/Combat/CombatState.cs', 'public static List<Unit> Roll(CombatState state, GameRng rng)', 7),
    'damage': ('Model/Combat/CombatState.cs', 'public static DamageResult Calculate(Unit source, Unit target, int baseDamage, bool crit)', 10),
    'statusset': ('Model/Combat/CombatState.cs', 'public sealed class StatusSet', 8),
    'deck_draw': ('Model/Combat/CombatState.cs', 'if (DrawPile.Count == 0)', 4),
    'cc_take': ('Combat/CombatController.cs', '// Golim lista modelului imediat ce am copiat-o (contractul CombatEngine).', 7),
    'cc_play': ('Combat/CombatController.cs', 'IEnumerator Play(List<CombatEvent> events)', 21),
    'cc_playone': ('Combat/CombatController.cs', 'IEnumerator PlayOne(CombatEvent e)', 14),
    'cc_safe': ('Combat/CombatController.cs', 'static IEnumerator Safe(IEnumerator routine)', 18),
    'cc_juice': ('Combat/CombatController.cs', '// Juice: hit-stop (60–100 ms, 150 ms la crit/kill), shake (trauma), punch-in, flash.', 7),
    'ease_cubic': ('Core/Easing.cs', 'case Ease.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);', 1),
    'ease_back': ('Core/Easing.cs', 'case Ease.OutBack:', 6),
    'ease_head': ('Core/Easing.cs', '// Funcții de easing: primesc t în [0,1]', 2),
    'tween_move': ('Core/Tween.cs', 'public static TweenHandle Move(Transform t, Vector3 to, float duration', 5),
    'tween_update': ('Core/Tween.cs', 'h.Elapsed += dt;', 3),
    'unit_anticipate': ('Combat/UnitView.cs', '// Anticipare: atacatorul se trage puțin înapoi și se turtește', 8),
    'unit_lunge': ('Combat/UnitView.cs', 'public IEnumerator Lunge(Vector3 targetFeet, float duration = 0.1f, float stop = 1.0f)', 13),
    'unit_breath': ('Combat/UnitView.cs', 'float breath = Dead ? 0f', 4),
    'cam_follow': ('Exploration/CameraRig.cs', 'if (CurrentMode == Mode.Follow && _target != null)', 6),
    'cam_orbit': ('Exploration/CameraRig.cs', 'float angle = angleA + delta * k;', 6),
    'cam_side': ('Exploration/CameraRig.cs', '// Vederea laterală: pe planul scenei de luptă se văd exact 10 unități în lățime', 5),
    'shake_head': ('FX/ScreenShake.cs', '// Screen shake bazat pe „trauma”', 4),
    'shake_tick': ('FX/ScreenShake.cs', 'float shake = Trauma * Trauma;', 7),
    'shake_noise': ('FX/ScreenShake.cs', '// PerlinNoise dă 0..1; îl mutăm în -1..1.', 2),
    'hitstop': ('FX/HitStop.cs', 'public static void Freeze(float seconds)', 9),
    'billboard': ('Exploration/Billboard.cs', 'if (FaceCamera && cam != null)', 6),
    'ui_scale': ('UI/UIRoot.cs', '// Cel mai mare factor întreg care încape în ecran', 6),
    'ui_head': ('UI/UIRoot.cs', '// Canvas-ul unic al jocului.', 2),
    'card_tear': ('Model/Data/Databases.cs', 'Add("tear", "Tear", "isaac"', 1),
    'card_add': ('Model/Data/Databases.cs', 'void Add(string id, string name, string hero, Rarity rarity', 1),
    'monstro': ('Model/Data/Databases.cs', 'd.Add("monstro", new EnemyDef("monstro", "Monstro"', 1),
    'ai_score': ('Model/Sim/AutoPlayer.cs', 'double damage = DamageCalculator.Calculate(hero, target, value, false)', 6),
    'sim_loop': ('Model/Sim/RunSimulator.cs', 'while (!engine.IsOver)', 5),
    'ease_orbit': ('Exploration/CameraRig.cs', '}, Ease.InOutCubic, () =>', 1),
}


def wrap(s, indent):
    """Rupe un rând lung după „; ”, „{ ” sau „, ”, păstrând indentarea."""
    out = []
    while len(s) > WRAP:
        cut = -1
        for sep in ('; ', '{ ', ' && ', ', ', ' '):
            k = s.rfind(sep, 30, WRAP)
            if k > 0: cut = k + len(sep); break
        if cut < 0: break
        out.append(s[:cut].rstrip()); s = ' ' * (indent + 4) + s[cut:].lstrip()
    out.append(s)
    return out


res, missing = {}, []
for sid, (f, anchor, n) in SNIPS.items():
    lines = open(os.path.join(SRC, f), encoding='utf-8').read().split('\n')
    idx = next((i for i, l in enumerate(lines) if anchor in l), None)
    if idx is None: missing.append(f'{sid}: {f} „{anchor}”'); continue
    block = lines[idx:idx + n]
    ind = min((len(l) - len(l.lstrip()) for l in block if l.strip()), default=0)
    out = []
    for k, l in enumerate(block):
        s = l[ind:].rstrip()
        parts = wrap(s, len(s) - len(s.lstrip()))
        out.append({'n': idx + 1 + k, 's': parts[0]})
        out += [{'n': None, 's': p} for p in parts[1:]]
    res[sid] = {'file': 'Assets/_Game/Scripts/' + f, 'start': idx + 1, 'lines': out}
os.makedirs(os.path.join(V, 'film/data'), exist_ok=True)
json.dump(res, open(os.path.join(V, 'film/data/snippets.json'), 'w'), ensure_ascii=False, indent=1)
print(f'{len(res)} bucăți de cod → film/data/snippets.json')
if missing: print('ANCORE NEGĂSITE:\n  ' + '\n  '.join(missing)); sys.exit(1)
