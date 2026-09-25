using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gnomes.Core;
using Gnomes.Core.Protocol;
using Godot;
using SockGang.Rendering;
using SockGang.App;
using SockGang.NPC;
using SockGang.Players;
using SockGang.Session;
using SockGang.World;

namespace SockGang.Dev
{
    /// <summary>
    /// Automated play-through used to verify builds without a human:
    ///   --autotest=solo   : hub -> portal -> night -> grab/throw/kick/pocket -> grandpa wakes -> report -> hub
    ///   --autotest=host   : like solo but waits for a client and plays the night together
    ///   --autotest=client : joins 127.0.0.1 and follows the host
    /// Optional: --shots=DIR saves screenshots, --port=N. Exits with code 0 on success, 1 on failure.
    /// </summary>
    public partial class AutoTest : Node
    {
        public Dictionary<string, string> Args;
        string shots;
        int shotIndex;
        readonly List<string> failures = new List<string>();

        GameApp App => GameApp.I;
        GameSession S => GameSession.I;
        GameWorld W => GameWorld.Current;

        public override void _Ready()
        {
            shots = Args.TryGetValue("shots", out var d) ? d : null;
            if (shots != null) DirAccess.MakeDirRecursiveAbsolute(shots);
            Run();
        }

        void Log(string s) => GD.Print("[autotest] " + s);

        /// <summary>
        /// Godot draws clockwise triangles as front faces, so on a closed model cross(c - a, b - a) must agree
        /// with the vertex normals. A wrong winding renders every model inside out (see-through parts).
        /// </summary>
        static bool OutwardFacing(string model)
        {
            var m = ModelLibrary.Get(model);
            if (m == null) return false;
            int good = 0, bad = 0;
            for (int ni = 0; ni < m.Nodes.Length; ni++)
            {
                if (m.Nodes[ni].Meshes.Length == 0) continue;
                var mesh = ModelLibrary.BuildMesh(m, ni);
                for (int si = 0; si < mesh.GetSurfaceCount(); si++)
                {
                    var arr = mesh.SurfaceGetArrays(si);
                    var v = (Vector3[])arr[(int)Mesh.ArrayType.Vertex];
                    var n = (Vector3[])arr[(int)Mesh.ArrayType.Normal];
                    var idx = (int[])arr[(int)Mesh.ArrayType.Index];
                    for (int t = 0; t + 2 < idx.Length; t += 3)
                    {
                        var c = (v[idx[t + 2]] - v[idx[t]]).Cross(v[idx[t + 1]] - v[idx[t]]);
                        if (c.LengthSquared() < 1e-12f) continue;
                        if (c.Dot(n[idx[t]] + n[idx[t + 1]] + n[idx[t + 2]]) > 0) good++;
                        else bad++;
                    }
                }
            }
            return good > 0 && bad == 0;
        }

        void Check(bool ok, string what)
        {
            Log((ok ? "PASS " : "FAIL ") + what);
            if (!ok) failures.Add(what);
        }

        async Task Wait(float seconds) => await ToSignal(GetTree().CreateTimer(seconds, true, true), SceneTreeTimer.SignalName.Timeout);

        /// <summary>Total absolute turning (degrees) of a yaw over the given time.</summary>
        async Task<float> YawTravel(Func<float> yaw, float seconds)
        {
            float total = 0, last = yaw(), t = 0;
            while (t < seconds)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                t += (float)GetProcessDeltaTime();
                float y = yaw();
                total += Mathf.Abs(Mathf.RadToDeg(Mathf.AngleDifference(last, y)));
                last = y;
            }
            return total;
        }

        async Task<bool> WaitFor(Func<bool> cond, float timeout, string what)
        {
            float t = 0;
            while (!cond())
            {
                if (t > timeout)
                {
                    Check(false, "timeout waiting for " + what);
                    return false;
                }
                await Wait(0.1f);
                t += 0.1f;
            }
            return true;
        }

        async Task Shot(string name)
        {
            if (shots == null) return;
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var img = GetViewport().GetTexture().GetImage();
            var path = $"{shots}/{shotIndex++:00}_{name}.png";
            img.SavePng(path);
            Log("shot " + path);
        }

        /// <summary>The front edge of a table-height furniture top with free floor in front of it.</summary>
        static bool FindClimbSpot(GameWorld w, out Vector3 edge, out Vector3 outward, out string what)
        {
            edge = outward = Vector3.Zero;
            what = "";
            foreach (var f in w.Furniture)
            {
                foreach (var s in f.Surfaces)
                {
                    var top = s.GlobalPosition;
                    if (top.Y < 1.8f || top.Y > 4.5f) continue;
                    var fwd = (-f.GlobalBasis.Z).Flat().Normalized();
                    // walk outwards along the top until it ends
                    var p = top;
                    int steps = 0;
                    while (steps++ < 40 && Phys.Raycast(p + fwd * 0.1f + Vector3.Up * 0.4f, Vector3.Down, 0.6f, Layers.Solid, out var hit) && Mathf.Abs(hit.Point.Y - top.Y) < 0.2f)
                        p += fwd * 0.1f;
                    if (steps >= 40) continue;
                    var stand = new Vector3(p.X, 0.6f, p.Z) + fwd * 1.4f;
                    if (Phys.CheckBox(stand, new Vector3(0.4f, 0.35f, 0.4f), Layers.Solid)) continue; // something in the way
                    if (!Phys.Raycast(stand, Vector3.Down, 1.5f, Layers.Solid, out var floor) || floor.Point.Y > 0.3f) continue;
                    edge = p;
                    outward = fwd;
                    what = f.Kind;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Point the local gnome's view at a world position.</summary>
        static void LookAt(LocalGnome g, Vector3 target)
        {
            var d = target - g.HeadPos;
            g.Yaw = GMath.YawOf(d.Flat());
            g.Pitch = Mathf.Atan2(d.Y, d.Flat().Length());
        }

        async void Run()
        {
            string mode = Args["autotest"];
            try
            {
                if (mode == "client") await RunClient();
                else if (mode == "ui") await RunUiTour();
                else await RunHostOrSolo(mode == "host");
            }
            catch (Exception e)
            {
                Check(false, "exception: " + e);
            }
            Log(failures.Count == 0 ? "ALL PASSED" : $"{failures.Count} FAILURES: " + string.Join("; ", failures));
            GetTree().Quit(failures.Count == 0 ? 0 : 1);
        }

        /// <summary>Visits every screen and overlay and takes a picture of each (for eyeballing the interface).</summary>
        async Task RunUiTour()
        {
            await Wait(1.5f);
            await Shot("ui_menu");
            App.Screen = AppScreen.HostSetup;
            await Wait(0.4f);
            await Shot("ui_host");
            App.Screen = AppScreen.JoinSetup;
            App.StartDiscovery();
            await Wait(0.6f);
            await Shot("ui_join");
            App.StopDiscovery();
            App.SettingsReturn = AppScreen.Menu;
            App.Screen = AppScreen.Settings;
            await Wait(0.4f);
            await Shot("ui_settings");
            App.Screen = AppScreen.Menu;
            await Wait(0.3f);
            App.StartSolo();
            await WaitFor(() => App.Screen == AppScreen.Playing && LocalGnome.I != null, 10, "hub loaded");
            await Wait(1.5f);
            await Shot("ui_village_hud");
            App.CraftOpen = true;
            await Wait(0.4f);
            await Shot("ui_craft");
            App.CraftOpen = false;
            App.ShowGreatSock();
            await Wait(0.4f);
            await Shot("ui_sock");
            App.SockOpen = false;
            App.Paused = true;
            await Wait(0.4f);
            await Shot("ui_pause");
            App.Paused = false;
            S.Host.StartNight();
            await Wait(0.05f);
            await Shot("ui_loading");
            await WaitFor(() => W != null && W.Kind == LevelKind.House && W.Night != null && LocalGnome.I != null, 20, "house loaded");
            await Wait(2f);
            await Shot("ui_house_hud");
            App.Toast("Проверка всплывающего сообщения", Colors.White);
            S.Host.DebugEndNight();
            await WaitFor(() => App.Screen == AppScreen.Report, 10, "report");
            await Wait(1f);
            await Shot("ui_report");
            Check(true, "ui tour done");
        }

        async Task RunHostOrSolo(bool host)
        {
            await Wait(1.5f);
            await Shot("menu");
            Check(W != null && W.Kind == LevelKind.Hub, "menu backdrop village built");
            Check(OutwardFacing("yarn") && OutwardFacing("apple") && OutwardFacing("greatSock"), "model triangles face outwards (Godot front faces are clockwise)");
            if (host)
            {
                if (Args.TryGetValue("port", out var p)) App.Settings.Port = int.Parse(p);
                App.StartHost();
            }
            else App.StartSolo();
            await WaitFor(() => App.Screen == AppScreen.Playing && LocalGnome.I != null, 10, "hub loaded");
            await Wait(1f);
            var g = LocalGnome.I;
            Check(g != null && g.GlobalPosition.Y > -1f && g.GlobalPosition.Y < 3f, $"gnome spawned on the village floor ({g?.GlobalPosition})");
            await Wait(1.5f);
            Check(g.Grounded, "gnome stands on the ground in the village");
            await Shot("village");
            // walk forward a bit with physics
            var before = g.GlobalPosition;
            g.Velocity = g.Forward * 4f;
            await Wait(0.5f);
            Check(W.HighGnome != null && W.CraftBench != null && W.PortalZone != null, "village has sock, knitting corner and portal");

            if (host)
            {
                await WaitFor(() => S.Players.Count >= 2, 30, "client joined");
                await Wait(2f);
                Check(W.Gnomes.Count >= 2, "host sees the client's gnome");
            }

            // --- into the house ---
            S.Host.StartNight();
            await WaitFor(() => W != null && W.Kind == LevelKind.House && W.Night != null && LocalGnome.I != null, 20, "house loaded");
            await Wait(2f);
            g = LocalGnome.I;
            var w = W;
            Log($"house: {w.Props.Count} props, {w.Furniture.Count} furniture, {w.Mechs.Count} mechs");
            Check(w.Props.Count > 40, "items placed in the house");
            Check(w.Furniture.Count > 50, "furniture built");
            Check(w.OldMan != null && w.OldMan.Mode == OldMan.St.Sleep, "grandpa asleep in bed");
            Check(g.Grounded && g.GlobalPosition.Y < 2f, $"gnome on the ground outside ({g.GlobalPosition})");
            await Shot("house_spawn");

            // navigation mesh exists?
            var map = w.GetWorld3D().NavigationMap;
            var bedSpot = w.Layout.Spots["bed"].G();
            var near = NavigationServer3D.MapGetClosestPoint(map, bedSpot);
            Check(near.DistanceTo(bedSpot) < 4f, $"navmesh near the bed ({near} vs {bedSpot})");
            var kitchen = w.Layout.Spots["kitchenTable"].G();
            var path = NavigationServer3D.MapGetPath(map, near, NavigationServer3D.MapGetClosestPoint(map, kitchen), true);
            Check(path.Length > 1, $"grandpa can path from the bed to the kitchen ({path.Length} corners)");

            // --- walk into the living room through the gnome hole and look around ---
            var livingPos = new V3(10.6f * GameConsts.HS, 0.3f, 8.8f * GameConsts.HS).G();
            g.Teleport(livingPos, 0);
            await Wait(1f);
            LookAt(g, w.FindFurniture("sofa")?.GlobalPosition ?? livingPos + Vector3.Forward);
            await Wait(0.5f);
            await Shot("living_room");
            g.ThirdPerson = true;
            await Wait(0.4f);
            await Shot("third_person");
            g.ThirdPerson = false;

            // --- pick up and throw the nearest pocketable / light item ---
            Prop target = null;
            float best = 1e9f;
            foreach (var p in w.Props.Values)
            {
                if (p.GlobalPosition.Y > 1.2f || p.Def.Mass > 1.5f) continue;
                float d = p.GlobalPosition.DistanceTo(g.GlobalPosition);
                if (d < best)
                {
                    best = d;
                    target = p;
                }
            }
            Check(target != null, "found a floor item to play with");
            if (target != null)
            {
                g.Teleport(target.GlobalPosition + new Vector3(1.4f, 0.2f, 0), 0);
                await Wait(0.8f);
                LookAt(g, target.GlobalPosition);
                await Wait(0.2f);
                var start = target.GlobalPosition;
                S.SendAction(new ActionMsg { Type = ActionType.Throw, Id = target.Id, A = (Vector3.Up * 6f + g.Forward * 3f).U() });
                await Wait(0.3f);
                Check(target.GlobalPosition.Y > start.Y + 0.3f || target.LinearVelocity.Length() > 0.5f, "thrown item flies (host physics)");
                await Wait(2f);
                Check(target.GlobalPosition.Y > -1f && target.GlobalPosition.Y < 12f, $"item landed inside the house ({target.GlobalPosition})");
            }

            // --- kick a lamp off ---
            var lamp = w.Furniture.Find(f => f.IsLamp && f.LightsOn);
            if (lamp != null)
            {
                // the host range-checks the kick against the gnome's position: stand next to the lamp first
                g.Teleport(lamp.GlobalPosition + new Vector3(1.2f, 0.2f, 0), 0);
                await Wait(0.5f);
                S.SendAction(new ActionMsg { Type = ActionType.Punch, I = 3, Id = lamp.Index, A = (lamp.GlobalPosition + Vector3.Up * 0.4f).U(), B = Vector3.Left.U() });
                await Wait(0.2f);
                Check(!lamp.LightsOn, "kicking a lamp switches it off");
            }

            // --- open the fridge door (mechanism) ---
            var fridge = w.FindFurniture("fridge")?.Mech("fridge");
            if (fridge != null)
            {
                g.Teleport(fridge.HandleWorld + new Vector3(0, 0, 0) + (-fridge.Furn.GlobalBasis.Z.Normalized()) * 1.5f + Vector3.Down * (fridge.HandleWorld.Y - 0.2f), 0);
                await Wait(0.5f);
                S.SendAction(new ActionMsg { Type = ActionType.Interact, Id = fridge.Index });
                await Wait(1.2f);
                Check(fridge.IsOpen && fridge.Current > 0.5f, "fridge door opens");
                LookAt(g, fridge.Furn.GlobalPosition + Vector3.Up * 3f);
                await Wait(0.2f);
                await Shot("fridge_open");
            }

            // --- climb the yarn onto a table / dresser: hook the edge, hold W ---
            if (FindClimbSpot(w, out var edge, out var outward, out var what))
            {
                g.Teleport(new Vector3(edge.X, 0.25f, edge.Z) + outward * 1.4f, GMath.YawOf(-outward));
                await Wait(0.5f);
                LookAt(g, edge - outward * 0.12f + Vector3.Up * 0.04f);
                await Wait(0.1f);
                bool hooked = g.DebugShootYarn();
                Check(hooked, $"the yarn hook catches the edge of the {what}");
                if (hooked)
                {
                    Keys.Simulated.Add(Key.W);
                    float y0 = g.GlobalPosition.Y;
                    await Wait(0.55f);
                    g.ThirdPerson = true;
                    await Shot("climbing_yarn");
                    bool up = await WaitFor(() => g.Grounded && g.GlobalPosition.Y > edge.Y - 0.3f, 7, "climbing the yarn");
                    Keys.Simulated.Clear();
                    Check(up, $"holding W climbs the yarn onto the {what} (from y {y0:0.0} to {g.GlobalPosition.Y:0.0}, top {edge.Y:0.0})");
                    await Wait(0.3f);
                    await Shot("climbed_up");
                }
            }
            else Check(false, "found a table to climb");

            // --- wake grandpa and watch him get up ---
            w.OldMan.DebugWake();
            await WaitFor(() => w.OldMan.Mode != OldMan.St.Sleep && w.OldMan.Mode != OldMan.St.WakeUp, 6, "grandpa stands up");
            var p0 = w.OldMan.GlobalPosition;
            float walkTurn = await YawTravel(() => w.OldMan.BodyYaw, 4f);
            Check(walkTurn < 540f, $"grandpa does not spin while walking (turned {walkTurn:0} deg in 4 s)");
            Check(w.OldMan.GlobalPosition.DistanceTo(p0) > 1f || w.OldMan.Mode == OldMan.St.Fix, $"grandpa walks around ({w.OldMan.Mode}, moved {w.OldMan.GlobalPosition.DistanceTo(p0):0.0})");
            w.OldMan.DebugSearch();
            float searchTurn = await YawTravel(() => w.OldMan.BodyYaw, 3.5f);
            Check(searchTurn < 400f, $"grandpa looks around without spinning (turned {searchTurn:0} deg in 3.5 s)");
            g.Teleport(w.OldMan.GlobalPosition + GMath.YawForward(w.OldMan.BodyYaw) * 9f + Vector3.Up * 0.2f, 0);
            await Wait(0.5f);
            LookAt(g, w.OldMan.GlobalPosition + Vector3.Up * 4f);
            await Wait(0.3f);
            await Shot("grandpa_awake");

            // --- catch + jar + rescue flow (host logic) ---
            var slot = S.LocalSlot;
            S.Host.Catch(slot.Id);
            await Wait(0.3f);
            Check(slot.Status == PlayerStatus.Carried, "caught gnome is carried");
            int jar = S.Host.FreeJar();
            Check(jar >= 0, "a free pickle jar exists");
            if (jar >= 0)
            {
                S.Host.PutInJar(slot.Id, jar);
                await Wait(0.5f);
                Check(slot.Status == PlayerStatus.Trapped, "gnome sits in the jar");
                await Shot("in_jar");
                for (int i = 0; i < GameConsts.StruggleJar; i++) S.SendAction(new ActionMsg { Type = ActionType.Struggle });
                await Wait(0.5f);
                Check(slot.Status == PlayerStatus.Free, "struggling out of the jar frees the gnome");
            }
            w.OldMan.DebugSleep();

            // --- flatten + hat revive (solo spare hats) ---
            if (!host)
            {
                int hats = S.Host.Night.SpareHats;
                S.Host.Flatten(slot.Id);
                await Wait(0.2f);
                Check(slot.Status == PlayerStatus.Dead, "fly swatter flattens the gnome");
                await WaitFor(() => slot.Status == PlayerStatus.Free, 8, "solo revive with a spare hat");
                Check(S.Host.Night.SpareHats == hats - 1, "a spare hat was used");
            }

            // --- bank an item in the sardine cart ---
            var loot = FirstPocketable(w);
            if (loot != null && w.StashZone != null)
            {
                int haul = S.Host.Night.HaulValue;
                loot.GlobalPosition = w.StashZone.GlobalPosition;
                loot.LinearVelocity = Vector3.Zero;
                await Wait(1f);
                Check(S.Host.Night.HaulValue > haul, "loot dropped into the cart is banked");
            }

            await Wait(1f);
            // --- end the night ---
            S.Host.DebugEndNight();
            await WaitFor(() => App.Screen == AppScreen.Report, 5, "report screen");
            await Wait(0.5f);
            await Shot("report");
            App.ContinueFromReport();
            await WaitFor(() => W != null && W.Kind == LevelKind.Hub && App.Screen == AppScreen.Playing, 10, "back in the village");
            Check(S.Save.Night >= 1, "save advanced");
            if (host) await Wait(3f); // let the client follow
            App.LeaveToMenu();
            await Wait(1f);
            Check(App.Screen == AppScreen.Menu, "back to the main menu");
        }

        static Prop FirstPocketable(GameWorld w)
        {
            foreach (var p in w.Props.Values) if (p.Def.Has(ItemFlags.Pocketable) && p.Def.Value > 0) return p;
            return null;
        }

        async Task RunClient()
        {
            await Wait(2f);
            int port = Args.TryGetValue("port", out var p) ? int.Parse(p) : GameConsts.DefaultPort;
            App.StartJoin("127.0.0.1", port);
            await WaitFor(() => App.Screen == AppScreen.Playing && LocalGnome.I != null, 20, "client joined the hub");
            Check(W.Kind == LevelKind.Hub, "client loaded the village");
            await WaitFor(() => W != null && W.Kind == LevelKind.House && LocalGnome.I != null, 40, "client followed into the house");
            await Wait(3f);
            var w = W;
            Check(w.Props.Count > 40, $"client sees the items ({w.Props.Count})");
            Check(w.Gnomes.Count >= 2, "client sees the host's gnome");
            var g = LocalGnome.I;
            Check(g.GlobalPosition.Y > -1f, "client gnome did not fall through the floor");
            await Shot("client_house");
            await WaitFor(() => App.Screen == AppScreen.Report || App.Screen == AppScreen.Menu, 90, "client got the night report");
            await Wait(1f);
            await Shot("client_report");
            await WaitFor(() => App.Screen == AppScreen.Menu || (W != null && W.Kind == LevelKind.Hub && App.Screen == AppScreen.Playing), 20, "client back in the village");
        }
    }
}
