using System.Collections.Generic;
using System.Linq;
using Cities.World.Buildings;
using Cities.World.Grid;
using static Cities.Editor.Models.BuildingMaterials.Slots;

namespace Cities.Editor.Models
{
    /// <summary>
    /// De gegroeide gebouwvarianten (E05-08): per zonetype en dichtheid drie, plus het vrijstaande huis dat al bestond. De
    /// aantallen woningen en arbeidsplaatsen volgen uit de vloeroppervlakte; productie en winkelcapaciteit uit het bedrijfstype.
    /// </summary>
    internal static class BuildingCatalog
    {
        public static IReadOnlyList<BuildingDesign> All { get; } = new List<BuildingDesign>
        {
            // Wonen, lage dichtheid.
            Home("residential-low-semi-detached", "Twee-onder-een-kap", "Twee woningen onder één zadeldak, elk met een eigen voordeur en voortuin.", 3, 2, ZoneDensity.Low, 2, 45,
                new BuildingDesign { Floors = 2, Wall = BrickYellow, Roof = RoofKind.Gable, Ground = GroundKind.Houses, Units = 2, Chimneys = true }),
            Home("residential-low-terraced", "Rijtjeswoningen", "Een rij van vier gelijke woningen in baksteen onder één zadeldak.", 4, 2, ZoneDensity.Low, 4, 60,
                new BuildingDesign { Floors = 2, Wall = BrickRed, Roof = RoofKind.Gable, Ground = GroundKind.Houses, Units = 4, Chimneys = true }),

            // Wonen, hoge dichtheid.
            Home("residential-high-walk-up", "Portiekflat", "Flat van vier lagen met portieken en een balkon bij elke tweede travee.", 3, 3, ZoneDensity.High, 16, 180,
                new BuildingDesign { Floors = 4, Wall = BrickRed, Ground = GroundKind.Entrance, Balconies = true }),
            Home("residential-high-gallery-flat", "Appartementengebouw", "Gepleisterd appartementengebouw van vijf lagen met balkons aan de straatkant.", 4, 2, ZoneDensity.High, 40, 240,
                new BuildingDesign { Floors = 5, Wall = Render, Ground = GroundKind.Entrance, Balconies = true }),
            Home("residential-high-tower", "Woontoren", "Woontoren van tien lagen met betonpanelen en balkons.", 3, 3, ZoneDensity.High, 80, 360,
                new BuildingDesign { Floors = 10, Wall = Concrete, Ground = GroundKind.Entrance, Balconies = true }),

            // Winkels.
            Shop("commercial-low-supermarket", "Buurtsupermarkt", "Supermarkt met etalage over de hele gevel en een groene luifel.", 3, 2, ZoneDensity.Low, 14, 9_000, 90,
                new BuildingDesign { Floors = 1, GroundHeight = 4.4f, Wall = BrickYellow, Accent = AccentGreen }),
            Shop("commercial-low-shop-house", "Winkelpand", "Winkel op de begane grond met een kantoor- of opslagverdieping erboven.", 2, 2, ZoneDensity.Low, 10, 6_000, 100,
                new BuildingDesign { Floors = 2, GroundHeight = 4.2f, FloorHeight = 3.2f, Wall = BrickRed, Accent = AccentRed }),
            Shop("commercial-low-hardware-store", "Bouwmarkt", "Brede, lage winkelhal met golfplaat en een blauw reclamebord.", 4, 3, ZoneDensity.Low, 30, 22_000, 150,
                new BuildingDesign { Floors = 1, GroundHeight = 6f, Wall = Cladding, Accent = AccentBlue, Windows = WindowKind.HighStrip }),
            Shop("commercial-high-shopping-centre", "Winkelcentrum", "Winkelcentrum van drie lagen met glasstroken en een lange etalage.", 4, 3, ZoneDensity.High, 140, 160_000, 400,
                new BuildingDesign { Floors = 3, GroundHeight = 4.5f, FloorHeight = 4f, Wall = Concrete, Accent = AccentBlue, Windows = WindowKind.Ribbon }),
            Shop("commercial-high-department-store", "Warenhuis", "Warenhuis van vijf lagen in baksteen met een rode luifel.", 3, 3, ZoneDensity.High, 160, 140_000, 450,
                new BuildingDesign { Floors = 5, GroundHeight = 5f, FloorHeight = 3.8f, Wall = BrickRed, Accent = AccentRed }),
            Shop("commercial-high-arcade", "Winkelgalerij", "Gepleisterde galerij van vier lagen met winkels en glasstroken.", 4, 2, ZoneDensity.High, 90, 80_000, 320,
                new BuildingDesign { Floors = 4, GroundHeight = 4.5f, FloorHeight = 3.6f, Wall = Render, Accent = AccentGreen, Windows = WindowKind.Ribbon }),

            // Kantoren.
            Office("office-low-villa", "Kantoorvilla", "Gepleisterde kantoorvilla van twee lagen.", 2, 2, ZoneDensity.Low, 22, 40, 40, 120,
                new BuildingDesign { Floors = 2, Wall = Render, Ground = GroundKind.Entrance }),
            Office("office-low-brick", "Kantoorgebouw", "Kantoorgebouw van drie lagen in gele baksteen.", 3, 2, ZoneDensity.Low, 48, 45, 40, 200,
                new BuildingDesign { Floors = 3, Wall = BrickYellow, Ground = GroundKind.Entrance }),
            Office("office-low-business-centre", "Bedrijfsverzamelgebouw", "Bedrijfsverzamelgebouw van twee lagen met golfplaat en glasstroken.", 4, 2, ZoneDensity.Low, 44, 40, 20, 180,
                new BuildingDesign { Floors = 2, Wall = Cladding, Ground = GroundKind.Entrance, Windows = WindowKind.Ribbon }),
            Office("office-high-tower", "Kantoortoren", "Kantoortoren van twaalf lagen met glasgevels en een hoge lobby.", 3, 3, ZoneDensity.High, 420, 30, 60, 700,
                new BuildingDesign { Floors = 12, GroundHeight = 5f, FloorHeight = 3.6f, Wall = Concrete, Ground = GroundKind.Lobby, Windows = WindowKind.Ribbon }),
            Office("office-high-block", "Kantoorblok", "Kantoorblok van zeven lagen met doorlopende glasstroken.", 4, 3, ZoneDensity.High, 380, 35, 50, 500,
                new BuildingDesign { Floors = 7, GroundHeight = 5f, FloorHeight = 3.6f, Wall = Concrete, Ground = GroundKind.Lobby, Windows = WindowKind.Ribbon }),
            Office("office-high-slab", "Kantoorgebouw met ramen", "Gepleisterd kantoorgebouw van negen lagen met rijen ramen.", 3, 3, ZoneDensity.High, 320, 30, 55, 600,
                new BuildingDesign { Floors = 9, GroundHeight = 4.5f, FloorHeight = 3.5f, Wall = Render, Ground = GroundKind.Entrance }),

            // Industrie.
            Industry("industrial-low-workshop", "Werkplaats", "Werkplaats met twee overheaddeuren en lichtstroken.", 3, 2, ZoneDensity.Low, 16, 40, 120,
                new BuildingDesign { Floors = 1, GroundHeight = 6.5f, Wall = Cladding, Ground = GroundKind.Hall, Windows = WindowKind.HighStrip }),
            Industry("industrial-low-warehouse", "Magazijn", "Magazijn met laadperron aan de achterkant.", 4, 3, ZoneDensity.Low, 30, 80, 200,
                new BuildingDesign { Floors = 1, GroundHeight = 9f, Wall = Cladding, Ground = GroundKind.Docks, Windows = WindowKind.HighStrip }),
            Industry("industrial-low-production-hall", "Productiehal", "Productiehal in gele baksteen met een zaagtanddak.", 4, 3, ZoneDensity.Low, 45, 110, 260,
                new BuildingDesign { Floors = 1, GroundHeight = 7f, Wall = BrickYellow, Roof = RoofKind.Sawtooth, Ground = GroundKind.Hall, Windows = WindowKind.HighStrip }),
            Industry("industrial-high-factory", "Fabriek", "Fabriek van twee lagen met schoorsteen en drie silo's.", 4, 4, ZoneDensity.High, 160, 420, 500,
                new BuildingDesign { Floors = 2, GroundHeight = 7f, FloorHeight = 6f, Wall = BrickRed, Ground = GroundKind.Hall, Windows = WindowKind.HighStrip, Stack = true, Rear = 9f, Silos = 3 }),
            Industry("industrial-high-distribution-centre", "Distributiecentrum", "Hoog distributiecentrum met een laadperron met veel deuren.", 4, 4, ZoneDensity.High, 110, 320, 400,
                new BuildingDesign { Floors = 1, GroundHeight = 12f, Wall = Cladding, Ground = GroundKind.Docks, Windows = WindowKind.HighStrip }),
            Industry("industrial-high-chemical-plant", "Chemische installatie", "Installatie met opslagtanks en een hoge schoorsteen.", 4, 3, ZoneDensity.High, 130, 520, 480,
                new BuildingDesign { Floors = 1, GroundHeight = 8f, Wall = Cladding, Ground = GroundKind.Hall, Windows = WindowKind.HighStrip, Stack = true, Rear = 8f, Silos = 3, SiloRadius = 3.2f, SiloHeight = 10f }),
        };

        private static BuildingDesign Home(string id, string title, string text, int width, int depth, ZoneDensity density, int households, int days, BuildingDesign d) =>
            Fill(d, id, title, text, days, new BuildingSpec { Zone = ZoneType.Residential, Density = density, FootprintWidth = width, FootprintDepth = depth, Households = households });

        private static BuildingDesign Shop(string id, string title, string text, int width, int depth, ZoneDensity density, int jobs, long capacity, int days, BuildingDesign d)
        {
            d.Ground = GroundKind.Shop;
            return Fill(d, id, title, text, days, new BuildingSpec
            {
                Zone = ZoneType.Commercial,
                Density = density,
                FootprintWidth = width,
                FootprintDepth = depth,
                Workplaces = jobs,
                MediumEducationPercent = 25,
                HighEducationPercent = 5,
                ShopCapacity = capacity,
            });
        }

        private static BuildingDesign Office(string id, string title, string text, int width, int depth, ZoneDensity density, int jobs, int medium, int high, int days, BuildingDesign d) =>
            Fill(d, id, title, text, days, new BuildingSpec
            {
                Zone = ZoneType.Office,
                Density = density,
                FootprintWidth = width,
                FootprintDepth = depth,
                Workplaces = jobs,
                MediumEducationPercent = medium,
                HighEducationPercent = high,
            });

        private static BuildingDesign Industry(string id, string title, string text, int width, int depth, ZoneDensity density, int jobs, int production, int days, BuildingDesign d) =>
            Fill(d, id, title, text, days, new BuildingSpec
            {
                Zone = ZoneType.Industrial,
                Density = density,
                FootprintWidth = width,
                FootprintDepth = depth,
                Workplaces = jobs,
                MediumEducationPercent = 15,
                HighEducationPercent = 5,
                Production = production,
            });

        private static BuildingDesign Fill(BuildingDesign d, string id, string title, string text, int days, BuildingSpec spec)
        {
            spec.Level = 1;
            spec.ConstructionDays = days;
            d.Id = "building." + id;
            d.Title = title;
            d.Description = text;
            d.Spec = spec;
            d.Name = string.Concat(id.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));
            return d;
        }
    }
}
