using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.World.Roads
{
    /// <summary>Een weg zoals die het knooppunt ingaat: richting naar buiten (horizontaal) en de breedte van rijbaan en stoep.</summary>
    public readonly struct JunctionArm
    {
        public JunctionArm(float3 direction, float carriageway, float sidewalk)
        {
            Direction = math.normalizesafe(new float3(direction.x, 0f, direction.z), new float3(0f, 0f, 1f));
            Carriageway = carriageway;
            Sidewalk = sidewalk;
        }

        public float3 Direction { get; }

        /// <summary>Halve breedte van de rijbaan (rijstroken en fietspaden), zonder stoep.</summary>
        public float Carriageway { get; }

        /// <summary>Breedte van de stoep naast de rijbaan, 0 zonder stoep.</summary>
        public float Sidewalk { get; }
    }

    /// <summary>Waar een weg ophoudt en het kruispunt begint: het midden van de afsnijlijn en de richting naar buiten.</summary>
    public readonly struct JunctionMouth
    {
        public JunctionMouth(float3 center, float3 direction, float carriageway, float sidewalk)
        {
            Center = center;
            Direction = math.normalizesafe(new float3(direction.x, 0f, direction.z), new float3(0f, 0f, 1f));
            Carriageway = carriageway;
            Sidewalk = sidewalk;
        }

        public float3 Center { get; }

        public float3 Direction { get; }

        public float Carriageway { get; }

        public float Sidewalk { get; }
    }

    /// <summary>
    /// Geometrie van een kruispunt van 3 tot 8 armen: eerst bepaalt <see cref="Trims"/> hoever elke weg van het
    /// knooppunt moet ophouden, dan vult <see cref="Build"/> het gat met wegdek, afgeronde hoeken met stoep en
    /// stoeprand, en een zebrapad op elke arm met stoep.
    /// </summary>
    public static class RoadJunctionBuilder
    {
        public const int MinArms = 3;
        public const int MaxArms = 8;

        /// <summary>Straal van de hoekafronding langs de rand van de rijbaan.</summary>
        public const float CornerRadius = 6f;

        /// <summary>Een weg stopt minstens zo ver van het midden; dat laat ruimte voor het zebrapad.</summary>
        public const float MinTrim = 6f;

        public const float MaxTrim = 40f;
        public const float CrossingDepth = 3f;
        public const float CrossingSetback = 0.8f;
        public const float BarWidth = 0.5f;
        public const float BarGap = 0.5f;
        public const float StraightGap = math.PI - 0.05f;
        private const float CrossingLift = 0.03f;

        /// <summary>De afstand vanaf het midden waar elke arm moet ophouden, in de volgorde van <paramref name="arms"/>.</summary>
        public static float[] Trims(IReadOnlyList<JunctionArm> arms)
        {
            Validate(arms == null ? 0 : arms.Count);
            var trims = new float[arms.Count];
            Array.Fill(trims, MinTrim);
            var order = Order(arms.Count, i => arms[i].Direction);
            for (var n = 0; n < order.Length; n++)
            {
                var i = order[n];
                var j = order[(n + 1) % order.Length];
                var a = arms[i];
                var b = arms[j];
                var gap = Gap(a.Direction, b.Direction);
                if (gap >= StraightGap)
                {
                    continue;
                }

                var from = (Right(a.Direction) * a.Carriageway, a.Direction);
                var to = (-Right(b.Direction) * b.Carriageway, b.Direction);
                var (u, v) = Corner(from.Item1, from.Item2, to.Item1, to.Item2);
                var k = math.min(Radius(a, b) / math.tan(gap * 0.5f), MaxTrim);
                trims[i] = math.min(math.max(trims[i], u + k), MaxTrim);
                trims[j] = math.min(math.max(trims[j], v + k), MaxTrim);
            }

            return trims;
        }

        /// <summary>Het wegdek van het kruispunt rond de gegeven afsnijlijnen (3 tot 8).</summary>
        public static RoadMeshData Build(IReadOnlyList<JunctionMouth> mouths)
        {
            Validate(mouths == null ? 0 : mouths.Count);
            var order = Order(mouths.Count, i => mouths[i].Direction);
            var middle = float3.zero;
            foreach (var mouth in mouths)
            {
                middle += mouth.Center;
            }

            middle /= mouths.Count;

            var data = new RoadMeshData();
            var ring = new List<float3>();
            for (var n = 0; n < order.Length; n++)
            {
                var a = mouths[order[n]];
                var b = mouths[order[(n + 1) % order.Length]];
                var right = Right(a.Direction);
                ring.Add(a.Center - (right * a.Carriageway));
                var path = new List<(float3 Point, float3 Normal)>();
                CornerPath(a, b, path);
                for (var i = 0; i < path.Count - 1; i++)
                {
                    ring.Add(path[i].Point);
                }

                AddSidewalk(data, path, math.min(a.Sidewalk, b.Sidewalk));
                AddCrossing(data, a, middle);
            }

            AddPavement(data, middle, ring);
            return data;
        }

        /// <summary>De punten langs de hoek van de rijbaan, van de rechterrand van <paramref name="a"/> tot de linkerrand van <paramref name="b"/> (inclusief), met per punt de normaal van de weg af.</summary>
        private static void CornerPath(JunctionMouth a, JunctionMouth b, List<(float3 Point, float3 Normal)> path)
        {
            var start = a.Center + (Right(a.Direction) * a.Carriageway);
            var end = b.Center - (Right(b.Direction) * b.Carriageway);
            var normalA = Right(a.Direction);
            var normalB = -Right(b.Direction);
            Append(path, start, normalA);

            var gap = Gap(a.Direction, b.Direction);
            if (gap < StraightGap)
            {
                var (u, v) = Corner(start, a.Direction, end, b.Direction);
                var k = math.min(Radius(a, b) / math.tan(gap * 0.5f), math.min(-u, -v));
                if (k > 0.05f)
                {
                    var radius = k * math.tan(gap * 0.5f);
                    var corner = start + (a.Direction * u);
                    var tangentA = corner + (a.Direction * k);
                    var tangentB = corner + (b.Direction * k);
                    var bisector = math.normalizesafe(a.Direction + b.Direction);
                    var origin = corner + (bisector * (radius / math.sin(gap * 0.5f)));
                    AddArc(path, origin, tangentA, tangentB, radius);
                }
            }

            Append(path, end, normalB);
            for (var i = 0; i < path.Count; i++)
            {
                var f = i / (float)(path.Count - 1);
                var p = path[i].Point;
                path[i] = (new float3(p.x, math.lerp(start.y, end.y, f), p.z), path[i].Normal);
            }
        }

        /// <summary>Voegt een punt toe, behalve als het (vrijwel) samenvalt met het vorige.</summary>
        private static void Append(List<(float3 Point, float3 Normal)> path, float3 point, float3 normal)
        {
            if (path.Count == 0 || math.distance(path[path.Count - 1].Point, point) > 1e-3f)
            {
                path.Add((point, normal));
            }
        }

        private static void AddArc(List<(float3 Point, float3 Normal)> path, float3 origin, float3 from, float3 to, float radius)
        {
            var vFrom = math.normalizesafe(from - origin);
            var vTo = math.normalizesafe(to - origin);
            var angle = math.acos(math.clamp(math.dot(vFrom, vTo), -1f, 1f));
            var steps = math.max(2, (int)math.ceil(angle / 0.2f));
            var sin = math.sin(angle);
            for (var i = 0; i <= steps; i++)
            {
                var f = i / (float)steps;
                var direction = sin < 1e-4f ? vFrom : ((math.sin((1f - f) * angle) * vFrom) + (math.sin(f * angle) * vTo)) / sin;
                Append(path, origin + (direction * radius), -direction);
            }
        }

        /// <summary>Waaier van driehoeken vanuit het midden naar de rand, met de bovenkant omhoog.</summary>
        private static void AddPavement(RoadMeshData data, float3 middle, List<float3> ring)
        {
            var first = data.Vertices.Count;
            data.Vertices.Add(middle);
            foreach (var point in ring)
            {
                data.Vertices.Add(point);
            }

            for (var i = 0; i < data.Vertices.Count - first; i++)
            {
                var p = data.Vertices[first + i];
                data.Normals.Add(new float3(0f, 1f, 0f));
                data.Uvs.Add(new float2(p.x, p.z) / RoadMeshBuilder.TextureMeters);
            }

            var triangles = data.Triangles[(int)RoadSurface.Asphalt];
            for (var i = 0; i < ring.Count; i++)
            {
                var a = first + 1 + i;
                var b = first + 1 + ((i + 1) % ring.Count);

                // Alleen vlakken die naar boven kijken: bij een hoek van meer dan 180° wijst de waaier de andere kant op.
                var side = math.cross(data.Vertices[a] - middle, data.Vertices[b] - middle).y;
                if (side > 0f)
                {
                    triangles.AddRange(new[] { first, a, b });
                }
                else if (side < 0f)
                {
                    triangles.AddRange(new[] { first, b, a });
                }
            }
        }

        /// <summary>Stoep met stoeprand langs een hoek; zonder stoep alleen een rok tegen kieren.</summary>
        private static void AddSidewalk(RoadMeshData data, List<(float3 Point, float3 Normal)> path, float width)
        {
            if (width > 0f)
            {
                Ribbon(data, path, width);
                Wall(data, path, 0f, 0f, RoadMeshBuilder.CurbHeight, -1f);
                Wall(data, path, width, -RoadMeshBuilder.SkirtDepth, RoadMeshBuilder.CurbHeight, 1f);
            }
            else
            {
                Wall(data, path, 0f, -RoadMeshBuilder.SkirtDepth, 0f, 1f);
            }
        }

        /// <summary>Stoepdek van de rand van de rijbaan tot <paramref name="width"/> naar buiten, een stoeprand hoog.</summary>
        private static void Ribbon(RoadMeshData data, List<(float3 Point, float3 Normal)> path, float width)
        {
            var up = new float3(0f, 1f, 0f);
            var first = data.Vertices.Count;
            var distance = 0f;
            for (var i = 0; i < path.Count; i++)
            {
                if (i > 0)
                {
                    distance += math.distance(path[i].Point, path[i - 1].Point);
                }

                var lift = new float3(0f, RoadMeshBuilder.CurbHeight, 0f);
                data.Vertices.Add(path[i].Point + lift);
                data.Vertices.Add(path[i].Point + (path[i].Normal * width) + lift);
                data.Normals.Add(up);
                data.Normals.Add(up);
                var v = distance / RoadMeshBuilder.TextureMeters;
                data.Uvs.Add(new float2(0f, v));
                data.Uvs.Add(new float2(1f, v));
            }

            RoadMeshBuilder.Stitch(data, RoadSurface.Sidewalk, first, path.Count, up);
        }

        /// <summary>Verticale wand langs de hoek op <paramref name="offset"/> van de rand; <paramref name="sign"/> -1 kijkt naar de weg, 1 ervan af.</summary>
        private static void Wall(RoadMeshData data, List<(float3 Point, float3 Normal)> path, float offset, float bottom, float top, float sign)
        {
            var first = data.Vertices.Count;
            var distance = 0f;
            for (var i = 0; i < path.Count; i++)
            {
                if (i > 0)
                {
                    distance += math.distance(path[i].Point, path[i - 1].Point);
                }

                var edge = path[i].Point + (path[i].Normal * offset);
                var facing = path[i].Normal * sign;
                data.Vertices.Add(edge + new float3(0f, top, 0f));
                data.Vertices.Add(edge + new float3(0f, bottom, 0f));
                data.Normals.Add(facing);
                data.Normals.Add(facing);
                var v = distance / RoadMeshBuilder.TextureMeters;
                data.Uvs.Add(new float2(0f, v));
                data.Uvs.Add(new float2(1f, v));
            }

            RoadMeshBuilder.Stitch(data, RoadSurface.Curb, first, path.Count, path[0].Normal * sign);
        }

        /// <summary>Zebrapad op de arm, net voorbij de afsnijlijn: witte banen in de rijrichting naast elkaar.</summary>
        private static void AddCrossing(RoadMeshData data, JunctionMouth mouth, float3 middle)
        {
            var reach = math.distance(mouth.Center.xz, middle.xz);
            if (mouth.Sidewalk <= 0f || reach < CrossingDepth + CrossingSetback + 1f)
            {
                return;
            }

            var bars = (int)math.floor((((2f * mouth.Carriageway) - BarWidth) / (BarWidth + BarGap)) + 1e-3f) + 1;
            var span = ((bars - 1) * (BarWidth + BarGap)) + BarWidth;
            for (var bar = 0; bar < bars; bar++)
            {
                var left = -(span * 0.5f) + (bar * (BarWidth + BarGap));
                var first = data.Vertices.Count;
                AddCrossingCorner(data, mouth, middle, reach, left, -CrossingSetback, 0f, 0f);
                AddCrossingCorner(data, mouth, middle, reach, left + BarWidth, -CrossingSetback, 1f, 0f);
                AddCrossingCorner(data, mouth, middle, reach, left, -CrossingSetback - CrossingDepth, 0f, CrossingDepth / RoadMeshBuilder.TextureMeters);
                AddCrossingCorner(data, mouth, middle, reach, left + BarWidth, -CrossingSetback - CrossingDepth, 1f, CrossingDepth / RoadMeshBuilder.TextureMeters);
                RoadMeshBuilder.Stitch(data, RoadSurface.MarkingSolid, first, 2, new float3(0f, 1f, 0f));
            }
        }

        private static void AddCrossingCorner(RoadMeshData data, JunctionMouth mouth, float3 middle, float reach, float across, float along, float u, float v)
        {
            var point = mouth.Center + (Right(mouth.Direction) * across) + (mouth.Direction * along);
            point.y = math.lerp(mouth.Center.y, middle.y, -along / reach) + CrossingLift;
            data.Vertices.Add(point);
            data.Normals.Add(new float3(0f, 1f, 0f));
            data.Uvs.Add(new float2(u, v));
        }

        private static float Radius(JunctionArm a, JunctionArm b) => math.max(CornerRadius, math.min(a.Sidewalk, b.Sidewalk) + 1f);

        private static float Radius(JunctionMouth a, JunctionMouth b) => math.max(CornerRadius, math.min(a.Sidewalk, b.Sidewalk) + 1f);

        /// <summary>De armen van links naar rechts gezien van boven, met de klok mee, oplopend in koers.</summary>
        private static int[] Order(int count, Func<int, float3> direction)
        {
            var order = new int[count];
            var yaw = new float[count];
            for (var i = 0; i < count; i++)
            {
                order[i] = i;
                var d = direction(i);
                yaw[i] = math.atan2(d.x, d.z);
            }

            Array.Sort(order, (x, y) => yaw[x].CompareTo(yaw[y]));
            return order;
        }

        /// <summary>De hoek met de klok mee van <paramref name="a"/> naar <paramref name="b"/>, in (0, 2π].</summary>
        private static float Gap(float3 a, float3 b)
        {
            var gap = math.atan2(b.x, b.z) - math.atan2(a.x, a.z);
            while (gap <= 0f)
            {
                gap += 2f * math.PI;
            }

            return gap;
        }

        private static float3 Right(float3 direction) => math.cross(math.up(), direction);

        /// <summary>
        /// Snijpunt van twee randlijnen, elk gegeven door een punt (<paramref name="pointA"/>, relatief aan een gemeenschappelijke oorsprong)
        /// en een richting: afstanden <c>u</c> langs de eerste en <c>v</c> langs de tweede vanaf het punt.
        /// </summary>
        private static (float U, float V) Corner(float3 pointA, float3 directionA, float3 pointB, float3 directionB, float3 shiftB = default)
        {
            var w = (pointB + shiftB).xz - pointA.xz;
            var denominator = Cross(directionA.xz, directionB.xz);
            if (math.abs(denominator) < 1e-4f)
            {
                return (0f, 0f);
            }

            return (Cross(w, directionB.xz) / denominator, Cross(w, directionA.xz) / denominator);
        }

        private static float Cross(float2 a, float2 b) => (a.x * b.y) - (a.y * b.x);

        private static void Validate(int count)
        {
            if (count < MinArms || count > MaxArms)
            {
                throw new ArgumentException($"Een kruispunt heeft {MinArms} tot {MaxArms} armen, niet {count}.");
            }
        }
    }
}
