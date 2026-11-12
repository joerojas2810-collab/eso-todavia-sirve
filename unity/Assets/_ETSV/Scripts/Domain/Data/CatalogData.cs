// [ETSV] Dominio puro — CATÁLOGO de objetos desmontables.
// Port 1:1 de prototype/js/data/catalog.js (v2), basado en GDD 3.0 (objeto 01:
// componentes, fijaciones, dependencias, resultados) y GDD 5.0 (clases de peso,
// valores base). Las recetas referencian MAT-xx y PART_DEFS de EconomyData:
// acá no hay precios ni capacidades (fuente única).
//
// DIFERENCIA deliberada con el JS: los campos `geo`/`offset` (geometría para la
// vista three.js) NO se portan al dominio — son presentación. En Unity vivirán
// en prefabs/datos de vista alimentados por la capa adaptadora (Integration).
// El dominio solo necesita: orden y tipo de fijaciones, dependencias, receta,
// peso, clase, valor base y flags especiales.

using System.Collections.Generic;

namespace Etsv.Domain
{
    // ---------------------------------------------------------------- fijaciones y herramientas
    public class FastenerTypeDef
    {
        public string Id;
        public string Name;
        public string Tool;   // herramienta requerida
        public double Time;   // segundos de trabajo (T_b)
        public string Verb;
        public string Icon;
    }

    public class ToolDef
    {
        public string Id;
        public string Name;
        public string Icon;
        public string Key;
    }

    // ---------------------------------------------------------------- recetas de recuperación
    /// <summary>Entrada de receta: material (Mat) o subensamble (Part), nunca ambos.
    /// Base = unidades abstractas B_i (GDD 5.0 §4.1: no son kilogramos).</summary>
    public class RecipeEntry
    {
        public string Mat;
        public string Part;
        public double Base;

        public static RecipeEntry OfMat(string mat, double b)
        {
            return new RecipeEntry { Mat = mat, Base = b };
        }
        public static RecipeEntry OfPart(string part, double b)
        {
            return new RecipeEntry { Part = part, Base = b };
        }
    }

    // ---------------------------------------------------------------- blueprint de objeto
    public class FastenerSlotDef
    {
        public string Type;   // id de FastenerTypeDef; el orden del array = orden de trabajo
    }

    public class ComponentDef
    {
        public string Id;
        public string Name;
        public double Kg;
        public string WeightClass;
        public int VBase;
        public string[] BlockedBy;      // dependencias de desmontaje (ids de componente)
        public bool Sealed;             // módulo sellado: sin apertura ni máquinas comunes
        public bool Special;            // residuo especial (gestión con coste)
        public bool IsProtected;        // componente narrativo protegido (campaña futura)
        public string Note;
        public RecipeEntry[] Recipe;    // resultados de descomposición
        public FastenerSlotDef[] Fasteners;
    }

    public class ObjectBlueprint
    {
        public string Id;
        public string Name;
        public string Family;      // GDD 3.0: E/M/…
        public string Size;
        public string Complexity;  // D1…
        public int Version;
        public ComponentDef[] Components;
    }

    // ---------------------------------------------------------------- datos del catálogo
    public static class GameCatalog
    {
        public static readonly Dictionary<string, FastenerTypeDef> FastenerTypes = new Dictionary<string, FastenerTypeDef>
        {
            { "tornillo", new FastenerTypeDef { Id = "tornillo", Name = "tornillo",  Tool = "destornillador", Time = 3.0, Verb = "Retirar tornillo", Icon = "🪛" } },
            { "bisagra",  new FastenerTypeDef { Id = "bisagra",  Name = "bisagra",   Tool = "llave",          Time = 2.0, Verb = "Liberar bisagra",  Icon = "🔧" } },
            { "fijacion", new FastenerTypeDef { Id = "fijacion", Name = "fijación",  Tool = "llave",          Time = 2.5, Verb = "Liberar fijación", Icon = "🔧" } },
        };

        public static readonly ToolDef[] Tools =
        {
            new ToolDef { Id = "llave",          Name = "Llave inglesa",         Icon = "🔧", Key = "1" },
            new ToolDef { Id = "destornillador", Name = "Destornillador manual", Icon = "🪛", Key = "2" },
            new ToolDef { Id = "manos",          Name = "Manos",                 Icon = "✋", Key = "3" },
        };

        public static ToolDef ToolById(string id)
        {
            foreach (var t in Tools) if (t.Id == id) return t;
            return null;
        }

        public static FastenerTypeDef FastenerTypeById(string id)
        {
            FastenerTypeDef f;
            return id != null && FastenerTypes.TryGetValue(id, out f) ? f : null;
        }

        // Objeto 01 · Microondas (GDD 3.0: E · M · D1 · Capítulo 1 · 6 componentes).
        // Cantidades B_i de receta PROVISORIAS (PROV-07). VBase carcasa/puerta/panel/plato
        // conservados del catálogo v0.2 (PROV-08). El motor rinde el subensamble
        // motor_pequenio (VBase = PART_DEFS, GDD 5.0 §6.3), no materiales sueltos.
        public static readonly ObjectBlueprint Microondas = new ObjectBlueprint
        {
            Id = "microondas",
            Name = "Microondas",
            Family = "E", Size = "M", Complexity = "D1",
            Version = 2,
            Components = new[]
            {
                new ComponentDef
                {
                    Id = "carcasa", Name = "Carcasa metálica", Kg = 6, WeightClass = "P2", VBase = 22, // PROV-08
                    BlockedBy = new string[0],
                    Recipe = new[] { RecipeEntry.OfMat("MAT-01", 4), RecipeEntry.OfMat("MAT-04", 1) }, // PROV-07 (GDD: Acero)
                    Fasteners = new[] { Slot("tornillo"), Slot("tornillo"), Slot("tornillo"), Slot("tornillo") },
                },
                new ComponentDef
                {
                    Id = "puerta", Name = "Puerta", Kg = 2, WeightClass = "P1", VBase = 14, // PROV-08
                    BlockedBy = new string[0],
                    Recipe = new[] { RecipeEntry.OfMat("MAT-05", 2), RecipeEntry.OfMat("MAT-04", 1) }, // PROV-07 (GDD: Vidrio, plástico)
                    Fasteners = new[] { Slot("bisagra"), Slot("bisagra") },
                },
                new ComponentDef
                {
                    Id = "panel", Name = "Panel de control", Kg = 0.8, WeightClass = "P1", VBase = 18, // PROV-08
                    BlockedBy = new string[0],
                    Recipe = new[] { RecipeEntry.OfMat("MAT-06", 2) }, // PROV-07 (GDD: Electrónica)
                    Fasteners = new[] { Slot("tornillo"), Slot("tornillo"), Slot("tornillo") },
                },
                new ComponentDef
                {
                    Id = "plato", Name = "Plato giratorio", Kg = 1.5, WeightClass = "P1", VBase = 9, // PROV-08
                    BlockedBy = new[] { "puerta" },
                    Recipe = new[] { RecipeEntry.OfMat("MAT-05", 2) }, // PROV-07 (GDD: Vidrio)
                    Fasteners = new FastenerSlotDef[0], // extracción directa
                },
                new ComponentDef
                {
                    // GDD 3.0: resultado "Motor pequeño" ⇒ subensamble reutilizable.
                    Id = "motor", Name = "Motor de giro", Kg = 3.5, WeightClass = "P2",
                    VBase = 90, // PART_DEFS.motor_pequenio — GDD 5.0 §6.3: misma pieza, mismo precio
                    BlockedBy = new[] { "carcasa" },
                    Recipe = new[] { RecipeEntry.OfPart("motor_pequenio", 1) },
                    Fasteners = new[] { Slot("tornillo"), Slot("tornillo") },
                },
                new ComponentDef
                {
                    // Módulo sellado: no se abre ni se procesa; entrega como residuo especial.
                    Id = "modulo", Name = "Módulo eléctrico sellado", Kg = 4, WeightClass = "P2", VBase = 0,
                    BlockedBy = new[] { "carcasa" },
                    Sealed = true, Special = true,
                    Note = "Sellado de fábrica: no se abre ni se procesa. Entrega en contenedor de residuos especiales (coste de gestión).",
                    Recipe = new[] { RecipeEntry.OfMat("MAT-08", 4) }, // PROV-09: unidades de coste de gestión
                    Fasteners = new[] { Slot("fijacion"), Slot("fijacion"), Slot("fijacion"), Slot("fijacion") },
                },
            },
        };

        private static FastenerSlotDef Slot(string type)
        {
            return new FastenerSlotDef { Type = type };
        }

        // Catálogo completo: objetos 02–30 se agregan acá con el mismo esquema,
        // sin modificar la lógica del sistema (Instrucción 05 §2).
        public static readonly ObjectBlueprint[] Objects = { Microondas };

        public static ObjectBlueprint ObjectById(string id)
        {
            foreach (var o in Objects) if (o.Id == id) return o;
            return null;
        }

        public static ComponentDef CompById(ObjectBlueprint bp, string id)
        {
            foreach (var c in bp.Components) if (c.Id == id) return c;
            return null;
        }
    }
}
