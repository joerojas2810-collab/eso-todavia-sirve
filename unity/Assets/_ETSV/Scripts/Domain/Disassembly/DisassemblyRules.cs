// [ETSV] Dominio puro — SISTEMA DE DESMONTAJE.
// Port 1:1 de prototype/js/systems/disassembly.js (Instrucción 01), verificado
// contra GDD 3.0 (secuencias y resultados) y GDD 5.0 §5 (calidad: solo baja por
// acciones destructivas). Las reglas no conocen nombres de objetos: leen el
// blueprint. Sin MonoBehaviour, sin físicas, sin networking.

using System.Collections.Generic;

namespace Etsv.Domain
{
    /// <summary>Estados de un componente (mismo orden que el prototipo):
    /// bloqueado → accesible → liberado → extraido → (procesado | vendido).</summary>
    public enum CompState
    {
        Bloqueado,
        Accesible,
        Liberado,
        Extraido,
        Procesado,
        Vendido,
    }

    public static class DisassemblyStates
    {
        // Estados que consideran "retirado" al componente a efectos de dependencias.
        public static readonly CompState[] Unlocks = { CompState.Extraido, CompState.Procesado, CompState.Vendido };
        // Estados terminales: ya no se puede operar ni recuperar nada de él.
        public static readonly CompState[] Terminal = { CompState.Procesado, CompState.Vendido };

        public static bool IsUnlock(CompState s) { return System.Array.IndexOf(Unlocks, s) >= 0; }
        public static bool IsTerminal(CompState s) { return System.Array.IndexOf(Terminal, s) >= 0; }
    }

    public class FastenerRuntime
    {
        public int I;
        public string Type;
        public bool Removed;
        public double Progress;   // 0..1 — se conserva al interrumpir (GDD: ~3 s por tornillo)
    }

    public class ComponentRuntime
    {
        public string Id;
        public CompState State;
        public int Quality;       // 0..100
        public List<FastenerRuntime> Fasteners;
        public string TerminalTag;
    }

    public class DisassemblyEvent
    {
        public string T;          // accessible | fastener | liberado | extraido | damage | procesado | vendido
        public string Comp;
        public int FIdx;
        public int Quality;
        public string Tag;
    }

    public class DisassemblyInstance
    {
        public string Id;                    // instancia del objeto (p. ej. "microondas_3")
        public ObjectBlueprint Bp;
        public Dictionary<string, ComponentRuntime> Comps = new Dictionary<string, ComponentRuntime>();
        public List<DisassemblyEvent> Events = new List<DisassemblyEvent>();
    }

    public class FastenerStatsResult
    {
        public int Done;
        public int Total;
    }

    public class WorkCheck
    {
        public bool Ok;
        public string Why;        // estado | ya | herramienta
        public CompState State;
        public double Time;
        public string Verb;
        public string Need;       // herramienta requerida
        public FastenerTypeDef Ft;
    }

    public enum WorkOutcome { Done, Progress }

    // partial: la serialización de instancias para el save v4 vive en
    // Persistence/SaveFull.cs (Instrucción 08), junto al resto del formato.
    public static partial class DisassemblyRules
    {
        public static DisassemblyInstance CreateInstance(ObjectBlueprint bp, string id)
        {
            var inst = new DisassemblyInstance { Id = id, Bp = bp };
            foreach (var c in bp.Components)
            {
                var rt = new ComponentRuntime
                {
                    Id = c.Id,
                    State = CompState.Bloqueado,
                    Quality = 100,
                    Fasteners = new List<FastenerRuntime>(),
                    TerminalTag = null,
                };
                if (c.Fasteners != null)
                    for (int i = 0; i < c.Fasteners.Length; i++)
                        rt.Fasteners.Add(new FastenerRuntime { I = i, Type = c.Fasteners[i].Type, Removed = false, Progress = 0 });
                inst.Comps[c.Id] = rt;
            }
            RefreshAccessibility(inst);
            return inst;
        }

        // bloqueado → accesible cuando todas sus dependencias fueron retiradas.
        public static void RefreshAccessibility(DisassemblyInstance inst)
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var c in inst.Bp.Components)
                {
                    var s = inst.Comps[c.Id];
                    if (s.State != CompState.Bloqueado) continue;
                    bool free = true;
                    if (c.BlockedBy != null)
                        foreach (var d in c.BlockedBy)
                            if (!DisassemblyStates.IsUnlock(inst.Comps[d].State)) { free = false; break; }
                    if (free)
                    {
                        s.State = CompState.Accesible;
                        // sin fijaciones ⇒ queda liberado de inmediato (extracción directa)
                        if (s.Fasteners.Count == 0) s.State = CompState.Liberado;
                        inst.Events.Add(new DisassemblyEvent { T = "accessible", Comp = c.Id });
                        changed = true;
                    }
                }
            }
        }

        public static FastenerStatsResult FastenerStats(DisassemblyInstance inst, string compId)
        {
            var f = inst.Comps[compId].Fasteners;
            int done = 0;
            foreach (var x in f) if (x.Removed) done++;
            return new FastenerStatsResult { Done = done, Total = f.Count };
        }

        // ¿Puede el jugador trabajar esta fijación con esta herramienta AHORA?
        public static WorkCheck CanWork(DisassemblyInstance inst, string compId, int fIdx, string toolId,
            Dictionary<string, FastenerTypeDef> ftTypes)
        {
            var s = inst.Comps[compId];
            if (s.State != CompState.Accesible)
                return new WorkCheck { Ok = false, Why = "estado", State = s.State };
            if (fIdx < 0 || fIdx >= s.Fasteners.Count) return new WorkCheck { Ok = false, Why = "ya" };
            var f = s.Fasteners[fIdx];
            if (f.Removed) return new WorkCheck { Ok = false, Why = "ya" };
            var ft = ftTypes[f.Type];
            if (ft.Tool != toolId)
                return new WorkCheck { Ok = false, Why = "herramienta", Need = ft.Tool, Ft = ft };
            return new WorkCheck { Ok = true, Time = ft.Time, Verb = ft.Verb, Ft = ft };
        }

        // Aplica dt segundos de trabajo sobre una fijación cuya operación dura `time`.
        // El progreso queda guardado: interrumpir no lo pierde.
        public static WorkOutcome Work(DisassemblyInstance inst, string compId, int fIdx, double dt, double time)
        {
            var s = inst.Comps[compId];
            var f = s.Fasteners[fIdx];
            f.Progress = System.Math.Min(1, f.Progress + dt / System.Math.Max(0.01, time));
            if (f.Progress >= 1 && !f.Removed)
            {
                f.Removed = true;
                inst.Events.Add(new DisassemblyEvent { T = "fastener", Comp = compId, FIdx = fIdx });
                bool all = true;
                foreach (var x in s.Fasteners) if (!x.Removed) { all = false; break; }
                if (all)
                {
                    s.State = CompState.Liberado;
                    inst.Events.Add(new DisassemblyEvent { T = "liberado", Comp = compId });
                    RefreshAccessibility(inst);
                }
                return WorkOutcome.Done;
            }
            return WorkOutcome.Progress;
        }

        public static bool CanExtract(DisassemblyInstance inst, string compId)
        {
            return inst.Comps[compId].State == CompState.Liberado;
        }

        public static ComponentRuntime Extract(DisassemblyInstance inst, string compId)
        {
            var s = inst.Comps[compId];
            if (s.State != CompState.Liberado) return null;
            s.State = CompState.Extraido;
            inst.Events.Add(new DisassemblyEvent { T = "extraido", Comp = compId });
            RefreshAccessibility(inst); // retirar esta pieza puede destapar otras
            return s;
        }

        // Daño por acciones destructivas (golpes). Nunca por uso correcto de herramienta.
        public static int Damage(DisassemblyInstance inst, string compId, int amt)
        {
            var s = inst.Comps[compId];
            if (DisassemblyStates.IsTerminal(s.State) || s.State == CompState.Extraido) return 0;
            int before = s.Quality;
            s.Quality = System.Math.Max(0, s.Quality - amt);
            int applied = before - s.Quality;
            if (applied > 0) inst.Events.Add(new DisassemblyEvent { T = "damage", Comp = compId, Quality = s.Quality });
            return applied;
        }

        // Cierre de ciclo: procesado (trituradora / residuo especial) o vendido (balanza).
        // Solo desde EXTRAIDO: única vía de recuperación ⇒ imposible recuperar dos veces.
        public static bool SetTerminal(DisassemblyInstance inst, string compId, CompState state, string tag)
        {
            var s = inst.Comps[compId];
            if (s.State != CompState.Extraido) return false;
            if (DisassemblyStates.IsTerminal(s.State)) return false;
            s.State = state;
            s.TerminalTag = tag != null ? tag : null;
            inst.Events.Add(new DisassemblyEvent { T = state == CompState.Procesado ? "procesado" : "vendido", Comp = compId, Tag = tag });
            RefreshAccessibility(inst);
            return true;
        }

        public static bool AllSettled(DisassemblyInstance inst)
        {
            foreach (var c in inst.Bp.Components)
                if (!DisassemblyStates.IsTerminal(inst.Comps[c.Id].State)) return false;
            return true;
        }

        public static List<DisassemblyEvent> DrainEvents(DisassemblyInstance inst)
        {
            var e = inst.Events;
            inst.Events = new List<DisassemblyEvent>();
            return e;
        }
    }
}
