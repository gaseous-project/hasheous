namespace hasheous_server.Models
{
    public class HasheousRomSignatureObject : gaseous_signature_parser.models.RomSignatureObject.RomSignatureObject
    {
        public long HasheousSourceId { get; set; } = 0;

        public List<Game> Games { get; set; } = new List<Game>();

        /// <summary>
        /// Creates a <see cref="HasheousRomSignatureObject"/> by copying all data from a parser-produced object.
        /// </summary>
        public static HasheousRomSignatureObject FromBase(gaseous_signature_parser.models.RomSignatureObject.RomSignatureObject source)
        {
            HasheousRomSignatureObject result = new HasheousRomSignatureObject();
            CopyProperties(source, result, nameof(Games));

            foreach (var game in source.Games)
            {
                result.Games.Add(Game.FromBase(game));
            }

            return result;
        }

        // Copies every readable/writable base-type property so new base members are picked up automatically.
        private static void CopyProperties(object source, object target, params string[] skip)
        {
            foreach (var property in source.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length > 0 || skip.Contains(property.Name))
                {
                    continue;
                }

                property.SetValue(target, property.GetValue(source));
            }
        }

        public class Game : gaseous_signature_parser.models.RomSignatureObject.RomSignatureObject.Game
        {
            public long HasheousGameId { get; set; } = 0;

            public List<Rom> Roms { get; set; } = new List<Rom>();

            internal static Game FromBase(gaseous_signature_parser.models.RomSignatureObject.RomSignatureObject.Game source)
            {
                Game result = new Game();
                CopyProperties(source, result, nameof(Roms));

                foreach (var rom in source.Roms)
                {
                    result.Roms.Add(Rom.FromBase(rom));
                }

                return result;
            }

            public class Rom : gaseous_signature_parser.models.RomSignatureObject.RomSignatureObject.Game.Rom
            {
                public long HasheousRomId { get; set; } = 0;

                internal static Rom FromBase(gaseous_signature_parser.models.RomSignatureObject.RomSignatureObject.Game.Rom source)
                {
                    Rom result = new Rom();
                    CopyProperties(source, result);
                    return result;
                }
            }
        }
    }
}
