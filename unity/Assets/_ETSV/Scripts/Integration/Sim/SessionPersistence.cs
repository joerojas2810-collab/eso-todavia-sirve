// [ETSV] Capa Sim (C# puro) — ORQUESTACIÓN DE PERSISTENCIA (Instrucción 08).
// Separación estricta (§7): el DTO/serialización/validación/migración viven en
// ETSV.Domain (Persistence/SaveFull.cs); acá vive la RECONSTRUCCIÓN LÓGICA —
// cómo el estado vivo del host (instancias de desmontaje + economía + transporte)
// se captura en un SaveGame v4 y cómo un SaveGame v4 restaura un host completo.
// La reconstrucción FÍSICA (NetworkObjects) y la SYNC de red viven en la capa
// Unity (ETSVBootstrap/PieceSpawner/ETSVSessionView) y consumen este resultado.
//
// Guardado seguro (GDD §16.3):
//   1. punto lógico de consistencia: PruneStaleWorkers + StopAllWork (cancela
//      interacciones manuales sin penalización, conserva progreso);
//   2. captura sobre clones (SerializeFull no muta el estado vivo);
//   3. la escritura temporal + verificación + reemplazo + respaldo vive en
//      SaveRepository (mismo ensamblado, System.IO, sin UnityEngine).

using System.Collections.Generic;
using Etsv.Domain;

namespace Etsv.Integration
{
    /// <summary>Pose física (la aporta la capa Unity desde los transforms; en tests
    /// puros se usan poses sintéticas). Yaw en grados.</summary>
    public struct SavePose
    {
        public double X, Y, Z, Yaw;
        public SavePose(double x, double y, double z, double yaw) { X = x; Y = y; Z = z; Yaw = yaw; }
    }

    /// <summary>Fuente de poses físicas. Devuelve false si el objeto/pieza no tiene
    /// físico visible (la captura usa entonces una pose determinista de respaldo).</summary>
    public interface IPoseSource
    {
        bool ObjectPose(string domainId, out SavePose pose);
        bool PiecePose(string uid, out SavePose pose);
    }

    /// <summary>Sin físico: poses de respaldo deterministas (documentado en
    /// PERSISTENCIA.md — misma entrada ⇒ misma salida, sin azar).</summary>
    public sealed class NullPoseSource : IPoseSource
    {
        public bool ObjectPose(string domainId, out SavePose pose) { pose = FallbackPose(domainId, 0); return false; }
        public bool PiecePose(string uid, out SavePose pose) { pose = FallbackPose(uid, 1); return false; }

        /// <summary>Anillo determinista alrededor del ancla de la mesa: hash FNV del
        /// id ⇒ ángulo y radio estables. Idéntico en cada peer y entre guardados.</summary>
        public static SavePose FallbackPose(string id, int ring)
        {
            int h = SaveFormat.Fnv1a(id ?? "");
            double ang = (h % 360) * System.Math.PI / 180.0;
            double r = 2.2 + ring * 0.6 + (h >> 16) % 5 * 0.1;
            return new SavePose(
                ScrapyardAnchors.TableX + System.Math.Cos(ang) * r,
                0.1,
                ScrapyardAnchors.TableZ + System.Math.Sin(ang) * r,
                (h % 360));
        }
    }

    /// <summary>Coordenadas de respaldo: el ancla lógica de la mesa es (0,0,0) en
    /// Sim (la capa Unity suma el offset del spawn point al materializar).</summary>
    public static class ScrapyardAnchors
    {
        public const double TableX = 0;
        public const double TableZ = 0;
    }

    public sealed class SessionPersistence
    {
        // ---------------------------------------------------------------- captura
        /// <summary>Captura completa del estado del host (GDD §16.3 pasos 1–3).
        /// saveId: el de la partida en curso (null ⇒ partida nueva, se genera).
        /// NO muta el estado vivo: las piezas carried/scale se clonan como world
        /// (política determinista de transporte, SaveFull.cs).</summary>
        public static SaveGame Capture(DisassemblySessionHost host, string saveId, IPoseSource poses)
        {
            if (host == null) return null;

            // 1. Punto lógico de consistencia.
            host.PruneStaleWorkers(0);
            host.StopAllWork();

            if (poses == null) poses = new NullPoseSource();

            // 2. Patio: instancias con desmontaje completo + pose física.
            var yard = new List<YardObject>();
            foreach (var si in host.AllInstances())
            {
                SavePose op;
                if (!poses.ObjectPose(si.DomainId, out op)) op = NullPoseSource.FallbackPose(si.DomainId, 0);
                yard.Add(new YardObject
                {
                    DomainId = si.DomainId,
                    BlueprintId = si.Bp.Id,
                    BpFingerprint = SaveFormat.BlueprintFingerprint(si.Bp),
                    X = op.X, Y = op.Y, Z = op.Z, Yaw = op.Yaw,
                    Components = DisassemblyRules.SerializeInstance(si.Inst),
                });
            }

            // 3. Poses de piezas físicas (world/carried/scale no terminales).
            //    CarriedBy: trazabilidad de quién la transportaba (GDD §16.1 Piezas).
            var carriedByUid = new Dictionary<string, string>(); // uid → playerTag
            foreach (var kv in host.Ctx.CarryByPlayer) carriedByUid[kv.Value] = kv.Key;

            var piecePoses = new List<PiecePose>();
            foreach (var p in host.State.Reg.Pieces.Values)
            {
                if (p.Terminal != null) continue;                       // vendida/procesada/entregada: sin físico
                if (p.Storage != null && p.Storage.StartsWith("stored:")) continue; // almacenada: sin físico
                SavePose pp;
                if (!poses.PiecePose(p.Uid, out pp)) pp = NullPoseSource.FallbackPose(p.Uid, 1);
                string carrier;
                piecePoses.Add(new PiecePose
                {
                    Uid = p.Uid,
                    X = pp.X, Y = pp.Y, Z = pp.Z, Yaw = pp.Yaw,
                    CarriedBy = carriedByUid.TryGetValue(p.Uid, out carrier) ? carrier : null,
                });
            }

            // 4. Contadores + progreso (estructuras extensibles, §2).
            var extras = new SaveExtras
            {
                SaveId = saveId,
                SavedAtUtc = System.DateTime.UtcNow.ToString("o"),
                Yard = yard,
                PiecePoses = piecePoses,
                Counters = new SaveCounters { NextInstance = NextInstanceOf(host) },
                Progress = SaveSerializer.DefaultProgress(),
            };
            return SaveSerializer.SerializeFull(host.State, extras);
        }

        private static int NextInstanceOf(DisassemblySessionHost host)
        {
            // El contador interno es privado: se deriva de las instancias vivas
            // (RestoreInstance también lo actualiza al cargar, doble seguro).
            int max = 0;
            foreach (var si in host.AllInstances())
            {
                int sep = si.DomainId.LastIndexOf('_');
                int n;
                if (sep > 0 && int.TryParse(si.DomainId.Substring(sep + 1), out n) && n > max) max = n;
            }
            return max + 1;
        }

        // ---------------------------------------------------------------- restauración lógica
        /// <summary>Restaura un save (v4, o v3 migrado explícitamente) sobre un host
        /// existente. Devuelve la lista de problemas (vacía = éxito). Si hay
        /// problemas NO toca el host (transacción fallida sin mutaciones).</summary>
        public static List<string> RestoreInto(SaveGame save, DisassemblySessionHost host)
        {
            var problems = new List<string>();
            if (host == null) { problems.Add("host_nulo"); return problems; }

            var migrated = SaveMigrations.MigrateToLatest(save);
            if (migrated == null)
            {
                problems.Add(save == null ? "save_nulo" : "version_desconocida:" + save.Version);
                return problems;
            }

            problems = SaveValidator.ValidateFull(migrated);
            if (problems.Count > 0) return problems; // host intacto

            var full = SaveSerializer.DeserializeFull(migrated);
            if (full == null) { problems.Add("deserializacion_fallida"); return problems; }

            // Estado económico (dinero, piezas, almacenes con fracciones).
            host.ApplyState(full.Gs);
            host.StopAllWork();

            // Patio: reinicia el registro de instancias y reconstruye cada objeto.
            host.ClearInstances();
            foreach (var y in full.Yard)
            {
                var bp = GameCatalog.ObjectById(y.BlueprintId); // validado != null
                var inst = DisassemblyRules.DeserializeInstance(bp, y.DomainId, y.Components);
                host.RestoreInstance(y.DomainId, bp, inst);
            }
            return problems; // vacía
        }
    }
}
