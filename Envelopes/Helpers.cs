using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Envelopes;

public static class Helpers
{
    public static bool IsServerSide()
    {
        return EnvelopesModSystem.Api.Side == EnumAppSide.Server;
    }

    public static bool IsClientSide()
    {
        return EnvelopesModSystem.Api.Side == EnumAppSide.Client;
    }

    public static string EnvelopesLangString(string entry)
    {
        return Lang.Get($"{EnvelopesModSystem.ModId}:{entry}");
    }
}