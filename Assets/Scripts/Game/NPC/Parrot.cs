using Gnomes.App;
using Gnomes.Audio;
using Gnomes.Core.Protocol;
using Gnomes.Players;
using Gnomes.World;
using UnityEngine;

namespace Gnomes.NPC
{
    /// <summary>
    /// Kesha the parrot sits in his cage in the living room and yells "THIEVES!" when he spots a gnome,
    /// which wakes grandpa. A towel over the cage or a cookie shuts him up.
    /// </summary>
    public class Parrot : NpcBase
    {
        public enum St : byte { Idle = 0, Alarm = 1, Quiet = 2 }

        Furniture cage;
        Transform bird, eye;
        Quaternion birdRest;
        float quietUntil;
        float suspicion;
        float nextSquawk;
        float flap;

        public static Parrot Attach(GameWorld w, bool authority)
        {
            var cage = w.FindFurniture("parrotCage");
            if (cage == null) return null;
            var p = cage.gameObject.AddComponent<Parrot>();
            p.NpcId = ParrotId;
            p.Authority = authority;
            p.cage = cage;
            p.bird = cage.Model.Node("parrot");
            p.eye = cage.Model.Node("ANCHOR_eye");
            if (p.bird) p.birdRest = p.bird.localRotation;
            w.Parrot = p;
            return p;
        }

        public bool Quiet => Time.time < quietUntil;

        public void Silence(float seconds)
        {
            quietUntil = Mathf.Max(quietUntil, Time.time + seconds);
            State = (byte)St.Quiet;
        }

        /// <summary>Host: called for every free gnome a few times per second.</summary>
        public void Watch(GnomeBody g)
        {
            if (!Authority || Quiet || eye == null) return;
            var from = eye.position;
            var to = g.Center - from;
            float d = to.magnitude;
            if (d > 17f) return;
            if (Vector3.Dot(to / d, cage.transform.forward) < -0.2f) return; // behind him
            if (Physics.Raycast(from, to / d, d - 0.3f, Layers.World, QueryTriggerInteraction.Ignore)) return;
            Look = g.Center;
            suspicion += (g.Crouching ? 0.12f : 0.3f) * (1f - d / 17f + 0.3f);
            if (suspicion > 1f && Time.time > nextSquawk) Squawk();
        }

        public void Squawk()
        {
            if (!Authority || Quiet) return;
            suspicion = 0;
            nextSquawk = Time.time + 6f;
            State = (byte)St.Alarm;
            var w = W;
            var p = eye ? eye.position : transform.position;
            w.EmitSound(SoundId.Squawk, p, 1f);
            w.Noise(p, 48f, NoiseKind.Alarm);
            GameApp.I?.Toast(Core.Loc.T("parrotAlarm"));
            w.Session?.Broadcast(new EventMsg { Type = EvType.Message, S = "parrotAlarm", P = 255 });
        }

        void Update()
        {
            if (Authority)
            {
                suspicion = Mathf.Max(0, suspicion - Time.deltaTime * 0.15f);
                if (State == (byte)St.Alarm && Time.time > nextSquawk - 4f) State = (byte)St.Idle;
                if (State == (byte)St.Quiet && !Quiet) State = (byte)St.Idle;
            }
            if (!bird) return;
            // bob, look at the gnome, flap when alarmed
            flap = State == (byte)St.Alarm ? Mathf.Sin(Time.time * 30f) * 20f : 0f;
            float yaw = 0;
            if (Look != Vector3.zero)
            {
                var local = cage.transform.InverseTransformPoint(Look);
                yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -80, 80);
            }
            float bob = Mathf.Sin(Time.time * 2.5f) * (State == (byte)St.Quiet ? 1f : 6f);
            bird.localRotation = birdRest * Quaternion.Euler(bob + flap, yaw, 0);
        }
    }
}
