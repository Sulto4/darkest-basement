using System.Collections;
using System.Collections.Generic;
using DarkestBasement.Combat;
using DarkestBasement.Core;
using DarkestBasement.Model.Combat;
using DarkestBasement.Model.Sim;
using UnityEngine;

namespace DarkestBasement.Exploration
{
    // Plasă de siguranță cât timp CombatController (echipa C) e încă un stub: arată unitățile pe scenă ca
    // sprite-uri simple și rezolvă lupta direct în model cu AutoPlayer, ca restul jocului (recompense, hartă,
    // etaje, QA) să poată fi jucat și verificat de la un capăt la altul. Nu e lupta adevărată.
    public sealed class StagePlaceholder : MonoBehaviour
    {
        readonly Dictionary<string, Billboard> _units = new Dictionary<string, Billboard>();
        CombatStageInfo _stage;

        public static StagePlaceholder Create(Transform parent, CombatEngine engine, CombatStageInfo stage)
        {
            var go = new GameObject("StagePlaceholder");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<StagePlaceholder>();
            p._stage = stage;
            foreach (var unit in engine.State.Units) p.Spawn(unit);
            return p;
        }

        Vector3 Position(Unit unit)
        {
            Vector3 right = _stage.Right.sqrMagnitude > 0.0001f ? _stage.Right.normalized : Vector3.forward;
            float offset = 0.8f + 1.6f * (unit.Rank - 1);
            return _stage.Center + right * (unit.IsHero ? -offset : offset);
        }

        void Spawn(Unit unit)
        {
            Sprite[] frames;
            if (unit is HeroUnit hero) frames = SpriteLibrary.Frames("Sprites/Heroes/" + hero.Def.Id + "/side_idle");
            else
            {
                var enemy = (EnemyUnit)unit;
                string folder = enemy.IsBoss ? "Sprites/Bosses/" : "Sprites/Enemies/";
                frames = SpriteLibrary.TryFrames(folder + enemy.Def.Id + "/idle") ?? SpriteLibrary.Frames(folder + enemy.Def.Id + "/idle");
            }
            var b = Billboard.Create(transform, unit.Id, frames[0], Position(unit));
            b.Play(frames, 2.5f, true, unit.Rank * 0.37f);
            if (!unit.IsAlive) b.SetVisible(false);
            _units[unit.Id] = b;
        }

        public IEnumerator Resolve(CombatEngine engine, float turnDelay)
        {
            if (engine.State.Round == 0) { engine.StartCombat(); Consume(engine, engine.Events); }
            var ai = new AutoPlayer();
            while (!engine.IsOver)
            {
                // Aceeași regulă ca simulatorul: fără atacuri sau după 40 de runde, echipa renunță.
                if (ai.HasNoOffense(engine.State) || engine.State.Round > 40) { engine.Concede(); break; }
                ai.PlayTurn(engine, events => Consume(engine, events));
                yield return new WaitForSeconds(turnDelay);
            }
        }

        void Consume(CombatEngine engine, IReadOnlyList<CombatEvent> events)
        {
            foreach (var ev in events)
            {
                if (ev is Damaged hit && _units.TryGetValue(hit.TargetId, out var target))
                {
                    target.Flash(hit.Crit ? new Color(1f, 0.9f, 0.3f) : Color.white, 0.12f);
                    if (hit.Amount > 0) FloatingText.Spawn(target.transform.position + Vector3.up * 1.4f, hit.Amount.ToString(), hit.Crit ? new Color(1f, 0.85f, 0.3f) : Color.white);
                }
                else if (ev is UnitDied died && _units.TryGetValue(died.UnitId, out var dead)) dead.SetVisible(false);
                else if (ev is Healed healed && healed.Revive && _units.TryGetValue(healed.TargetId, out var revived)) revived.SetVisible(true);
                else if (ev is Summoned summoned)
                {
                    foreach (var unit in engine.State.Units)
                        if (unit.Id == summoned.UnitId && !_units.ContainsKey(unit.Id)) Spawn(unit);
                }
                else if (ev is UnitMoved)
                {
                    foreach (var unit in engine.State.Units)
                        if (_units.TryGetValue(unit.Id, out var b)) b.transform.position = Position(unit);
                }
            }
        }
    }
}
