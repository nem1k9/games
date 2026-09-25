using System.Collections.Generic;
using Gnomes.App;
using Gnomes.Audio;
using Gnomes.Core;
using Gnomes.Core.Level;
using Gnomes.Core.Protocol;
using Gnomes.Core.Rules;
using Gnomes.NPC;
using Gnomes.Players;
using Gnomes.World;
using UnityEngine;

namespace Gnomes.Session
{
    /// <summary>Host-only game rules: level flow, the night's pranks, captures, loot and the village.</summary>
    public class HostLogic
    {
        readonly GameSession S;
        GameWorld W => GameWorld.Current;
        NightState night;
        public NightState Night => night;

        readonly Dictionary<ushort, string> homeRoom = new Dictionary<ushort, string>();
        readonly Dictionary<ushort, byte> hatOwner = new Dictionary<ushort, byte>();
        readonly Dictionary<ushort, float> paperDist = new Dictionary<ushort, float>();
        readonly Dictionary<ushort, Vector3> paperLast = new Dictionary<ushort, Vector3>();
        readonly HashSet<ushort> unrolled = new HashSet<ushort>();
        readonly Dictionary<ushort, int> holders = new Dictionary<ushort, int>();
        int[] sentProgress; // task progress the clients already know about
        float zoneTimer, chaosTimer, noiseTimer, portalTimer, allOutTimer;
        bool parrotTowel;
        float parrotQuietUntil;
        bool ending;
        float reportTimer = -1;

        public HostLogic(GameSession s)
        {
            S = s;
        }

        GearEffects Gear => GearEffects.From(S.Save);

        // ================================================================== level flow

        public void GoToHub()
        {
            ending = false;
            night = null;
            S.Level = LevelKind.Hub;
            S.Seed = Random.Range(1, int.MaxValue);
            S.Night = S.Save.Night;
            foreach (var p in S.Players.Values)
            {
                p.Status = PlayerStatus.Free;
                p.Hp = GameConsts.MaxHealth;
                p.Pocket.Clear();
                p.GoHomeVote = false;
                p.JarMech = -1;
                p.HatProp = 0;
            }
            var w = WorldLoader.Build(LevelKind.Hub, S.Seed, true, S);
            S.SpawnGnomes(w);
            S.SendLoadLevel(MakeLoad());
            GameApp.I?.OnLevelLoaded(LevelKind.Hub);
        }

        public void StartNight()
        {
            ending = false;
            reportTimer = -1;
            S.Level = LevelKind.House;
            S.Seed = Random.Range(1, int.MaxValue);
            S.Night = S.Save.Night;
            foreach (var p in S.Players.Values)
            {
                p.Status = PlayerStatus.Free;
                p.Hp = GameConsts.MaxHealth;
                p.Pocket.Clear();
                p.GoHomeVote = false;
                p.JarMech = -1;
                p.HatProp = 0;
                p.Struggles = 0;
            }
            var w = WorldLoader.Build(LevelKind.House, S.Seed, true, S);
            night = new NightState(S.Night, S.Seed, S.Players.Count, Gear);
            w.Night = night;
            sentProgress = (int[])night.Tasks.Progress.Clone(); // LoadLevel carries the starting values
            S.SpareHats = night.SpareHats;
            homeRoom.Clear();
            hatOwner.Clear();
            paperDist.Clear();
            paperLast.Clear();
            unrolled.Clear();
            parrotTowel = false;
            parrotQuietUntil = 0;
            foreach (var ps in HouseBuilder.PlaceItems(w, w.Layout, S.Seed))
            {
                var prop = w.SpawnProp(ps.Id, ps.Kind, ps.Pos.U(), ps.Rot.U());
                var room = w.Layout.RoomAt(ps.Pos.x, ps.Pos.z);
                homeRoom[ps.Id] = room != null ? room.Id : "";
            }
            S.SpawnGnomes(w);
            S.SendLoadLevel(MakeLoad());
            GameApp.I?.OnLevelLoaded(LevelKind.House);
        }

        LoadLevelMsg MakeLoad()
        {
            var w = W;
            var m = new LoadLevelMsg
            {
                Level = S.Level,
                Seed = S.Seed,
                Night = S.Night,
                Save = S.Save.Serialize(),
                SpareHats = night != null ? night.SpareHats : 0,
                TimeLeft = night != null ? night.TimeLeft : 0,
            };
            if (night != null)
            {
                m.TaskIds = night.Tasks.Ids();
                m.TaskProgress = (int[])night.Tasks.Progress.Clone();
            }
            if (w != null)
            {
                foreach (var p in w.Props.Values)
                    m.Props.Add(new PropSpawn { Id = p.Id, Kind = p.Def.Kind, Pos = p.transform.position.ToCoreV(), Rot = p.transform.rotation.ToCoreQ() });
                foreach (var f in w.Furniture) if (f.Broken) m.BrokenFurniture.Add(f.Index);
            }
            foreach (var p in S.Players.Values) m.Players.Add(new PlayerInfo { Id = p.Id, Name = p.Name, Hat = p.Hat, Status = p.Status });
            return m;
        }

        void EndNight(bool dawn)
        {
            if (ending || night == null) return;
            ending = true;
            // players still inside lose what's in their pockets; those who went home bank theirs
            var report = night.MakeReport(dawn);
            report.Chaos = night.Chaos;
            report.Giggles = night.Tasks.GigglesEarned + night.Chaos / 2;
            report.Fired = S.Save.ApplyReport(report);
            SaveStore.Save(S.Save);
            S.LastReport = report;
            if (dawn) W?.EmitSound(SoundId.Rooster, W.GoHomePoint + Vector3.up * 6f, 1f);
            S.Broadcast(new EventMsg { Type = EvType.NightEnd, S = ReportCodec.Encode(report) });
            GameApp.I?.OnNightEnd(report);
        }

        /// <summary>Called by the UI when the host presses "continue" on the report.</summary>
        public void ContinueAfterReport()
        {
            GoToHub();
        }

        // ================================================================== players

        public void OnPlayerJoined(PlayerSlot slot)
        {
            S.Broadcast(new EventMsg { Type = EvType.PlayerJoined, P = slot.Id, S = slot.Name, I = slot.Hat });
            var w = W;
            if (w != null)
            {
                if (!w.Gnomes.ContainsKey(slot.Id)) RemoteGnome.Create(w, slot.Id, slot.Name, slot.Color, w.SpawnPoint(slot.Id));
                if (w.Kind == LevelKind.House)
                {
                    // late joiners start as a ghost-free gnome at the porch
                    slot.Status = PlayerStatus.Free;
                }
            }
            S.SendLoadLevel(MakeLoad(), slot);
            if (w != null)
            {
                foreach (var e in w.Ties.AllAsEvents()) S.SendEventTo(slot, e);
                foreach (var kv in hatOwner) S.SendEventTo(slot, new EventMsg { Type = EvType.PropSpawned, Id = kv.Key, S = "gnomeHat", P = (byte)(S.Players.TryGetValue(kv.Value, out var o) ? o.Hat + 1 : 1), Pos = w.GetProp(kv.Key) != null ? w.GetProp(kv.Key).transform.position.ToCoreV() : V3.zero, Rot = Q4.identity });
            }
        }

        public void OnPlayerLeft(PlayerSlot slot)
        {
            var w = W;
            if (w == null) return;
            if (w.OldMan != null) w.OldMan.ForgetPlayer(slot.Id);
            if (w.Gnomes.TryGetValue(slot.Id, out var g))
            {
                w.Gnomes.Remove(slot.Id);
                Object.Destroy(g.gameObject);
            }
        }

        public void SetStatus(PlayerSlot slot, PlayerStatus st, Vector3? teleport = null)
        {
            slot.Status = st;
            if (st != PlayerStatus.Trapped) slot.JarMech = -1;
            slot.Struggles = 0;
            var e = new EventMsg { Type = EvType.PlayerStatus, P = slot.Id, I = (int)st, F = slot.Hp, Id = (ushort)(teleport.HasValue ? 1 : 0), Pos = (teleport ?? Vector3.zero).ToCoreV() };
            S.Broadcast(e);
            // apply for the host's own gnome and the host-side proxies
            if (W != null && W.Gnomes.TryGetValue(slot.Id, out var g))
            {
                g.Status = st;
                if (g is LocalGnome lg)
                {
                    lg.OnStatus(st);
                    if (teleport.HasValue) lg.Teleport(teleport.Value, lg.Yaw);
                }
                else if (g is RemoteGnome rg && teleport.HasValue) rg.SnapTo(teleport.Value);
            }
        }

        public PlayerSlot Slot(byte id) => S.Players.TryGetValue(id, out var s) ? s : null;

        public Vector3 PlayerPos(PlayerSlot slot)
        {
            if (W != null && W.Gnomes.TryGetValue(slot.Id, out var g)) return g.transform.position;
            return slot.State.Pos.U();
        }

        void Message(string key, byte to = 255) => S.Broadcast(new EventMsg { Type = EvType.Message, S = key, P = to });

        void LocalToast(string key, byte to = 255)
        {
            Message(key, to);
            if (to == 255 || to == S.LocalId) GameApp.I?.Toast(Loc.T(key));
        }

        public void OnGameEvent(GameEvent e)
        {
            if (night == null) return;
            var done = night.Tasks.Apply(e);
            SyncTaskProgress();
            foreach (var i in done) GameApp.I?.OnTaskDone(night.Tasks.Tasks[i]); // clients get it from TaskProgress
        }

        /// <summary>Send only the task counters that changed (zone checks fire several times a second).</summary>
        void SyncTaskProgress()
        {
            var prog = night.Tasks.Progress;
            if (sentProgress == null || sentProgress.Length != prog.Length)
            {
                sentProgress = new int[prog.Length];
                for (int i = 0; i < prog.Length; i++) sentProgress[i] = -1;
            }
            for (int i = 0; i < prog.Length; i++)
            {
                if (prog[i] == sentProgress[i]) continue;
                sentProgress[i] = prog[i];
                S.Broadcast(new EventMsg { Type = EvType.TaskProgress, P = (byte)i, I = prog[i] });
            }
        }

        // ================================================================== captures (called by the old man)

        public bool CanBeCaught(GnomeBody g) => g != null && g.Status == PlayerStatus.Free && !IsHidden(g);

        /// <summary>A gnome under low furniture (bed, sofa) can't be reached by grandpa's big hands.</summary>
        public bool IsHidden(GnomeBody g)
        {
            var p = g.transform.position + Vector3.up * 0.9f;
            return Physics.Raycast(p, Vector3.up, GameConsts.HideCeiling, Layers.World, QueryTriggerInteraction.Ignore) && g.transform.position.y < 1.5f;
        }

        public void Catch(byte id)
        {
            var slot = Slot(id);
            if (slot == null || slot.Status != PlayerStatus.Free) return;
            night.TimesCaught++;
            SpillPockets(slot, PlayerPos(slot) + Vector3.up);
            SetStatus(slot, PlayerStatus.Carried);
            LocalToast("caught", id);
            OnGameEvent(new GameEvent { Type = Ev.Caught });
        }

        public int FreeJar()
        {
            var w = W;
            var shelf = w?.FindFurniture("jarShelf");
            if (shelf == null) return -1;
            foreach (var m in shelf.Mechs)
            {
                if (m.Role != "jar") continue;
                bool used = false;
                foreach (var p in S.Players.Values) if (p.Status == PlayerStatus.Trapped && p.JarMech == m.Index) used = true;
                if (!used) return m.Index;
            }
            return -1;
        }

        public Vector3 JarPos(int mechIndex)
        {
            var m = W?.GetMech(mechIndex);
            if (m == null) return Vector3.zero;
            string anchor = "jar" + m.name.Substring(m.name.Length - 1);
            return m.Owner.AnchorPos(anchor, m.transform.position - Vector3.up * 1.2f);
        }

        public void PutInJar(byte id, int mechIndex)
        {
            var slot = Slot(id);
            if (slot == null) return;
            var m = W.GetMech(mechIndex);
            if (m == null) return;
            W.HostSetMech(m, false);
            slot.JarMech = mechIndex;
            slot.TrapTime = 0;
            SetStatus(slot, PlayerStatus.Trapped);
            slot.JarMech = mechIndex;
            LocalToast("trapped", id);
            W.EmitSound(SoundId.Click, m.transform.position, 1f);
        }

        public void FreeFromJar(PlayerSlot slot, bool loud)
        {
            var m = W.GetMech(slot.JarMech);
            if (m != null)
            {
                W.HostSetMech(m, true);
                Mono.Delay(3f, () => { if (m != null) W?.HostSetMech(m, false); });
            }
            var shelf = m != null ? m.Owner : null;
            var drop = shelf != null ? shelf.AnchorPos("free", JarPos(slot.JarMech) + Vector3.down * 4f) : PlayerPos(slot);
            if (loud)
            {
                W.EmitSound(SoundId.Clonk, JarPos(slot.JarMech), 1f);
                W.Noise(JarPos(slot.JarMech), 38f, NoiseKind.Impact);
            }
            SetStatus(slot, PlayerStatus.Free, drop + Vector3.up * 0.2f);
        }

        public void DropCarried(byte id, Vector3 pos)
        {
            var slot = Slot(id);
            if (slot == null || slot.Status != PlayerStatus.Carried) return;
            SetStatus(slot, PlayerStatus.Free, pos);
            StunPlayer(slot, 1.2f);
        }

        /// <summary>Squashed by the fly swatter: the gnome becomes a ghost and drops its hat.</summary>
        public void Flatten(byte id)
        {
            var slot = Slot(id);
            if (slot == null || slot.Status == PlayerStatus.Dead || slot.Status == PlayerStatus.Home) return;
            var pos = PlayerPos(slot);
            if (slot.Status == PlayerStatus.Trapped) pos = JarPos(slot.JarMech) + Vector3.down * 4f;
            SpillPockets(slot, pos + Vector3.up);
            slot.Hp = 0;
            slot.DeadTime = Time.time;
            night.Deaths++;
            SetStatus(slot, PlayerStatus.Dead);
            W.EmitSound(SoundId.Punch, pos, 1f);
            W.EmitSound(SoundId.Death, pos, 0.8f);
            Fx.Puff(W, pos + Vector3.up * 0.4f, 0.7f);
            // the hat stays behind: friends carry it to the yarn basket
            var hat = W.HostSpawnProp("gnomeHat", pos + Vector3.up * 0.6f, Quaternion.identity, Vector3.up * 3f, (byte)(slot.Hat + 1));
            if (hat != null)
            {
                hat.Model.SetTint(slot.Color);
                slot.HatProp = hat.Id;
                hatOwner[hat.Id] = slot.Id;
            }
        }

        public void StunPlayer(PlayerSlot slot, float seconds)
        {
            var e = new EventMsg { Type = EvType.Stun, P = slot.Id, F = seconds };
            if (slot.Id == S.LocalId) LocalGnome.I?.Stun(seconds);
            else S.SendEventTo(slot, e);
        }

        void Revive(PlayerSlot slot)
        {
            if (slot.HatProp != 0)
            {
                hatOwner.Remove(slot.HatProp);
                if (W.GetProp(slot.HatProp) != null) W.HostRemoveProp(slot.HatProp, 4);
                slot.HatProp = 0;
            }
            slot.Hp = GameConsts.MaxHealth;
            SetStatus(slot, PlayerStatus.Free, W.RevivePoint + Vector3.up * 0.3f);
            W.EmitSound(SoundId.Revive, W.RevivePoint, 1f);
            Fx.Sparkles(W, W.RevivePoint + Vector3.up, slot.Color, 16);
            LocalToast("revived");
        }

        void SpillPockets(PlayerSlot slot, Vector3 at)
        {
            if (slot.Pocket.Count == 0) return;
            foreach (var kind in slot.Pocket)
                W.HostSpawnProp(kind, at + Random.insideUnitSphere * 0.3f, Random.rotation, new Vector3(Random.Range(-2f, 2f), 3f, Random.Range(-2f, 2f)));
            slot.Pocket.Clear();
            SendPocket(slot);
        }

        void SendPocket(PlayerSlot slot)
        {
            S.Broadcast(new EventMsg { Type = EvType.Pocket, P = slot.Id, S = string.Join(",", slot.Pocket) });
        }

        // ================================================================== per frame

        public void Update(float dt)
        {
            var w = W;
            if (w == null) return;
            if (w.Kind == LevelKind.Hub) UpdateHub(dt);
            else UpdateNight(dt);
        }

        void UpdateHub(float dt)
        {
            var w = W;
            int inPortal = 0, total = 0;
            foreach (var slot in S.Players.Values)
            {
                total++;
                if (w.Gnomes.TryGetValue(slot.Id, out var g) && GameWorld.ZoneContains(w.PortalZone, g.Center)) inPortal++;
                slot.PortalReady = w.Gnomes.TryGetValue(slot.Id, out var g2) && GameWorld.ZoneContains(w.PortalZone, g2.Center);
            }
            if (total > 0 && inPortal == total)
            {
                portalTimer += dt;
                if (portalTimer > 1.5f)
                {
                    portalTimer = 0;
                    StartNight();
                }
            }
            else portalTimer = 0;
        }

        void UpdateNight(float dt)
        {
            var w = W;
            if (night == null || ending) return;
            night.TimeLeft -= dt;
            if (night.TimeLeft <= 0)
            {
                night.TimeLeft = 0;
                LocalToast("dawn");
                EndNight(true);
                return;
            }

            // carried / trapped gnomes follow grandpa's hand or sit in their jar
            foreach (var slot in S.Players.Values)
            {
                if (!w.Gnomes.TryGetValue(slot.Id, out var g)) continue;
                Vector3? forced = null;
                if (slot.Status == PlayerStatus.Carried && w.OldMan != null) forced = w.OldMan.HandPos + Vector3.down * 0.6f;
                if (slot.Status == PlayerStatus.Trapped)
                {
                    forced = JarPos(slot.JarMech);
                    slot.TrapTime += dt;
                    if (slot.TrapTime > GameConsts.JarAirSeconds)
                    {
                        Flatten(slot.Id); // fainted in the jar
                        continue;
                    }
                }
                if (forced.HasValue)
                {
                    if (g is LocalGnome lg) lg.ForcedPos = forced.Value;
                    else if (g is RemoteGnome rg)
                    {
                        rg.Rb.MovePosition(forced.Value);
                        rg.transform.position = forced.Value;
                    }
                }
                if (slot.Status == PlayerStatus.Dead && S.IsSolo && Time.time - slot.DeadTime > GameConsts.SoloReviveDelay)
                {
                    if (night.SpareHats > 0)
                    {
                        night.SpareHats--;
                        S.SpareHats = night.SpareHats;
                        S.Broadcast(new EventMsg { Type = EvType.SpareHats, I = night.SpareHats });
                        Revive(slot);
                    }
                    else
                    {
                        EndNight(false);
                        return;
                    }
                }
            }

            FootstepsAndFloor(dt);

            zoneTimer -= dt;
            if (zoneTimer <= 0)
            {
                zoneTimer = 0.35f;
                CheckZones();
            }
            chaosTimer -= dt;
            if (chaosTimer <= 0)
            {
                chaosTimer = 2f;
                night.Chaos = CountChaos();
                OnGameEvent(GameEvent.Chaos(night.Chaos));
            }
            noiseTimer -= dt;
            if (noiseTimer <= 0)
            {
                noiseTimer = 2f;
                var tv = w.FirstOfKind("tv");
                if (tv != null && tv.TvOn) w.Noise(tv.transform.position, 30f, NoiseKind.Tv);
                foreach (var f in w.Furniture)
                    foreach (var m in f.Mechs)
                        if ((m.Role == "faucet" || m.Role == "tubFaucet") && m.IsOpen) w.Noise(m.transform.position, 14f, NoiseKind.Water);
            }
            UnrollPaper(dt);
            CheckAllOut(dt);
        }

        void CheckAllOut(float dt)
        {
            int free = 0, home = 0, stuck = 0;
            foreach (var p in S.Players.Values)
            {
                if (p.Status == PlayerStatus.Free) free++;
                else if (p.Status == PlayerStatus.Home) home++;
                else if (p.Status != PlayerStatus.Dead || !S.IsSolo || night.SpareHats > 0) stuck++;
            }
            bool over = free == 0 && (home > 0 || stuck == 0 || AllCaptured());
            allOutTimer = over ? allOutTimer + dt : 0;
            if (allOutTimer > 2.5f) EndNight(false);
        }

        bool AllCaptured()
        {
            foreach (var p in S.Players.Values)
            {
                if (p.Status == PlayerStatus.Free || p.Status == PlayerStatus.Carried) return false;
                if (p.Status == PlayerStatus.Dead && S.IsSolo) return false; // solo revive pending
            }
            return true;
        }

        void FootstepsAndFloor(float dt)
        {
            var w = W;
            float noiseMul = Gear.FootstepNoiseMul;
            foreach (var slot in S.Players.Values)
            {
                if (slot.Status != PlayerStatus.Free || !w.Gnomes.TryGetValue(slot.Id, out var g)) continue;
                var pos = g.transform.position;
                if (slot.LastPos == Vector3.zero) slot.LastPos = pos;
                var moved = (pos - slot.LastPos).Flat().magnitude;
                slot.LastPos = pos;
                if (g.Grounded && moved < 2f)
                {
                    slot.StepAccum += moved;
                    if (slot.StepAccum > 0.9f)
                    {
                        slot.StepAccum = 0;
                        float r = g.Crouching ? 0f : g.Sprinting ? 13f : 5f;
                        if (r > 0) w.Noise(pos, r * noiseMul, NoiseKind.Step);
                    }
                }
                // hard landings
                float vy = g.Velocity.y;
                if (g.Grounded && slot.LastVy < -10f) w.Noise(pos, 10f * noiseMul, NoiseKind.Step);
                slot.LastVy = vy;
                if (slot.CreakCooldown > 0) slot.CreakCooldown -= dt;
            }
        }

        // ================================================================== zones

        void CheckZones()
        {
            var w = W;
            var toBank = new List<Prop>();
            var reviveHats = new List<Prop>();
            foreach (var p in w.Props.Values)
            {
                if (p.Removed) continue;
                var c = p.transform.position;
                if (p.Def.HasTag("gnomeHat"))
                {
                    if (GameWorld.ZoneContains(w.ReviveZone, c)) reviveHats.Add(p);
                    continue;
                }
                if (GameWorld.ZoneContains(w.StashZone, c))
                {
                    toBank.Add(p);
                    continue;
                }
                foreach (var f in w.Furniture)
                {
                    foreach (var z in f.Zones)
                    {
                        if (z.Key == "trap" || z.Key == "creak" || z.Key == "stash" || z.Key == "revive") continue;
                        if (!GameWorld.ZoneContains(z.Value, c)) continue;
                        OnGameEvent(GameEvent.InZone(z.Key, p.Def.Kind));
                        if (z.Key == "cageTop" && p.Def.HasTag("towel") && !parrotTowel)
                        {
                            parrotTowel = true;
                            w.Parrot?.Silence(9999f);
                            OnGameEvent(GameEvent.Silenced("towel"));
                            LocalToast("parrotQuiet");
                        }
                        if (z.Key == "cage" && p.Def.HasTag("food") && Time.time > parrotQuietUntil)
                        {
                            parrotQuietUntil = Time.time + 90f;
                            w.Parrot?.Silence(90f);
                            w.HostRemoveProp(p.Id, 4);
                            w.EmitSound(SoundId.Slurp, z.Value.transform.position, 1f);
                            OnGameEvent(GameEvent.Silenced("food"));
                            LocalToast("parrotQuiet");
                        }
                        if (z.Key == "trap" || p.Removed) break;
                    }
                }
                // props snap mousetraps too
                foreach (var f in w.Furniture)
                {
                    if (f.Kind != "mousetrap" || f.Mechs.Count == 0 || f.Mechs[0].IsOpen) continue;
                    if (f.Zones.TryGetValue("trap", out var tz) && GameWorld.ZoneContains(tz, c) && p.Rb.Vel().sqrMagnitude > 0.5f) SnapTrap(f, null);
                }
            }
            foreach (var p in toBank) Bank(p);
            foreach (var h in reviveHats)
            {
                if (hatOwner.TryGetValue(h.Id, out var owner) && S.Players.TryGetValue(owner, out var slot) && slot.Status == PlayerStatus.Dead) Revive(slot);
                else w.HostRemoveProp(h.Id, 4);
            }

            // gnomes vs mousetraps & creaky boards
            foreach (var slot in S.Players.Values)
            {
                if (slot.Status != PlayerStatus.Free || !w.Gnomes.TryGetValue(slot.Id, out var g)) continue;
                var c = g.transform.position + Vector3.up * 0.15f;
                foreach (var f in w.Furniture)
                {
                    if (f.Kind == "mousetrap" && f.Mechs.Count > 0 && !f.Mechs[0].IsOpen && f.Zones.TryGetValue("trap", out var tz) && GameWorld.ZoneContains(tz, c))
                        SnapTrap(f, slot);
                    else if (f.Kind == "creakyBoard" && f.Zones.TryGetValue("creak", out var cz) && GameWorld.ZoneContains(cz, c) && !g.Crouching && g.Velocity.Flat().sqrMagnitude > 1f && slot.CreakCooldown <= 0)
                    {
                        slot.CreakCooldown = 1.2f;
                        w.EmitSound(SoundId.Door, c, 0.9f);
                        w.Noise(c, 19f, NoiseKind.Step);
                    }
                }
                // parrot watches
                w.Parrot?.Watch(g);
            }
        }

        void SnapTrap(Furniture trap, PlayerSlot victim)
        {
            var w = W;
            w.HostSetMech(trap.Mechs[0], true);
            w.EmitSound(SoundId.Punch, trap.transform.position, 1f);
            w.EmitSound(SoundId.Clonk, trap.transform.position, 0.8f);
            w.Noise(trap.transform.position, 24f, NoiseKind.Impact);
            if (victim != null)
            {
                StunPlayer(victim, 2.5f);
                LocalToast("mousetrap", victim.Id);
            }
        }

        void Bank(Prop p)
        {
            var w = W;
            var kind = p.Def.Kind;
            w.HostRemoveProp(p.Id, 0);
            var done = night.Bank(kind);
            w.EmitSound(SoundId.Bank, w.StashZone.transform.position, 0.8f);
            Fx.Sparkles(w, w.StashZone.transform.position + Vector3.up * 0.5f, new Color32(255, 230, 120, 255), 8);
            S.Broadcast(new EventMsg { Type = EvType.Banked, S = kind, I = p.Def.Value });
            GameApp.I?.OnBanked(kind, p.Def.Value);
            SyncTaskProgress();
            foreach (var i in done) GameApp.I?.OnTaskDone(night.Tasks.Tasks[i]);
        }

        int CountChaos()
        {
            var w = W;
            int n = 0;
            foreach (var p in w.Props.Values)
            {
                if (!homeRoom.TryGetValue(p.Id, out var home) || home.Length == 0) continue;
                var pos = p.transform.position;
                var room = w.Layout.RoomAt(pos.x, pos.z);
                string now = room != null ? room.Id : "outside";
                if (now != home) n++;
            }
            // stolen things also count: grandpa can't find them either
            return n + night.ItemsStolen;
        }

        void UnrollPaper(float dt)
        {
            var w = W;
            foreach (var p in w.Props.Values)
            {
                if (!p.Def.HasTag("toiletPaper") || unrolled.Contains(p.Id)) continue;
                var pos = p.transform.position;
                if (!paperLast.TryGetValue(p.Id, out var last))
                {
                    paperLast[p.Id] = pos;
                    continue;
                }
                paperLast[p.Id] = pos;
                if (pos.y > 1f || p.IsHeld) continue; // rolling on the floor only
                float d = (pos - last).Flat().magnitude;
                if (d < 0.02f || d > 2f) continue;
                paperDist.TryGetValue(p.Id, out var total);
                total += d;
                paperDist[p.Id] = total;
                if (total > 16f)
                {
                    unrolled.Add(p.Id);
                    OnGameEvent(GameEvent.Unrolled(p.Def.Kind));
                }
            }
        }

        // ================================================================== physics from remote players

        public void FixedUpdate(float dt)
        {
            var w = W;
            if (w == null) return;
            holders.Clear();
            foreach (var slot in S.Players.Values)
            {
                if (slot.Id == S.LocalId || !slot.HasState || slot.Status != PlayerStatus.Free) continue;
                var a = slot.State.Arms;
                if (a.Mode == ArmMode.HoldProp)
                {
                    holders.TryGetValue(a.PropId, out int n);
                    holders[a.PropId] = n + 1;
                }
            }
            var local = LocalGnome.I;
            if (local != null && local.CarriedProp != 0)
            {
                holders.TryGetValue(local.CarriedProp, out int n);
                holders[local.CarriedProp] = n + 1;
            }
            foreach (var p in w.Props.Values) p.HeldMask = 0;
            foreach (var slot in S.Players.Values)
            {
                if (slot.Id == S.LocalId || !slot.HasState || slot.Status != PlayerStatus.Free) continue;
                if (Time.time - slot.StateTime > 0.5f) continue; // stale
                var a = slot.State.Arms;
                var prop = w.GetProp(a.PropId);
                if (prop == null) continue;
                if (a.Mode == ArmMode.HoldProp)
                {
                    var anchor = prop.transform.TransformPoint(a.Anchor.U());
                    ApplyHoldInternal(slot.Id, prop, anchor, a.Hand.U());
                }
                else if (a.Mode == ArmMode.YarnProp)
                {
                    var anchor = prop.transform.TransformPoint(a.Anchor.U());
                    var hand = slot.State.Pos.U() + Vector3.up * GameConsts.GnomeShoulder;
                    ApplyYarnPull(slot.Id, prop, anchor, hand, a.Hand.x);
                }
            }
        }

        public void ApplyHold(byte pid, Prop prop, Vector3 anchor, Vector3 target) => ApplyHoldInternal(pid, prop, anchor, target);

        void ApplyHoldInternal(byte pid, Prop prop, Vector3 anchor, Vector3 target)
        {
            if (prop.Removed || prop.Rb.isKinematic) return;
            holders.TryGetValue(prop.Id, out int n);
            float share = 1f / Mathf.Max(1, n);
            var f = GrabPhysics.HoldForce(anchor, prop.Rb.GetPointVelocity(anchor), target, prop.Rb.mass, GameConsts.HoldStrength * Gear.StrengthMul, share);
            prop.Rb.AddForceAtPosition(f, anchor, ForceMode.Force);
            prop.Rb.angularVelocity *= 0.9f;
            prop.HeldMask |= 1 << (pid & 31);
            prop.LastTouchedBy = pid;
        }

        public void ApplyYarnPull(byte pid, Prop prop, Vector3 anchor, Vector3 hand, float length)
        {
            if (prop.Removed || prop.Rb.isKinematic) return;
            var f = GrabPhysics.YarnTension(anchor, prop.Rb.GetPointVelocity(anchor), hand, length, GameConsts.YarnPullStrength * Gear.StrengthMul, 120f, 12f);
            prop.Rb.AddForceAtPosition(f, anchor, ForceMode.Force);
            prop.LastTouchedBy = pid;
        }

        // ================================================================== actions

        public void HandleAction(byte pid, ActionMsg a)
        {
            var slot = Slot(pid);
            var w = W;
            if (slot == null || w == null) return;
            Vector3 me = PlayerPos(slot);
            switch (a.Type)
            {
                case ActionType.Interact:
                {
                    if (slot.Status != PlayerStatus.Free) return;
                    var m = w.GetMech(a.Id);
                    if (m == null || m.HumanOnly || Vector3.Distance(me, m.HandleWorld) > GameConsts.MechReach + 2f) return;
                    Interact(slot, m);
                    break;
                }
                case ActionType.Unscrew:
                {
                    if (slot.Status != PlayerStatus.Free) return;
                    var m = w.GetMech(a.Id);
                    if (m == null || m.Role != "jar" || Vector3.Distance(me, m.HandleWorld) > GameConsts.MechReach + 2f) return;
                    foreach (var other in S.Players.Values)
                        if (other.Status == PlayerStatus.Trapped && other.JarMech == m.Index)
                        {
                            FreeFromJar(other, false);
                            return;
                        }
                    w.HostToggleMech(m);
                    break;
                }
                case ActionType.Pocket:
                {
                    if (slot.Status != PlayerStatus.Free) return;
                    var p = w.GetProp(a.Id);
                    if (p == null || !p.Def.Has(ItemFlags.Pocketable) || Vector3.Distance(me, p.transform.position) > 4f) return;
                    if (slot.Pocket.Count >= Gear.PocketSize)
                    {
                        Message("pocketFull", pid);
                        return;
                    }
                    slot.Pocket.Add(p.Def.Kind);
                    w.HostRemoveProp(p.Id, 2);
                    w.EmitSound(SoundId.Pickup, p.transform.position, 0.6f);
                    SendPocket(slot);
                    break;
                }
                case ActionType.DropPockets:
                {
                    if (slot.Pocket.Count == 0) return;
                    var at = a.A.U();
                    if (Vector3.Distance(at, me) > 3f) at = me + Vector3.up;
                    foreach (var kind in slot.Pocket) w.HostSpawnProp(kind, at + Random.insideUnitSphere * 0.2f, Random.rotation, a.B.U() + Random.insideUnitSphere);
                    slot.Pocket.Clear();
                    SendPocket(slot);
                    break;
                }
                case ActionType.Throw:
                {
                    var p = w.GetProp(a.Id);
                    if (p == null || Vector3.Distance(me, p.transform.position) > 5f) return;
                    var v = Vector3.ClampMagnitude(a.A.U(), 16f);
                    p.Rb.SetVel(v);
                    p.Rb.angularVelocity = Random.insideUnitSphere * 4f;
                    p.HeldMask &= ~(1 << (pid & 31));
                    p.LastTouchedBy = pid;
                    break;
                }
                case ActionType.Release:
                {
                    var p = w.GetProp(a.Id);
                    if (p != null) p.HeldMask &= ~(1 << (pid & 31));
                    break;
                }
                case ActionType.Punch:
                    Kick(slot, a, me);
                    break;
                case ActionType.Struggle:
                    Struggle(slot);
                    break;
                case ActionType.GoHome:
                {
                    if (slot.Status != PlayerStatus.Free || w.Kind != LevelKind.House) return;
                    if (Vector3.Distance(me, w.GoHomePoint) > 5f) return;
                    // stash pockets and step out of the night
                    foreach (var kind in slot.Pocket)
                        foreach (var i in night.Bank(kind)) GameApp.I?.OnTaskDone(night.Tasks.Tasks[i]);
                    slot.Pocket.Clear();
                    SendPocket(slot);
                    SyncTaskProgress();
                    SetStatus(slot, PlayerStatus.Home);
                    w.EmitSound(SoundId.Sparkle, w.GoHomePoint, 0.8f);
                    break;
                }
                case ActionType.Potion:
                {
                    if (S.Save.Potions <= 0 || slot.Status != PlayerStatus.Free) return;
                    S.Save.Potions--;
                    night.PotionsUsed++;
                    S.Broadcast(new EventMsg { Type = EvType.SaveState, S = S.Save.Serialize() });
                    var start = a.A.U();
                    if (Vector3.Distance(start, me) > 3f) start = me + Vector3.up;
                    w.HostSpawnProp("sleepDust", start, Random.rotation, Vector3.ClampMagnitude(a.B.U(), 15f));
                    break;
                }
                case ActionType.Honk:
                    w.EmitSound(SoundId.Honk, me + Vector3.up, 1f);
                    w.Noise(me, 22f, NoiseKind.Voice);
                    break;
                case ActionType.Tie:
                    Tie(slot, a, me);
                    break;
                case ActionType.Craft:
                    if (w.Kind != LevelKind.Hub) return;
                    if (S.Save.Craft((GearId)a.I))
                    {
                        SaveStore.Save(S.Save);
                        S.Broadcast(new EventMsg { Type = EvType.SaveState, S = S.Save.Serialize() });
                        LocalGnome.I?.RefreshGear();
                        w.EmitSound(SoundId.Sparkle, me, 1f);
                    }
                    break;
                case ActionType.Chat:
                    if (!string.IsNullOrWhiteSpace(a.S))
                    {
                        string text = slot.Name + ": " + (a.S.Length > 120 ? a.S.Substring(0, 120) : a.S);
                        S.Broadcast(new EventMsg { Type = EvType.Chat, P = pid, S = text });
                        GameApp.I?.Toast(text);
                    }
                    break;
            }
        }

        void Interact(PlayerSlot slot, Mechanism m)
        {
            var w = W;
            switch (m.Role)
            {
                case "safe":
                    if (m.Locked)
                    {
                        Message("mechSafeLocked", slot.Id);
                        if (slot.Id == S.LocalId) GameApp.I?.Toast(Loc.T("mechSafeLocked"));
                        return;
                    }
                    break;
                case "jar":
                case "mousetrap":
                    return;
                case "tvPower":
                    if (m.Owner != null && m.Owner.Broken) return;
                    break;
                case "flush":
                    m.PressedTime = Time.time;
                    w.EmitSound(SoundId.Flush, m.transform.position, 1f);
                    w.Noise(m.transform.position, 26f, NoiseKind.Water);
                    if (m.Owner != null && m.Owner.Zones.TryGetValue("toiletBowl", out var bowl))
                    {
                        var flushed = new List<Prop>();
                        foreach (var p in w.Props.Values) if (GameWorld.ZoneContains(bowl, p.transform.position)) flushed.Add(p);
                        foreach (var p in flushed)
                        {
                            w.HostRemoveProp(p.Id, 1);
                            OnGameEvent(GameEvent.Flushed(p.Def.Kind));
                        }
                    }
                    S.Broadcast(new EventMsg { Type = EvType.Mech, Id = m.Index, I = 0 });
                    return;
            }
            bool on = w.HostToggleMech(m);
            OnGameEvent(GameEvent.Mech(m.Role, on));
            if (m.Role == "window" && on) w.Noise(m.transform.position, 12f, NoiseKind.Impact);
            if (m.Role == "tvPower" && on) w.Noise(m.transform.position, 30f, NoiseKind.Tv);
        }

        void Kick(PlayerSlot slot, ActionMsg a, Vector3 me)
        {
            if (slot.Status != PlayerStatus.Free) return;
            var w = W;
            var point = a.A.U();
            if (Vector3.Distance(point, me) > 3.5f) return;
            var dir = a.B.U().normalized;
            w.Noise(point, 9f, NoiseKind.Impact);
            switch (a.I)
            {
                case 0:
                {
                    var p = w.GetProp(a.Id);
                    if (p == null) return;
                    p.Rb.AddForceAtPosition((dir + Vector3.up * 0.35f) * GameConsts.PunchImpulse * Mathf.Clamp(p.Rb.mass, 0.5f, 3f), point, ForceMode.Impulse);
                    p.LastTouchedBy = slot.Id;
                    if (p.Def.Has(ItemFlags.Breakable) && p.Def.BreakImpulse < 4.5f) w.BreakProp(p, point);
                    break;
                }
                case 1:
                    if (a.Id == NpcBase.OldManId) w.OldMan?.OnPoked(slot.Id, point);
                    else if (a.Id == NpcBase.CatId) w.Cat?.OnKicked(dir);
                    else if (a.Id == NpcBase.ParrotId) w.Parrot?.Squawk();
                    break;
                case 2:
                {
                    var m = w.GetMech(a.Id);
                    if (m == null) return;
                    if (m.Role == "safe" && m.Locked)
                    {
                        m.LockHits--;
                        w.EmitSound(SoundId.Clonk, point, 1f);
                        w.Noise(point, 20f, NoiseKind.Impact);
                        if (m.LockHits <= 0)
                        {
                            m.Locked = false;
                            w.HostSetMech(m, true);
                            OnGameEvent(GameEvent.Mech("safe", true));
                            w.EmitSound(SoundId.TaskDone, point, 0.6f);
                        }
                    }
                    else if (m.Role == "mousetrap" && !m.IsOpen) SnapTrap(m.Owner, null);
                    break;
                }
                case 3:
                {
                    if (a.Id >= w.Furniture.Count) return;
                    var f = w.Furniture[a.Id];
                    if (f.Hp > 0 && !f.Broken)
                    {
                        w.EmitSound(SoundId.Clonk, point, 0.8f);
                        if (f.Damage(12f)) w.HostBreakFurniture(f);
                    }
                    break;
                }
            }
        }

        void Struggle(PlayerSlot slot)
        {
            var w = W;
            slot.Struggles++;
            if (slot.Status == PlayerStatus.Carried)
            {
                if (slot.Struggles >= GameConsts.StruggleCarried && w.OldMan != null) w.OldMan.DropCarried();
            }
            else if (slot.Status == PlayerStatus.Trapped)
            {
                if (slot.Struggles % 4 == 0)
                {
                    var p = JarPos(slot.JarMech);
                    w.EmitSound(SoundId.Clink, p, 0.8f);
                    w.Noise(p, 8f, NoiseKind.Impact);
                }
                if (slot.Struggles >= GameConsts.StruggleJar) FreeFromJar(slot, true);
            }
        }

        void Tie(PlayerSlot slot, ActionMsg a, Vector3 me)
        {
            if (slot.Status != PlayerStatus.Free) return;
            var w = W;
            var pa = a.Id != 0 ? w.GetProp(a.Id) : null;
            Prop pb = null;
            if (!string.IsNullOrEmpty(a.S) && ushort.TryParse(a.S, out var bid)) pb = w.GetProp(bid);
            var A = a.A.U();
            var B = a.B.U();
            if (Vector3.Distance(A, me) > Gear.YarnRange + 3f || Vector3.Distance(B, me) > Gear.YarnRange + 3f) return;
            if (pa == null && pb == null) return;
            var e = w.Ties.HostCreate(pa, A, pb, B, slot.Hat);
            if (!e.HasValue) return;
            S.Broadcast(e.Value);
            LocalToast("tied", slot.Id);
            string kindA = pa != null ? pa.Def.Kind : "furniture";
            string kindB = pb != null ? pb.Def.Kind : "furniture";
            if (pa == null)
            {
                kindA = kindB;
                kindB = "furniture";
            }
            OnGameEvent(GameEvent.Tied(kindA, kindB));
        }

        // ================================================================== sleep dust

        public void OnSleepDust(Vector3 pos)
        {
            var w = W;
            Fx.Sparkles(w, pos, new Color32(190, 140, 255, 255), 24, 2.5f);
            Fx.Puff(w, pos, 1.6f);
            w.OldMan?.SleepDust(pos);
            w.Cat?.SleepDust(pos);
        }
    }
}
