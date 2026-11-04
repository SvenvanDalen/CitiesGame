using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Cities.Core.Configuration;
using Cities.World.Grid;
using UnityEngine;

namespace Cities.World.Buildings
{
    /// <summary>
    /// Beschrijving van een eigen gebouw (E21-08), uit een tekstbestand met regels <c>sleutel = waarde</c> (<c>#</c> begint commentaar):
    /// <c>id</c>, <c>name</c>, <c>model</c> (bestandsnaam, standaard <c>model.obj</c>), <c>footprint</c> (bijvoorbeeld <c>3x2</c>),
    /// <c>zone</c>, <c>density</c>, <c>level</c>, <c>households</c>, <c>workplaces</c>, <c>cost</c>, <c>upkeep</c>, <c>days</c>,
    /// <c>scale</c> (leeg of 0 past het model in de footprint) en <c>yaw</c> (0, 90, 180 of 270 graden om de voorkant naar -Z te draaien).
    /// </summary>
    public sealed class CustomBuildingManifest
    {
        public const string FileName = "building.txt";

        public string Id { get; private set; } = string.Empty;

        public string Name { get; private set; } = string.Empty;

        public string ModelFile { get; private set; } = "model.obj";

        public BuildingSpec Spec { get; private set; }

        /// <summary>Schaal van het model; 0 laat het model passen op de footprint.</summary>
        public float Scale { get; private set; }

        public int YawDegrees { get; private set; }

        /// <summary>Leest en controleert het bestand; problemen komen in <paramref name="errors"/> en geven <c>null</c>.</summary>
        public static CustomBuildingManifest Parse(string text, ICollection<string> errors)
        {
            var manifest = new CustomBuildingManifest();
            var spec = new BuildingSpec { FootprintWidth = 1, FootprintDepth = 1, Level = 1, ConstructionDays = 30 };
            var failed = false;
            var lineNumber = 0;
            foreach (var raw in (text ?? string.Empty).Split('\n'))
            {
                lineNumber++;
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                var separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    errors.Add($"Regel {lineNumber}: verwacht \"sleutel = waarde\".");
                    failed = true;
                    continue;
                }

                var key = line.Substring(0, separator).Trim().ToLowerInvariant();
                var value = line.Substring(separator + 1).Trim();
                if (!manifest.Apply(key, value, ref spec, out var problem))
                {
                    errors.Add($"Regel {lineNumber}: {problem}");
                    failed = true;
                }
            }

            manifest.Spec = spec;
            if (!DefinitionId.IsValid(manifest.Id) || DefinitionId.KindOf(manifest.Id) != "building")
            {
                errors.Add("Het id ontbreekt of is ongeldig; het begint met \"building.\" (bijvoorbeeld building.mijn-huis).");
                failed = true;
            }

            if (string.IsNullOrWhiteSpace(manifest.Name))
            {
                errors.Add("De naam ontbreekt.");
                failed = true;
            }

            var specErrors = new List<string>();
            spec.Validate(specErrors);
            foreach (var error in specErrors)
            {
                errors.Add(error);
                failed = true;
            }

            return failed ? null : manifest;
        }

        private bool Apply(string key, string value, ref BuildingSpec spec, out string problem)
        {
            problem = null;
            switch (key)
            {
                case "id":
                    Id = value;
                    return true;
                case "name":
                    Name = value;
                    return true;
                case "model":
                    if (value.IndexOfAny(new[] { '/', '\\' }) >= 0 || value.Contains(".."))
                    {
                        problem = "het modelbestand moet in dezelfde map staan.";
                        return false;
                    }

                    ModelFile = value;
                    return true;
                case "footprint":
                    var sizes = value.ToLowerInvariant().Split('x');
                    if (sizes.Length != 2 || !TryInt(sizes[0], out var width) || !TryInt(sizes[1], out var depth))
                    {
                        problem = "footprint is breedte x diepte, bijvoorbeeld 3x2.";
                        return false;
                    }

                    spec.FootprintWidth = width;
                    spec.FootprintDepth = depth;
                    return true;
                case "zone":
                    return TryEnum(value, out spec.Zone, out problem);
                case "density":
                    return TryEnum(value, out spec.Density, out problem);
                case "level":
                    return TryNumber(value, out spec.Level, out problem);
                case "households":
                    return TryNumber(value, out spec.Households, out problem);
                case "workplaces":
                    return TryNumber(value, out spec.Workplaces, out problem);
                case "days":
                    return TryNumber(value, out spec.ConstructionDays, out problem);
                case "cost":
                    return TryLong(value, out spec.ConstructionCost, out problem);
                case "upkeep":
                    return TryLong(value, out spec.UpkeepPerMonth, out problem);
                case "scale":
                    if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale) || scale < 0f || float.IsInfinity(scale))
                    {
                        problem = "schaal is een getal van 0 of hoger.";
                        return false;
                    }

                    Scale = scale;
                    return true;
                case "yaw":
                    if (!TryInt(value, out var yaw) || yaw % 90 != 0)
                    {
                        problem = "yaw is 0, 90, 180 of 270.";
                        return false;
                    }

                    YawDegrees = ((yaw % 360) + 360) % 360;
                    return true;
                default:
                    problem = $"onbekende sleutel \"{key}\".";
                    return false;
            }
        }

        private static bool TryInt(string text, out int value) =>
            int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        private static bool TryNumber(string text, out int value, out string problem)
        {
            problem = TryInt(text, out value) ? null : $"\"{text}\" is geen geheel getal.";
            return problem == null;
        }

        private static bool TryLong(string text, out long value, out string problem)
        {
            problem = long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? null : $"\"{text}\" is geen geheel getal.";
            return problem == null;
        }

        private static bool TryEnum<T>(string text, out T value, out string problem) where T : struct
        {
            if (Enum.TryParse(text, true, out value) && Enum.IsDefined(typeof(T), value))
            {
                problem = null;
                return true;
            }

            problem = $"\"{text}\" is geen geldige waarde voor {typeof(T).Name}.";
            return false;
        }
    }

    /// <summary>Een geladen eigen gebouw: de instellingen en het model, gedraaid, geschaald en op de grond gezet volgens het gebouw-sjabloon.</summary>
    public sealed class CustomBuilding
    {
        public CustomBuilding(CustomBuildingManifest manifest, ObjModel model)
        {
            Manifest = manifest;
            Model = model;
        }

        public CustomBuildingManifest Manifest { get; }

        public ObjModel Model { get; }
    }

    /// <summary>Past een model aan het gebouw-sjabloon aan (E21-02) en laadt eigen gebouwen uit een map.</summary>
    public static class CustomBuildingLibrary
    {
        /// <summary>Het deel van de footprint dat een automatisch geschaald model vult.</summary>
        public const float AutoFit = 0.9f;

        /// <summary>Draait, schaalt en verplaatst het model zodat het midden op (0, 0) staat, de onderkant op de grond en het past in de footprint.</summary>
        public static ObjModel Fit(ObjModel source, CustomBuildingManifest manifest, float cellSize)
        {
            var angle = manifest.YawDegrees * Mathf.Deg2Rad;
            var cos = (float)Math.Cos(angle);
            var sin = (float)Math.Sin(angle);
            Vector3 Rotate(Vector3 v) => new Vector3((cos * v.x) + (sin * v.z), v.y, (cos * v.z) - (sin * v.x));
            var rotated = new List<Vector3>(source.Vertices.Count);
            foreach (var vertex in source.Vertices)
            {
                rotated.Add(Rotate(vertex));
            }

            var min = rotated[0];
            var max = rotated[0];
            foreach (var v in rotated)
            {
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
            }

            var sizeX = Math.Max(max.x - min.x, 1e-4f);
            var sizeZ = Math.Max(max.z - min.z, 1e-4f);
            var scale = manifest.Scale > 0f
                ? manifest.Scale
                : Math.Min((manifest.Spec.FootprintWidth * cellSize * AutoFit) / sizeX, (manifest.Spec.FootprintDepth * cellSize * AutoFit) / sizeZ);
            var center = new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f);

            var result = new ObjModel();
            foreach (var v in rotated)
            {
                result.Vertices.Add((v - center) * scale);
            }

            foreach (var normal in source.Normals)
            {
                result.Normals.Add(Rotate(normal));
            }

            result.Uvs.AddRange(source.Uvs);
            result.Materials.AddRange(source.Materials);
            foreach (var submesh in source.Submeshes)
            {
                result.Submeshes.Add(new List<int>(submesh));
            }

            return result;
        }

        /// <summary>Controleert het gepaste model met <see cref="BuildingTemplate"/>; problemen komen in <paramref name="errors"/>.</summary>
        public static void Validate(CustomBuilding building, float cellSize, ICollection<string> errors)
        {
            var (min, max) = building.Model.Bounds();
            BuildingTemplate.Validate(building.Manifest.Spec, (min.x, min.y, min.z), (max.x, max.y, max.z), building.Model.Submeshes.Count, cellSize, errors);
        }

        /// <summary>
        /// Leest elke submap van <paramref name="root"/> met een <see cref="CustomBuildingManifest.FileName"/>. Een map met problemen wordt
        /// overgeslagen en komt met de melding in <paramref name="errors"/>; de rest laadt gewoon.
        /// </summary>
        public static List<CustomBuilding> LoadFolder(string root, float cellSize, ICollection<string> errors)
        {
            var buildings = new List<CustomBuilding>();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return buildings;
            }

            var directories = Directory.GetDirectories(root);
            Array.Sort(directories, StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var directory in directories)
            {
                var manifestPath = Path.Combine(directory, CustomBuildingManifest.FileName);
                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                var folderName = Path.GetFileName(directory);
                var problems = new List<string>();
                var building = TryLoad(directory, manifestPath, cellSize, problems);
                if (building != null && !ids.Add(building.Manifest.Id))
                {
                    problems.Add($"Het id {building.Manifest.Id} is al in gebruik.");
                    building = null;
                }

                foreach (var problem in problems)
                {
                    errors.Add($"{folderName}: {problem}");
                }

                if (building != null && problems.Count == 0)
                {
                    buildings.Add(building);
                }
            }

            return buildings;
        }

        private static CustomBuilding TryLoad(string directory, string manifestPath, float cellSize, List<string> problems)
        {
            var manifest = CustomBuildingManifest.Parse(File.ReadAllText(manifestPath), problems);
            if (manifest == null)
            {
                return null;
            }

            var modelPath = Path.Combine(directory, manifest.ModelFile);
            if (!File.Exists(modelPath))
            {
                problems.Add($"Het modelbestand {manifest.ModelFile} ontbreekt.");
                return null;
            }

            try
            {
                var building = new CustomBuilding(manifest, Fit(ObjParser.Parse(File.ReadAllText(modelPath)), manifest, cellSize));
                Validate(building, cellSize, problems);
                return building;
            }
            catch (FormatException exception)
            {
                problems.Add(exception.Message);
                return null;
            }
        }
    }
}
