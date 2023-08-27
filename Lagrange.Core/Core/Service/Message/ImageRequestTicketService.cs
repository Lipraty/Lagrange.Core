using Lagrange.Core.Common;
using Lagrange.Core.Core.Event.Protocol;
using Lagrange.Core.Core.Event.Protocol.Message;
using Lagrange.Core.Core.Packets;
using Lagrange.Core.Core.Packets.Message.Element.Implementation;
using Lagrange.Core.Core.Packets.Service.Highway;
using Lagrange.Core.Core.Service.Abstraction;
using Lagrange.Core.Utility;
using Lagrange.Core.Utility.Binary;
using Lagrange.Core.Utility.Extension;
using ProtoBuf;

namespace Lagrange.Core.Core.Service.Message;

[EventSubscribe(typeof(ImageRequestTicketEvent))]
[Service("LongConn.OffPicUp")]
internal class ImageRequestTicketService : BaseService<ImageRequestTicketEvent>
{
    protected override bool Build(ImageRequestTicketEvent input, BotKeystore keystore, BotAppInfo appInfo, BotDeviceInfo device,
        out BinaryPacket output, out List<BinaryPacket>? extraPackets)
    {
        input.Stream.Seek(0, SeekOrigin.Begin);
        
        var buffer = new byte[1024]; // parse image header
        int _ = input.Stream.Read(buffer.AsSpan());
        var type = ImageResolver.Resolve(buffer, out var size);
        
        string imageExt = type switch
        {
            ImageFormat.Jpeg => ".jpg",
            ImageFormat.Png => ".png",
            ImageFormat.Gif => ".gif",
            ImageFormat.Webp => ".webp",
            ImageFormat.Bmp => ".bmp",
            ImageFormat.Tiff => ".tiff",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
        
        var packet = new OffPicUp<OffPicUpRequest>
        {
            SubCmd = 1,
            Info = new OffPicUpRequest
            {
                SrcUin = keystore.Uin,
                FileId = 1,
                FileMd5 = input.FileMd5.UnHex(),
                FileSize = input.FileSize,
                FileName = input.FileMd5 + imageExt,
                SrcTerm = 2,
                PlatformType = 8,
                AddressBook = false,
                BuType = 8,
                PicOriginal = true,
                PicWidth = (uint)size.X,
                PicHeight = (uint)size.Y,
                PicType = 1001,
                SrvUpload = 0,
                TargetUid = input.TargetUid
            },
            NetType = 10
        };
        
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, packet);
        output = new BinaryPacket(stream);
        
        extraPackets = null;
        return true;
    }

    protected override bool Parse(SsoPacket input, BotKeystore keystore, BotAppInfo appInfo, BotDeviceInfo device,
        out ImageRequestTicketEvent output, out List<ProtocolEvent>? extraEvents)
    {
        var payload = input.Payload.ReadBytes(BinaryPacket.Prefix.Uint32 | BinaryPacket.Prefix.WithPrefix);
        var packet = Serializer.Deserialize<OffPicUp<OffPicUpResponse>>(payload.AsSpan());
        
        output = ImageRequestTicketEvent.Result((int)(packet.Info?.Result ?? 1),
                                                packet.Info?.UpUkey?.Hex(true) ?? "", 
                                                packet.Info?.FileExit ?? false, 
                                                packet.Info?.UpResid ?? "");
        extraEvents = null;
        return true;
    }
}