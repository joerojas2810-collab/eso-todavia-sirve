// [ETSV] Capa Sim (C# PURO, assembly ETSV.Sim) — sincronización compacta.
// Estado derivable mínimo por componente (Instrucción 06 §6: no sincronizar
// continuamente datos derivados): estado + calidad + fijaciones empaquetadas.
// CompSync es unmanaged + IEquatable ⇒ válido para NetworkList<CompSync> de
// NGO 2.12, e implementa INetworkSerializeByMemcpy (tag interface): SIN él, el
// codegen ILPP de NGO no genera serializador para structs custom y el runtime
// cae en FallbackSerializer ⇒ ArgumentException en el primer sync (verificado
// contra Editor/CodeGen/NetworkBehaviourILPP.cs y Documentation~/basics/
// networkvariable.md del paquete real 2.12.0).
// El progreso de cada fijación se cuantiza a 7 bits (0..127) para la red; el
// valor exacto vive solo en el host (fuente de verdad).

using System.Collections.Generic;
using Etsv.Domain;

namespace Etsv.Integration
{
    /// <summary>Estado sincronizable de UN componente (8 bytes). Tag
    /// INetworkSerializeByMemcpy: serialización por memcpy generada por el ILPP
    /// de NGO (todos sus campos son POD: byte×3 + ulong).</summary>
    public struct CompSync : System.IEquatable<CompSync>, Unity.Netcode.INetworkSerializeByMemcpy
    {
        public byte CompIdx;        // índice en ObjectBlueprint.Components
        public byte State;          // (byte)CompState
        public byte Quality;        // 0..100
        public ulong FastenerBits;  // 8 bits por fijación (máx 8): bit7=retirada, bits0-6=progreso 0..127

        public bool Equals(CompSync other)
        {
            return CompIdx == other.CompIdx && State == other.State
                && Quality == other.Quality && FastenerBits == other.FastenerBits;
        }

        public override bool Equals(object obj) { return obj is CompSync && Equals((CompSync)obj); }
        public override int GetHashCode()
        {
            return CompIdx | (State << 8) | (Quality << 16) | FastenerBits.GetHashCode();
        }
    }

    /// <summary>Vista decodificada de un componente (lado cliente / tests).</summary>
    public class DecodedComp
    {
        public string Id;
        public CompState State;
        public int Quality;
        public bool[] FastenerRemoved;
        public double[] FastenerProgress; // aproximación cuantizada (±1/127)
    }

    public static class DisassemblySync
    {
        public const int MaxFastenersPerComp = 8;
        private const int ProgressBits = 7;
        private const int ProgressMax = (1 << ProgressBits) - 1; // 127

        public static ulong PackFasteners(List<FastenerRuntime> fasteners)
        {
            ulong bits = 0;
            int n = fasteners.Count < MaxFastenersPerComp ? fasteners.Count : MaxFastenersPerComp;
            for (int i = 0; i < n; i++)
            {
                var f = fasteners[i];
                int q = (int)(f.Progress * ProgressMax + 0.5);
                if (q < 0) q = 0;
                if (q > ProgressMax) q = ProgressMax;
                ulong field = (f.Removed ? 1UL << ProgressBits : 0UL) | (uint)q;
                bits |= field << (i * 8);
            }
            return bits;
        }

        public static CompSync EncodeComp(int compIdx, ComponentRuntime rt)
        {
            return new CompSync
            {
                CompIdx = (byte)compIdx,
                State = (byte)rt.State,
                Quality = (byte)rt.Quality,
                FastenerBits = PackFasteners(rt.Fasteners),
            };
        }

        public static CompSync[] EncodeInstance(DisassemblyInstance inst)
        {
            var comps = inst.Bp.Components;
            var snap = new CompSync[comps.Length];
            for (int i = 0; i < comps.Length; i++) snap[i] = EncodeComp(i, inst.Comps[comps[i].Id]);
            return snap;
        }

        /// <summary>
        /// Reconstruye la vista de estado desde un snapshot (lo que hace cada
        /// cliente —incluido el que se incorpora tarde— al recibir la lista).
        /// </summary>
        public static List<DecodedComp> Decode(CompSync[] snapshot, ObjectBlueprint bp)
        {
            var result = new List<DecodedComp>();
            foreach (var s in snapshot)
            {
                if (s.CompIdx >= bp.Components.Length) continue;
                var def = bp.Components[s.CompIdx];
                int nf = def.Fasteners != null ? def.Fasteners.Length : 0;
                if (nf > MaxFastenersPerComp) nf = MaxFastenersPerComp;
                var d = new DecodedComp
                {
                    Id = def.Id,
                    State = (CompState)s.State,
                    Quality = s.Quality,
                    FastenerRemoved = new bool[nf],
                    FastenerProgress = new double[nf],
                };
                for (int i = 0; i < nf; i++)
                {
                    ulong field = (s.FastenerBits >> (i * 8)) & 0xFF;
                    d.FastenerRemoved[i] = (field & (1UL << ProgressBits)) != 0;
                    d.FastenerProgress[i] = (field & (ulong)ProgressMax) / (double)ProgressMax;
                }
                result.Add(d);
            }
            return result;
        }
    }
}
