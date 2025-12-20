using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Envelopes.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Envelopes.Behaviors;

public class SetSealColorFromAttributes : CollectibleBehavior, IContainedMeshSource
{
    private ICoreAPI _api;

    private const string MeshRefsCacheKey = "EnvelopeRefs";

    public override void OnLoaded(ICoreAPI api)
    {
        _api = api;
        base.OnLoaded(api);
    }


    public override void OnUnloaded(ICoreAPI api)
    {
        base.OnUnloaded(api);
        var meshRefs =
            ObjectCacheUtil.TryGet<Dictionary<string, MultiTextureMeshRef>>(api,
                MeshRefsCacheKey);
        meshRefs?.Foreach(meshRef => meshRef.Value?.Dispose());
        ObjectCacheUtil.Delete(api, MeshRefsCacheKey);
    }

    public SetSealColorFromAttributes(CollectibleObject collObj) : base(collObj)
    {
    }


    public MeshData? CreateMesh(ItemStack itemstack)
    {
        if (_api is not ICoreClientAPI capi)
        {
            return null;
        }


        var shape = GenShape(capi, itemstack);

        var tps = new ShapeTextureSource(capi, shape, "envelopesealsource");
        var cacheKey = GetMeshCacheKey(itemstack);
        capi.Tesselator.TesselateShape(cacheKey, shape, out var meshdata, tps);

        return meshdata;
    }

    private Shape GenShape(ICoreClientAPI api, ItemStack stack)
    {
        var shapeloc = stack.Item.Shape.Base.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
        var shape = api.Assets.TryGet(shapeloc)?.ToObject<Shape>();
        if (shape == null)
        {
            api.Logger.Error("Could not find shape for seal {0} at {1}", stack.Item.Code, shapeloc);
            return api.TesselatorManager.GetCachedShape(stack.Item.Shape.Base).Clone();
        }

        var color = stack.Attributes.GetString(EnvelopeAttributes.WaxColor);

        if (string.IsNullOrEmpty(color))
        {
            return shape;
        }

        RemapShapeElementTextures(shape, "wax", color);
        RemapShapeElementTextures(shape, "wax2", color);
        RemapShapeElementTextures(shape, "waxPiece", color);
        RemapShapeElementTextures(shape, "waxPiece2", color);

        var stampDesign = stack.Attributes.GetString(StampAttributes.StampDesign);

        if (!string.IsNullOrEmpty(stampDesign))
        {
            ApplyStampEmblemTexture(api, shape, stampDesign, color);
        }

        return shape;
    }

    public void RemapShapeElementTextures(Shape shape, string elementName, string newTextureName)
    {
        var elementByName = shape.GetElementByName(elementName);
        if (elementByName == null)
        {
            return;
        }

        foreach (var face in elementByName.FacesResolved)
        {
            if (face == null)
            {
                continue;
            }

            face.Texture = newTextureName;
        }
    }

    public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target,
        ref ItemRenderInfo renderinfo)
    {
        var meshRefs =
            ObjectCacheUtil.GetOrCreate(capi, MeshRefsCacheKey, () => new Dictionary<string, MultiTextureMeshRef>());

        var key = GetMeshCacheKey(itemstack);

        if (!meshRefs.TryGetValue(key, out var meshref))
        {
            var mesh = GenMesh(itemstack, capi.ItemTextureAtlas, null);
            meshref = capi.Render.UploadMultiTextureMesh(mesh);
            meshRefs[key] = meshref;
        }

        renderinfo.ModelRef = meshref;
        renderinfo.NormalShaded = true;

        base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
    }

    public MeshData? GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos)
    {
        return CreateMesh(itemstack);
    }

    public string GetMeshCacheKey(ItemStack itemstack)
    {
        var color = itemstack.Attributes.GetString(EnvelopeAttributes.WaxColor);
        var stampDesign = itemstack.Attributes.GetString(StampAttributes.StampDesign);

        var stampHash = string.IsNullOrEmpty(stampDesign)
            ? "nostamp"
            : GetDesignHash(stampDesign);

        return $"{itemstack.Collectible.Code.ToShortString()}-{color ?? "seal-default"}-{stampHash}";
    }

    private bool[] ParseDesignString(string designString)
    {
        if (string.IsNullOrEmpty(designString)) return System.Array.Empty<bool>();

        try
        {
            return designString.ToCharArray().Select(c => c == '1').ToArray();
        }
        catch
        {
            return [];
        }
    }

    private string GetStampTextureCacheKey(string design, string waxColor)
    {
        var designHash = GetDesignHash(design);
        return $"{designHash}-{waxColor}";
    }

    private string GetDesignHash(string design)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(design);
        var hash = sha.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").Substring(0, 16);
    }

    private void ApplyStampEmblemTexture(ICoreClientAPI api, Shape shape, string designString, string waxColor)
    {
        var design = ParseDesignString(designString);
        if (design.Length == 0) return;

        AddStampImpressionGeometry(api, shape, design, waxColor);
    }

    private void AddStampImpressionGeometry(ICoreClientAPI api, Shape shape, bool[] design, string waxColor)
    {
        var waxElement = shape.GetElementByName("wax");
        if (waxElement == null) return;

        const double waxMinX = 0;
        const double waxMaxX = 2;
        const double waxMinZ = 0;
        const double waxMaxZ = 1.4;
        const double waxTopY = 0.1;

        double waxWidth = waxMaxX - waxMinX;
        double waxDepth = waxMaxZ - waxMinZ;
        double waxCenterX = (waxMinX + waxMaxX) / 2.0;
        double waxCenterZ = (waxMinZ + waxMaxZ) / 2.0;

        double stampDiameter = Math.Min(waxWidth, waxDepth) * 0.95;
        double cellSize = stampDiameter / 24.0;
        double gridOffset = stampDiameter / 2.0;

        var impressionTexture = waxColor + "impression";

        var impressionElements = new List<ShapeElement>();

        var impressionFace = new ShapeElementFace
        {
            Texture = impressionTexture,
            Uv = [0f, 0f, 1f, 1f]
        };

        var faces = new ShapeElementFace[6];
        faces[0] = impressionFace;
        faces[1] = impressionFace;
        faces[2] = impressionFace;
        faces[3] = impressionFace;
        faces[4] = impressionFace;
        faces[5] = impressionFace;

        for (int row = 0; row < 24; row++)
        {
            for (int col = 0; col < 24; col++)
            {
                int idx = row * 24 + col;
                if (idx < design.Length && design[idx])
                {
                    double dx = (col - 11.5) / 12.0;
                    double dz = (row - 11.5) / 12.0;
                    if (dx * dx + dz * dz > 1.0) continue;

                    double xStart = waxCenterX - gridOffset + (col * cellSize);
                    double zStart = waxCenterZ - gridOffset + (row * cellSize);

                    var element = new ShapeElement
                    {
                        Name = $"StampImpression{idx}",
                        From = new[]
                        {
                            xStart,
                            waxTopY + 0.07,
                            zStart
                        },
                        To = new[]
                        {
                            xStart + cellSize,
                            waxTopY + 0.17,
                            zStart + cellSize
                        },
                        FacesResolved = faces
                    };
                    impressionElements.Add(element);
                }
            }
        }

        if (impressionElements.Count > 0)
        {
            var existingChildren = waxElement.Children?.ToList() ?? new List<ShapeElement>();
            existingChildren.AddRange(impressionElements);
            waxElement.Children = existingChildren.ToArray();
        }
    }
}