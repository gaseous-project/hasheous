namespace DATImport
{
    public class SignatureIngestor
    {
        private static readonly object _datImporterLock = new object();
        private static readonly List<Func<IDATFileImport>> _datImporterFactories = new List<Func<IDATFileImport>>();
        private static readonly HashSet<string> _registeredFactoryKeys = new HashSet<string>(StringComparer.Ordinal);
        private static List<IDATFileImport> _datImporter = new List<IDATFileImport>();

        /// <summary>
        /// Registers a DAT importer type with the static importer registry.
        /// </summary>
        public static void Register<T>() where T : IDATFileImport, new()
        {
            string key = typeof(T).FullName ?? typeof(T).Name;

            lock (_datImporterLock)
            {
                if (_registeredFactoryKeys.Add(key))
                {
                    _datImporterFactories.Add(() => new T());
                    // Reset materialized cache so subsequent calls include newly registered factories.
                    _datImporter = new List<IDATFileImport>();
                }
            }
        }

        /// <summary>
        /// Gets a list of all the DAT file importers that have been registered with the SignatureIngestor.
        /// </summary>
        public static List<IDATFileImport> DATImporters
        {
            get
            {
                lock (_datImporterLock)
                {
                    if (_datImporter.Count == 0)
                    {
                        _datImporter = _datImporterFactories.Select(factory => factory()).ToList();
                    }

                    return _datImporter;
                }
            }
        }

        /// <summary>
        /// Gets a list of all the registered ingestors in the system, and builds a list of QueueItems for each ingestor to be processed in the queue.
        /// </summary>
        /// <returns>
        /// A list of QueueItems representing the registered ingestors in the system.
        /// </returns>
        public static List<Classes.ProcessQueue.QueueProcessor.QueueItem> GetRegisteredIngestors()
        {
            List<Classes.ProcessQueue.QueueProcessor.QueueItem> queueItems = new List<Classes.ProcessQueue.QueueProcessor.QueueItem>();

            foreach (var datImporter in DATImporters)
            {
                if (datImporter.IsEnabled)
                {
                    Classes.ProcessQueue.QueueProcessor.QueueItem queueItem = new Classes.ProcessQueue.QueueProcessor.QueueItem(Classes.ProcessQueue.QueueItemType.SignatureIngestor, datImporter.Interval, false, true, false, datImporter);
                    queueItems.Add(queueItem);
                }
            }

            return queueItems;
        }

        public enum SignatureType
        {
            // reimplement source types from gaseous_signature_parser.parser.SignatureParser

            TOSEC = gaseous_signature_parser.parser.SignatureParser.TOSEC,
            MAMEArcade = gaseous_signature_parser.parser.SignatureParser.MAMEArcade,
            MAMEMess = gaseous_signature_parser.parser.SignatureParser.MAMEMess,
            NoIntro = gaseous_signature_parser.parser.SignatureParser.NoIntro,
            Redump = gaseous_signature_parser.parser.SignatureParser.Redump,
            WHDLoad = gaseous_signature_parser.parser.SignatureParser.WHDLoad,
            RetroAchievements = gaseous_signature_parser.parser.SignatureParser.RetroAchievements,
            FBNeo = gaseous_signature_parser.parser.SignatureParser.FBNeo,
            PureDOSDAT = gaseous_signature_parser.parser.SignatureParser.PureDOSDAT,
            Pleasuredome = gaseous_signature_parser.parser.SignatureParser.Pleasuredome,
            MAMERedump = gaseous_signature_parser.parser.SignatureParser.MAMERedump,
            eXo = gaseous_signature_parser.parser.SignatureParser.eXo,
            HackHash = gaseous_signature_parser.parser.SignatureParser.HackHash,
            Generic = gaseous_signature_parser.parser.SignatureParser.Generic,
            TotalDOSCollection = gaseous_signature_parser.parser.SignatureParser.TotalDOSCollection,
            libretro = gaseous_signature_parser.parser.SignatureParser.libretro,
            ScreenScraper = gaseous_signature_parser.parser.SignatureParser.ScreenScraper,
            Unknown = gaseous_signature_parser.parser.SignatureParser.Unknown
        }
    }
}