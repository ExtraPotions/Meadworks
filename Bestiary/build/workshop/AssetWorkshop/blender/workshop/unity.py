"""Unity's YAML files in the reference export (prefabs, materials, mesh headers) and the GUID index that finds an
asset's file from the GUID other files reference it by. No Blender here, so plain Python can import it too (the codex's
measuring scripts do): prefab.py and prefab_parts.py build on it.

Walking every .meta file of the export takes a few seconds, so the index is kept in out/reference_guids.json and
rebuilt when it is missing or a GUID is not in it (once per Blender session: the export may have been ripped again).
"""
import functools
import json
import os
import re

ROOT = os.path.join(os.environ.get("USERPROFILE", ""), "ValheimReference", "ExportedProject", "Assets")

GUID_INDEX = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "out", "reference_guids.json")
BUILTIN_MESHES = "0000000000000000e000000000000000"     # Unity's own meshes: 10202 cube, 10206 cylinder, 10208 capsule...
BUILTIN_ASSETS = "0000000000000000f000000000000000"     # Unity's own shaders and default materials
NUMBER = r"-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?"
_HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?[ \t]*$", re.MULTILINE)
_guids = {}
_rebuilt = False


def read(path):
    """A file under the reference Assets folder (or an absolute path) as text."""
    with open(os.path.join(ROOT, path), encoding="utf-8") as handle:
        return handle.read()


def documents(path):
    """{fileID: (class ID, body)} for every '--- !u!<class> &<fileID>' document in a Unity YAML file."""
    text = read(path)
    heads = list(_HEADER.finditer(text))
    ends = [head.start() for head in heads[1:]] + [len(text)]
    return {head.group(2): (int(head.group(1)), text[head.end():end]) for head, end in zip(heads, ends)}


def field(body, name):
    """A top-level field's raw value, '' when it is absent."""
    match = re.search(rf"^  {name}: ?(.*)$", body, re.MULTILINE)
    return match.group(1).strip() if match else ""


def items(body, name):
    """The entries of a top-level list field ('  - {fileID: ...}'), each as its text after the dash."""
    match = re.search(rf"^  {name}:\n((?:  - .*\n)*)", body, re.MULTILINE)
    return re.findall(r"^  - (.*)$", match.group(1), re.MULTILINE) if match else []


def numbers(value):
    """'{x: 1, y: 2, z: 3}' -> (1.0, 2.0, 3.0), in the order written (x y z w, r g b a)."""
    return tuple(float(v) for v in re.findall(rf"\w+: ({NUMBER})", value))


def ref(value):
    """'{fileID: 4300000, guid: 24bd..., type: 2}' -> ('4300000', '24bd...'); the guid is '' inside the same file."""
    match = re.search(r"fileID: (-?\d+)(?:, guid: (\w+))?", value)
    return (match.group(1), match.group(2) or "") if match else ("0", "")


def asset_path(guid):
    """The file with this GUID, relative to the reference Assets folder, or None."""
    global _guids
    if not _guids:
        _guids = _load_index()
    path = _guids.get(guid)
    if (path is None or not os.path.exists(os.path.join(ROOT, path))) and not _rebuilt:
        _guids = _build_index()
        path = _guids.get(guid)
    return path


@functools.lru_cache(maxsize=None)
def material(path):
    """What a preview needs of a .mat: its shader's file, textures (path, scale, offset), tint, cutout and culling.
    Cached: do not change the dict."""
    text = read(path)
    shader_id, shader_guid = ref(_nested(text, "m_Shader"))
    keywords = re.findall(r"^  - (_\w+)$", text, re.MULTILINE)
    return {
        "name": _nested(text, "m_Name"),
        "shader": f"builtin:{shader_id}" if shader_guid in ("", BUILTIN_ASSETS) else asset_path(shader_guid) or "",
        "albedo": _texture(text, "_MainTex"),
        "normal": _texture(text, "_BumpMap"),
        "color": numbers(_nested(text, "_Color") or "{r: 1, g: 1, b: 1, a: 1}"),
        "cutoff": float(_nested(text, "_Cutoff") or 0.5),
        "cutout": _nested(text, "_Mode") == "1" or "_ALPHATEST_ON" in keywords,
        "cull": int(float(_nested(text, "_Cull") or 2)),
    }


def _nested(text, name):
    match = re.search(rf"^\s+{name}: ?(.*)$", text, re.MULTILINE)
    return match.group(1).strip() if match else ""


def _texture(text, name):
    """(path or None, (scale u, v), (offset u, v)) of a material's texture slot."""
    match = re.search(rf"^\s+{name}:\n\s+m_Texture: (.*)\n\s+m_Scale: (.*)\n\s+m_Offset: (.*)$", text, re.MULTILINE)
    if not match:
        return None, (1.0, 1.0), (0.0, 0.0)
    _, guid = ref(match.group(1))
    path = asset_path(guid) if guid and guid != BUILTIN_ASSETS else None
    return path, numbers(match.group(2)), numbers(match.group(3))


def _load_index():
    try:
        with open(GUID_INDEX, encoding="utf-8") as handle:
            return json.load(handle)
    except (OSError, ValueError):
        return _build_index()


def _build_index():
    """Walks every .meta file under the export once and saves {guid: path}."""
    global _rebuilt
    _rebuilt, index = True, {}
    for folder, _, files in os.walk(ROOT):
        for name in files:
            if name.endswith(".meta"):
                guid = _meta_guid(os.path.join(folder, name))
                if guid:
                    index[guid] = os.path.relpath(os.path.join(folder, name[:-5]), ROOT).replace("\\", "/")
    os.makedirs(os.path.dirname(GUID_INDEX), exist_ok=True)
    temp = f"{GUID_INDEX}.{os.getpid()}.tmp"
    with open(temp, "w", encoding="utf-8") as handle:
        json.dump(index, handle)
    os.replace(temp, GUID_INDEX)   # atomic, so a build running alongside never reads half a file
    return index


def _meta_guid(path):
    with open(path, "rb") as handle:
        match = re.search(rb"guid: ([0-9a-f]{32})", handle.read(160))
    return match.group(1).decode() if match else None
