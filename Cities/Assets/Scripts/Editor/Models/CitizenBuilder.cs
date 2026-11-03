using System.Collections.Generic;
using Cities.Simulation.Animation;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Cities.Editor.Models
{
    /// <summary>
    /// Bouwt het model van een voetganger met gebakken looppas (E21-05). De burger bestaat uit blokken (romp, hoofd, armen, benen, schoenen);
    /// armen en benen zwaaien om heup en schouder volgens <see cref="WalkCycle"/>. Die zwaai wordt voor één cyclus in een textuur gebakken
    /// (<see cref="AgentModels.BakeClip"/>), zodat de shader per hoekpunt twee frames mengt en er geen skinning of CPU-werk per frame nodig is.
    /// De voorkant ligt aan +Z; de oorsprong staat tussen de voeten op de grond; de lengte is 1,75 m (schaal 1 = volwassene).
    /// </summary>
    internal static class CitizenBuilder
    {
        private const int Skin = 0;
        private const int Shirt = 1;
        private const int Trousers = 2;
        private const int Shoes = 3;
        private const int Hair = 4;
        private const int Count = 5;
        private const float HipHeight = 0.9f;
        private const float ShoulderHeight = 1.36f;
        private const int FramesPerCycle = 24;

        private enum Limb
        {
            Body,
            LegRight,
            LegLeft,
            ArmRight,
            ArmLeft,
        }

        [MenuItem("Cities/Modellen/Burgers bouwen")]
        public static void Build()
        {
            var parts = new List<Part>();
            var m = new ModelMesh(Count);
            Add(m, parts, Limb.Body, () =>
            {
                m.Box(Shirt, new Vector3(-0.2f, 0.86f, -0.11f), new Vector3(0.2f, 1.4f, 0.11f));
                m.Box(Skin, new Vector3(-0.05f, 1.4f, -0.05f), new Vector3(0.05f, 1.47f, 0.05f));
                m.Box(Skin, new Vector3(-0.1f, 1.47f, -0.11f), new Vector3(0.1f, 1.72f, 0.11f));
                m.Box(Hair, new Vector3(-0.105f, 1.62f, -0.115f), new Vector3(0.105f, 1.75f, 0.02f));
            });
            foreach (var side in new[] { Limb.LegRight, Limb.LegLeft })
            {
                var x = side == Limb.LegRight ? 0.1f : -0.1f;
                Add(m, parts, side, () =>
                {
                    m.Box(Trousers, new Vector3(x - 0.08f, 0.07f, -0.075f), new Vector3(x + 0.08f, HipHeight, 0.075f));
                    m.Box(Shoes, new Vector3(x - 0.08f, 0f, -0.08f), new Vector3(x + 0.08f, 0.07f, 0.17f));
                });
            }

            foreach (var side in new[] { Limb.ArmRight, Limb.ArmLeft })
            {
                var x = side == Limb.ArmRight ? 0.26f : -0.26f;
                Add(m, parts, side, () =>
                {
                    m.Box(Shirt, new Vector3(x - 0.05f, 1.1f, -0.05f), new Vector3(x + 0.05f, ShoulderHeight + 0.02f, 0.05f));
                    m.Box(Skin, new Vector3(x - 0.045f, 0.84f, -0.045f), new Vector3(x + 0.045f, 1.1f, 0.045f));
                });
            }

            System.IO.Directory.CreateDirectory(AgentModels.Folder);
            var mesh = ModelMesh.Save(m.Build("CitizenMesh"), $"{AgentModels.Folder}/CitizenMesh.asset");
            var vertexCount = mesh.vertexCount;
            var vertices = mesh.vertices;
            var limbOf = new Limb[vertexCount];
            foreach (var part in parts)
            {
                for (var i = part.First; i < part.First + part.Count; i++)
                {
                    limbOf[i] = part.Kind;
                }
            }

            var clip = AgentModels.BakeClip("CitizenWalkClip", vertexCount, FramesPerCycle, (vertex, phase) => Offset(limbOf[vertex], vertices[vertex], phase));
            var materials = new[]
            {
                AgentModels.Material("CitizenSkin", new Color(0.8f, 0.62f, 0.5f), 0.25f, clip, FramesPerCycle, vertexCount),
                AgentModels.Material("CitizenShirt", new Color(0.2f, 0.36f, 0.58f), 0.2f, clip, FramesPerCycle, vertexCount),
                AgentModels.Material("CitizenTrousers", new Color(0.15f, 0.16f, 0.2f), 0.15f, clip, FramesPerCycle, vertexCount),
                AgentModels.Material("CitizenShoes", new Color(0.08f, 0.07f, 0.07f), 0.3f, clip, FramesPerCycle, vertexCount),
                AgentModels.Material("CitizenHair", new Color(0.22f, 0.15f, 0.09f), 0.2f, clip, FramesPerCycle, vertexCount),
            };
            AgentModels.RendererPrefab("Citizen", mesh, materials, AgentAnimationKind.Walk, 1f);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>Verplaatsing van een hoekpunt t.o.v. de rustpose op een fase van de cyclus.</summary>
        private static float3 Offset(Limb limb, float3 rest, float phase)
        {
            var bob = new float3(0f, WalkCycle.BodyBob(phase), 0f);
            switch (limb)
            {
                case Limb.LegRight:
                    return LimbPose.RotateAroundX(rest, new float2(HipHeight, 0f), WalkCycle.LegSwing(phase)) - rest + bob;
                case Limb.LegLeft:
                    return LimbPose.RotateAroundX(rest, new float2(HipHeight, 0f), WalkCycle.LegSwing(phase + 0.5f)) - rest + bob;
                case Limb.ArmRight:
                    return LimbPose.RotateAroundX(rest, new float2(ShoulderHeight, 0f), WalkCycle.ArmSwing(phase)) - rest + bob;
                case Limb.ArmLeft:
                    return LimbPose.RotateAroundX(rest, new float2(ShoulderHeight, 0f), WalkCycle.ArmSwing(phase + 0.5f)) - rest + bob;
                default:
                    return bob;
            }
        }

        private static void Add(ModelMesh m, List<Part> parts, Limb limb, System.Action build)
        {
            var first = m.VertexCount;
            build();
            parts.Add(new Part(limb, first, m.VertexCount - first));
        }

        private readonly struct Part
        {
            public Part(Limb limb, int first, int count)
            {
                Kind = limb;
                First = first;
                Count = count;
            }

            public Limb Kind { get; }

            public int First { get; }

            public int Count { get; }
        }
    }
}
