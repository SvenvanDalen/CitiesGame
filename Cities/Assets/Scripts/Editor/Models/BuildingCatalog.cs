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
            Home("residential-low-semi-detached", "Twee-onder-een-kap", "Twee woningen onder één zadeldak, elk met een eigen voordeur en voortuin.", 2,
                new BuildingDesign { Width = 3, Depth = 2, Density = ZoneDensity.Low, Days = 45, Floors = 2, Wall = BrickYellow, Roof = RoofKind.Gable, Ground = GroundKind.Houses, Units = 2, Chimneys = true }),
            Home("residential-low-terraced", "Rijtjeswoningen", "Een rij van vier gelijke woningen in baksteen onder één zadeldak.", 4,
                new BuildingDesign { Width = 4, Depth = 2, Density = ZoneDensity.Low, Days = 60, Floors = 2, Wall = BrickRed, Roof = RoofKind.Gable, Ground = GroundKind.Houses, Units = 4, Chimneys = true }),

            // Wonen, hoge dichtheid.
            Home("residential-high-walk-up", "Portiekflat", "Flat van vier lagen met portieken en een balkon bij elke tweede travee.", 16,
                new BuildingDesign { Width = 3, Depth = 3, Density = ZoneDensity.High, Days = 180, Floors = 4, Wall = BrickRed, Ground = GroundKind.Entrance, Balconies = true }),
            Home("residential-high-gallery-flat", "Appartementengebouw", "Gepleisterd appartementengebouw van vijf lagen met balkons aan de straatkant.", 40,
                new BuildingDesign { Width = 4, Depth = 2, Density = ZoneDensity.High, Days = 240, Floors = 5, Wall = Render, Ground = GroundKind.Entrance, Balconies = true }),
            Home("residential-high-tower", "Woontoren", "Woontoren van tien lagen met betonpanelen en balkons.", 80,
                new BuildingDesign { Width = 3, Depth = 3, Density = ZoneDensity.High, Days = 360, Floors = 10, Wall = Concrete, Ground = GroundKind.Entrance, Balconies = true }),

            // Winkels.
            Shop("commercial-low-supermarket", "Buurtsupermarkt", "Supermarkt met etalage over de hele gevel en een groene luifel.", 14, 9_000,
                new BuildingDesign { Width = 3, Depth = 2, Density = ZoneDensity.Low, Days = 90, Floors = 1, GroundHeight = 4.4f, Wall = BrickYellow, Accent = AccentGreen }),
            Shop("commercial-low-shop-house", "Winkelpand", "Winkel op de begane grond met een kantoor- of opslagverdieping erboven.", 10, 6_000,
                new BuildingDesign { Width = 2, Depth = 2, Density = ZoneDensity.Low, Days = 100, Floors = 2, GroundHeight = 4.2f, FloorHeight = 3.2f, Wall = BrickRed, Accent = AccentRed }),
            Shop("commercial-low-hardware-store", "Bouwmarkt", "Brede, lage winkelhal met golfplaat en een blauw reclamebord.", 30, 22_000,
                new BuildingDesign { Width = 4, Depth = 3, Density = ZoneDensity.Low, Days = 150, Floors = 1, GroundHeight = 6f, Wall = Cladding, Accent = AccentBlue, Windows = WindowKind.HighStrip }),
            Shop("commercial-high-shopping-centre", "Winkelcentrum", "Winkelcentrum van drie lagen met glasstroken en een lange etalage.", 140, 160_000,
                new BuildingDesign { Width = 4, Depth = 3, Density = ZoneDensity.High, Days = 400, Floors = 3, GroundHeight = 4.5f, FloorHeight = 4f, Wall = Concrete, Accent = AccentBlue, Windows = WindowKind.Ribbon }),
            Shop("commercial-high-department-store", "Warenhuis", "Warenhuis van vijf lagen in baksteen met een rode luifel.", 160, 140_000,
                new BuildingDesign { Width = 3, Depth = 3, Density = ZoneDensity.High, Days = 450, Floors = 5, GroundHeight = 5f, FloorHeight = 3.8f, Wall = BrickRed, Accent = AccentRed }),
            Shop("commercial-high-arcade", "Winkelgalerij", "Gepleisterde galerij van vier lagen met winkels en glasstroken.", 90, 80_000,
                new BuildingDesign { Width = 4, Depth = 2, Density = ZoneDensity.High, Days = 320, Floors = 4, GroundHeight = 4.5f, FloorHeight = 3.6f, Wall = Render, Accent = AccentGreen, Windows = WindowKind.Ribbon }),

            // Kantoren.
            Office("office-low-villa", "Kantoorvilla", "Gepleisterde kantoorvilla van twee lagen.", 22, 40, 40,
                new BuildingDesign { Width = 2, Depth = 2, Density = ZoneDensity.Low, Days = 120, Floors = 2, Wall = Render, Ground = GroundKind.Entrance }),
            Office("office-low-brick", "Kantoorgebouw", "Kantoorgebouw van drie lagen in gele baksteen.", 48, 45, 40,
                new BuildingDesign { Width = 3, Depth = 2, Density = ZoneDensity.Low, Days = 200, Floors = 3, Wall = BrickYellow, Ground = GroundKind.Entrance }),
            Office("office-low-business-centre", "Bedrijfsverzamelgebouw", "Bedrijfsverzamelgebouw van twee lagen met golfplaat en glasstroken.", 44, 40, 20,
                new BuildingDesign { Width = 4, Depth = 2, Density = ZoneDensity.Low, Days = 180, Floors = 2, Wall = Cladding, Ground = GroundKind.Entrance, Windows = WindowKind.Ribbon }),
            Office("office-high-tower", "Kantoortoren", "Kantoortoren van twaalf lagen met glasgevels en een hoge lobby.", 420, 30, 60,
                new BuildingDesign { Width = 3, Depth = 3, Density = ZoneDensity.High, Days = 700, Floors = 12, GroundHeight = 5f, FloorHeight = 3.6f, Wall = Concrete, Ground = GroundKind.Lobby, Windows = WindowKind.Ribbon }),
            Office("office-high-block", "Kantoorblok", "Kantoorblok van zeven lagen met doorlopende glasstroken.", 380, 35, 50,
                new BuildingDesign { Width = 4, Depth = 3, Density = ZoneDensity.High, Days = 500, Floors = 7, GroundHeight = 5f, FloorHeight = 3.6f, Wall = Concrete, Ground = GroundKind.Lobby, Windows = WindowKind.Ribbon }),
            Office("office-high-slab", "Kantoorgebouw met ramen", "Gepleisterd kantoorgebouw van negen lagen met rijen ramen.", 320, 30, 55,
                new BuildingDesign { Width = 3, Depth = 3, Density = ZoneDensity.High, Days = 600, Floors = 9, GroundHeight = 4.5f, FloorHeight = 3.5f, Wall = Render, Ground = GroundKind.Entrance }),

            // Industrie.
            Industry("industrial-low-workshop", "Werkplaats", "Werkplaats met twee overheaddeuren en lichtstroken.", 16, 40,
                new BuildingDesign { Width = 3, Depth = 2, Density = ZoneDensity.Low, Days = 120, Floors = 1, GroundHeight = 6.5f, Wall = Cladding, Ground = GroundKind.Hall, Windows = WindowKind.HighStrip }),
            Industry("industrial-low-warehouse", "Magazijn", "Magazijn met laadperron aan de achterkant.", 30, 80,
                new BuildingDesign { Width = 4, Depth = 3, Density = ZoneDensity.Low, Days = 200, Floors = 1, GroundHeight = 9f, Wall = Cladding, Ground = GroundKind.Docks, Windows = WindowKind.HighStrip }),
            Industry("industrial-low-production-hall", "Productiehal", "Productiehal in gele baksteen met een zaagtanddak.", 45, 110,
                new BuildingDesign { Width = 4, Depth = 3, Density = ZoneDensity.Low, Days = 260, Floors = 1, GroundHeight = 7f, Wall = BrickYellow, Roof = RoofKind.Sawtooth, Ground = GroundKind.Hall, Windows = WindowKind.HighStrip }),
            Industry("industrial-high-factory", "Fabriek", "Fabriek van twee lagen met schoorsteen en drie silo's.", 160, 420,
                new BuildingDesign { Width = 4, Depth = 4, Density = ZoneDensity.High, Days = 500, Floors = 2, GroundHeight = 7f, FloorHeight = 6f, Wall = BrickRed, Ground = GroundKind.Hall, Windows = WindowKind.HighStrip, Stack = true, Rear = 9f, Silos = 3 }),
            Industry("industrial-high-distribution-centre", "Distributiecentrum", "Hoog distributiecentrum met een laadperron met veel deuren.", 110, 320,
                new BuildingDesign { Width = 4, Depth = 4, Density = ZoneDensity.High, Days = 400, Floors = 1, GroundHeight = 12f, Wall = Cladding, Ground = GroundKind.Docks, Windows = WindowKind.HighStrip }),
            Industry("industrial-high-chemical-plant", "Chemische installatie", "Installatie met opslagtanks en een hoge schoorsteen.", 130, 520,
                new BuildingDesign { Width = 4, Depth = 3, Density = ZoneDensity.High, Days = 480, Floors = 1, GroundHeight = 8f, Wall = Cladding, Ground = GroundKind.Hall, Windows = WindowKind.HighStrip, Stack = true, Rear = 8f, Silos = 3, SiloRadius = 3.2f, SiloHeight = 10f }),
        };

        private static BuildingDesign Home(string id, string title, string text, int households, BuildingDesign d) =>
            Fill(d, id, title, text, new BuildingSpec { Zone = ZoneType.Residential, Households = households });

        private static BuildingDesign Shop(string id, string title, string text, int jobs, long capacity, BuildingDesign d)
        {
            d.Ground = GroundKind.Shop;
            return Fill(d, id, title, text, new BuildingSpec { Zone = ZoneType.Commercial, Workplaces = jobs, MediumEducationPercent = 25, HighEducationPercent = 5, ShopCapacity = capacity });
        }

        private static BuildingDesign Office(string id, string title, string text, int jobs, int medium, int high, BuildingDesign d) =>
            Fill(d, id, title, text, new BuildingSpec { Zone = ZoneType.Office, Workplaces = jobs, MediumEducationPercent = medium, HighEducationPercent = high });

        private static BuildingDesign Industry(string id, string title, string text, int jobs, int production, BuildingDesign d) =>
            Fill(d, id, title, text, new BuildingSpec { Zone = ZoneType.Industrial, Workplaces = jobs, MediumEducationPercent = 15, HighEducationPercent = 5, Production = production });

        private static BuildingDesign Fill(BuildingDesign d, string id, string title, string text, BuildingSpec spec)
        {
            spec.Density = d.Density;
            spec.FootprintWidth = d.Width;
            spec.FootprintDepth = d.Depth;
            spec.Level = 1;
            spec.ConstructionDays = d.Days;
            d.Id = "building." + id;
            d.Title = title;
            d.Description = text;
            d.Spec = spec;
            d.Name = string.Concat(id.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));
            return d;
        }
    }
}
