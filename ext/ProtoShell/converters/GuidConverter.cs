using System;

namespace FsStorm.ProtoShell
{
    using Google.Protobuf;

    public static class GuidConverter
    {
        public static Guid ToGuid(this ByteString bytes)
        {
            return new Guid(bytes.ToByteArray());
        }

        public static ByteString ToByteString(this Guid guid)
        {
            return ByteString.CopyFrom(guid.ToByteArray());
        }
    }
}
