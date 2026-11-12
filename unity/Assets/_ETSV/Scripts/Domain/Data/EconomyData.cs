// [ETSV] Dominio puro — CAPA DE DATOS: fuente única de verdad económica.
// Port 1:1 de prototype/js/data/economyData.js (Instrucción 02), contrastado
// contra GDD 5.0 §6.2 (precios), §6.3 (valores base), §4.2 (rendimiento),
// §8.5 (capacidades) y GDD 3.0 §13.1 (clases de peso).
// Ningún otro archivo define precios, recetas ni capacidades.
// Los valores PROVISORIOS (PROV-01..11) siguen siendo provisionales: no se
// convirtió ninguno en decisión definitiva (Instrucción 05 §5).

using System.Collections.Generic;

namespace Etsv.Domain
{
    // ---------------------------------------------------------------- definiciones
    public class MaterialDef
    {
        public string Id;
        public string Name;
        public int Price;    // créditos por unidad. MAT-08: negativo = coste de gestión
        public int Color;    // dato de presentación (hex), sin dependencia de engine
    }

    public class PartDef
    {
        public string Id;
        public string Name;
        public int VBase;        // valor base (GDD 5.0 §6.3)
        public double Kg;
        public string WeightClass;
    }

    public class WeightClassDef
    {
        public string Id;
        public string Name;
        public bool Carryable;
        public double SpeedMul;
        public string Storage;   // zona por defecto al almacenar/producir
    }

    public class MachineDef
    {
        public string Id;
        public string Name;
        public double E;         // eficiencia de proceso (GDD 5.0 §4.2)
    }

    public class StorageZoneDef
    {
        public string Id;
        public string Name;
        public string[] Accepts; // clases de peso aceptadas
        public int Capacity;
    }

    public class ProvisionalEntry
    {
        public string Id;
        public string Campo;
        public string Valor;
        public string Nota;
    }

    // ---------------------------------------------------------------- datos (fuente única)
    public static class EconomyData
    {
        // GDD 5.0 §6.2 — precios por unidad.
        public static readonly Dictionary<string, MaterialDef> Materials = new Dictionary<string, MaterialDef>
        {
            { "MAT-01", new MaterialDef { Id = "MAT-01", Name = "Acero/hierro",        Price = 4,  Color = 0x9aa2ad } },
            { "MAT-02", new MaterialDef { Id = "MAT-02", Name = "Aluminio",            Price = 9,  Color = 0xc9d2da } },
            { "MAT-03", new MaterialDef { Id = "MAT-03", Name = "Cobre",               Price = 18, Color = 0xc47a3d } },
            { "MAT-04", new MaterialDef { Id = "MAT-04", Name = "Plástico",            Price = 2,  Color = 0x4f6d8c } },
            { "MAT-05", new MaterialDef { Id = "MAT-05", Name = "Vidrio",              Price = 2,  Color = 0xbfe3da } },
            { "MAT-06", new MaterialDef { Id = "MAT-06", Name = "Electrónica",         Price = 14, Color = 0x7d9a4a } },
            { "MAT-07", new MaterialDef { Id = "MAT-07", Name = "Caucho/mixtos",       Price = 1,  Color = 0x3a3a3a } },
            { "MAT-08", new MaterialDef { Id = "MAT-08", Name = "Residuos especiales", Price = -5, Color = 0xa8433a } },
        };
        public static readonly string[] MatIds =
            { "MAT-01", "MAT-02", "MAT-03", "MAT-04", "MAT-05", "MAT-06", "MAT-07", "MAT-08" };

        // GDD 5.0 §6.3 — piezas genéricas: misma pieza, mismo precio sin importar el objeto.
        public static readonly Dictionary<string, PartDef> PartDefs = new Dictionary<string, PartDef>
        {
            { "motor_pequenio",         new PartDef { Id = "motor_pequenio",         Name = "Motor pequeño",             VBase = 90,   Kg = 3,   WeightClass = "P2" } },
            { "motor_electrodomestico", new PartDef { Id = "motor_electrodomestico", Name = "Motor de electrodoméstico", VBase = 300,  Kg = 9,   WeightClass = "P3" } },
            { "bomba_pequenio",         new PartDef { Id = "bomba_pequenio",         Name = "Bomba pequeña",             VBase = 160,  Kg = 4,   WeightClass = "P2" } },
            { "placa_electronica",      new PartDef { Id = "placa_electronica",      Name = "Placa electrónica común",   VBase = 110,  Kg = 1,   WeightClass = "P1" } },
            { "procesador",             new PartDef { Id = "procesador",             Name = "Procesador",                VBase = 180,  Kg = 0.5, WeightClass = "P1" } },
            { "alternador",             new PartDef { Id = "alternador",             Name = "Alternador",                VBase = 420,  Kg = 6,   WeightClass = "P2" } },
            { "motor_motocicleta",      new PartDef { Id = "motor_motocicleta",      Name = "Motor de motocicleta",      VBase = 650,  Kg = 22,  WeightClass = "P3" } },
            { "motor_automovil",        new PartDef { Id = "motor_automovil",        Name = "Motor de automóvil",        VBase = 1400, Kg = 90,  WeightClass = "P4" } },
            { "motor_industrial",       new PartDef { Id = "motor_industrial",       Name = "Motor industrial",          VBase = 1800, Kg = 200, WeightClass = "P5" } },
        };

        // GDD 5.0 §13.1 — clases de peso. P3+ no transportable (PROV-06: sin carro/grúa todavía).
        public static readonly Dictionary<string, WeightClassDef> WeightClasses = new Dictionary<string, WeightClassDef>
        {
            { "P1", new WeightClassDef { Id = "P1", Name = "Ligera",       Carryable = true,  SpeedMul = 1.0, Storage = "estanteria" } },
            { "P2", new WeightClassDef { Id = "P2", Name = "Media",        Carryable = true,  SpeedMul = 0.6, Storage = "pale" } },
            { "P3", new WeightClassDef { Id = "P3", Name = "Pesada",       Carryable = false, SpeedMul = 0,   Storage = "suelo" } },
            { "P4", new WeightClassDef { Id = "P4", Name = "Muy pesada",   Carryable = false, SpeedMul = 0,   Storage = "suelo" } },
            { "P5", new WeightClassDef { Id = "P5", Name = "Industrial",   Carryable = false, SpeedMul = 0,   Storage = "suelo" } },
        };

        // GDD 4.0 — máquinas. E no numérica en el GDD → PROV-01.
        public static readonly Dictionary<string, MachineDef> Machines = new Dictionary<string, MachineDef>
        {
            { "trituradora_basica", new MachineDef { Id = "trituradora_basica", Name = "Trituradora básica", E = 0.9 } }, // PROV-01
        };

        // Factor de conservación por daño P_i = calidad/100 (PROV-02).
        public static double ConservationFactor(int quality)
        {
            return quality / 100.0;
        }

        // Modificador comercial M_c (GDD 5.0 §6.3). Sin bonificaciones activas: PROV-03.
        public const double CommercialMod = 1.0;

        // GDD 5.0 §8.5 — capacidades físicas por categoría de espacio (PROV-04).
        public static readonly Dictionary<string, StorageZoneDef> StorageZones = new Dictionary<string, StorageZoneDef>
        {
            { "estanteria", new StorageZoneDef { Id = "estanteria", Name = "Estantería", Accepts = new[] { "P1" }, Capacity = 8 } },
            { "pale",       new StorageZoneDef { Id = "pale",       Name = "Palé",       Accepts = new[] { "P2" }, Capacity = 6 } },
            { "suelo",      new StorageZoneDef { Id = "suelo",      Name = "Suelo",      Accepts = new[] { "P3", "P4", "P5" }, Capacity = 4 } },
        };

        // Capacidad de contenedores de materiales (unidades por material): PROV-05.
        public const int MaterialContainerCap = 120;

        // Registro PROVISORIO — requieren aprobación; NO son decisiones definitivas.
        public static readonly ProvisionalEntry[] Provisional =
        {
            new ProvisionalEntry { Id = "PROV-01", Campo = "Machines[trituradora_basica].E", Valor = "0.9", Nota = "Eficiencia de proceso no numérica en GDD 4.0/5.0" },
            new ProvisionalEntry { Id = "PROV-02", Campo = "ConservationFactor", Valor = "q/100", Nota = "Forma funcional del factor de conservación P_i" },
            new ProvisionalEntry { Id = "PROV-03", Campo = "CommercialMod", Valor = "1.0", Nota = "Modificador comercial M_c sin bonificaciones activas" },
            new ProvisionalEntry { Id = "PROV-04", Campo = "StorageZones.Capacity", Valor = "8/6/4", Nota = "Capacidades de estantería/palé/suelo no numéricas en GDD 5.0 §8.5" },
            new ProvisionalEntry { Id = "PROV-05", Campo = "MaterialContainerCap", Valor = "120", Nota = "Capacidad de contenedores de materiales por unidad" },
            new ProvisionalEntry { Id = "PROV-06", Campo = "WeightClasses P3+.Carryable", Valor = "false", Nota = "Carro/grúa no implementados todavía" },
            new ProvisionalEntry { Id = "PROV-07", Campo = "recetas B_i del microondas", Valor = "ver CatalogData", Nota = "GDD 3.0 lista materiales sin cantidades; unidades provisorias" },
            new ProvisionalEntry { Id = "PROV-08", Campo = "VBase carcasa/puerta/panel/plato", Valor = "22/14/18/9", Nota = "No están en la tabla GDD 5.0 §6.3; se conservan del catálogo v0.2" },
            new ProvisionalEntry { Id = "PROV-09", Campo = "coste de gestión módulo sellado", Valor = "4 × $5", Nota = "Unidades MAT-08 del módulo no especificadas; se cobran al entregar" },
            new ProvisionalEntry { Id = "PROV-10", Campo = "calidad de pieza procesada heredada", Valor = "q origen", Nota = "Calidad de subensamblajes producidos (motor pequeño)" },
            new ProvisionalEntry { Id = "PROV-11", Campo = "persistencia física", Valor = "parcial", Nota = "Piezas en mundo/mano no se restauran al cargar; almacén y dinero sí" },
        };
    }
}
