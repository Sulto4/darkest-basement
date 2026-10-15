# Darkest Basement — reguli pentru agenți

Joc turn-based roguelike în Unity (fan game privat, proiect de facultate al lui Radu). Citește întâi:
`docs/GDD.md` (design), `docs/ARCHITECTURE.md` (cod), `docs/ART.md` (artă).

## Fapte fixe

- Calea fizică: `/Volumes/Dock & Hub 2TB/work/claude-worktrees/darkest-basement` (root SSD aprobat de `radu-heavy`). `~/Projects/darkest-basement` e un symlink spre ea. Pentru `codex exec -C` folosește calea fizică.
- Unity **6000.3.26f1**, instalat pe SSD: `/Volumes/Dock & Hub 2TB/DevStorage/unity/editors/6000.3.26f1/Unity.app` (+ modulul Windows Mono). URP 17.3, Input System 1.20, uGUI 2.0 (cu TextMeshPro inclus).
- .NET 8 SDK (pentru `tools/sim`): `/Volumes/Dock & Hub 2TB/DevStorage/dotnet/sdk8/dotnet`.
- GitHub: `Sulto4/darkest-basement`, **public** din 15.10.2026, la decizia lui Radu, ca să poată da link-ul mai departe. Nu pune niciodată chei, parole sau date personale în repo.

## Cum verifici

```bash
tools/sim/run.sh                 # compilează Model-ul în .NET și rulează testele + simularea (secunde, fără Unity)
tools/unity.sh compile           # compilare Unity în batchmode (cere licența activată)
tools/unity.sh test              # teste EditMode
radu-heavy --target "/Volumes/Dock & Hub 2TB/work/claude-worktrees/darkest-basement" -- tools/unity.sh build-mac   # pașii grei trec prin radu-heavy
```

## Reguli de cod

- **Model** (`Assets/_Game/Scripts/Model`) e C# pur: fără `UnityEngine`, C# 9 fără `record` / `init`. Toată aleatoritatea trece prin `GameRng`.
- **View** construiește totul din cod. Scena are un singur `GameBootstrap`. Nu edita la mână fișiere `.unity` / `.prefab` / `.asset`. Dacă îți trebuie un asset, generează-l dintr-un script de editor.
- Identificatori în engleză. Comentarii scurte, în **română**, care explică *de ce*. Codul e și material de învățare pentru Radu: clar înainte de deștept.
- Fișiere `.meta`: le generează Unity. Nu le scrie de mână, cu excepția cazului în care e nevoie de un GUID stabil.
- Fără pachete din Asset Store și fără DOTween/Cinemachine. Tween-urile, easing-ul și camera sunt cod propriu (fac parte din ce explicăm în video).
- Lucrează doar în folderele care îți sunt atribuite. Nu face commit și nu da push (le face coordonatorul).

## Reguli de artă

Vezi `docs/ART.md`. Generatoarele din `tools/art/` sunt sursa de adevăr. Fiecare set livrează și o planșă ×6 în `docs/art-review/`.
