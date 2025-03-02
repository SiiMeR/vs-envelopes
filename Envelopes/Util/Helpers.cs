using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Envelopes.Util;

public static class Helpers
{
    public static bool IsServerSide()
    {
        return GetApi().Side == EnumAppSide.Server;
    }

    public static bool IsClientSide()
    {
        return GetApi().Side == EnumAppSide.Client;
    }

    public static string EnvelopesLangString(string entry)
    {
        return Lang.Get($"{EnvelopesModSystem.ModId}:{entry}");
    }

    public static ICoreAPI GetApi()
    {
        var api = EnvelopesModSystem.Api;
        if (api == null)
        {
            throw new Exception("Tried to access CoreAPI, but it is not available. Weird.");
        }

        return api;
    }
}