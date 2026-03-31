using System;
using System.Collections.Generic;
using System.Linq;
using Envelopes.Gui;
using Envelopes.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Envelopes.Behaviors;

public class RenderStampEmblem : CollectibleBehavior, IContainedMeshSource
{
    private ICoreAPI _api;

    private const string MeshRefsCacheKey = "StampRefs";

    private static Dictionary<string, MultiTextureMeshRef> _meshRefs;

    public override void OnLoaded(ICoreAPI api)
    {
        _api = api;
        _meshRefs = ObjectCacheUtil.GetOrCreate(api, MeshRefsCacheKey,
            () => new Dictionary<string, MultiTextureMeshRef>());
        base.OnLoaded(api);
    }

    public static void InvalidateMeshCacheKey(ItemStack itemstack)
    {
        var key = GetMeshCacheKeyFor(itemstack);
        if (_meshRefs.TryGetValue(key, out var m))
        {
            m.Dispose();
            _meshRefs.Remove(key);
        }
    }

    public override void OnUnloaded(ICoreAPI api)
    {
        base.OnUnloaded(api);

        _meshRefs.Foreach(meshRef => meshRef.Value.Dispose());
        ObjectCacheUtil.Delete(api, MeshRefsCacheKey);
    }

    private bool _faceUp;

    public RenderStampEmblem(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void Initialize(JsonObject properties)
    {
        _faceUp = properties?["faceUp"].AsBool(false) ?? false;
        base.Initialize(properties);
    }

    public MeshData? CreateMesh(ItemStack itemstack)
    {
        if (_api is not ICoreClientAPI capi)
        {
            return null;
        }

        var cacheKey = GetMeshCacheKey(itemstack);
        var shape = GenShape(capi, itemstack);

        var tps = new ShapeTextureSource(capi, shape, "stampssealsource", itemstack.Item.Textures, p => p);
        capi.Tesselator.TesselateShape(cacheKey, shape, out var meshdata, tps);

        return meshdata;
    }

    private Shape GenShape(ICoreClientAPI api, ItemStack stack)
    {
        var shapeloc = stack.Item.Shape.Base.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
        var shape = api.Assets.TryGet(shapeloc)?.ToObject<Shape>();

        if (shape == null)
        {
            api.Logger.Error("Could not find shape for stamp {0} at {1}", stack.Item.Code, shapeloc);
            return api.TesselatorManager.GetCachedShape(stack.Item.Shape.Base).Clone();
        }

        var bodyMetal = stack.Collectible.Variant?["metal"]
            ?? stack.Attributes.GetString(StampAttributes.StampBodyMetal)
            ?? "steel";
        shape.Textures["metal"] = new AssetLocation($"game:block/metal/ingot/{bodyMetal}");

        var engravingMetal = stack.Attributes.GetString(StampAttributes.EngravingMetal) ?? "gold";
        shape.Textures["engraving"] = ResolveEngravingTexture(_api, engravingMetal);

        var design = ParseDesign(stack);

        var stamp = shape.GetElementByName("Stamp");
        if (stamp == null) return shape;

        var cellSizeX = (stamp.To[0] - stamp.From[0]) / Constants.GridDimensions;
        var cellSizeZ = (stamp.To[2] - stamp.From[2]) / Constants.GridDimensions;
        var cellUvSize = (float)cellSizeX;
        double yBottom, yTop;
        if (_faceUp)
        {
            var stampHeight = stamp.To[1] - stamp.From[1];
            yBottom = stampHeight;
            yTop = yBottom + cellSizeX;
        }
        else
        {
            yTop = 0.0;
            yBottom = -cellSizeX;
        }

        var list = new List<ShapeElement>();
        for (var row = 0; row < Constants.GridDimensions; row++)
        {
            for (var col = 0; col < Constants.GridDimensions; col++)
            {
                var idx = row * Constants.GridDimensions + col;
                if (design.Length == 0 || !design[idx])
                {
                    var uv = new[] { col * cellUvSize, row * cellUvSize, (col + 1) * cellUvSize, (row + 1) * cellUvSize };
                    var face = new ShapeElementFace { Texture = "metal", Uv = uv };
                    var faces = new ShapeElementFace[6];
                    faces[0] = face; faces[1] = face; faces[2] = face; faces[3] = face;
                    if (_faceUp) faces[4] = face; else faces[5] = face;
                    list.Add(new ShapeElement
                    {
                        Name = "StampFace" + idx,
                        From = new[] { row * cellSizeX, yBottom, col * cellSizeZ },
                        To = new[] { (row + 1) * cellSizeX, yTop, (col + 1) * cellSizeZ },
                        FacesResolved = faces
                    });
                }
            }
        }

        stamp.Children = list.ToArray();

        return shape;
    }

    public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target,
        ref ItemRenderInfo renderinfo)
    {
        var key = GetMeshCacheKey(itemstack);

        if (!_meshRefs.TryGetValue(key, out var meshref))
        {
            var mesh = CreateMesh(itemstack);
            if (mesh == null) return;
            meshref = capi.Render.UploadMultiTextureMesh(mesh);
            _meshRefs[key] = meshref;
        }

        renderinfo.ModelRef = meshref;
        renderinfo.NormalShaded = true;

        base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
    }

    private static bool[] ParseDesign(ItemStack stack)
    {
        var designString = stack.Attributes.GetString(StampAttributes.StampDesign);
        if (string.IsNullOrEmpty(designString)) return Array.Empty<bool>();

        try
        {
            return designString.ToCharArray()
                .Select(x => x == '1')
                .ToArray();
        }
        catch
        {
            return Array.Empty<bool>();
        }
    }

    public MeshData? GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos)
    {
        return CreateMesh(itemstack);
    }

    public string GetMeshCacheKey(ItemStack itemstack)
    {
        return GetMeshCacheKeyFor(itemstack);
    }

    public static string GetMeshCacheKeyFor(ItemStack itemstack)
    {
        var bodyMetal = itemstack.Collectible.Variant?["metal"]
            ?? itemstack.Attributes.GetString(StampAttributes.StampBodyMetal)
            ?? "steel";

        if (itemstack.Collectible.Code.Path.EndsWith("blank"))
        {
            return $"{itemstack.Collectible.Code.ToShortString()}-{bodyMetal}";
        }

        var engravingMetal = itemstack.Attributes.GetString(StampAttributes.EngravingMetal) ?? "gold";
        var stampId = itemstack.Attributes.GetLong(StampAttributes.StampId);
        if (stampId == 0L)
            return $"{itemstack.Collectible.Code.ToShortString()}-{bodyMetal}-{engravingMetal}";

        return $"{itemstack.Collectible.Code.ToShortString()}-{stampId}-{bodyMetal}-{engravingMetal}";
    }

    internal static AssetLocation ResolveEngravingTexture(ICoreAPI api, string metal)
    {
        var toolPath = new AssetLocation($"game:textures/item/tool/material/{metal}.png");
        return api.Assets.TryGet(toolPath) != null
            ? new AssetLocation($"game:item/tool/material/{metal}")
            : new AssetLocation($"game:block/metal/ingot/{metal}");
    }
}