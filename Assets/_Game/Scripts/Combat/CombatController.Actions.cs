using System;
using System.Collections;
using System.Collections.Generic;
using DarkestBasement.Audio;
using DarkestBasement.Core;
using DarkestBasement.FX;
using DarkestBasement.Model.Combat;
using DarkestBasement.UI;
using UnityEngine;

namespace DarkestBasement.Combat
{
    // A doua jumătate a controller-ului: CUM arată fiecare acțiune.
    // WindUp = anticiparea atacatorului; Deliver = drumul până la țintă (lunge, lacrimă, rază, piciorul lui Mom),
    // care se termină exact la impact; EndAction = întoarcerea acasă.
    public sealed partial class CombatController
    {
        // ---------- pregătirea și livrarea atacurilor ----------

        // Anticiparea: ce face atacatorul înainte de impact, după tipul de livrare.
        IEnumerator WindUp(ActionContext ctx)
        {
            var actor = ctx.Actor;
            var s = ctx.Style;
            switch (s.Delivery)
            {
                case Delivery.Melee:
                    yield return actor.Anticipate(s.Big ? 0.2f : 0.14f, s.Big ? 0.32f : 0.22f);
                    break;
                case Delivery.Projectile:
                case Delivery.Volley:
                    if (s.FromIncubus) actor.Hold(actor.Frames.Attack[0], 0.5f);
                    else yield return actor.Anticipate(0.1f, 0.1f);
                    break;
                case Delivery.Beam:
                {
                    actor.Hold(actor.Frames.Attack[0], 1f);
                    actor.Squash(1.1f, 0.9f);
                    AudioManager.Play("brimstone_charge", 0.8f);
                    if (s.FromIncubus && actor.Incubus != null) yield return BeamFx.Charge(actor.Incubus.Muzzle, 0.25f, s.Beam);
                    else yield return BeamFx.Charge(actor.Muzzle, s.Big ? 0.42f : 0.3f, s.Beam);
                    break;
                }
                case Delivery.Cast:
                case Delivery.Self:
                    actor.Hold(actor.Frames.Attack[actor.Frames.Attack.Length - 1], 0.4f);
                    actor.Flash(s.CastColor, 0.22f);
                    actor.Squash(0.88f, 1.14f);
                    Particles.Burst(actor.Center, s.CastParticles, 12, Vector3.up, 1f, 1f);
                    if (ctx.AbilityId == "closed") actor.SetHostOpen(false);
                    if (ctx.AbilityId == "taunt") DamageNumbers.Spawn(actor.Head + Vector3.up * 0.3f, "TAUNT", NumberStyle.Info, actor.Id + "_s", CombatUi.StatusColor(Status.Taunt));
                    yield return Wait(0.18f);
                    break;
                case Delivery.Summon:
                    actor.Hold(actor.Frames.Attack[actor.Frames.Attack.Length - 1], 0.6f);
                    actor.Squash(1.2f, 0.85f);
                    ScreenShake.Add(0.3f);
                    yield return Wait(0.25f);
                    break;
                case Delivery.Eye:
                    yield return ShowMomEye(ctx);
                    break;
                case Delivery.Spray:
                case Delivery.Rain:
                    actor.Hold(actor.Frames.Attack[actor.Frames.Attack.Length - 1], 0.7f);
                    actor.Squash(1.15f, 0.88f);
                    ScreenShake.Add(0.2f);
                    yield return Wait(0.2f);
                    break;
                case Delivery.Stomp:
                case Delivery.Hand:
                case Delivery.Leap:
                case Delivery.None:
                    break;
            }
        }

        // Livrarea pe o țintă: corutina se termină exact în momentul impactului.
        IEnumerator Deliver(UnitView target)
        {
            var ctx = _action;
            if (ctx == null || ctx.Actor == null || target == null) yield break;
            var actor = ctx.Actor;
            var s = ctx.Style;
            bool first = !ctx.DeliveredTo.Contains(target.Id);
            ctx.DeliveredTo.Add(target.Id);
            Vector3 dir = target.Feet - actor.Feet; dir.y = 0f;
            switch (s.Delivery)
            {
                case Delivery.Melee:
                    if (!ctx.Lunged)
                    {
                        ctx.Lunged = true;
                        if (!string.IsNullOrEmpty(s.LaunchSfx)) AudioManager.Play(s.LaunchSfx);
                        float stop = actor.HalfWidth + target.HalfWidth + 0.1f;
                        yield return actor.Lunge(target.Feet, s.Big ? 0.09f : 0.1f, stop);
                        if (s.MiniBeam) BeamFx.Fire(actor.Muzzle, target.Center + dir.normalized * 0.3f, BeamStyle.Lil);
                        if (s.CastParticles == ParticleKind.Gold) Particles.Burst(target.Center, ParticleKind.Gold, 16, -dir, 1.2f, 1f);
                    }
                    break;

                case Delivery.Projectile:
                    if (s.FromIncubus && actor.Incubus != null)
                        yield return actor.Incubus.Shoot(target.Center, s.Projectile, s.ProjectileSize);
                    else
                    {
                        if (!string.IsNullOrEmpty(s.LaunchSfx)) AudioManager.Play(s.LaunchSfx);
                        actor.StartCoroutine(actor.Recoil());
                        // La un atac pe mai multe ținte (Dr. Fetus), proiectilele de după primul zboară mai repede.
                        float flight = ctx.DeliveredTo.Count > 1 ? 0.22f : -1f;
                        yield return Projectiles.Fly(s.Projectile, actor.Muzzle, target.Center, s.ProjectileSize, 1f, flight);
                    }
                    Projectiles.Impact(s.Projectile, target.Center, dir.normalized, s.ProjectileSize);
                    break;

                case Delivery.Volley:
                    if (!ctx.Delivered)
                    {
                        ctx.Delivered = true;
                        actor.StartCoroutine(actor.Recoil());
                        Vector3 center = Centroid(ActionTargets());
                        yield return Projectiles.Fly(s.Projectile, actor.Muzzle, center + Vector3.up * 0.4f, s.ProjectileSize);
                        Projectiles.Impact(s.Projectile, center + Vector3.up * 0.3f, dir.normalized, 1.5f);
                    }
                    break;

                case Delivery.Beam:
                    if (!ctx.Delivered)
                    {
                        ctx.Delivered = true;
                        Vector3 origin = s.FromIncubus && actor.Incubus != null ? actor.Incubus.Muzzle : actor.Muzzle;
                        // Raza trece prin toate țintele și se oprește puțin după cea mai îndepărtată.
                        Vector3 far = target.Center;
                        foreach (var t in ActionTargets()) if (Vector3.Distance(origin, t.Center) > Vector3.Distance(origin, far)) far = t.Center;
                        far += (far - origin).normalized * 1.2f;
                        far.y = origin.y + (far.y - origin.y) * 0.3f;
                        AudioManager.Play("brimstone_beam");
                        BeamFx.Fire(origin, far, s.Beam);
                        FxVolume.Chromatic(s.Big ? 0.55f : 0.4f, s.Beam.Duration + 0.2f);
                        actor.Hold(actor.Frames.Attack[actor.Frames.Attack.Length - 1], s.Beam.Duration);
                        actor.StartCoroutine(actor.Recoil(0.18f));
                        CombatCamera.PunchIn((origin + far) * 0.5f, s.Big ? 0.16f : 0.12f, s.Beam.Duration + 0.15f);
                        ctx.PunchedIn = true;
                        ScreenShake.Add(s.Big ? 0.5f : 0.3f);
                        Emit(actor.IsBoss ? "boss_impact" : "beam");
                        yield return Wait(0.06f);
                    }
                    break;

                case Delivery.Leap:
                    if (!ctx.Lunged)
                    {
                        ctx.Lunged = true;
                        yield return actor.Leap(target.Feet, s.LeapHeight, s.Big ? 0.42f : 0.32f, actor.HalfWidth + target.HalfWidth);
                        Particles.Burst(actor.Feet, ParticleKind.Dust, s.Big ? 22 : 10, Vector3.up, 1.2f, 1f);
                        if (actor.IsBoss) { AudioManager.Play("monstro_land"); ScreenShake.Add(0.45f); Emit("boss_impact"); }
                    }
                    break;

                case Delivery.Stomp:
                    if (!ctx.Delivered)
                    {
                        ctx.Delivered = true;
                        yield return MomStomp(ctx, Centroid(ActionTargets()));
                    }
                    break;

                case Delivery.Hand:
                    if (!ctx.Delivered)
                    {
                        ctx.Delivered = true;
                        yield return MomHand(ctx, target);
                    }
                    break;

                case Delivery.Rain:
                    if (first)
                    {
                        Vector3 top = target.Center + Vector3.up * 6.5f + CombatStage.Right(_stage) * UnityEngine.Random.Range(-0.3f, 0.3f);
                        yield return Projectiles.Fly(s.Projectile, top, target.Center, 1.3f, 0f, 0.22f);
                        Projectiles.Impact(s.Projectile, target.Center, Vector3.down, 1.3f);
                    }
                    break;

                case Delivery.Spray:
                    if (!ctx.Delivered)
                    {
                        ctx.Delivered = true;
                        actor.StartCoroutine(actor.Recoil(0.2f));
                        foreach (var t in ActionTargets())
                        {
                            Vector3 d = t.Center - actor.Muzzle;
                            Particles.Burst(actor.Muzzle, ParticleKind.Blood, 18, d.normalized, 2.2f, 0.35f);
                        }
                        yield return Wait(0.16f);
                    }
                    break;

                case Delivery.Cast:
                case Delivery.Eye:
                    if (first && target != actor)
                    {
                        Vector3 origin = ctx.HasCastOrigin ? ctx.CastOrigin : actor.Muzzle;
                        AudioManager.Play("tear_shot", 0.5f, 1.4f);
                        yield return Projectiles.Fly(ProjectileKind.Hex, origin, target.Center, 1.4f, 0.6f, 0.22f, s.CastColor);
                        Particles.Burst(target.Center, s.CastParticles, 10, Vector3.up, 1f, 1f);
                    }
                    break;
            }
        }

        // Piciorul lui Mom: iese din cadru, umbra crește peste ținte, apoi calcă. Rămâne jos până la EndAction.
        IEnumerator MomStomp(ActionContext ctx, Vector3 center)
        {
            var mom = ctx.Actor;
            var raised = mom.Frames.Attack[0];
            var impact = mom.Frames.Attack[mom.Frames.Attack.Length - 1];
            mom.Hold(raised, 3f);
            mom.SetOrderBoost(20);
            yield return mom.TweenOffset(mom.Offset, Vector3.up * 7f, 0.22f, Ease.InQuad);
            Vector3 above = center - mom.Home; above.y = 7f;
            mom.SetOffset(above);
            // Umbra care crește: avertismentul clasic din Isaac.
            var shadow = FxHost.Spawn("MomShadow").AddComponent<SpriteRenderer>();
            shadow.sprite = PixelSprites.Shadow(72, 10);
            shadow.sortingOrder = 30;
            float t = 0f;
            while (t < 0.32f)
            {
                t += Time.deltaTime;
                shadow.transform.position = new Vector3(center.x, center.y + 0.02f, center.z);
                shadow.transform.rotation = CombatCamera.Billboard;
                shadow.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.3f, t / 0.32f);
                yield return null;
            }
            Vector3 down = center - mom.Home; down.y = 0f;
            yield return mom.TweenOffset(above, down, 0.08f, Ease.InCubic);
            Destroy(shadow.gameObject);
            mom.Hold(impact, 3f);
            mom.Squash(1.15f, 0.85f);
            AudioManager.Play("mom_stomp");
            ScreenShake.Add(0.75f);
            ScreenFlash.Flash(new Color(1f, 0.9f, 0.8f), 0.15f, 0.12f);
            CombatCamera.PunchIn(center + Vector3.up, 0.16f, 0.5f);
            ctx.PunchedIn = true;
            Particles.Burst(center, ParticleKind.Dust, 30, Vector3.up, 1.6f, 1f);
            Decals.BloodSplat(center, 1.8f, CombatStage.Right(_stage));
            ctx.Stomped = true;
            Emit("boss_impact");
        }

        IEnumerator MomStompReturn(UnitView mom)
        {
            yield return Wait(0.2f);
            Vector3 up = mom.Offset; up.y = 7f;
            yield return mom.TweenOffset(mom.Offset, up, 0.2f, Ease.InQuad);
            mom.SetOffset(Vector3.up * 7f);
            mom.ReleaseHold();
            yield return mom.TweenOffset(Vector3.up * 7f, Vector3.zero, 0.25f, Ease.OutBounce);
            mom.SetOrderBoost(0);
            ScreenShake.Add(0.2f);
        }

        // Mâna lui Mom intră din marginea dreaptă și înșfacă ținta (rangul 4), apoi o trage în față.
        IEnumerator MomHand(ActionContext ctx, UnitView target)
        {
            var frames = ctx.Actor.Frames.Extra("hand");
            var go = new GameObject("MomHand");
            go.transform.SetParent(transform, true);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 32;
            sr.sprite = frames != null ? frames[0] : ctx.Actor.Frames.Idle[0];
            // Mâneca se continuă spre dreapta, în afara ecranului: întindem ultima coloană a sprite-ului.
            var sleeve = new GameObject("Sleeve").AddComponent<SpriteRenderer>();
            sleeve.transform.SetParent(go.transform, false);
            sleeve.sortingOrder = 31;
            Rect r = sr.sprite.textureRect;
            sleeve.sprite = SleeveOf(sr.sprite);
            sleeve.transform.localPosition = new Vector3((r.width - sr.sprite.pivot.x) / sr.sprite.pixelsPerUnit, 0f, 0f);
            sleeve.transform.localScale = new Vector3(600f, 1f, 1f);
            ctx.Prop = go;

            Vector3 right = CombatStage.Right(_stage);
            float handHalf = r.width / sr.sprite.pixelsPerUnit * 0.5f;
            Vector3 start = _stage.Center + right * 12f + Vector3.up * 0.4f;
            Vector3 grab = target.Feet + right * (handHalf + target.HalfWidth * 0.3f) + Vector3.up * 0.2f;
            float t = 0f;
            AudioManager.Play("knife_throw", 0.8f, 0.6f);
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                go.transform.position = Vector3.Lerp(start, grab, Easing.Evaluate(Ease.OutCubic, t / 0.3f));
                go.transform.rotation = CombatCamera.Billboard;
                yield return null;
            }
            if (frames != null && frames.Length > 1) sr.sprite = frames[1];
            ScreenShake.Add(0.4f);
            Emit("boss_impact");
        }

        // Ultima coloană de pixeli a mâinii (mâneca), ca sprite separat; o calculăm o singură dată.
        static readonly Dictionary<Sprite, Sprite> Sleeves = new Dictionary<Sprite, Sprite>();
        static Sprite SleeveOf(Sprite hand)
        {
            if (Sleeves.TryGetValue(hand, out var s)) return s;
            Rect r = hand.textureRect;
            s = Sprite.Create(hand.texture, new Rect(r.x + r.width - 1, r.y, 1, r.height), new Vector2(0f, hand.pivot.y / r.height), hand.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            Sleeves[hand] = s;
            return s;
        }

        IEnumerator MomHandReturn(ActionContext ctx)
        {
            var go = ctx.Prop;
            if (go == null) yield break;
            Vector3 from = go.transform.position;
            Vector3 to = _stage.Center + CombatStage.Right(_stage) * 13f + Vector3.up * 0.4f;
            float t = 0f;
            while (t < 0.3f && go != null)
            {
                t += Time.deltaTime;
                go.transform.position = Vector3.Lerp(from, to, Easing.Evaluate(Ease.InCubic, t / 0.3f));
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        // Ochiul lui Mom apare într-o ușă din peretele din spate (Mom's Eye, Door Summon).
        IEnumerator ShowMomEye(ActionContext ctx)
        {
            var frames = ctx.Actor.Frames.Extra("eye");
            // Piciorul se ridică din cadru și lasă la vedere ușa din spatele lui, unde se deschide ochiul.
            var mom = ctx.Actor;
            mom.Hold(mom.Frames.Attack[0], 3f);
            mom.SetOrderBoost(20);
            yield return mom.TweenOffset(mom.Offset, Vector3.up * 7f, 0.22f, Ease.InQuad);
            ctx.Stomped = true;   // la final piciorul trebuie să coboare la loc (MomStompReturn)
            Vector3 pos = CombatStage.Point(_stage, false, CombatStage.BossCenter) + CombatStage.Depth(_stage) * 1.4f;
            var go = new GameObject("MomEye");
            go.transform.SetParent(transform, true);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames != null ? frames[0] : ctx.Actor.Frames.Idle[0];
            sr.sortingOrder = 4;
            go.transform.position = pos;
            go.transform.rotation = CombatCamera.Billboard;
            ctx.Prop = go;
            ctx.CastOrigin = pos + Vector3.up * (sr.sprite.rect.height * 0.45f / sr.sprite.pixelsPerUnit);
            ctx.HasCastOrigin = true;
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                go.transform.localScale = new Vector3(1f, Easing.Evaluate(Ease.OutBack, Mathf.Clamp01(t / 0.3f)), 1f);
                yield return null;
            }
            if (frames != null && frames.Length > 1) sr.sprite = frames[1];
            AudioManager.Play(ctx.AbilityId == "moms_eye" ? "debuff" : "door_open");
            Particles.Burst(ctx.CastOrigin, ctx.Style.CastParticles, 16, Vector3.up, 1.2f, 1f);
            CombatCamera.PunchIn(ctx.CastOrigin, 0.1f, 0.4f);
            Emit("boss_impact");
            yield return Wait(0.25f);
        }

        // Sfârșitul acțiunii: atacatorul se întoarce acasă, recuzita lui Mom dispare.
        IEnumerator EndAction()
        {
            var ctx = _action;
            _action = null;
            if (ctx == null) yield break;
            if (ctx.Style.Delivery == Delivery.Stomp && ctx.Stomped && ctx.Actor != null && !ctx.Actor.Dead) yield return MomStompReturn(ctx.Actor);
            else if (ctx.Style.Delivery == Delivery.Hand) yield return MomHandReturn(ctx);
            else if (ctx.Style.Delivery == Delivery.Eye)
            {
                if (ctx.Prop != null) StartCoroutine(HideProp(ctx.Prop));
                if (ctx.Stomped && ctx.Actor != null && !ctx.Actor.Dead) yield return MomStompReturn(ctx.Actor);
            }
            if (ctx.Actor != null && ctx.Actor.Incubus != null) StartCoroutine(ctx.Actor.Incubus.Return());
            if (ctx.Actor != null && !ctx.Actor.Dead) yield return ctx.Actor.ReturnHome(ctx.Lunged ? 0.24f : 0.12f);
        }

        IEnumerator HideProp(GameObject prop)
        {
            float t = 0f;
            while (t < 0.25f && prop != null)
            {
                t += Time.deltaTime;
                prop.transform.localScale = new Vector3(1f, 1f - t / 0.25f, 1f);
                yield return null;
            }
            if (prop != null) Destroy(prop);
        }

        // ---------- mici ajutoare ----------

        // Țintele acțiunii curente: privim înainte în listă, până la următoarea acțiune.
        List<UnitView> ActionTargets()
        {
            var result = new List<UnitView>();
            if (_events == null) return result;
            for (int i = _cursor; i < _events.Count; i++)
            {
                var e = _events[i];
                if (i > _cursor && (e is CardPlayed || e is EnemyActed || e is TurnStarted || e is RoundStarted || e is CombatEnded)) break;
                string id = e is Damaged d ? d.TargetId : e is StatusApplied s ? s.TargetId : null;
                var v = View(id);
                if (v != null && !result.Contains(v) && (_action == null || v != _action.Actor)) result.Add(v);
            }
            return result;
        }

        static Vector3 Centroid(List<UnitView> views)
        {
            if (views.Count == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            foreach (var v in views) sum += v.Feet;
            return sum / views.Count;
        }

        // Un Damaged venit imediat după StatusTicked(Bleed/Poison) pe aceeași unitate e damage din status.
        bool PreviousTick(string unitId, out Status status)
        {
            status = Status.Bleed;
            if (_events == null || _cursor <= 0) return false;
            if (_events[_cursor - 1] is StatusTicked tick && tick.UnitId == unitId && (tick.Status == Status.Bleed || tick.Status == Status.Poison))
            {
                status = tick.Status;
                return true;
            }
            return false;
        }

        bool NextIsRevive(string unitId)
        {
            if (_events == null || _cursor + 1 >= _events.Count) return false;
            return _events[_cursor + 1] is Healed h && h.TargetId == unitId && h.Revive;
        }

        void StunStars(UnitView t, float duration) => StartCoroutine(StunStarsRoutine(t, duration));

        IEnumerator StunStarsRoutine(UnitView t, float duration)
        {
            var stars = new SpriteRenderer[3];
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i] = FxHost.Spawn("StunStar").AddComponent<SpriteRenderer>();
                stars[i].sprite = PixelSprites.Star;
                stars[i].sortingOrder = 44;
            }
            float time = 0f;
            while (time < duration && t != null)
            {
                time += Time.deltaTime;
                for (int i = 0; i < stars.Length; i++)
                {
                    float a = time * 7f + i * Mathf.PI * 2f / stars.Length;
                    var face = CombatCamera.Billboard;
                    stars[i].transform.position = t.Head + Vector3.up * 0.15f + face * new Vector3(Mathf.Cos(a) * 0.4f, Mathf.Sin(a) * 0.12f, 0f);
                    stars[i].transform.rotation = face;
                }
                yield return null;
            }
            foreach (var s in stars) if (s != null) Destroy(s.gameObject);
        }

        IEnumerator Cheer(UnitView v)
        {
            yield return Wait(UnityEngine.Random.Range(0f, 0.25f));
            for (int i = 0; i < 2; i++)
            {
                v.Squash(0.85f, 1.2f);
                yield return v.TweenOffset(Vector3.zero, Vector3.up * 0.35f, 0.14f, Ease.OutQuad);
                yield return v.TweenOffset(v.Offset, Vector3.zero, 0.14f, Ease.InQuad);
                v.Squash(1.2f, 0.85f);
            }
        }

        // Intrarea în scenă (GDD §3): eroii sunt deja pe locurile lor (echipa de explorare a mers până acolo),
        // deci doar „se așază”; inamicii cad de sus, cu praf.
        IEnumerator Intro()
        {
            // Camera face push-in din vederea laterală largă spre încadrarea de luptă (×2).
            StartCoroutine(CombatCamera.EnterCombatFraming(_stage, 0.6f));
            Vector3 right = CombatStage.Right(_stage);
            foreach (var v in _views.Values)
            {
                if (!v.IsHero) { v.SetOffset(Vector3.up * (v.IsBoss ? 9f : 4f)); continue; }
                // Echipa de explorare (W) s-a oprit pe PartyFormation.StagePosition; eroii noștri pleacă de acolo
                // și pășesc în sloturile luptei, ca predarea să nu „sară” (azi pozițiile coincid, deci nu se mișcă).
                var info = _state.Unit(v.Id);
                if (info == null) continue;
                Vector3 party = Exploration.PartyFormation.StagePosition(_stage, info.Rank);
                Vector3 delta = party - v.Home; delta.y = 0f;
                v.SetOffset(delta);
            }
            int i = 0;
            foreach (var v in _views.Values)
            {
                StartCoroutine(IntroOne(v, i * 0.07f));
                i++;
            }
            yield return Wait(0.6f + i * 0.07f);
            Emit("intro");
        }

        IEnumerator IntroOne(UnitView v, float delay)
        {
            yield return Wait(delay);
            if (v.IsHero)
            {
                if (v.Offset.sqrMagnitude > 1e-4f)
                {
                    v.Squash(0.9f, 1.1f);
                    yield return v.TweenOffset(v.Offset, Vector3.zero, 0.45f, Ease.OutCubic);
                }
                v.Squash(1.12f, 0.9f);
                Particles.Burst(v.Feet, ParticleKind.Dust, 4, Vector3.up, 0.6f, 1f);
            }
            else
            {
                yield return v.TweenOffset(v.Offset, Vector3.zero, v.IsBoss ? 0.5f : 0.35f, Ease.InQuad);
                v.Squash(1.3f, 0.72f);
                _hud.UnitHud(v.Id)?.PopIn();
                Particles.Burst(v.Feet, ParticleKind.Dust, v.IsBoss ? 30 : 10, Vector3.up, 1.2f, 1f);
                if (v.IsBoss) { AudioManager.Play("monster_roar"); ScreenShake.Add(0.6f); CombatCamera.PunchIn(v.Center, 0.12f, 0.6f); }
                else ScreenShake.Add(0.08f);
            }
        }

        static IEnumerator Wait(float seconds)
        {
            if (seconds > 0f) yield return new WaitForSeconds(seconds);
        }

        // Momentele frecvente (lovituri) cer cel mult câteva capturi QA pe luptă, ca folderul să nu se umple.
        readonly Dictionary<string, int> _momentCounts = new Dictionary<string, int>();

        void Emit(string moment)
        {
            try { Moment?.Invoke(moment); }
            catch (Exception e) { Debug.LogException(e); }
            _momentCounts.TryGetValue(moment, out int n);
            _momentCounts[moment] = n + 1;
            int limit = moment == "hit" || moment == "card_played" || moment == "round" || moment == "hero_turn" ? 2 : moment == "input" ? 1 : 3;
            if (n < limit) QA.QaHooks.Moment("combat_" + moment);
        }
    }
}
