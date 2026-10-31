using Cities.World.Grid;
using UnityEngine;
using static Cities.Editor.Models.BuildingMaterials.Slots;

namespace Cities.Editor.Models
{
    /// <summary>
    /// Bouwt het model van een <see cref="BuildingDesign"/> (E05-08): romp op een plint, dak (zadel, plat of zaagtand), gevels met
    /// ramen, balkons, etalages, overheaddeuren of laadperron, en het terrein eromheen. Het model past in de cellen van de kavel
    /// (8 m per cel); de voorkant (straat) ligt aan lokaal -Z en de oorsprong is het midden van de kavel op maaiveld.
    /// </summary>
    internal sealed class BuildingBuilder
    {
        private const float PlinthTop = 0.45f;
        private const float Frame = 0.07f;
        private const float FrameDepth = 0.07f;

        private readonly BuildingDesign _d;
        private readonly ModelMesh _m = new ModelMesh(Count);
        private readonly float _hx;
        private readonly float _hz;
        private readonly float _x0;
        private readonly float _x1;
        private readonly float _z0;
        private readonly float _z1;
        private readonly float _top;
        private readonly int _glass;
        private readonly System.Random _random;

        private BuildingBuilder(BuildingDesign design)
        {
            _d = design;
            _hx = design.Spec.FootprintWidth * 4f;
            _hz = design.Spec.FootprintDepth * 4f;
            var front = design.Zone == ZoneType.Residential && !design.HighDensity ? 3f : 2f;
            _x0 = -(_hx - 1f);
            _x1 = _hx - 1f;
            _z0 = -(_hz - front);
            _z1 = _hz - design.Rear;
            _top = PlinthTop + design.GroundHeight + ((design.Floors - 1) * design.FloorHeight);
            _glass = design.Zone == ZoneType.Office && design.HighDensity ? Curtain : Glass;
            var seed = 17;
            foreach (var c in design.Name)
            {
                seed = (seed * 31) + c;
            }

            _random = new System.Random(seed);
        }

        private float Length => _x1 - _x0;

        private float Depth => _z1 - _z0;

        private float ZMid => (_z0 + _z1) * 0.5f;

        public static Mesh BuildMesh(BuildingDesign design) => new BuildingBuilder(design).Build();

        private Mesh Build()
        {
            _m.Box(Plinth, new Vector3(_x0 - 0.03f, -0.4f, _z0 - 0.03f), new Vector3(_x1 + 0.03f, PlinthTop, _z1 + 0.03f));
            _m.Box(_d.Wall, new Vector3(_x0, PlinthTop, _z0), new Vector3(_x1, _top, _z1));
            switch (_d.Roof)
            {
                case RoofKind.Gable:
                    GableRoof();
                    break;
                case RoofKind.Sawtooth:
                    SawtoothRoof();
                    break;
                default:
                    FlatRoof();
                    break;
            }

            Facades();
            Site();
            Yard();
            return _m.Build(_d.Name + "Mesh");
        }

        // ---------- Daken ----------

        private void GableRoof()
        {
            const float overhang = 0.45f;
            const float verge = 0.3f;
            const float thickness = 0.2f;
            var half = Depth * 0.5f;
            var ridge = _top + (half * 0.75f);
            var slope = (ridge - _top) / half;
            var lift = thickness * Mathf.Sqrt(1f + (slope * slope));
            var eaveY = _top - (overhang * slope);
            var xa = _x0 - verge;
            var xb = _x1 + verge;

            foreach (var x in new[] { _x0, _x1 })
            {
                _m.Triangle(_d.Wall, new Vector3(x, _top, _z0), new Vector3(x, _top, _z1), new Vector3(x, ridge, ZMid), new Vector3(Mathf.Sign(x), 0f, 0f));
            }

            foreach (var side in new[] { -1f, 1f })
            {
                var eaveZ = side < 0f ? _z0 - overhang : _z1 + overhang;
                _m.Hexahedron(RoofTiles, new[]
                {
                    new Vector3(xa, eaveY, eaveZ), new Vector3(xb, eaveY, eaveZ), new Vector3(xb, eaveY + lift, eaveZ), new Vector3(xa, eaveY + lift, eaveZ),
                    new Vector3(xa, ridge, ZMid), new Vector3(xb, ridge, ZMid), new Vector3(xb, ridge + lift, ZMid), new Vector3(xa, ridge + lift, ZMid),
                });

                // Boeideel en bakgoot langs de dakrand.
                _m.Box(Trim, new Vector3(xa, eaveY - 0.18f, Mathf.Min(eaveZ, eaveZ + (side * 0.03f))), new Vector3(xb, eaveY + lift, Mathf.Max(eaveZ, eaveZ + (side * 0.03f))));
                var gutter = eaveZ + (side * 0.14f);
                _m.Box(Zinc, new Vector3(_x0 - 0.3f, eaveY - 0.32f, Mathf.Min(eaveZ, gutter)), new Vector3(_x1 + 0.3f, eaveY - 0.18f, Mathf.Max(eaveZ, gutter)));

                // Regenpijp op de hoek, recht naar beneden langs de gevel.
                var wallZ = side < 0f ? _z0 - 0.08f : _z1;
                _m.Box(Zinc, new Vector3(_x1 - 0.25f, 0.15f, wallZ), new Vector3(_x1 - 0.17f, eaveY - 0.32f, wallZ + 0.08f));
            }

            _m.Box(RoofTiles, new Vector3(xa, ridge + lift - 0.05f, ZMid - 0.16f), new Vector3(xb, ridge + lift + 0.12f, ZMid + 0.16f));

            if (_d.Chimneys)
            {
                var count = Mathf.Max(1, _d.Units / 2);
                for (var i = 0; i < count; i++)
                {
                    var x = Mathf.Lerp(_x0, _x1, (i + 0.5f) / count);
                    _m.Box(_d.Wall, new Vector3(x - 0.35f, ridge - 2.2f, ZMid + 0.35f), new Vector3(x + 0.35f, ridge + 1.1f, ZMid + 1.05f));
                    _m.Box(Stone, new Vector3(x - 0.43f, ridge + 1.1f, ZMid + 0.27f), new Vector3(x + 0.43f, ridge + 1.2f, ZMid + 1.13f));
                }
            }
        }

        private void FlatRoof()
        {
            const float parapet = 0.6f;
            const float wall = 0.25f;
            var t = _top;
            _m.Box(RoofFlat, new Vector3(_x0, t, _z0), new Vector3(_x1, t + 0.1f, _z1));
            _m.Box(_d.Wall, new Vector3(_x0 - 0.02f, t, _z0 - 0.02f), new Vector3(_x1 + 0.02f, t + parapet, _z0 + wall));
            _m.Box(_d.Wall, new Vector3(_x0 - 0.02f, t, _z1 - wall), new Vector3(_x1 + 0.02f, t + parapet, _z1 + 0.02f));
            _m.Box(_d.Wall, new Vector3(_x0 - 0.02f, t, _z0), new Vector3(_x0 + wall, t + parapet, _z1));
            _m.Box(_d.Wall, new Vector3(_x1 - wall, t, _z0), new Vector3(_x1 + 0.02f, t + parapet, _z1));

            // Afdekkap van natuursteen op de dakrand.
            _m.Box(Stone, new Vector3(_x0 - 0.06f, t + parapet, _z0 - 0.06f), new Vector3(_x1 + 0.06f, t + parapet + 0.06f, _z0 + wall + 0.04f));
            _m.Box(Stone, new Vector3(_x0 - 0.06f, t + parapet, _z1 - wall - 0.04f), new Vector3(_x1 + 0.06f, t + parapet + 0.06f, _z1 + 0.06f));
            _m.Box(Stone, new Vector3(_x0 - 0.06f, t + parapet, _z0), new Vector3(_x0 + wall + 0.04f, t + parapet + 0.06f, _z1));
            _m.Box(Stone, new Vector3(_x1 - wall - 0.04f, t + parapet, _z0), new Vector3(_x1 + 0.06f, t + parapet + 0.06f, _z1));

            RoofEquipment(t + 0.1f);
        }

        /// <summary>Verspreidt luchtbehandeling, een liftkern en een antenne over het platte dak, vast per ontwerp.</summary>
        private void RoofEquipment(float roofY)
        {
            var columns = Mathf.Max(2, Mathf.FloorToInt(Length / 8f));
            var rows = Mathf.Max(1, Mathf.FloorToInt(Depth / 8f));
            var slots = new System.Collections.Generic.List<Vector2>();
            for (var c = 0; c < columns; c++)
            {
                for (var r = 0; r < rows; r++)
                {
                    slots.Add(new Vector2(Mathf.Lerp(_x0 + 2.5f, _x1 - 2.5f, (c + 0.5f) / columns), Mathf.Lerp(_z0 + 2.5f, _z1 - 2.5f, (r + 0.5f) / rows)));
                }
            }

            for (var i = slots.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                (slots[i], slots[j]) = (slots[j], slots[i]);
            }

            var index = 0;
            if (_d.Floors >= 4)
            {
                var core = slots[index++];
                _m.Box(Concrete, new Vector3(core.x - 1.8f, roofY, core.y - 1.8f), new Vector3(core.x + 1.8f, roofY + 2.9f, core.y + 1.8f));
                _m.Box(Stone, new Vector3(core.x - 1.9f, roofY + 2.9f, core.y - 1.9f), new Vector3(core.x + 1.9f, roofY + 3f, core.y + 1.9f));
            }

            var units = Mathf.Min(slots.Count - index, 1 + (int)(Length * Depth / 140f));
            for (var i = 0; i < units; i++)
            {
                var at = slots[index++];
                _m.Box(Zinc, new Vector3(at.x - 1f, roofY + 0.3f, at.y - 0.6f), new Vector3(at.x + 1f, roofY + 1.5f, at.y + 0.6f));
                _m.Box(Stone, new Vector3(at.x - 1.1f, roofY, at.y - 0.7f), new Vector3(at.x + 1.1f, roofY + 0.3f, at.y + 0.7f));
                _m.Cylinder(Door, new Vector3(at.x - 0.45f, roofY + 1.5f, at.y), 0.38f, 0.06f, 10);
                _m.Cylinder(Door, new Vector3(at.x + 0.45f, roofY + 1.5f, at.y), 0.38f, 0.06f, 10);
            }

            if (_d.Floors >= 8)
            {
                _m.Box(Zinc, new Vector3(_x0 + 3.8f, roofY, _z0 + 3.8f), new Vector3(_x0 + 3.95f, roofY + 9f, _z0 + 3.95f));
                _m.Box(Zinc, new Vector3(_x0 + 3.3f, roofY + 7f, _z0 + 3.84f), new Vector3(_x0 + 4.45f, roofY + 7.06f, _z0 + 3.91f));
            }
        }

        /// <summary>Zaagtanddak voor hallen: een schuin dakvlak met aan de hoge kant een glazen lichtstraat op het noorden.</summary>
        private void SawtoothRoof()
        {
            const float rise = 2.2f;
            var teeth = Mathf.Max(2, Mathf.RoundToInt(Depth / 7f));
            var period = Depth / teeth;
            _m.Box(RoofFlat, new Vector3(_x0, _top, _z0), new Vector3(_x1, _top + 0.1f, _z1));
            for (var i = 0; i < teeth; i++)
            {
                var za = _z0 + (i * period);
                var zb = za + period;
                var y = _top + 0.1f;
                _m.Quad(Cladding, new Vector3(_x0, y, za), new Vector3(_x1, y, za), new Vector3(_x1, y + rise, zb), new Vector3(_x0, y + rise, zb), new Vector3(0f, 1f, -0.5f));
                _m.Quad(Glass, new Vector3(_x0, y, zb), new Vector3(_x1, y, zb), new Vector3(_x1, y + rise, zb), new Vector3(_x0, y + rise, zb), Vector3.forward);
                foreach (var x in new[] { _x0, _x1 })
                {
                    _m.Triangle(_d.Wall, new Vector3(x, y, za), new Vector3(x, y, zb), new Vector3(x, y + rise, zb), new Vector3(Mathf.Sign(x), 0f, 0f));
                }
            }
        }

        // ---------- Gevels ----------

        private void Facades()
        {
            var front = new Facade(new Vector3(0f, 0f, _z0), Vector3.right, Vector3.back);
            var back = new Facade(new Vector3(0f, 0f, _z1), Vector3.left, Vector3.forward);
            var left = new Facade(new Vector3(_x0, 0f, ZMid), Vector3.back, Vector3.left);
            var right = new Facade(new Vector3(_x1, 0f, ZMid), Vector3.forward, Vector3.right);

            FrontGround(front);
            Openings(front, Length, true);
            Openings(back, Length, false);
            Openings(left, Depth, false);
            Openings(right, Depth, false);
            if (_d.Ground == GroundKind.Docks)
            {
                LoadingDock(back);
            }
        }

        private float FloorY(int floor) => floor == 0 ? PlinthTop : PlinthTop + _d.GroundHeight + ((floor - 1) * _d.FloorHeight);

        private float FloorHeightOf(int floor) => floor == 0 ? _d.GroundHeight : _d.FloorHeight;

        /// <summary>Of de begane grond van de voorgevel al is ingevuld (etalage, lobby, deuren), zodat de ramen eerst boven beginnen.</summary>
        private bool GroundTaken(bool front) => front &&
            (_d.Ground == GroundKind.Shop || _d.Ground == GroundKind.Lobby || _d.Ground == GroundKind.Hall || _d.Ground == GroundKind.Houses ||
             (_d.Windows != WindowKind.Punched && (_d.Ground == GroundKind.Entrance || _d.Ground == GroundKind.Docks)));

        private void Openings(Facade f, float length, bool front)
        {
            var first = GroundTaken(front) ? 1 : 0;
            if (_d.Ground == GroundKind.Houses && front)
            {
                HouseFront(f, length);
                return;
            }

            switch (_d.Windows)
            {
                case WindowKind.Ribbon:
                    for (var floor = first; floor < _d.Floors; floor++)
                    {
                        Ribbon(f, length, FloorY(floor) + 0.85f, FloorY(floor) + FloorHeightOf(floor) - 0.5f);
                    }

                    break;
                case WindowKind.HighStrip:
                    for (var floor = first; floor < _d.Floors; floor++)
                    {
                        HighStrip(f, length, FloorY(floor) + FloorHeightOf(floor) - 1.5f);
                    }

                    break;
                default:
                    for (var floor = first; floor < _d.Floors; floor++)
                    {
                        Punched(f, length, floor, front && _d.Balconies && floor > 0);
                    }

                    break;
            }
        }

        private void Punched(Facade f, float length, int floor, bool balconies)
        {
            var bays = Mathf.Max(1, Mathf.RoundToInt(length / 3.6f));
            var step = length / bays;
            var width = Mathf.Min(1.6f, step * 0.5f);
            for (var i = 0; i < bays; i++)
            {
                var u = (-length * 0.5f) + (step * (i + 0.5f));
                if (floor == 0 && _d.Ground == GroundKind.Entrance && Mathf.Abs(u) < 2.2f && f.Normal == Vector3.back)
                {
                    continue;
                }

                if (balconies && i % 2 == 0)
                {
                    Balcony(f, u, FloorY(floor));
                }
                else
                {
                    Window(f, u, FloorY(floor) + 0.95f, width, 1.4f);
                }
            }
        }

        private void Ribbon(Facade f, float length, float y0, float y1)
        {
            var u0 = (-length * 0.5f) + 0.5f;
            var u1 = (length * 0.5f) - 0.5f;
            _m.Quad(_glass, f.Point(u0, y0, 0.02f), f.Point(u1, y0, 0.02f), f.Point(u1, y1, 0.02f), f.Point(u0, y1, 0.02f), f.Normal);
            _m.Hexahedron(Zinc, f.Block(u0, u1, y0 - 0.06f, y0, 0f, 0.07f));
            _m.Hexahedron(Zinc, f.Block(u0, u1, y1, y1 + 0.06f, 0f, 0.07f));
            var bays = Mathf.Max(1, Mathf.RoundToInt((u1 - u0) / 1.5f));
            for (var i = 0; i <= bays; i++)
            {
                var u = Mathf.Lerp(u0, u1, i / (float)bays);
                _m.Hexahedron(Zinc, f.Block(u - 0.03f, u + 0.03f, y0, y1, 0f, 0.09f));
            }
        }

        private void HighStrip(Facade f, float length, float y)
        {
            var bays = Mathf.Max(1, Mathf.RoundToInt(length / 4.5f));
            var step = length / bays;
            for (var i = 0; i < bays; i++)
            {
                var u = (-length * 0.5f) + (step * (i + 0.5f));
                Window(f, u, y, step * 0.6f, 0.9f, false);
            }
        }

        private void Window(Facade f, float u, float sill, float width, float height, bool stoneSill = true)
        {
            var u0 = u - (width * 0.5f);
            var u1 = u + (width * 0.5f);
            var top = sill + height;
            _m.Quad(_glass, f.Point(u0, sill, 0.015f), f.Point(u1, sill, 0.015f), f.Point(u1, top, 0.015f), f.Point(u0, top, 0.015f), f.Normal);
            Casing(f, u0, u1, sill, top);
            if (width > 1.25f)
            {
                _m.Hexahedron(Trim, f.Block(u - (Frame * 0.5f), u + (Frame * 0.5f), sill, top, 0f, FrameDepth * 0.8f));
            }

            if (height > 1.5f)
            {
                var transom = sill + (height * 0.74f);
                _m.Hexahedron(Trim, f.Block(u0, u1, transom - (Frame * 0.5f), transom + (Frame * 0.5f), 0f, FrameDepth * 0.8f));
            }

            if (stoneSill)
            {
                _m.Hexahedron(Stone, f.Block(u0 - 0.06f, u1 + 0.06f, sill - 0.07f, sill, -0.02f, 0.11f));
            }

            _m.Hexahedron(Zinc, f.Block(u0 - 0.1f, u1 + 0.1f, top, top + 0.04f, 0f, 0.012f));
        }

        private void Casing(Facade f, float u0, float u1, float bottom, float top)
        {
            _m.Hexahedron(Trim, f.Block(u0, u0 + Frame, bottom, top, -0.02f, FrameDepth));
            _m.Hexahedron(Trim, f.Block(u1 - Frame, u1, bottom, top, -0.02f, FrameDepth));
            _m.Hexahedron(Trim, f.Block(u0, u1, bottom, bottom + Frame, -0.02f, FrameDepth));
            _m.Hexahedron(Trim, f.Block(u0, u1, top - Frame, top, -0.02f, FrameDepth));
        }

        /// <summary>Balkon met een deurraam: betonnen vloer, glazen borstwering en een stalen handrail.</summary>
        private void Balcony(Facade f, float u, float floorY)
        {
            const float reach = 1.3f;
            Window(f, u, floorY + 0.05f, 1.6f, 2.15f, false);
            var u0 = u - 1.3f;
            var u1 = u + 1.3f;
            _m.Hexahedron(Concrete, f.Block(u0, u1, floorY - 0.15f, floorY + 0.02f, 0f, reach));
            _m.Hexahedron(_glass, f.Block(u0, u1, floorY + 0.1f, floorY + 1f, reach - 0.04f, reach - 0.01f));
            _m.Hexahedron(_glass, f.Block(u0, u0 + 0.03f, floorY + 0.1f, floorY + 1f, 0f, reach));
            _m.Hexahedron(_glass, f.Block(u1 - 0.03f, u1, floorY + 0.1f, floorY + 1f, 0f, reach));
            _m.Hexahedron(Zinc, f.Block(u0, u1, floorY + 1f, floorY + 1.05f, reach - 0.06f, reach));
            _m.Hexahedron(Zinc, f.Block(u0, u0 + 0.05f, floorY + 1f, floorY + 1.05f, 0f, reach));
            _m.Hexahedron(Zinc, f.Block(u1 - 0.05f, u1, floorY + 1f, floorY + 1.05f, 0f, reach));
        }

        // ---------- Begane grond ----------

        private void FrontGround(Facade f)
        {
            switch (_d.Ground)
            {
                case GroundKind.Entrance:
                    EntranceDoor(f, 0f, 2f);
                    break;
                case GroundKind.Shop:
                    Shopfront(f);
                    break;
                case GroundKind.Lobby:
                    Lobby(f);
                    break;
                case GroundKind.Hall:
                    RollerDoors(f);
                    break;
                case GroundKind.Docks:
                    EntranceDoor(f, -Length * 0.5f + 3f, 1.1f);
                    break;
            }
        }

        private void HouseFront(Facade f, float length)
        {
            var unit = length / _d.Units;
            for (var i = 0; i < _d.Units; i++)
            {
                var c = (-length * 0.5f) + (unit * (i + 0.5f));
                EntranceDoor(f, c - (unit * 0.2f), 1.0f);
                Window(f, c + (unit * 0.22f), PlinthTop + 0.85f, Mathf.Min(2.4f, unit * 0.34f), 1.7f);
                for (var floor = 1; floor < _d.Floors; floor++)
                {
                    Window(f, c - (unit * 0.2f), FloorY(floor) + 0.7f, 1.1f, 1.35f);
                    Window(f, c + (unit * 0.22f), FloorY(floor) + 0.7f, Mathf.Min(1.6f, unit * 0.3f), 1.35f);
                }
            }
        }

        private void EntranceDoor(Facade f, float u, float width)
        {
            var height = 2.3f;
            var u0 = u - (width * 0.5f);
            var u1 = u + (width * 0.5f);
            _m.Hexahedron(Door, f.Block(u0, u1, PlinthTop, PlinthTop + height, -0.05f, 0.02f));
            if (width > 1.5f)
            {
                _m.Quad(_glass, f.Point(u0 + 0.1f, PlinthTop + 0.15f, 0.025f), f.Point(u1 - 0.1f, PlinthTop + 0.15f, 0.025f), f.Point(u1 - 0.1f, PlinthTop + height - 0.1f, 0.025f), f.Point(u0 + 0.1f, PlinthTop + height - 0.1f, 0.025f), f.Normal);
                _m.Hexahedron(Zinc, f.Block(u - 0.03f, u + 0.03f, PlinthTop, PlinthTop + height, 0f, 0.06f));
            }

            Casing(f, u0 - Frame, u1 + Frame, PlinthTop, PlinthTop + height + 0.5f);
            _m.Hexahedron(Trim, f.Block(u0, u1, PlinthTop + height, PlinthTop + height + Frame, 0f, FrameDepth));
            _m.Quad(_glass, f.Point(u0, PlinthTop + height + Frame, 0.01f), f.Point(u1, PlinthTop + height + Frame, 0.01f), f.Point(u1, PlinthTop + height + 0.5f - Frame, 0.01f), f.Point(u0, PlinthTop + height + 0.5f - Frame, 0.01f), f.Normal);
            _m.Hexahedron(Zinc, f.Block(u1 - 0.18f, u1 - 0.06f, PlinthTop + 1.02f, PlinthTop + 1.06f, 0.02f, 0.08f));

            // Stoep en, bij brede entrees, een betonnen luifel.
            _m.Hexahedron(Stone, f.Block(u0 - 0.3f, u1 + 0.3f, -0.2f, PlinthTop - 0.02f, 0f, 0.7f));
            if (width > 1.5f)
            {
                _m.Hexahedron(Concrete, f.Block(u0 - 1.2f, u1 + 1.2f, PlinthTop + 2.95f, PlinthTop + 3.15f, 0f, 1.7f));
                _m.Hexahedron(Zinc, f.Block(u0 - 1.1f, u0 - 1.04f, PlinthTop, PlinthTop + 2.95f, 1.6f, 1.66f));
                _m.Hexahedron(Zinc, f.Block(u1 + 1.04f, u1 + 1.1f, PlinthTop, PlinthTop + 2.95f, 1.6f, 1.66f));
            }
        }

        private void Shopfront(Facade f)
        {
            var g = _d.GroundHeight;
            var u0 = -Length * 0.5f + 0.4f;
            var u1 = Length * 0.5f - 0.4f;
            var glassTop = PlinthTop + g - 1.15f;

            _m.Hexahedron(Plinth, f.Block(u0, u1, PlinthTop, PlinthTop + 0.4f, -0.02f, 0.05f));
            _m.Quad(Glass, f.Point(u0, PlinthTop + 0.4f, 0.02f), f.Point(u1, PlinthTop + 0.4f, 0.02f), f.Point(u1, glassTop, 0.02f), f.Point(u0, glassTop, 0.02f), f.Normal);
            var bays = Mathf.Max(2, Mathf.RoundToInt((u1 - u0) / 1.7f));
            for (var i = 0; i <= bays; i++)
            {
                var u = Mathf.Lerp(u0, u1, i / (float)bays);
                _m.Hexahedron(Zinc, f.Block(u - 0.04f, u + 0.04f, PlinthTop + 0.4f, glassTop, 0f, 0.1f));
            }

            _m.Hexahedron(Zinc, f.Block(u0, u1, PlinthTop + 0.36f, PlinthTop + 0.44f, 0f, 0.1f));
            _m.Hexahedron(Zinc, f.Block(u0, u1, glassTop - 0.04f, glassTop + 0.04f, 0f, 0.1f));

            // Dubbele schuifdeur in de middelste travee.
            var doorU = Mathf.Lerp(u0, u1, (bays / 2) / (float)bays) + (((u1 - u0) / bays) * 0.5f);
            _m.Hexahedron(Door, f.Block(doorU - 0.9f, doorU + 0.9f, PlinthTop + 0.1f, glassTop - 0.1f, 0.02f, 0.06f));
            _m.Hexahedron(Zinc, f.Block(doorU - 0.04f, doorU + 0.04f, PlinthTop + 0.1f, glassTop - 0.1f, 0.06f, 0.1f));

            // Reclamebord boven de etalage en een schuine luifel.
            _m.Hexahedron(_d.Accent, f.Block(u0 - 0.1f, u1 + 0.1f, glassTop + 0.35f, PlinthTop + g + 0.6f, -0.02f, 0.25f));
            _m.Hexahedron(Trim, f.Block(u0 - 0.1f, u1 + 0.1f, glassTop + 0.28f, glassTop + 0.35f, -0.02f, 0.27f));
            _m.Hexahedron(_d.Accent, new[]
            {
                f.Point(u0, glassTop + 0.3f, 0.25f), f.Point(u1, glassTop + 0.3f, 0.25f), f.Point(u1, glassTop + 0.3f, 0.3f), f.Point(u0, glassTop + 0.3f, 0.3f),
                f.Point(u0, glassTop - 0.15f, 1.3f), f.Point(u1, glassTop - 0.15f, 1.3f), f.Point(u1, glassTop - 0.1f, 1.3f), f.Point(u0, glassTop - 0.1f, 1.3f),
            });
        }

        private void Lobby(Facade f)
        {
            var g = _d.GroundHeight;
            var u0 = -Length * 0.5f + 0.4f;
            var u1 = Length * 0.5f - 0.4f;
            var top = PlinthTop + g - 0.4f;
            _m.Quad(Curtain, f.Point(u0, PlinthTop, 0.02f), f.Point(u1, PlinthTop, 0.02f), f.Point(u1, top, 0.02f), f.Point(u0, top, 0.02f), f.Normal);
            var bays = Mathf.Max(2, Mathf.RoundToInt((u1 - u0) / 1.5f));
            for (var i = 0; i <= bays; i++)
            {
                var u = Mathf.Lerp(u0, u1, i / (float)bays);
                _m.Hexahedron(Zinc, f.Block(u - 0.04f, u + 0.04f, PlinthTop, top, 0f, 0.12f));
            }

            _m.Hexahedron(Zinc, f.Block(u0, u1, top - 0.04f, top + 0.04f, 0f, 0.12f));

            // Draaideur met een luifel op twee kolommen.
            var centre = f.Point(0f, PlinthTop, 1.0f);
            _m.Cylinder(Glass, centre, 1.15f, 2.4f, 12);
            _m.Hexahedron(Concrete, f.Block(-3.5f, 3.5f, top - 0.2f, top + 0.05f, 0f, 1.8f));
            _m.Hexahedron(Zinc, f.Block(-3.4f, -3.32f, PlinthTop, top - 0.2f, 1.6f, 1.68f));
            _m.Hexahedron(Zinc, f.Block(3.32f, 3.4f, PlinthTop, top - 0.2f, 1.6f, 1.68f));
            _m.Hexahedron(Stone, f.Block(-3.6f, 3.6f, -0.2f, PlinthTop - 0.02f, 0f, 2f));
        }

        private void RollerDoors(Facade f)
        {
            var count = Mathf.Max(1, Mathf.FloorToInt(Length / 10f));
            var step = Length / count;
            for (var i = 0; i < count; i++)
            {
                RollerDoor(f, (-Length * 0.5f) + (step * (i + 0.5f)), PlinthTop, 4.2f, 4.5f);
            }

            // Personeelsdeur naast de eerste overheaddeur.
            EntranceDoor(f, (Length * 0.5f) - 2f, 1.0f);
        }

        private void RollerDoor(Facade f, float u, float baseY, float width, float height)
        {
            var u0 = u - (width * 0.5f);
            var u1 = u + (width * 0.5f);
            Casing(f, u0 - 0.2f, u1 + 0.2f, baseY, baseY + height + 0.2f);
            _m.Hexahedron(Cladding, f.Block(u0, u1, baseY, baseY + height, -0.06f, 0.03f));
            for (var i = 1; i < 12; i++)
            {
                var y = baseY + (height * i / 12f);
                _m.Hexahedron(Zinc, f.Block(u0, u1, y - 0.015f, y + 0.015f, 0.02f, 0.05f));
            }

            _m.Hexahedron(AccentRed, f.Block(u0, u1, baseY + height - 0.18f, baseY + height - 0.08f, 0.02f, 0.06f));
            _m.Hexahedron(Concrete, f.Block(u0 - 0.2f, u1 + 0.2f, -0.2f, baseY, 0f, 0.6f));
        }

        private void LoadingDock(Facade f)
        {
            const float deck = 1.2f;
            var u0 = -Length * 0.5f + 1f;
            var u1 = Length * 0.5f - 1f;
            _m.Hexahedron(Concrete, f.Block(u0, u1, -0.2f, PlinthTop + deck, 0f, 2.2f));
            _m.Hexahedron(Asphalt, f.Block(u0, u1, PlinthTop + deck, PlinthTop + deck + 0.04f, 0f, 2.2f));
            var count = Mathf.Max(2, Mathf.FloorToInt((u1 - u0) / 6f));
            var step = (u1 - u0) / count;
            for (var i = 0; i < count; i++)
            {
                RollerDoor(f, u0 + (step * (i + 0.5f)), PlinthTop + deck + 0.05f, 3f, 3.2f);
            }

            _m.Hexahedron(Zinc, f.Block(u0, u1, PlinthTop + deck + 3.7f, PlinthTop + deck + 3.8f, 0f, 2.2f));
        }

        // ---------- Terrein ----------

        private void Site()
        {
            if (_d.Zone == ZoneType.Industrial)
            {
                _m.Box(Asphalt, new Vector3(-_hx, -0.2f, -_hz), new Vector3(_hx, 0.03f, _hz));
                return;
            }

            var low = _d.Zone == ZoneType.Residential && !_d.HighDensity;
            _m.Box(Paving, new Vector3(_x0, -0.2f, -_hz), new Vector3(_x1, 0.03f, _z0));
            if (!low)
            {
                // Plantenbakken op de hoeken van het voorterrein.
                foreach (var x in new[] { _x0 + 0.1f, _x1 - 1.3f })
                {
                    _m.Box(Stone, new Vector3(x, 0.03f, -_hz + 0.1f), new Vector3(x + 1.2f, 0.5f, -_hz + 0.7f));
                    _m.Box(Hedge, new Vector3(x + 0.05f, 0.5f, -_hz + 0.15f), new Vector3(x + 1.15f, 0.95f, -_hz + 0.65f));
                }

                return;
            }

            // Lage haag langs de straat, met een opening voor het pad naar elke voordeur.
            var gaps = new System.Collections.Generic.List<float>();
            var unit = Length / _d.Units;
            for (var i = 0; i < _d.Units; i++)
            {
                gaps.Add((-Length * 0.5f) + (unit * (i + 0.5f)) - (unit * 0.2f));
            }

            var from = _x0 - 0.5f;
            foreach (var gap in gaps)
            {
                _m.Box(Hedge, new Vector3(from, 0f, -_hz + 0.1f), new Vector3(gap - 0.85f, 0.85f, -_hz + 0.7f));
                from = gap + 0.85f;
            }

            _m.Box(Hedge, new Vector3(from, 0f, -_hz + 0.1f), new Vector3(_x1 + 0.5f, 0.85f, -_hz + 0.7f));
        }

        /// <summary>Alles achter de romp: schoorsteen, silo's en tanks.</summary>
        private void Yard()
        {
            if (_d.Stack)
            {
                _m.Cylinder(Concrete, new Vector3(_x1 - 3f, 0f, _z1 + 2f), 1.4f, 30f, 14);
                _m.Cylinder(AccentRed, new Vector3(_x1 - 3f, 26f, _z1 + 2f), 1.42f, 2f, 14);
            }

            for (var i = 0; i < _d.Silos; i++)
            {
                var x = Mathf.Lerp(_x0 + _d.SiloRadius + 1f, _x1 - _d.SiloRadius - 1f, _d.Silos == 1 ? 0.5f : i / (float)(_d.Silos - 1));
                var z = _z1 + _d.SiloRadius + 0.8f;
                _m.Cylinder(Zinc, new Vector3(x, 0f, z), _d.SiloRadius, _d.SiloHeight, 20);
                _m.Cylinder(Stone, new Vector3(x, _d.SiloHeight, z), _d.SiloRadius * 0.4f, 0.8f, 12);
                _m.Box(Zinc, new Vector3(x + _d.SiloRadius - 0.1f, 0.5f, z - 0.1f), new Vector3(x + _d.SiloRadius + 0.1f, _d.SiloHeight, z + 0.1f));
            }
        }
    }
}
