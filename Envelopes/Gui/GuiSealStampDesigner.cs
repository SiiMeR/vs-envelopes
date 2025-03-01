using Vintagestory.API.Client;

namespace Envelopes.Gui;

public class GuiSealStampDesigner : GuiDialog
{
    public GuiSealStampDesigner(ICoreClientAPI capi) : base(capi)
    {
    }

    public override string ToggleKeyCombinationCode => "sealdesignerkeycombo";
}