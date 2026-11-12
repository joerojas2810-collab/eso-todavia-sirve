// [ETSV] Capa Sim (C# PURO, assembly ETSV.Sim) — índices estables del catálogo.
// Traduce ids de texto del dominio (blueprints, definiciones de pieza) a índices
// numéricos compactos para la red (NetworkVariable<byte>/ushort) y viceversa.
// Determinista: todas las peers construyen la misma tabla desde los mismos datos
// del dominio, sin UnityEngine.

using System.Collections.Generic;
using Etsv.Domain;

namespace Etsv.Integration
{
    public static class CatalogIndex
    {
        /// <summary>Blueprints publicables (hoy: microondas; mañana: los 30 del GDD).</summary>
        public static readonly ObjectBlueprint[] Blueprints = GameCatalog.Objects;

        /// <summary>
        /// Definiciones de pieza transportables por red: ids de componentes de todos
        /// los blueprints + ids de PART_DEFS (subensambles), sin duplicados y en orden
        /// estable (componentes por blueprint, luego PART_DEFS alfabético).
        /// </summary>
        public static readonly string[] PieceDefIds;

        private static readonly Dictionary<string, int> PieceDefIdx = new Dictionary<string, int>();

        static CatalogIndex()
        {
            var ids = new List<string>();
            foreach (var bp in Blueprints)
                foreach (var c in bp.Components)
                    if (!ids.Contains(c.Id)) ids.Add(c.Id);
            var parts = new List<string>(EconomyData.PartDefs.Keys);
            parts.Sort(System.StringComparer.Ordinal);
            foreach (var p in parts)
                if (!ids.Contains(p)) ids.Add(p);
            PieceDefIds = ids.ToArray();
            for (int i = 0; i < PieceDefIds.Length; i++) PieceDefIdx[PieceDefIds[i]] = i;
        }

        public static int BlueprintIndexOf(string id)
        {
            for (int i = 0; i < Blueprints.Length; i++) if (Blueprints[i].Id == id) return i;
            return -1;
        }

        public static ObjectBlueprint BlueprintAt(int idx)
        {
            return idx >= 0 && idx < Blueprints.Length ? Blueprints[idx] : null;
        }

        public static int PieceDefIndexOf(string id)
        {
            int v;
            return PieceDefIdx.TryGetValue(id, out v) ? v : -1;
        }

        public static string PieceDefAt(int idx)
        {
            return idx >= 0 && idx < PieceDefIds.Length ? PieceDefIds[idx] : null;
        }
    }
}
