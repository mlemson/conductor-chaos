"""Schrijft Unity-bijbestanden voor gegenereerde modellen: materialen voor de stylized ramp-shader
(Assets/Art/Shaders/StylizedRamp.shader, issue #33; met USE_STYLIZED = False: URP Lit met de middentoon
van elke ramp), en .meta-bestanden met stabiele GUID's
(afgeleid van het asset-pad, dus regenereren verandert geen GUID's).

Let op: deze bestanden zijn met de hand geschreven YAML en nog niet door de
Unity Editor ingelezen (zie issue #9 / TESTPLAN). Unity vult ontbrekende
importer-velden zelf aan bij de eerste import.
"""
import hashlib
import os

URP_LIT_GUID = "933532a4fcc9baf4fa0491de14d08ed7"  # Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader
STYLIZED_SHADER = "Assets/Art/Shaders/StylizedRamp.shader"
USE_STYLIZED = True       # False = alle materialen als URP Lit (middentoon), zoals voor issue #33


def guid_for(asset_path):
    return hashlib.md5(("conductor-chaos:" + asset_path.replace("\\", "/")).encode()).hexdigest()


def _hex(h):
    h = h.lstrip("#")
    return [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]


def write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


def folder_meta(root, rel_dir):
    """Maak .meta voor elke map van Assets/... tot rel_dir (als die nog niet bestaat)."""
    parts = rel_dir.replace("\\", "/").split("/")
    for i in range(2, len(parts) + 1):
        sub = "/".join(parts[:i])
        meta = os.path.join(root, sub + ".meta")
        if not os.path.exists(meta):
            write(meta, "fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
                        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid_for(sub))


def shader_meta(root):
    """ShaderImporter-meta voor de stylized ramp-shader (vaste GUID, zodat materialen ernaar kunnen verwijzen)."""
    write(os.path.join(root, STYLIZED_SHADER + ".meta"),
          "fileFormatVersion: 2\nguid: %s\nShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n"
          "  nonModifiableTextures: []\n  preprocessorOverride: 0\n  userData: \n  assetBundleName: \n"
          "  assetBundleVariant: \n" % guid_for(STYLIZED_SHADER))
    folder_meta(root, os.path.dirname(STYLIZED_SHADER))
    return guid_for(STYLIZED_SHADER)


def stylized_material(root, rel_path, name, ramp, ramp_shift=0.0, opts=None, emissive_hex=None):
    """Materiaal voor ConductorChaos/StylizedRamp: de 5 ramp-tinten + shaderhints (zelfde betekenis als de render)."""
    o = dict(opts or {})
    alpha = float(o.get("opacity", 1.0))
    emissive = float(o.get("emissive", 0.0))
    floats = {
        "_RampShift": float(ramp_shift), "_Flat": float(o.get("flat", 0.0)), "_Edge": float(o.get("edge", 1.0)),
        "_Rim": float(o.get("rim", 0.32)), "_Spec": float(o.get("spec", 0.55)),
        "_Cells": 0.0 if emissive else float(o.get("cells", 1.0)), "_CellSize": float(o.get("cellSize", 0.45)),
        "_Grime": float(o.get("grime", 0.0)), "_Emissive": emissive, "_Opacity": alpha, "_HasShade": 1.0,
        "_ReceiveShadowsF": 0.0 if o.get("noShadow") else 1.0, "_Cull": 2.0,
        "_SrcBlend": 5.0 if alpha < 1 else 1.0, "_DstBlend": 10.0 if alpha < 1 else 0.0, "_ZWrite": 0.0 if alpha < 1 else 1.0,
    }
    gy = o.get("grimeY", [-0.35, 0.85])
    colors = {"_Ramp%d" % k: tuple(_hex(h)) + (1.0,) for k, h in enumerate(ramp)}
    colors.update({"_BaseColor": tuple(_hex(ramp[2])) + (alpha,), "_RimColor": tuple(_hex("#B2C5F3")) + (1.0,),
                   "_DirtColor": tuple(_hex("#503330")) + (1.0,), "_GrimeY": (float(gy[0]), float(gy[1]), 0.0, 0.0)})
    render_type, queue = ("Transparent", 3000) if alpha < 1 else ("Opaque", -1)
    fl = "\n".join("    - %s: %s" % (k, v) for k, v in floats.items())
    co = "\n".join("    - %s: {r: %.6f, g: %.6f, b: %.6f, a: %.3f}" % (k, *v) for k, v in colors.items())
    text = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 4800000, guid: {guid_for(STYLIZED_SHADER)}, type: 3}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 1
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: {queue}
  stringTagMap:
    RenderType: {render_type}
  disabledShaderPasses: []
  m_LockedProperties: 
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs: []
    m_Ints: []
    m_Floats:
{fl}
    m_Colors:
{co}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
"""
    path = os.path.join(root, rel_path)
    write(path, text)
    write(path + ".meta", "fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n"
                          "  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid_for(rel_path))
    return guid_for(rel_path)


def material(root, rel_path, name, base_hex, smoothness, emissive_hex=None, opacity=None, ramp=None, ramp_shift=0.0, opts=None):
    if USE_STYLIZED and ramp:
        shader_meta(root)
        o = dict(opts or {})
        if opacity is not None:
            o.setdefault("opacity", opacity)
        return stylized_material(root, rel_path, name, ramp, ramp_shift, o, emissive_hex)
    return urp_lit_material(root, rel_path, name, base_hex, smoothness, emissive_hex, opacity)


def urp_lit_material(root, rel_path, name, base_hex, smoothness, emissive_hex=None, opacity=None):
    r, g, b = _hex(base_hex)
    floats = {"_Smoothness": smoothness, "_Glossiness": smoothness, "_Metallic": 0.0, "_SpecularHighlights": 1.0,
              "_EnvironmentReflections": 1.0, "_Surface": 0.0, "_Cull": 2.0, "_ReceiveShadows": 1.0,
              "_WorkflowMode": 1.0, "_BumpScale": 1.0, "_OcclusionStrength": 1.0}
    alpha = 1.0 if opacity is None else float(opacity)
    colors = {"_BaseColor": (r, g, b, alpha), "_Color": (r, g, b, alpha)}
    keywords = []
    render_type, queue = "Opaque", -1
    if alpha < 1.0:                                   # URP Lit transparant (alpha-blend), bv. glas
        floats.update({"_Surface": 1.0, "_Blend": 0.0, "_SrcBlend": 5.0, "_DstBlend": 10.0, "_SrcBlendAlpha": 1.0,
                       "_DstBlendAlpha": 10.0, "_ZWrite": 0.0, "_AlphaClip": 0.0})
        keywords.append("_SURFACE_TYPE_TRANSPARENT")
        render_type, queue = "Transparent", 3000
    if emissive_hex:
        er, eg, eb = _hex(emissive_hex)
        colors["_EmissionColor"] = (er, eg, eb, 1.0)
        keywords.append("_EMISSION")
    else:
        colors["_EmissionColor"] = (0, 0, 0, 1.0)
    fl = "\n".join("    - %s: %s" % (k, v) for k, v in floats.items())
    co = "\n".join("    - %s: {r: %.6f, g: %.6f, b: %.6f, a: %.3f}" % (k, *v) for k, v in colors.items())
    kw = ("\n" + "\n".join("  - " + k for k in keywords)) if keywords else " []"
    text = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 4800000, guid: {URP_LIT_GUID}, type: 3}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords:{kw}
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 1
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: {queue}
  stringTagMap:
    RenderType: {render_type}
  disabledShaderPasses: []
  m_LockedProperties: 
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs: []
    m_Ints: []
    m_Floats:
{fl}
    m_Colors:
{co}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
"""
    path = os.path.join(root, rel_path)
    write(path, text)
    write(path + ".meta", "fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n"
                          "  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid_for(rel_path))
    return guid_for(rel_path)


def model_meta(root, rel_path, material_guids):
    """ModelImporter-meta die de FBX-materialen op de externe .mat-bestanden mapt."""
    ext = "".join(
        "  - first:\n      type: UnityEngine:Material\n      assembly: UnityEngine.CoreModule\n      name: %s\n"
        "    second: {fileID: 2100000, guid: %s, type: 2}\n" % (n, g) for n, g in material_guids.items())
    text = f"""fileFormatVersion: 2
guid: {guid_for(rel_path)}
ModelImporter:
  serializedVersion: 22200
  internalIDToNameTable: []
  externalObjects:
{ext}  materials:
    materialImportMode: 2
    materialName: 0
    materialSearch: 1
    materialLocation: 1
  animations:
    importAnimation: 0
  meshes:
    globalScale: 1
    useFileUnits: 1
    useFileScale: 1
    importBlendShapes: 0
    importVisibility: 1
    importCameras: 0
    importLights: 0
    generateSecondaryUV: 0
    normalImportMode: 0
    tangentImportMode: 3
    normalSmoothingSource: 0
  importAnimation: 0
  animationType: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    write(os.path.join(root, rel_path + ".meta"), text)


def export_unity(root, scene, fbx_rel, mat_dir_rel):
    """Schrijf .mat-bestanden + metas voor een meshkit-Scene waarvan de FBX al op fbx_rel staat."""
    folder_meta(root, os.path.dirname(fbx_rel))
    folder_meta(root, mat_dir_rel)
    guids = {}
    mat_abs = os.path.join(root, mat_dir_rel)
    for f in os.listdir(mat_abs) if os.path.isdir(mat_abs) else []:   # verouderde materialen opruimen
        if f.endswith(".mat") and f[:-4] not in scene.materials:
            os.remove(os.path.join(mat_abs, f))
            if os.path.exists(os.path.join(mat_abs, f + ".meta")):
                os.remove(os.path.join(mat_abs, f + ".meta"))
    for name, md in scene.materials.items():
        guids[name] = material(root, f"{mat_dir_rel}/{name}.mat", name, md["base"], md["smoothness"], md.get("emissive"),
                               (md.get("opts") or {}).get("opacity"), md.get("ramp"), md.get("ramp_shift", 0.0), md.get("opts"))
    model_meta(root, fbx_rel, guids)
    return guids


def texture_meta(root, rel_path, srgb=True, wrap_repeat=True):
    """TextureImporter-meta: 'Default' textuur, optioneel lineair (data) en herhalend."""
    text = f"""fileFormatVersion: 2
guid: {guid_for(rel_path)}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: {1 if srgb else 0}
    linearTexture: 0
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 4
    mipBias: 0
    wrapU: {0 if wrap_repeat else 1}
    wrapV: {0 if wrap_repeat else 1}
    wrapW: {0 if wrap_repeat else 1}
  alphaUsage: 0
  alphaIsTransparency: 0
  textureType: 0
  textureShape: 1
  maxTextureSize: 1024
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    write(os.path.join(root, rel_path + ".meta"), text)
