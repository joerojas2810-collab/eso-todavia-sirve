// [ETSV] Capa Sim (C# PURO) — punto de acceso a la sesión del host.
// Solo el anfitrión la instancia (ETSVBootstrap); en clientes queda null y las
// cáscaras de Unity (NetworkBehaviour) lo usan para no ejecutar lógica local.

namespace Etsv.Integration
{
    public static class ETSVSession
    {
        /// <summary>Cerebro del host (null en clientes).</summary>
        public static DisassemblySessionHost Host;
    }
}
