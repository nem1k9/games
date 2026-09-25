using System.Globalization;
using Godot;

namespace SockGang.World
{
    /// <summary>
    /// A movable furniture part: hinged doors (oven, freezer, fridge, safe), sliding window sashes
    /// and buttons (TV power, taps, flush). The host owns the target state; everyone animates.
    /// Lives as a child of the MECH_ node it moves.
    /// </summary>
    public partial class Mechanism : Node
    {
        public ushort Index;
        public string Role;
        public string Kind; // hinge | slide | button
        public char Axis = 'y';
        public float OpenAmount; // radians (hinge) or world units (slide)
        public bool HumanOnly;
        public Vector3 HandleLocal;
        public Furniture Furn;
        public Node3D Part;
        public string PartName;

        public float Target;
        public float Current;
        public bool Locked; // safe before it is cracked
        public int LockHits;
        public float PressedTime = -10;

        Vector3 basePos;
        Quaternion baseRot;

        public bool IsOpen => Target > 0.5f;
        public bool IsButton => Kind == "button";
        public Vector3 HandleWorld => Part.ToGlobal(HandleLocal);
        public Vector3 GlobalPosition => Part.GlobalPosition;

        public void Init(ushort index, Furniture owner, Node3D part, string partName)
        {
            Index = index;
            Furn = owner;
            Part = part;
            PartName = partName;
            part.SetMeta("mech_index", (int)index);
            var props = owner.Model.Model.Find(partName)?.Props;
            string Get(string k, string d) => props != null && props.TryGetValue(k, out var v) ? v : d;
            float F(string k, float d) => float.TryParse(Get(k, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : d;
            Role = Get("role", "door");
            Kind = Get("kind", "hinge");
            Axis = Get("axis", "y")[0];
            OpenAmount = F("open", 1.5f);
            HumanOnly = Get("humanOnly", "0") == "1";
            HandleLocal = new Vector3(F("hx", 0), F("hy", 0), -F("hz", 0));
            basePos = part.Position;
            baseRot = part.Quaternion;
            if (Role == "safe")
            {
                Locked = true;
                LockHits = 5;
            }
        }

        /// <summary>The mechanism a collider belongs to (walks up to the MECH part).</summary>
        public static Mechanism FromCollider(Node n)
        {
            var w = GameWorld.Current;
            while (n != null)
            {
                if (n.HasMeta("mech_index")) return w?.GetMech((int)n.GetMeta("mech_index"));
                n = n.GetParent();
            }
            return null;
        }

        // Unity-space axis letter -> Godot vector (Z mirrored)
        Vector3 AxisVec => Axis == 'x' ? Vector3.Right : Axis == 'z' ? new Vector3(0, 0, -1) : Vector3.Up;

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;
            if (IsButton)
            {
                Current = GMath.MoveTowards(Current, Target, 8f * dt);
                float press = Mathf.Clamp(1f - (Clock.Now - PressedTime) * 5f, 0f, 1f);
                Part.Position = basePos + baseRot * new Vector3(0, 0, 0.02f * press); // bob "into" the furniture
                return;
            }
            float speed = Role == "trap" ? 2.2f : 1.6f;
            Current = GMath.MoveTowards(Current, Target, speed * dt);
            float eased = Current * Current * (3 - 2 * Current);
            if (Kind == "hinge")
            {
                // Unity AngleAxis(angle, axis) mirrored: rotate about the mirrored axis by -angle
                Part.Quaternion = baseRot * new Quaternion(AxisVec, -OpenAmount * eased);
                Part.Position = basePos;
            }
            else if (Kind == "slide")
            {
                Part.Position = basePos + baseRot * (AxisVec * (OpenAmount * eased));
                Part.Quaternion = baseRot;
            }
        }

        /// <summary>Host: toggle. Returns the new on/open state.</summary>
        public bool Toggle()
        {
            PressedTime = Clock.Now;
            Target = Target > 0.5f ? 0f : 1f;
            return Target > 0.5f;
        }

        public void SetState(bool open) => Target = open ? 1f : 0f;

        public byte NetState => (byte)Mathf.RoundToInt(Mathf.Clamp(Target, 0f, 1f) * 255f);

        public void ApplyNet(byte b)
        {
            float t = b / 255f;
            if (IsButton && Mathf.Abs(t - Target) > 0.01f) PressedTime = Clock.Now;
            Target = t;
        }
    }
}
