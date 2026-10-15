using System;
using System.Collections;
using System.Collections.Generic;
using DarkestBasement.Audio;
using DarkestBasement.Core;
using DarkestBasement.FX;
using DarkestBasement.Model.Combat;
using DarkestBasement.Model.Core;
using DarkestBasement.Model.Data;
using DarkestBasement.Model.Sim;
using DarkestBasement.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkestBasement.Combat
{
    public struct CombatStageInfo
    {
        public Vector3 Center;
        public Vector3 Right;
        public int Floor;
        public bool IsBoss;
        public bool IsAmbush;
    }

    // Puntea dintre model și ce vezi. Modelul (CombatEngine) rezolvă o comandă instant și produce
    // o listă de evenimente; controller-ul le redă UNA CÂTE UNA ca animații scurte („bătăi”),
    // într-o corutină. Fiecare tip de eveniment are metoda lui mică (OnDamaged, OnUnitDied...).
    //
    // Fluxul: Begin → intro → StartCombat → [tura eroului: așteaptă o comandă → redă evenimentele] ... → final → onFinished(victorie).
    public sealed partial class CombatController : MonoBehaviour
    {
        public static bool Fast;                 // viteza ×2 (tasta F)
        public static bool AutoPlay;             // QA: eroii sunt jucați de Model.Sim.AutoPlayer, cu animații
        public static bool TutorialHintShown;    // hint-ul „Drag a card onto an enemy” apare o singură dată
        public static CombatController Active { get; private set; }

        // Momente-cheie pentru capturile QA (echipa W): "intro", "round", "hero_turn", "input", "card_played",
        // "hit", "crit", "kill", "boss_attack", "victory", "defeat", "finished".
        public static event Action<string> Moment;

        CombatEngine _engine;
        CombatStageInfo _stage;
        Action<bool> _onFinished;
        CombatViewState _state;
        CombatHud _hud;
        readonly Dictionary<string, UnitView> _views = new Dictionary<string, UnitView>();

        // Contextul acțiunii în curs (o carte jucată sau atacul unui inamic).
        sealed class ActionContext
        {
            public UnitView Actor;
            public ActionStyle Style;
            public string CardId, AbilityId;
            public bool Lunged, Delivered, PunchedIn, Stomped;
            public int Hits;
            public GameObject Prop;          // mâna sau ușa cu ochi a lui Mom
            public Vector3 CastOrigin;
            public bool HasCastOrigin;
            public readonly HashSet<string> DeliveredTo = new HashSet<string>();
        }

        ActionContext _action;
        List<CombatEvent> _events;
        int _cursor;
        Action _command;
        bool _momFurious;

        bool _finished, _lastStand;
        const int MaxCorpses = 5;
        readonly Queue<UnitView> _corpses = new Queue<UnitView>();

        // Într-o luptă lungă cu invocări (Gurdy, Mom) cadavrele s-ar aduna: păstrăm ultimele câteva, cele vechi se sting.
        void KeepCorpse(UnitView corpse)
        {
            _corpses.Enqueue(corpse);
            while (_corpses.Count > MaxCorpses)
            {
                var old = _corpses.Dequeue();
                if (old == null) continue;
                _views.Remove(old.Id);
                StartCoroutine(FadeAway(old));
            }
        }

        static IEnumerator FadeAway(UnitView v)
        {
            float t = 0f;
            while (t < 0.6f && v != null) { t += Time.deltaTime; v.Alpha = 1f - t / 0.6f; yield return null; }
            if (v != null) Destroy(v.gameObject);
        }

        public CombatEngine Engine => _engine;
        public CombatHud Hud => _hud;
        public CombatViewState State => _state;
        public CombatStageInfo Stage => _stage;
        public IReadOnlyDictionary<string, UnitView> Views => _views;
        public bool AcceptingInput { get; private set; }
        public HeroUnit InputHero { get; private set; }

        // ---------- pornire ----------

        // Prezintă o luptă deja creată de Run.CreateCombat. La final cheamă onFinished(victory).
        // NU cheamă Run.ApplyCombatResult: asta o face GameFlow (W).
        public static CombatController Begin(CombatEngine engine, CombatStageInfo stage, Action<bool> onFinished)
        {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            var go = new GameObject("CombatController");
            var c = go.AddComponent<CombatController>();
            c.Init(engine, stage, onFinished);
            return c;
        }

        void Init(CombatEngine engine, CombatStageInfo stage, Action<bool> onFinished)
        {
            Active = this;
            _engine = engine;
            _stage = stage;
            _onFinished = onFinished;
            UIRoot.Create();
            FxHost.Ensure();
            _state = new CombatViewState(engine.State);
            FxHost.Stage = transform;
            CombatCamera.StageFocus = stage.Center + Vector3.up;
            Particles.FloorY = stage.Center.y;
            // Petele de sânge aparțin camerei: când W distruge camera, dispar și ele.
            Decals.Clear();
            Decals.AttachTo(FindRoom(stage.Center));
            // "-combat-fast" (QA, sandbox): luptele lungi (boss-ii) se văd la viteză ×2.
            if (CommandLine.Has("-combat-fast")) Fast = true;
            HitStop.BaseTimeScale = Fast ? 2f : 1f;
            // QA-ul automat (echipa W) nu are mâini: eroii sunt jucați de AutoPlayer, cu toate animațiile.
            // Excepție: scriptul "input" joacă chiar prin mouse și tastatură.
            if (QA.QaHooks.Active && !QA.QaHooks.ManualInput) AutoPlay = true;

            foreach (var u in _state.Units.Values) if (!u.Dead) SpawnView(u);
            foreach (var v in _views.Values) v.Home = HomeOf(v.Id);
            _hud = CombatHud.Create(this);
            foreach (var v in _views.Values) _hud.AddUnit(v, hidden: !v.IsHero);
            if (_state.LivingCount(true) == 1) { _lastStand = true; _hud.SetLastStand(true); }

            AudioManager.PlayMusic(stage.IsBoss ? "music_boss" : "music_combat");
            StartCoroutine(Run());
        }

        UnitView SpawnView(CombatViewState.UnitInfo u)
        {
            var frames = u.IsHero ? UnitFrames.ForHero(u.DefId) : UnitFrames.ForEnemy(u.DefId, u.IsBoss);
            var v = UnitView.Create(u.Id, u.DefId, u.Name, u.IsHero, u.IsBoss, frames, _stage.Center, CombatStage.Right(_stage));
            v.transform.SetParent(transform, true);
            _views[u.Id] = v;
            if (u.DefId == "lilith") IncubusView.Attach(v).transform.SetParent(transform, true);
            return v;
        }

        // Camera (sau holul) în care se dă lupta: cel mai apropiat RoomBuilder / CorridorBuilder de centrul scenei.
        static Transform FindRoom(Vector3 center)
        {
            Transform best = null; float bestDist = 40f * 40f;
            foreach (var room in FindObjectsByType<Exploration.RoomBuilder>(FindObjectsSortMode.None))
            {
                float d = (room.transform.position - center).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = room.transform; }
            }
            if (best != null) return best;
            foreach (var corridor in FindObjectsByType<Exploration.CorridorBuilder>(FindObjectsSortMode.None)) return corridor.transform;
            return null;
        }

        // `v != null` folosește verificarea Unity: un obiect distrus devine null adevărat, deci `?.` e sigur după.
        public UnitView View(string id) => id != null && _views.TryGetValue(id, out var v) && v != null ? v : null;

        public Unit ModelUnit(string id)
        {
            foreach (var u in _engine.State.Units) if (u.Id == id) return u;
            return null;
        }

        // ---------- așezarea pe ranguri ----------

        // Fiecare rang are slotul lui (CombatStage.RankPosition). Boss-ii sunt prea lați pentru un slot:
        // stau în mijlocul părții lor, desenați în spatele minionilor (ca marii boss-i din Darkest Dungeon).
        Vector3 HomeOf(string id)
        {
            var target = _state.Unit(id);
            if (target == null) return _stage.Center;
            if (target.IsBoss) return CombatStage.Point(_stage, false, BossDistance(id));
            if (target.Dead && _views.TryGetValue(id, out var corpse)) return corpse.Home;
            return CombatStage.RankPosition(_stage, target.IsHero, target.Rank);
        }

        // Boss-ul stă în mijlocul părții lui, dar marginea lui stângă rămâne la cel puțin ~60 px de centru
        // (Gurdy e foarte lat: altfel aproape atinge eroul de pe rangul 1). Nici nu iese din cadru în dreapta.
        float BossDistance(string id)
        {
            float half = _views.TryGetValue(id, out var v) && v != null ? v.HalfWidth : 1.2f;
            float d = Mathf.Max(CombatStage.BossCenter, 0.95f + half);
            return Mathf.Min(d, 4.85f - half);
        }

        // ---------- bucla principală ----------

        IEnumerator Run()
        {
            yield return Safe(Intro());
            _engine.StartCombat();
            yield return Play(Take());
            while (!_engine.IsOver)
            {
                var hero = _engine.CurrentActor as HeroUnit;
                if (hero == null) { Debug.LogError("[Combat] modelul nu așteaptă niciun erou; opresc lupta."); break; }
                if (AutoPlay) yield return Safe(AutoTurn(hero));
                else yield return Safe(PlayerTurn(hero));
                // Un frame liber: dacă o tură ar crăpa instant, bucla nu blochează jocul.
                yield return null;
            }
            yield return Wait(0.2f);
            Finish();
        }

        // Golim lista modelului imediat ce am copiat-o (contractul CombatEngine).
        List<CombatEvent> Take()
        {
            var list = new List<CombatEvent>(_engine.Events);
            _engine.Events.Clear();
            return list;
        }

        IEnumerator PlayerTurn(HeroUnit hero)
        {
            _command = null;
            InputHero = hero;
            AcceptingInput = true;
            _hud.BeginInput(hero, Hopeless());
            if (!TutorialHintShown) _hud.ShowHint("Drag a card onto an enemy");
            Emit("input");
            while (_command == null) yield return null;
            AcceptingInput = false;
            _hud.EndInput();
            var command = _command;
            _command = null;
            bool ok = true;
            try { command(); }
            catch (InvalidOperationException e) { Debug.LogWarning("[Combat] comandă refuzată: " + e.Message); ok = false; }
            if (ok) yield return Play(Take());
        }

        // QA: AutoPlayer joacă toată tura dintr-odată în model; noi redăm comenzile una câte una, cu pauze.
        IEnumerator AutoTurn(HeroUnit hero)
        {
            yield return Wait(0.5f);
            var ai = new AutoPlayer();
            if (ai.HasNoOffense(_engine.State) || _engine.State.Round > 40) { _engine.Concede(); yield return Play(Take()); yield break; }
            var batches = new List<List<CombatEvent>>();
            ai.PlayTurn(_engine, events => batches.Add(new List<CombatEvent>(events)));
            _engine.Events.Clear();
            for (int i = 0; i < batches.Count; i++)
            {
                yield return Play(batches[i]);
                if (i < batches.Count - 1) yield return Wait(0.3f);
            }
        }

        // ---------- comenzi din UI ----------

        public bool CanRequest => AcceptingInput && _command == null && !GameFlow.Paused;

        public bool RequestPlay(CardInstance card, Unit target)
        {
            if (!CanRequest || card == null || target == null) return false;
            var hero = InputHero;
            if (!_engine.CanPlay(hero, card, out _)) return false;
            bool valid = false;
            foreach (var t in _engine.ValidTargets(hero, card)) if (t == target) valid = true;
            if (!valid) return false;
            _command = () => _engine.PlayCard(hero, card, target);
            return true;
        }

        // direction: -1 = spre față (rang mai mic), +1 = spre spate.
        public bool RequestMove(int direction)
        {
            if (!CanRequest || !CanMove(direction)) return false;
            var hero = InputHero;
            _command = () => _engine.Move(hero, direction);
            return true;
        }

        public bool CanMove(int direction)
        {
            var hero = InputHero;
            if (hero == null || hero.Energy < 1) return false;
            int rank = hero.Rank + direction;
            foreach (var u in _engine.State.Living(true)) if (u.Rank == rank) return true;
            return false;
        }

        // Renunțarea: doar când lupta e cu adevărat fără ieșire (vezi Hopeless). Evenimentele se redau normal (înfrângere).
        public void RequestGiveUp()
        {
            if (!CanRequest || !Hopeless()) return;
            AudioManager.Play("ui_click");
            _hud.Hand.DiscardAll();
            _command = () => _engine.Concede();
        }

        // Plasă de siguranță: eroii vii nu mai pot răni pe nimeni — nicio carte ofensivă în tot pachetul care să se poată
        // juca din vreun rang la care pot ajunge (sau din orice rang, în Last Stand) și niciun Bleed/Poison pe inamici.
        public bool Hopeless()
        {
            foreach (var e in _engine.State.Living(false))
                if (e.Statuses.Has(Status.Bleed) || e.Statuses.Has(Status.Poison)) return false;
            var living = _engine.State.Living(true);
            foreach (var u in living)
            {
                var hero = u as HeroUnit;
                if (hero == null) continue;
                bool anyRank = _engine.IsLastStand(hero);
                foreach (var pile in new[] { hero.Deck.Hand, hero.Deck.DrawPile, hero.Deck.DiscardPile })
                    foreach (var card in pile)
                    {
                        if (!Offensive(card.Def)) continue;
                        if (anyRank) return false;
                        for (int rank = 1; rank <= living.Count; rank++) if (card.Def.CanUseRank(rank)) return false;
                    }
            }
            return true;
        }

        static bool Offensive(CardDef def)
        {
            foreach (var fx in def.Effects)
                if (fx.Kind == EffectKind.Damage || (fx.Kind == EffectKind.ApplyStatus && (fx.Status == Status.Bleed || fx.Status == Status.Poison))) return true;
            return false;
        }

        public void RequestEndTurn()
        {
            if (!CanRequest) return;
            AudioManager.Play("end_turn");
            // Mâna pleacă imediat în discard: răspunsul la apăsare trebuie să fie instant.
            _hud.Hand.DiscardAll();
            _command = () => _engine.EndTurn();
        }

        // Unitatea vie de sub un punct din UI (pentru drag & drop și click pe țintă).
        public UnitView UnitAtUI(Vector2 ui)
        {
            UnitView best = null; float bestDist = float.MaxValue;
            foreach (var v in _views.Values)
            {
                if (v.Dead) continue;
                var r = v.ScreenRectUI();
                if (!r.Contains(ui)) continue;
                float d = (r.center - ui).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = v; }
            }
            return best;
        }

        // ---------- redarea evenimentelor ----------

        IEnumerator Play(List<CombatEvent> events)
        {
            _events = events;
            for (int i = 0; i < events.Count; i++)
            {
                _cursor = i;
                if (events[i] is UnitMoved)
                {
                    // Mutările vin în rafală (schimb de locuri, avansare după o moarte): le jucăm deodată.
                    var moves = new List<UnitMoved>();
                    int j = i;
                    while (j < events.Count && events[j] is UnitMoved m) { moves.Add(m); j++; }
                    yield return Safe(OnUnitsMoved(moves));
                    i = j - 1;
                    continue;
                }
                yield return Safe(PlayOne(events[i]));
            }
            yield return Safe(EndAction());
            _state.SettleInFlight();
            _hud.RefreshAll();
        }

        // Plasă de siguranță: o excepție într-o animație se scrie în log, dar lupta merge mai departe
        // (altfel corutina s-ar opri și jocul ar rămâne blocat în luptă). Rulăm noi pașii corutinei (MoveNext)
        // și învelim la fel și corutinele imbricate.
        static IEnumerator Safe(IEnumerator routine)
        {
            while (routine != null)
            {
                object current;
                try
                {
                    if (!routine.MoveNext()) yield break;
                    current = routine.Current;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    yield break;
                }
                yield return current is IEnumerator nested ? Safe(nested) : current;
            }
        }

        IEnumerator PlayOne(CombatEvent e)
        {
            switch (e)
            {
                case RoundStarted ev: return OnRoundStarted(ev);
                case TurnStarted ev: return OnTurnStarted(ev);
                case IntentChosen ev: return OnIntentChosen(ev);
                case CardsDrawn ev: return OnCardsDrawn(ev);
                case Shuffled ev: return OnShuffled(ev);
                case EnergyChanged ev: return OnEnergyChanged(ev);
                case CardPlayed ev: return OnCardPlayed(ev);
                case EnemyActed ev: return OnEnemyActed(ev);
                case Damaged ev: return OnDamaged(ev);
                case Healed ev: return OnHealed(ev);
                case BlockGained ev: return OnBlockGained(ev);
                case StatusApplied ev: return OnStatusApplied(ev);
                case StatusTicked ev: return OnStatusTicked(ev);
                case Summoned ev: return OnSummoned(ev);
                case UnitDied ev: return OnUnitDied(ev);
                case TurnSkipped ev: return OnTurnSkipped(ev);
                case CombatEnded ev: return OnCombatEnded(ev);
                default: _state.Apply(e); return null;
            }
        }

        // ---------- câte o metodă pentru fiecare eveniment ----------

        IEnumerator OnRoundStarted(RoundStarted e)
        {
            yield return EndAction();
            _state.Apply(e);
            foreach (var v in _views.Values)
            {
                var u = _state.Unit(v.Id);
                if (u != null && u.DefId == "host" && !u.Dead) v.SetHostOpen(u.HostOpen);
            }
            _hud.ClearIntents();
            _hud.TurnOrder.SetOrder(e.Order);
            _hud.Banner("ROUND " + e.Round, CombatUi.Gold, false);
            Emit("round");
            yield return Wait(e.Round == 1 ? 0.45f : 0.3f);
        }

        IEnumerator OnIntentChosen(IntentChosen e)
        {
            _state.Apply(e);
            var u = _state.Unit(e.EnemyId);
            int hits = 1; string name = e.AbilityId == "reposition" ? "Reposition" : e.AbilityId;
            if (u != null && EnemyDatabase.All.TryGetValue(u.DefId, out var def))
                foreach (var a in def.Abilities)
                    if (a.Id == e.AbilityId)
                    {
                        name = a.Name;
                        foreach (var fx in a.Effects) if (fx.Kind == EffectKind.Damage) { hits = fx.Hits; break; }
                    }
            _hud.Intent(e.EnemyId)?.Set(e.ActionIndex, e.Kind, e.PredictedDamage, hits, e.TargetIds, name);
            yield return Wait(0.05f);
        }

        IEnumerator OnTurnStarted(TurnStarted e)
        {
            yield return EndAction();
            _state.Apply(e);
            // Tura eroului anterior s-a terminat: cărțile rămase pleacă în discard.
            if (_hud.HandHero != null && _hud.HandHero != e.UnitId) _hud.Hand.DiscardAll();
            foreach (var v in _views.Values) v.SetActiveTurn(v.Id == e.UnitId);
            _hud.TurnOrder.SetActive(e.UnitId);
            var u = _state.Unit(e.UnitId);
            if (u != null && u.IsHero)
            {
                AudioManager.Play("turn_start");
                _hud.ShowHero(e.UnitId);
                yield return Wait(0.25f);
            }
            else
            {
                // În tura inamicilor panoul eroului (lacrimi, pachete) nu are ce arăta.
                _hud.ClearHero();
                yield return Wait(0.18f);
            }
        }

        IEnumerator OnShuffled(Shuffled e)
        {
            _state.Apply(e);
            if (_hud.HandHero == e.HeroId)
            {
                AudioManager.Play("shuffle");
                _hud.RefreshPiles(true);
                yield return Wait(0.2f);
            }
        }

        IEnumerator OnCardsDrawn(CardsDrawn e)
        {
            _state.Apply(e);
            if (_hud.HandHero != e.HeroId) _hud.ShowHero(e.HeroId);
            var hero = ModelUnit(e.HeroId) as HeroUnit;
            int n = 0;
            foreach (var uid in e.CardIds)
            {
                var card = FindCard(hero, uid);
                if (card == null) continue;
                _hud.Hand.Add(card, n * 0.08f);
                n++;
            }
            _hud.RefreshPiles(false);
            yield return Wait(n * 0.08f + 0.12f);
            // Captura QA „tura eroului” se face când mâna e completă, nu în clipa în care tura începe.
            if (_state.Active == e.HeroId) Emit("hero_turn");
        }

        static CardInstance FindCard(HeroUnit hero, string uid)
        {
            if (hero == null) return null;
            // Modelul poate fi deja „mai departe” (cartea jucată sau aruncată), dar rămâne în pachetul eroului.
            foreach (var c in hero.Deck.Hand) if (c.Uid == uid) return c;
            foreach (var c in hero.Deck.DiscardPile) if (c.Uid == uid) return c;
            foreach (var c in hero.Deck.DrawPile) if (c.Uid == uid) return c;
            return null;
        }

        IEnumerator OnEnergyChanged(EnergyChanged e)
        {
            _state.Apply(e);
            if (_hud.HandHero == e.HeroId) _hud.RefreshEnergy(e.Delta);
            yield break;
        }

        IEnumerator OnCardPlayed(CardPlayed e)
        {
            yield return EndAction();
            _state.Apply(e);
            var hero = View(e.HeroId);
            var target = View(e.TargetId);
            var style = AbilityFx.ForCard(e.CardId);
            AudioManager.Play("card_play");
            // Cartea e „absorbită” de erou: el o folosește, apoi atacul pleacă spre țintă.
            _hud.Hand.PlayOut(e.CardUid, hero != null ? hero.Center : (target != null ? target.Head : _stage.Center));
            if (CombatViewState.DiscardsHand(e.CardId)) _hud.Hand.DiscardAll();
            _hud.RefreshPiles(false);
            if (!TutorialHintShown) { TutorialHintShown = true; _hud.HideHint(); }
            _action = new ActionContext { Actor = hero, Style = style, CardId = e.CardId };
            Emit("card_played");
            if (hero != null) yield return WindUp(_action);
        }

        IEnumerator OnEnemyActed(EnemyActed e)
        {
            yield return EndAction();
            _state.Apply(e);
            var enemy = View(e.EnemyId);
            var style = AbilityFx.ForEnemyAbility(e.AbilityId);
            _hud.Intent(e.EnemyId)?.Consume(e.ActionIndex);
            if (!string.IsNullOrEmpty(style.Voice)) AudioManager.Play(style.Voice);
            _action = new ActionContext { Actor = enemy, Style = style, AbilityId = e.AbilityId };
            if (enemy != null && enemy.IsBoss) Emit("boss_attack");
            if (enemy != null) yield return WindUp(_action);
        }

        IEnumerator OnDamaged(Damaged e)
        {
            var target = View(e.TargetId);
            bool dot = PreviousTick(e.TargetId, out Status dotStatus);
            bool self = e.SourceId == e.TargetId;
            var ctx = _action;
            if (!dot && !self && ctx != null && target != null) yield return Deliver(target);

            int phaseBefore = _state.Unit(e.TargetId)?.Phase ?? 1;
            _state.Apply(e);
            if (target == null) yield break;
            var info = _state.Unit(e.TargetId);
            bool lethal = e.HpAfter <= 0;
            bool aoeFollowUp = ctx != null && ctx.Hits > 0 && (ctx.Style.Delivery == Delivery.Beam || ctx.Style.Delivery == Delivery.Volley || ctx.Style.Delivery == Delivery.Spray);
            bool big = e.Crit || lethal || (ctx != null && ctx.Style.Big);
            if (ctx != null && !dot) ctx.Hits++;

            // Numărul de damage: alb, galben la crit, roșu/verde la sângerare/otravă, albastru dacă blocul a oprit tot.
            Vector3 numberAt = target.Head + Vector3.up * 0.2f;
            if (dot) DamageNumbers.Spawn(numberAt, e.Amount.ToString(), dotStatus == Status.Poison ? NumberStyle.Poison : NumberStyle.Bleed, e.TargetId);
            else if (e.Amount == 0 && e.Blocked > 0) DamageNumbers.Spawn(numberAt, "BLOCKED", NumberStyle.Block, e.TargetId);
            else DamageNumbers.Spawn(numberAt, e.Crit ? e.Amount + "!" : e.Amount.ToString(), e.Crit ? NumberStyle.Crit : NumberStyle.Damage, e.TargetId);
            if (!dot && e.Blocked > 0 && e.Amount > 0) DamageNumbers.Spawn(numberAt, "-" + e.Blocked + " BLOCK", NumberStyle.Blocked, e.TargetId + "_b");

            // Sunet.
            if (e.Amount == 0 && e.Blocked > 0) AudioManager.Play("block_hit");
            else
            {
                if (e.Crit) AudioManager.Play("crit_hit");
                AudioManager.Play("hit_flesh", dot ? 0.6f : 1f);
                if (e.Blocked > 0) AudioManager.Play("block_hit", 0.6f);
            }

            // Corpul: clipire, recul, sânge.
            Vector3 dir = ctx != null && ctx.Actor != null && !self ? target.Feet - ctx.Actor.Feet : Vector3.up;
            dir.y = 0f;
            if (e.Amount > 0 || !dot) target.Hurt(dir, big);
            int blood = dot ? 5 : Mathf.Clamp(6 + e.Amount * 2, 6, 34) + (e.Crit ? 12 : 0);
            ParticleKind gore = info != null && (info.DefId == "bony" || info.DefId == "host") ? ParticleKind.Bone : ParticleKind.Blood;
            if (e.Amount > 0) Particles.Burst(target.Center, gore, blood, dir.sqrMagnitude > 0f ? dir.normalized : Vector3.up, e.Crit ? 1.4f : 1f, 0.8f);
            if (e.Amount >= 3 || e.Crit || lethal) Decals.BloodSplat(target.Feet + CombatStage.Right(_stage) * UnityEngine.Random.Range(-0.4f, 0.4f), UnityEngine.Random.Range(0.8f, 1.2f) + (e.Crit ? 0.3f : 0f), CombatStage.Right(_stage));

            // Juice: hit-stop (60–100 ms, 150 ms la crit/kill), shake (trauma), punch-in, flash.
            float severity = Mathf.Clamp01(e.RawAmount / 12f);
            float stop = dot ? 0.04f : (e.Crit || lethal) ? 0.15f : Mathf.Lerp(0.06f, 0.1f, severity);
            if (e.Amount > 0 || !dot) HitStop.Freeze(stop);
            float trauma = dot ? 0.08f : 0.14f + 0.22f * severity + (e.Crit ? 0.25f : 0f) + (lethal ? 0.1f : 0f);
            if (ctx != null && ctx.Hits == 1 && !dot) trauma += ctx.Style.Shake;
            ScreenShake.Add(trauma);
            if (ctx != null && !ctx.PunchedIn && !dot && ctx.Actor != null && (e.Crit || ctx.Style.PunchIn || ctx.Actor.IsBoss))
            {
                ctx.PunchedIn = true;
                CombatCamera.PunchIn((ctx.Actor.Center + target.Center) * 0.5f, e.Crit ? 0.15f : 0.11f, 0.4f);
            }
            if (e.Crit) ScreenFlash.Flash(Color.white, 0.18f, 0.08f);
            else if (ctx != null && ctx.Actor != null && ctx.Actor.IsBoss && ctx.Hits == 1) ScreenFlash.Flash(new Color(0.8f, 0.1f, 0.1f), 0.18f, 0.12f);

            _hud.UnitHud(e.TargetId)?.OnDamaged(e.Amount, e.Blocked);
            Emit(e.Crit ? "crit" : "hit");

            // Mom intră în faza a doua: anunț mare.
            if (info != null && info.DefId == "mom" && phaseBefore == 1 && info.Phase == 2 && !lethal && !_momFurious)
            {
                _momFurious = true;
                target.SetFury(true);
                AudioManager.Play("monster_roar");
                _hud.Banner("MOM IS FURIOUS", Palette.Blood, true);
                ScreenShake.Add(0.5f);
            }

            yield return Wait(dot ? 0.2f : aoeFollowUp ? 0.09f : e.Crit ? 0.3f : 0.17f);
        }

        IEnumerator OnHealed(Healed e)
        {
            var t = View(e.TargetId);
            if (e.Revive)
            {
                _state.Apply(e);
                if (t == null) yield break;
                AudioManager.Play("heal");
                _hud.UnitHud(t.Id)?.SetDead(false);
                DamageNumbers.Spawn(t.Head + Vector3.up * 0.2f, "REGEN", NumberStyle.Heal, t.Id);
                yield return t.Revive();
                yield break;
            }
            if (t != null && _action != null && t != _action.Actor) yield return Deliver(t);
            _state.Apply(e);
            if (t == null) yield break;
            AudioManager.Play("heal");
            Particles.Burst(t.Center, ParticleKind.Heal, 14, Vector3.up, 1f, 1f);
            t.Flash(new Color(0.55f, 1f, 0.45f), 0.25f);
            t.Squash(0.9f, 1.12f);
            DamageNumbers.Spawn(t.Head + Vector3.up * 0.2f, "+" + e.Amount, NumberStyle.Heal, t.Id);
            _hud.UnitHud(t.Id)?.Refresh();
            yield return Wait(0.22f);
        }

        IEnumerator OnBlockGained(BlockGained e)
        {
            var t = View(e.TargetId);
            if (t != null && _action != null && t != _action.Actor) yield return Deliver(t);
            _state.Apply(e);
            if (t == null) yield break;
            AudioManager.Play("block_gain");
            Particles.Burst(t.Center, ParticleKind.Block, 12, Vector3.up, 1f, 1f);
            t.Flash(new Color(0.5f, 0.75f, 1f), 0.2f);
            t.Squash(1.1f, 0.92f);
            DamageNumbers.Spawn(t.Head + Vector3.up * 0.2f, "+" + e.Amount, NumberStyle.Block, t.Id);
            _hud.UnitHud(t.Id)?.PopStatus(Status.Block);
            yield return Wait(0.2f);
        }

        IEnumerator OnStatusApplied(StatusApplied e)
        {
            var t = View(e.TargetId);
            // Blestemele fără damage (Cursed Eye, ochiul lui Mom) au nevoie și ele de o „livrare” vizibilă.
            if (t != null && _action != null && t != _action.Actor && !_action.DeliveredTo.Contains(t.Id)) yield return Deliver(t);
            _state.Apply(e);
            if (t == null) yield break;
            Color color = CombatUi.StatusColor(e.Status);
            switch (e.Status)
            {
                case Status.Bleed: AudioManager.Play("bleed_apply"); Particles.Burst(t.Center, ParticleKind.Blood, 8, Vector3.down, 0.6f, 1f); break;
                case Status.Poison: AudioManager.Play("poison_apply"); Particles.Burst(t.Center, ParticleKind.Poison, 12, Vector3.up, 1f, 1f); break;
                case Status.Stun: AudioManager.Play("stun"); StunStars(t, 0.6f); break;
                case Status.Weak:
                case Status.Mark: AudioManager.Play("debuff"); Particles.Burst(t.Center, ParticleKind.Hex, 10, Vector3.up, 1f, 1f); break;
                default: AudioManager.Play("buff"); Particles.Burst(t.Center, ParticleKind.Ember, 10, Vector3.up, 1f, 1f); t.Squash(0.86f, 1.18f); break;
            }
            t.Flash(color, 0.18f);
            string label = e.Status == Status.Strength ? "+" + e.Stacks + " STR" : e.Status.ToString().ToUpperInvariant() + " " + e.Stacks;
            DamageNumbers.Spawn(t.Head + Vector3.up * 0.35f, label, NumberStyle.Info, t.Id + "_s", color);
            _hud.UnitHud(t.Id)?.PopStatus(e.Status);
            yield return Wait(0.16f);
        }

        IEnumerator OnStatusTicked(StatusTicked e)
        {
            _state.Apply(e);
            var t = View(e.UnitId);
            _hud.UnitHud(e.UnitId)?.Refresh();
            if (t == null) yield break;
            switch (e.Status)
            {
                case Status.Block:
                    if (e.Amount > 0) { Particles.Burst(t.Center, ParticleKind.Block, 6, Vector3.down, 0.6f, 1f); yield return Wait(0.06f); }
                    break;
                case Status.Bleed:
                    AudioManager.Play("bleed_apply", 0.5f);
                    Particles.Burst(t.Center, ParticleKind.Blood, 10, Vector3.down, 0.7f, 1f);
                    _hud.UnitHud(e.UnitId)?.PopStatus(Status.Bleed);
                    yield return Wait(0.14f);
                    break;
                case Status.Poison:
                    AudioManager.Play("poison_apply", 0.5f);
                    Particles.Burst(t.Center, ParticleKind.Poison, 12, Vector3.up, 0.8f, 1f);
                    _hud.UnitHud(e.UnitId)?.PopStatus(Status.Poison);
                    yield return Wait(0.14f);
                    break;
            }
        }

        IEnumerator OnTurnSkipped(TurnSkipped e)
        {
            _state.Apply(e);
            var t = View(e.UnitId);
            if (t == null) yield break;
            AudioManager.Play("stun");
            // Modelul nu emite EnemyActed pentru tura pierdută: intenția trebuie ștearsă de noi.
            _hud.Intent(t.Id)?.Clear();
            StunStars(t, 0.9f);
            t.Squash(1.15f, 0.85f);
            DamageNumbers.Spawn(t.Head + Vector3.up * 0.3f, "STUNNED", NumberStyle.Info, t.Id + "_s", CombatUi.StatusColor(Status.Stun));
            yield return Wait(0.6f);
        }

        IEnumerator OnUnitDied(UnitDied e)
        {
            _state.Apply(e);
            var t = View(e.UnitId);
            if (t == null) yield break;
            bool revives = NextIsRevive(e.UnitId);
            AudioManager.Play(e.IsHero ? "hero_death" : "enemy_death_splat");
            Vector3 right = CombatStage.Right(_stage);
            Particles.Burst(t.Center, ParticleKind.Blood, t.IsBoss ? 70 : 34, Vector3.up, 1.3f, 1f);
            Decals.BloodSplat(t.Feet, t.IsBoss ? 2.2f : 1.4f, right);
            Decals.BloodSplat(t.Feet + right * UnityEngine.Random.Range(-0.7f, 0.7f), 0.8f, right);
            ScreenShake.Add(t.IsBoss ? 0.8f : 0.32f);
            if (t.IsBoss) { ScreenFlash.Flash(new Color(0.85f, 0.8f, 0.75f), 0.2f, 0.3f); CombatCamera.PunchIn(t.Center, 0.18f, 0.7f); HitStop.Freeze(0.25f); }
            if (!revives)
            {
                // Globin se reface imediat și își păstrează intenția; numai moartea definitivă o șterge.
                _hud.Intent(t.Id)?.Clear();
                _hud.UnitHud(t.Id)?.SetDead(true);
                _hud.TurnOrder.Remove(t.Id);
            }
            yield return t.Die();
            if (!revives)
            {
                // Cadavrul rămâne pe jos, desenat sub cei vii (supraviețuitorii trec prin fața lui).
                t.SetOrderBoost(-7);
                _hud.RemoveUnit(t.Id);
                if (!t.IsHero) KeepCorpse(t);
                Emit("kill");
                if (t.IsHero && _state.LivingCount(true) == 1 && !_lastStand)
                {
                    // Ultimul erou rămas poate juca orice carte din orice rang (regula Last Stand din model).
                    _lastStand = true;
                    AudioManager.Play("buff");
                    _hud.Banner("LAST STAND", Palette.Blood, true);
                    _hud.SetLastStand(true);
                    Emit("last_stand");
                    yield return Wait(0.6f);
                }
            }
            yield return Wait(revives ? 0.15f : 0.3f);
        }

        // Mutările NU încheie acțiunea curentă: după o moarte, compactarea vine între loviturile aceluiași atac
        // (Dr. Fetus, Blood Beam), iar restul țintelor trebuie să-și primească în continuare proiectilul.
        IEnumerator OnUnitsMoved(List<UnitMoved> moves)
        {
            foreach (var m in moves) _state.Apply(m);
            float duration = 0.26f;
            bool any = false;
            foreach (var v in _views.Values)
            {
                if (v.Dead) continue;
                Vector3 home = HomeOf(v.Id);
                if ((home - v.Home).sqrMagnitude < 1e-4f) continue;
                StartCoroutine(v.SlideTo(home, duration));
                any = true;
            }
            if (any)
            {
                AudioManager.Play("footstep_stone", 0.7f);
                _hud.RefreshAll();
                yield return Wait(duration + 0.04f);
            }
        }

        IEnumerator OnSummoned(Summoned e)
        {
            _state.Apply(e);
            var info = _state.Unit(e.UnitId);
            var v = SpawnView(info);
            v.Home = HomeOf(v.Id);
            // Invocatorul: Gurdy / Mulligan (cadavrul) / ușa lui Mom.
            var source = View(e.SourceId);
            Vector3 from = _action != null && _action.HasCastOrigin ? _action.CastOrigin : source != null ? source.Center : v.Home + Vector3.up * 3f;
            _hud.AddUnit(v, hidden: true);
            AudioManager.Play(info.DefId == "fly" || info.DefId == "pooter" ? "fly_buzz" : "gaper_groan", 0.8f);
            Particles.Burst(from, ParticleKind.Smoke, 10, Vector3.up, 1f, 1f);
            yield return v.PopIn(from);
            _hud.UnitHud(v.Id)?.PopIn();
            yield return Wait(0.08f);
        }

        IEnumerator OnCombatEnded(CombatEnded e)
        {
            yield return EndAction();
            _state.Apply(e);
            foreach (var v in _views.Values) v.SetActiveTurn(false);
            _hud.Hand.DiscardAll();
            _hud.HideHint();
            _hud.ClearIntents();
            if (e.Victory)
            {
                AudioManager.Play("victory_sting");
                _hud.Banner("VICTORY", CombatUi.Gold, true);
                foreach (var v in _views.Values) if (v.IsHero && !v.Dead) StartCoroutine(Cheer(v));
                Emit("victory");
            }
            else
            {
                AudioManager.Play("defeat_sting");
                _hud.Banner("DEFEAT", Palette.Blood, true);
                ScreenFlash.Flash(new Color(0.4f, 0f, 0f), 0.5f, 1.2f);
                Emit("defeat");
            }
            yield return Wait(1.7f);
        }

        // ---------- final ----------

        void Finish()
        {
            _finished = true;
            CombatCamera.StageFocus = null;
            if (Active == this) Active = null;
            HitStop.Reset();
            HitStop.BaseTimeScale = 1f;
            ScreenShake.Clear();
            if (_hud != null) { _hud.Close(); _hud = null; }
            // Cadavrele rămân în cameră (ca petele de sânge); unitățile vii dispar (W arată echipa de explorare).
            foreach (var v in _views.Values)
            {
                if (v == null) continue;
                if (v.Dead) Decals.Keep(v.gameObject);
                else Destroy(v.gameObject);
            }
            _views.Clear();
            Emit("finished");
            var callback = _onFinished;
            bool victory = _engine.Victory;
            Destroy(gameObject);
            callback?.Invoke(victory);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame && !GameFlow.Paused)
            {
                Fast = !Fast;
                HitStop.BaseTimeScale = Fast ? 2f : 1f;
                if (_hud != null) _hud.ShowSpeed(Fast);
            }
        }

        // Lupta poate fi distrusă din afară (Abandon run din meniul de pauză): curățăm tot ce ține de ea.
        void OnDestroy()
        {
            if (FxHost.Stage == transform) FxHost.Stage = null;
            if (_finished) return;
            CombatCamera.StageFocus = null;
            if (Active == this) Active = null;
            HitStop.Reset();
            ScreenShake.Clear();
            Particles.Clear();
            if (_hud != null) { _hud.Close(); _hud = null; }
            foreach (var v in _views.Values) if (v != null && v.Incubus != null) Destroy(v.Incubus.gameObject);
        }
    }
}
