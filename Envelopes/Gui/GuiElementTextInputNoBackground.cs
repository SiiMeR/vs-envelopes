using System;
using System.Reflection;
using Cairo;
using Vintagestory.API.Client;

namespace Envelopes.Gui;

public static class GuiElementHelpers
{
    public static GuiComposer AddTextInputNoBackground(
        this GuiComposer composer,
        ElementBounds bounds,
        Action<string> onTextChanged,
        CairoFont? font = null,
        string? key = null)
    {
        if (font == null)
        {
            font = CairoFont.TextInput();
        }

        if (!composer.Composed)
        {
            composer.AddInteractiveElement(
                new GuiElementTextInputNoBackground(composer.Api, bounds, onTextChanged, font), key);
        }

        return composer;
    }
}

public class GuiElementTextInputNoBackground : GuiElementTextInput
{
    public GuiElementTextInputNoBackground(ICoreClientAPI capi, ElementBounds bounds, Action<string> onTextChanged,
        CairoFont font) : base(capi, bounds, onTextChanged, font)
    {
    }

    public override void ComposeTextElements(Context ctx, ImageSurface surface)
    {
        EmbossRoundRectangleElement(ctx, Bounds, true, radius: 1);
        ctx.SetSourceRGBA(1.0, 1.0, 1.0, 0.1);
        ElementRoundRectangle(ctx, Bounds, radius: 1.0);
        ctx.Fill();
        var surface1 = new ImageSurface(Format.Argb32, (int)Bounds.OuterWidth, (int)Bounds.OuterHeight);
        var context = genContext(surface1);
        context.SetSourceRGBA(1.0, 1.0, 1.0, 0.04);
        context.Paint();
        generateTexture(surface1, ref highlightTexture);
        context.Dispose();
        surface1.Dispose();
        highlightBounds = Bounds.CopyOffsetedSibling().WithFixedPadding(0.0, 0.0)
            .FixedGrow(2.0 * Bounds.absPaddingX, 2.0 * Bounds.absPaddingY);
        highlightBounds.CalcWorldBounds();


        var method = typeof(GuiElementTextInput).GetMethod(
            "RecomposeText",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        method?.Invoke(this, null);
    }
}