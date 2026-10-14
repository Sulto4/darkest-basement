# Contractele dintre părțile View

Două echipe lucrează în paralel:
- **W** (World): `Core/`, `Exploration/`, `UI/Map*`, `UI/Menus*`, `UI/Screens*` (Treasure, Bedroom, End), `QA/`
- **C** (Combat): `Combat/`, `FX/`, `UI/Combat*`, `UI/CardView.cs`, `UI/RewardScreen.cs`

Fiecare echipă implementează **doar** fișierele ei. API-urile publice de mai jos sunt fixe: le poți extinde, dar nu le poți rupe. Stub-urile există deja, ca proiectul să compileze de la început.

## Spațiul 3D

- Unitate de lume: 1 = 32 px de artă (PPU 32). Podeaua e la `y = 0`.
- Holul merge pe axa **+Z** (înainte). Camera de explorare stă în spatele echipei, mai sus, și privește spre +Z.
- O cameră (încăpere) are centrul `stage.Center`. **Eroii stau pe partea `-stage.Right`, inamicii pe `+stage.Right`**. Rangul 1 al fiecărei părți e cel mai aproape de centru. Distanța dintre ranguri e ~1,6 unități.
- Lumea se randează într-un **RenderTexture de 640×360** cu filtru Point, afișat ×N întreg sub UI. Așa 3D-ul arată pixelat, la fel ca sprite-urile. Proprietar: W.

## API-uri

```csharp
// W — Exploration/CameraRig.cs
public sealed class CameraRig : MonoBehaviour {
    public static CameraRig Instance;
    public Camera Camera;                 // camera lumii (randează în RenderTexture-ul de 640×360)
    public Transform ShakeRoot;           // ScreenShake (C) scrie aici offset-ul local
    public void Follow(Transform target);                                 // explorare: din spate, lin
    public TweenHandle GoToSideView(CombatStageInfo stage, float duration = 1.2f);
    public TweenHandle ReturnToFollow(float duration = 1.0f);
    public void PunchIn(Vector3 worldFocus, float amount = 0.12f, float duration = 0.35f); // zoom cinematic scurt, revine singur
    public Vector2 WorldToUI(Vector3 worldPos);                           // poziție în UIRoot.Root (640×360)
}

// C — Combat/CombatController.cs
public struct CombatStageInfo { public Vector3 Center; public Vector3 Right; public int Floor; public bool IsBoss; public bool IsAmbush; }
public sealed class CombatController : MonoBehaviour {
    // Prezintă o luptă deja creată de Run.CreateCombat. La final cheamă onFinished(victory).
    // NU cheamă Run.ApplyCombatResult: asta o face GameFlow (W).
    public static CombatController Begin(CombatEngine engine, CombatStageInfo stage, Action<bool> onFinished);
    public static bool Fast;              // viteza animațiilor ×2 (tasta F)
}

// C — UI/CardView.cs
public sealed class CardView : MonoBehaviour {
    public static CardView Create(Transform parent, CardDef def, bool upgraded, Vector2 pos);
    public CardDef Def; public bool Upgraded;
    public Action<CardView> OnClicked;    // folosit de ecranele W (Treasure, Bedroom)
    public void SetHighlighted(bool on);
}

// C — UI/RewardScreen.cs
public static class RewardScreen {
    // Animația de pachet + Take / Skip. onDone(true) = a luat cartea.
    public static void Show(CardDef card, bool rare, Action<bool> onDone);
}

// C — FX/ScreenShake.cs, FX/HitStop.cs
public static class ScreenShake { public static void Add(float trauma); }      // 0..1, se cumulează; shake = trauma²
public static class HitStop { public static void Freeze(float seconds); }      // oprește timpul scurt (UI-ul folosește timp unscaled)
```

## Fluxul unei lupte (W ↔ C)

1. W: `engine = run.CreateCombat(run.PendingEncounter)`. Ascunde sprite-urile echipei de explorare, iar camera trece în `GoToSideView(stage)`.
2. W: `CombatController.Begin(engine, stage, victory => …)`.
3. C: spawnează eroii și inamicii la ranguri, rulează `engine.StartCombat()`, redă evenimentele, primește input pentru cărți și dă End Turn, până la `engine.IsOver`.
4. W, în callback: `run.ApplyCombatResult(engine)`. Dacă e victorie: `RewardScreen.Show(run.RollCardReward(), …)`, apoi `run.TakeCard()` / `run.SkipCard()`. Dacă e înfrângere: ecranul de final.
5. W: `ReturnToFollow()`, echipa de explorare reapare (cu HP-ul și morții actualizați), iar explorarea continuă.

## QA automat (proprietar W, folosit de toți)

Build-ul pornit cu `-qa <folder> [-seed N] [-qa-script tour|full|combat]` se joacă singur cu `Model.Sim.AutoPlayer` și salvează capturi PNG la momentele cheie (meniu, hartă, hol, ușă, intrare în luptă, cărți în mână, lovituri, recompensă, boss, final) în `<folder>`. Mai salvează `qa-log.txt` cu toate erorile și excepțiile, apoi iese cu cod 0 sau 1. Cu `-capture-fps 30` scrie toate cadrele (pentru video), cu `Time.captureFramerate` fix.
Rulare: `tools/forge.sh qa` (build Linux + Xvfb pe Forge, capturile vin înapoi în `Logs/qa/`).
