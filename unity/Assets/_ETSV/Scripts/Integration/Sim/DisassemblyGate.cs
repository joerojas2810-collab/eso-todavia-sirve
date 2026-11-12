// [ETSV] Capa Sim (C# PURO, assembly ETSV.Sim) — validaciones del host.
// Toda solicitud de red pasa por acá ANTES de tocar el dominio (Instrucción 06 §5):
// identidad del jugador, distancia, herramienta equipada (registro autoritativo
// del host, nunca la que declara el cliente), estado de la fijación, dependencias
// y operaciones simultáneas. Sin UnityEngine: testeable en NUnit/Mono.

using Etsv.Domain;

namespace Etsv.Integration
{
    /// <summary>Posición 3D pura (sin Vector3) para validaciones de distancia.</summary>
    public struct P3
    {
        public float X, Y, Z;
        public P3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static double Distance(P3 a, P3 b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }

    /// <summary>Sesión de trabajo sobre una fijación (anti operación simultánea).</summary>
    public struct WorkSession
    {
        public ulong ClientId;
        public double Age; // segundos desde el último tick
    }

    public static class DisassemblyGate
    {
        /// <summary>Alcance de interacción (paridad con PlayerMovement.interactRange = 5).</summary>
        public const double MaxInteractDistance = 5.0;

        /// <summary>Tope de dt por tick de red: evita avance acelerado con ticks manipulados.</summary>
        public const double MaxTickDt = 0.25;

        /// <summary>Sin ticks por este tiempo, la sesión de trabajo se libera (desconexión/soltar).</summary>
        public const double WorkerTimeout = 0.75;

        /// <summary>Identidad + distancia. playerKnown = el host tiene al cliente conectado y con avatar.</summary>
        public static OpResult ValidatePresence(bool playerKnown, P3 playerPos, P3 objectPos)
        {
            if (!playerKnown) return OpResult.Fail("jugador_desconocido");
            if (P3.Distance(playerPos, objectPos) > MaxInteractDistance) return OpResult.Fail("distancia_invalida");
            return OpResult.Success();
        }

        /// <summary>
        /// Conflicto de operación simultánea: devuelve null si el trabajador puede
        /// operar (sesión libre o ya es suya); si no, la razón estable.
        /// </summary>
        public static string ValidateWorker(WorkSession? active, ulong requester)
        {
            if (active == null) return null;
            var w = active.Value;
            return w.ClientId == requester ? null : "ocupado_por_otro";
        }

        /// <summary>Rango de índice de componente contra el blueprint.</summary>
        public static ComponentDef ValidateCompIdx(ObjectBlueprint bp, int compIdx)
        {
            if (bp == null || compIdx < 0 || compIdx >= bp.Components.Length) return null;
            return bp.Components[compIdx];
        }

        /// <summary>Índice de fijación contra el componente.</summary>
        public static bool ValidateFastenerIdx(ComponentDef def, int fIdx)
        {
            if (def == null || def.Fasteners == null) return false;
            return fIdx >= 0 && fIdx < def.Fasteners.Length;
        }
    }
}
